using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal.Internal;

namespace UnityEngine.Rendering.Universal
{
    internal sealed class UberPostProcessPass : PostProcessPass
    {
        private enum PresentationMode
        {
            Uber = 0,
            Grade = 1,
            Identity = 2
        }

        private static readonly ProfilingSampler s_UberSampler = new ProfilingSampler("UberPost");
        private static readonly ProfilingSampler s_GradeSampler = new ProfilingSampler("Color Grade Present");
        private static readonly ProfilingSampler s_IdentitySampler = new ProfilingSampler("Identity Present");

        private static ProfilingSampler GetProfilingSampler(PresentationMode mode)
        {
            return mode switch
            {
                PresentationMode.Identity => s_IdentitySampler,
                PresentationMode.Grade => s_GradeSampler,
                _ => s_UberSampler
            };
        }

        private LensDistortion m_LensDistortion;
        private ChromaticAberration m_ChromaticAberration;
        private Vignette m_Vignette;
        private ColorLookup m_ColorLookup;
        private ColorAdjustments m_ColorAdjustments;
        private Tonemapping m_Tonemapping;
        private FilmGrain m_FilmGrain;
        private bool m_HasFinalPass;
        private bool m_EnableColorEncodingIfNeeded;

        private RTHandle m_UserLut;
        private RTHandle m_LensDirtTexture;
        private RTHandle m_FilmGrainTexture;
        private RTHandle[] m_BlueNoiseTextures;
        private int m_DitheringTextureIndex;

        public UberPostProcessPass(PostProcessMaterialLibrary materials)
            : base(materials)
        {
            var blueNoiseTextures = materials.resources.textures.blueNoise16LTex;
            m_BlueNoiseTextures = blueNoiseTextures == null ? new RTHandle[0] : new RTHandle[blueNoiseTextures.Length];
            if (blueNoiseTextures != null)
            {
                for (int i = 0; i < blueNoiseTextures.Length; i++)
                {
                    if (blueNoiseTextures[i] != null)
                        m_BlueNoiseTextures[i] = RTHandles.Alloc(blueNoiseTextures[i]);
                }
            }
        }

        public Tonemapping Tonemapping => m_Tonemapping;

        public void SetSettings(
            LensDistortion lensDistortion,
            ChromaticAberration chromaticAberration,
            Vignette vignette,
            ColorLookup colorLookup,
            ColorAdjustments colorAdjustments,
            Tonemapping tonemapping,
            FilmGrain filmGrain,
            bool hasFinalPass,
            bool enableColorEncodingIfNeeded)
        {
            m_LensDistortion = lensDistortion;
            m_ChromaticAberration = chromaticAberration;
            m_Vignette = vignette;
            m_ColorLookup = colorLookup;
            m_ColorAdjustments = colorAdjustments;
            m_Tonemapping = tonemapping;
            m_FilmGrain = filmGrain;
            m_HasFinalPass = hasFinalPass;
            m_EnableColorEncodingIfNeeded = enableColorEncodingIfNeeded;
        }

        public override void Dispose()
        {
            ReleaseHandle(ref m_UserLut);
            ReleaseHandle(ref m_LensDirtTexture);
            ReleaseHandle(ref m_FilmGrainTexture);
            for (int i = 0; i < m_BlueNoiseTextures.Length; i++)
                ReleaseHandle(ref m_BlueNoiseTextures[i]);
        }

        private static void ReleaseHandle(ref RTHandle handle)
        {
            handle?.Release();
            handle = null;
        }

        private static TextureHandle ImportExternalTexture(RenderGraph renderGraph, ref RTHandle handle, Texture texture)
        {
            if (texture == null)
            {
                ReleaseHandle(ref handle);
                return TextureHandle.nullHandle;
            }

            if (handle == null || handle.externalTexture != texture)
            {
                ReleaseHandle(ref handle);
                handle = RTHandles.Alloc(texture);
            }

            return renderGraph.ImportTexture(handle);
        }



        private TextureHandle TryGetUserLutTextureHandle(RenderGraph renderGraph)
        {
            var userLut = m_ColorLookup != null && m_ColorLookup.IsActive() && m_ColorLookup.texture.value != null
                ? m_ColorLookup.texture.value
                : Texture2D.whiteTexture;
            return ImportExternalTexture(renderGraph, ref m_UserLut, userLut);
        }

        private static float ChromaTapPosition(float normalizedDispersion, float spread)
        {
            return Mathf.Clamp01(0.5f - 0.5f * normalizedDispersion * spread);
        }

        private static void BuildChromaticAberrationParameters(
            ChromaticAberration chromaticAberration,
            int sourceWidth,
            int sourceHeight,
            out Vector4 chromaParams,
            out Vector4 chromaRadialCoefficients,
            out Vector4 chromaTangentialCoefficients,
            out Vector4 chromaTapPositions0,
            out Vector4 chromaTapPositions1,
            out bool simpleLens,
            out bool enabled)
        {
            enabled = chromaticAberration.IsActive();

            if (!enabled)
            {
                chromaParams = Vector4.zero;
                chromaRadialCoefficients = Vector4.zero;
                chromaTangentialCoefficients = Vector4.zero;
                chromaTapPositions0 = Vector4.zero;
                chromaTapPositions1 = Vector4.zero;
                simpleLens = false;
                return;
            }

            float aspect = sourceHeight > 0 ? sourceWidth / (float)sourceHeight : 1f;
            Vector2 center = chromaticAberration.center.value;
            Vector3 radial = chromaticAberration.radialCoefficients.value;
            Vector2 tangential = chromaticAberration.tangentialCoefficients.value;
            float amount = chromaticAberration.intensity.value * 0.05f;
            float spread = chromaticAberration.spectralSpread.value;

            Vector2 maxRadius = new Vector2(
                Mathf.Max(center.x, 1f - center.x) * 2f * aspect,
                Mathf.Max(center.y, 1f - center.y) * 2f);
            float maxR = Mathf.Max(maxRadius.magnitude, 1e-5f);
            float inverseMaxR = 1f / maxR;

            const float reducedMiddleTap = -0.06410042f;

            chromaParams = new Vector4(
                amount,
                1f / Mathf.Max(aspect, 1e-6f),
                center.x,
                center.y);

            chromaRadialCoefficients = new Vector4(radial.x, radial.y, radial.z, aspect);
            chromaTangentialCoefficients = new Vector4(
                tangential.x,
                tangential.y,
                inverseMaxR,
                amount * spread * maxR);

            chromaTapPositions0 = new Vector4(
                ChromaTapPosition(-1.20f, spread),
                ChromaTapPosition(-0.72f, spread),
                ChromaTapPosition(-0.28f, spread),
                ChromaTapPosition(0.12f, spread));

            chromaTapPositions1 = new Vector4(
                ChromaTapPosition(0.58f, spread),
                ChromaTapPosition(1.00f, spread),
                ChromaTapPosition(reducedMiddleTap, spread),
                0f);

            simpleLens =
                radial.x == 1f && radial.y == 0f && radial.z == 0f &&
                tangential.x == 0f && tangential.y == 0f;
        }

        private static void BuildLensDistortionParameters(LensDistortion lensDistortion, bool isSceneView, out Vector4 distortionParams1, out Vector4 distortionParams2, out bool enabled)
        {
            float amount = 1.6f * Mathf.Max(Mathf.Abs(lensDistortion.intensity.value * 100f), 1f);
            float theta = Mathf.Deg2Rad * Mathf.Min(160f, amount);
            float sigma = 2f * Mathf.Tan(theta * 0.5f);
            var center = lensDistortion.center.value * 2f - Vector2.one;

            distortionParams1 = new Vector4(
                center.x,
                center.y,
                Mathf.Max(lensDistortion.xMultiplier.value, 1e-4f),
                Mathf.Max(lensDistortion.yMultiplier.value, 1e-4f));

            distortionParams2 = new Vector4(
                lensDistortion.intensity.value >= 0f ? theta : 1f / theta,
                sigma,
                1f / lensDistortion.scale.value,
                lensDistortion.intensity.value * 100f);

            enabled = lensDistortion.IsActive() && !isSceneView;
        }

        private static void BuildVignetteParameters(Vignette vignette, int width, int height, out Vector4 vignetteParams1, out Vector4 vignetteParams2)
        {
            var color = vignette.color.value;
            var center = vignette.center.value;
            float aspectRatio = width / (float)Mathf.Max(1, height);

            vignetteParams1 = new Vector4(
                color.r, color.g, color.b,
                vignette.rounded.value ? aspectRatio : 1f);

            vignetteParams2 = new Vector4(
                center.x, center.y,
                vignette.intensity.value * 3f,
                vignette.smoothness.value * 5f);
        }

        private static Vector4 BuildOffscreenUIViewportParams(Rect pixelRect, bool isRenderToBackBufferTarget)
        {
            if (!isRenderToBackBufferTarget)
                return new Vector4(0f, 0f, 1f, 1f);

            var rcpScreenSize = new Vector2(1f / Screen.width, 1f / Screen.height);
            return new Vector4(
                pixelRect.x * rcpScreenSize.x,
                pixelRect.y * rcpScreenSize.y,
                pixelRect.width * rcpScreenSize.x,
                pixelRect.height * rcpScreenSize.y);
        }

        private TextureHandle TryGetFilmGrainTextureHandle(RenderGraph renderGraph, UniversalCameraData cameraData, out Vector4 grainParams, out Vector4 grainTilingParams, out bool enabled)
        {
            enabled = !m_HasFinalPass && m_FilmGrain.IsActive();
            grainParams = enabled ? new Vector4(m_FilmGrain.intensity.value * 4f, m_FilmGrain.response.value, 0f, 0f) : Vector4.zero;
            grainTilingParams = Vector4.zero;

            if (!enabled)
                return TextureHandle.nullHandle;

            var texture = m_FilmGrain.texture.value;
            if (m_FilmGrain.type.value != FilmGrainLookup.Custom)
                texture = Materials.resources.textures.filmGrainTex[(int)m_FilmGrain.type.value];

            if (texture == null)
            {
                enabled = false;
                return TextureHandle.nullHandle;
            }

#if LWRP_DEBUG_STATIC_POSTFX
            float rndOffsetX = 0f;
            float rndOffsetY = 0f;
#else
            var oldState = Random.state;
            Random.InitState(Time.frameCount);
            float rndOffsetX = Random.value;
            float rndOffsetY = Random.value;
            Random.state = oldState;
#endif

            grainTilingParams = new Vector4(
                cameraData.pixelWidth / (float)texture.width,
                cameraData.pixelHeight / (float)texture.height,
                rndOffsetX,
                rndOffsetY);

            return ImportExternalTexture(renderGraph, ref m_FilmGrainTexture, texture);
        }

        private TextureHandle TryGetBlueNoiseTextureHandle(RenderGraph renderGraph, UniversalCameraData cameraData, out Vector4 ditheringParams, out bool enabled)
        {
            enabled = !m_HasFinalPass && cameraData.isDitheringEnabled;
            ditheringParams = Vector4.zero;

            if (!enabled)
                return TextureHandle.nullHandle;

            var blueNoise = Materials.resources.textures.blueNoise16LTex;
            if (blueNoise == null || blueNoise.Length == 0)
            {
                enabled = false;
                return TextureHandle.nullHandle;
            }

#if LWRP_DEBUG_STATIC_POSTFX
            m_DitheringTextureIndex = 0;
            float rndOffsetX = 0f;
            float rndOffsetY = 0f;
#else
            if (++m_DitheringTextureIndex >= blueNoise.Length)
                m_DitheringTextureIndex = 0;

            var oldState = Random.state;
            Random.InitState(Time.frameCount);
            float rndOffsetX = Random.value;
            float rndOffsetY = Random.value;
            Random.state = oldState;
#endif

            var noiseTex = blueNoise[m_DitheringTextureIndex];
            ditheringParams = new Vector4(
                cameraData.pixelWidth / (float)noiseTex.width,
                cameraData.pixelHeight / (float)noiseTex.height,
                rndOffsetX,
                rndOffsetY);

            return ImportExternalTexture(renderGraph, ref m_BlueNoiseTextures[m_DitheringTextureIndex], noiseTex);
        }


        private sealed class PresentationPassData
        {
            internal TextureHandle destinationTexture;
            internal TextureHandle sourceTexture;
            internal TextureHandle lutTexture;
            internal TextureHandle userLutTexture;
            internal TextureHandle bloomTexture;
            internal TextureHandle screenSpaceLensFlareTexture;
            internal TextureHandle lensDirtTexture;
            internal TextureHandle grainTexture;
            internal TextureHandle blueNoiseTexture;
            internal TextureHandle overlayUITexture;
            internal TextureHandle exposureTexture;
            internal Material material;
            internal UniversalCameraData cameraData;
            internal bool isActiveTargetBackBuffer;
            internal int shaderPass;
        }

        private static PostProcessMaterialFlags BloomFlags(bool active, bool lensDirt)
        {
            if (!active)
                return PostProcessMaterialFlags.None;

            return lensDirt ? PostProcessMaterialFlags.BloomDirt : PostProcessMaterialFlags.Bloom;
        }

        private static PostProcessMaterialFlags HdrOutputFlags(HDROutputUtils.Operation operations)
        {
            if (operations.HasFlag(HDROutputUtils.Operation.ColorEncoding))
                return PostProcessMaterialFlags.HDREncoding;

            if (operations.HasFlag(HDROutputUtils.Operation.ColorConversion))
                return PostProcessMaterialFlags.HDRInput;

            return PostProcessMaterialFlags.None;
        }

        internal bool CanFinalPassConsumeSourceDirectly(
            in TextureDesc sourceDesc,
            in TextureHandle lutTexture,
            BloomFrameData bloomFrameData,
            in PaniniProjectionParameters panini,
            AutoExposureState autoExposure,
            bool requireHDROutput)
        {
            if (requireHDROutput || QualitySettings.activeColorSpace != ColorSpace.Linear)
                return false;

            if (PostProcessPassUtils.IsHDRFormat(sourceDesc.format))
                return false;

            if (panini.enabled || (bloomFrameData.textureActive && bloomFrameData.texture.IsValid()))
                return false;

            if (m_LensDistortion.IsActive() || m_ChromaticAberration.IsActive() || m_Vignette.IsActive())
                return false;

            if (autoExposure.IsActive || !Mathf.Approximately(m_ColorAdjustments.postExposure.value, 0f))
                return false;

            return !lutTexture.IsValid();
        }

        public void RenderPresentation(RenderGraph renderGraph, ContextContainer frameData, UniversalCameraData cameraData, UniversalPostProcessingData postProcessingData,
            in TextureHandle sourceTexture, in TextureHandle destTexture, in TextureHandle lutTexture, BloomFrameData bloomFrameData,
            in PaniniProjectionParameters panini, in TextureHandle overlayUITexture, AutoExposureState autoExposure, bool requireHDROutput, bool enableAlphaOutput, bool isActiveTargetBackBuffer, bool hasFinalPass)
        {
            bool hdrGrading = postProcessingData.gradingMode == ColorGradingMode.HighDynamicRange;
            int lutSize = postProcessingData.lutSize;

            float postExposureLinear = Mathf.Pow(2f, m_ColorAdjustments.postExposure.value);
            // ApplyLut3D expects (1 / size, size - 1); post exposure remains in w.
            var lutParams = new Vector4(1f / lutSize, lutSize - 1f, 0f, postExposureLinear);

            bool userLutActive = m_ColorLookup != null && m_ColorLookup.IsActive() && m_ColorLookup.texture.value != null;
            var userLutTexture = TryGetUserLutTextureHandle(renderGraph);
            var userLutParams = !userLutActive
                ? Vector4.zero
                : new Vector4(1f / m_ColorLookup.texture.value.width,
                    1f / m_ColorLookup.texture.value.height,
                    m_ColorLookup.texture.value.height - 1f,
                    m_ColorLookup.contribution.value);

            var srcDesc = sourceTexture.GetDescriptor(renderGraph);
            var dstInfo = renderGraph.GetRenderTargetInfo(destTexture);
            bool bloomTextureActive = bloomFrameData.textureActive && bloomFrameData.texture.IsValid();
            var bloomTexture = bloomTextureActive ? bloomFrameData.texture : TextureHandle.nullHandle;
            bool screenSpaceLensFlareActive = bloomFrameData.screenSpaceLensFlareActive && bloomFrameData.screenSpaceLensFlareTexture.IsValid();
            var screenSpaceLensFlareTexture = screenSpaceLensFlareActive ? bloomFrameData.screenSpaceLensFlareTexture : TextureHandle.nullHandle;
            var bloomUberParams = bloomFrameData.uberPostParameters;
            var bloomDesc = bloomTextureActive ? bloomTexture.GetDescriptor(renderGraph) : default;
            var screenSpaceLensFlareDesc = screenSpaceLensFlareActive ? screenSpaceLensFlareTexture.GetDescriptor(renderGraph) : default;

            BuildLensDistortionParameters(m_LensDistortion, cameraData.isSceneViewCamera, out var distortionParams1, out var distortionParams2, out bool distortionActive);
            BuildVignetteParameters(m_Vignette, dstInfo.width, dstInfo.height, out var vignetteParams1, out var vignetteParams2);
            bool vignetteActive = m_Vignette.IsActive();
            BuildChromaticAberrationParameters(
                m_ChromaticAberration,
                Mathf.Max(1, srcDesc.width),
                Mathf.Max(1, srcDesc.height),
                out var chromaParams,
                out var chromaRadialCoefficients,
                out var chromaTangentialCoefficients,
                out var chromaTapPositions0,
                out var chromaTapPositions1,
                out bool simpleChromaticAberrationLens,
                out bool chromaticAberrationActive);
            var grainTexture = TryGetFilmGrainTextureHandle(renderGraph, cameraData, out var grainParams, out var grainTilingParams, out bool filmGrainActive);
            var blueNoiseTexture = TryGetBlueNoiseTextureHandle(renderGraph, cameraData, out var ditheringParams, out bool ditheringActive);
            var lensDirtTexture = bloomUberParams.hasLensDirt && bloomUberParams.lensDirtTexture != null
                ? ImportExternalTexture(renderGraph, ref m_LensDirtTexture, bloomUberParams.lensDirtTexture)
                : TextureHandle.nullHandle;

            Vector4 hdrOutputLuminanceParams = new Vector4(-1f, -1f, -1f, -1f);
            var hdrOperations = HDROutputUtils.Operation.None;
            if (requireHDROutput)
            {
                UniversalRenderPipeline.GetHDROutputLuminanceParameters(cameraData.hdrDisplayInformation, cameraData.hdrDisplayColorGamut, m_Tonemapping, out hdrOutputLuminanceParams);
                hdrOperations = !hasFinalPass && m_EnableColorEncodingIfNeeded ? HDROutputUtils.Operation.ColorEncoding : HDROutputUtils.Operation.None;
            }

            bool renderToBackBuffer = !hasFinalPass && cameraData.resolveFinalTarget;
            var offscreenUIViewportParams = BuildOffscreenUIViewportParams(cameraData.pixelRect, renderToBackBuffer);
            bool finalEffectsInUberPost = !hasFinalPass;
            bool bloomLensDirt = bloomUberParams.hasLensDirt && lensDirtTexture.IsValid();
            bool hdrOverlay = !hasFinalPass && requireHDROutput && cameraData.rendersOverlayUI && overlayUITexture.IsValid();
            bool linearToSrgbConversion = PostProcessPassUtils.RequireSRGBConversionBlitToBackBuffer(cameraData.requireSrgbConversion, m_EnableColorEncodingIfNeeded);
            bool postExposureActive = !Mathf.Approximately(m_ColorAdjustments.postExposure.value, 0f);
            // LUT allocation is decided once when the frame graph is recorded. Its handle is the
            // canonical grading/tonemapping state for presentation.
            bool useColorLut = lutTexture.IsValid();
            // Panini is a virtual source transform shared by all presentation passes. Exposure is
            // handled by GradePresent when a LUT is already required. Grain and dithering belong to
            // FinalPost whenever that pass exists, so none of those conditions should promote an
            // otherwise simple presentation to the full Uber variant.
            bool finalGrainOrDitherInUber = finalEffectsInUberPost && (filmGrainActive || ditheringActive);
            bool exposureNeedsUber = !useColorLut && (autoExposure.IsActive || postExposureActive);
            bool requiresUber = distortionActive || chromaticAberrationActive || vignetteActive ||
                                bloomTextureActive || finalGrainOrDitherInUber || exposureNeedsUber;
            PresentationMode presentationMode = requiresUber
                ? PresentationMode.Uber
                : useColorLut ? PresentationMode.Grade : PresentationMode.Identity;

            var flags = HdrOutputFlags(hdrOperations);
            if (distortionActive)
            {
                flags |= PostProcessMaterialFlags.Distortion;
                if (m_LensDistortion.intensity.value > 0f)
                    flags |= PostProcessMaterialFlags.DistortionPositive;
            }
            if (chromaticAberrationActive)
            {
                flags |= PostProcessMaterialFlags.ChromaticAberration;
                if (!simpleChromaticAberrationLens)
                {
                    flags |= PostProcessMaterialFlags.ChromaticAberrationCustomLens;
                    if (chromaTangentialCoefficients.x != 0f || chromaTangentialCoefficients.y != 0f)
                        flags |= PostProcessMaterialFlags.ChromaticAberrationTangential;
                }
            }
            if (panini.enabled)
                flags |= PostProcessMaterialFlags.PaniniProjection;
            if (vignetteActive)
                flags |= PostProcessMaterialFlags.Vignette;
            flags |= BloomFlags(bloomTextureActive && bloomTexture.IsValid(), bloomLensDirt);
            if (screenSpaceLensFlareActive)
                flags |= PostProcessMaterialFlags.ScreenSpaceLensFlare;
            if (autoExposure.IsActive)
                flags |= PostProcessMaterialFlags.AutoExposure;
            if (useColorLut)
            {
                flags |= PostProcessMaterialFlags.ColorGrading;
                if (hdrGrading)
                    flags |= PostProcessMaterialFlags.HDRGrading;
            }
            if (finalEffectsInUberPost && filmGrainActive && grainTexture.IsValid())
                flags |= PostProcessMaterialFlags.FilmGrain;
            if (finalEffectsInUberPost && ditheringActive && blueNoiseTexture.IsValid())
                flags |= PostProcessMaterialFlags.Dithering;
            if (linearToSrgbConversion)
                flags |= PostProcessMaterialFlags.LinearToSRGBConversion;
            if (postProcessingData.useFastSRGBLinearConversion)
                flags |= PostProcessMaterialFlags.FastSRGBLinearConversion;
            if (enableAlphaOutput)
                flags |= PostProcessMaterialFlags.AlphaOutput;
            if (hdrOverlay)
                flags |= PostProcessMaterialFlags.HDROverlay;

            var material = Materials.GetKeywordVariant(Materials.uber,
                PostProcessMaterialKey.UberPost(flags, m_Tonemapping.mode.value));

            // UberPost.shader follows Unity's _TexelSize convention: xy = texel size, zw = texture size.
            material.SetVector(PostProcessShaderConstants._BloomTexture_TexelSize, bloomTextureActive && bloomTexture.IsValid()
                ? new Vector4(1f / Mathf.Max(1, bloomDesc.width), 1f / Mathf.Max(1, bloomDesc.height), bloomDesc.width, bloomDesc.height)
                : Vector4.one);
            material.SetVector(PostProcessShaderConstants._Lut_Params, lutParams);
            material.SetVector(PostProcessShaderConstants._UserLut_Params, userLutParams);
            material.SetVector(PostProcessShaderConstants._PaniniParams, panini.shaderParameters);
            material.SetFloat(PostProcessShaderConstants._PaniniEnabled, panini.enabled ? 1f : 0f);
            material.SetVector(PostProcessShaderConstants._Distortion_Params1, distortionParams1);
            material.SetVector(PostProcessShaderConstants._Distortion_Params2, distortionParams2);
            material.SetVector(PostProcessShaderConstants._Chroma_Params, chromaParams);
            material.SetVector(PostProcessShaderConstants._Chroma_RadialCoefficients, chromaRadialCoefficients);
            material.SetVector(PostProcessShaderConstants._Chroma_TangentialCoefficients, chromaTangentialCoefficients);
            material.SetVector(PostProcessShaderConstants._Chroma_TapPositions0, chromaTapPositions0);
            material.SetVector(PostProcessShaderConstants._Chroma_TapPositions1, chromaTapPositions1);
            material.SetVector(PostProcessShaderConstants._Vignette_Params1, vignetteParams1);
            material.SetVector(PostProcessShaderConstants._Vignette_Params2, vignetteParams2);
            material.SetVector(PostProcessShaderConstants._Bloom_Params, bloomUberParams.bloomParams);
            material.SetVector(PostProcessShaderConstants._BloomTint, bloomUberParams.bloomTint);
            material.SetVector(PostProcessShaderConstants._BloomBicubicParams, bloomUberParams.bloomBicubicParams);
            material.SetVector(PostProcessShaderConstants._Bloom_TextureScaleBias, bloomUberParams.bloomTextureScaleBias);
            material.SetVector(PostProcessShaderConstants._ScreenSpaceLensFlareTexture_TexelSize, screenSpaceLensFlareActive
                ? new Vector4(1f / Mathf.Max(1, screenSpaceLensFlareDesc.width), 1f / Mathf.Max(1, screenSpaceLensFlareDesc.height), screenSpaceLensFlareDesc.width, screenSpaceLensFlareDesc.height)
                : Vector4.one);
            material.SetVector(PostProcessShaderConstants._LensDirt_Params, bloomUberParams.lensDirtParams);
            material.SetVector(PostProcessShaderConstants._Grain_Params, grainParams);
            material.SetVector(PostProcessShaderConstants._Grain_TilingParams, grainTilingParams);
            material.SetVector(PostProcessShaderConstants._Dithering_Params, ditheringParams);
            material.SetVector(ShaderPropertyId.hdrOutputLuminanceParams, hdrOutputLuminanceParams);
            material.SetVector(PostProcessShaderConstants._OffscreenUIViewportParams, offscreenUIViewportParams);

            if (requireHDROutput)
                HDROutputUtils.ConfigureHDROutput(material, cameraData.hdrDisplayColorGamut, hdrOperations);

            var profilingSampler = GetProfilingSampler(presentationMode);
            using (var builder = renderGraph.AddRasterRenderPass<PresentationPassData>(profilingSampler.name, out var passData, profilingSampler))
            {
                passData.destinationTexture = destTexture;
                builder.SetRenderAttachment(destTexture, 0, AccessFlags.Write);
                passData.sourceTexture = sourceTexture;
                builder.UseTexture(sourceTexture, AccessFlags.Read);
                if (presentationMode != PresentationMode.Identity && useColorLut)
                {
                    passData.lutTexture = lutTexture;
                    builder.UseTexture(lutTexture, AccessFlags.Read);
                    passData.userLutTexture = userLutTexture;
                    if (userLutTexture.IsValid())
                        builder.UseTexture(userLutTexture, AccessFlags.Read);
                }
                if (bloomTextureActive && bloomTexture.IsValid())
                {
                    passData.bloomTexture = bloomTexture;
                    builder.UseTexture(bloomTexture, AccessFlags.Read);
                }
                if (screenSpaceLensFlareActive && screenSpaceLensFlareTexture.IsValid())
                {
                    passData.screenSpaceLensFlareTexture = screenSpaceLensFlareTexture;
                    builder.UseTexture(screenSpaceLensFlareTexture, AccessFlags.Read);
                }
                if (bloomLensDirt)
                {
                    passData.lensDirtTexture = lensDirtTexture;
                    builder.UseTexture(lensDirtTexture, AccessFlags.Read);
                }
                if (filmGrainActive && grainTexture.IsValid())
                {
                    passData.grainTexture = grainTexture;
                    builder.UseTexture(grainTexture, AccessFlags.Read);
                }
                if (ditheringActive && blueNoiseTexture.IsValid())
                {
                    passData.blueNoiseTexture = blueNoiseTexture;
                    builder.UseTexture(blueNoiseTexture, AccessFlags.Read);
                }
                if (hdrOverlay)
                {
                    passData.overlayUITexture = overlayUITexture;
                    builder.UseTexture(overlayUITexture, AccessFlags.Read);
                }
                if (autoExposure.IsActive)
                {
                    passData.exposureTexture = autoExposure.Texture;
                    builder.UseTexture(autoExposure.Texture, AccessFlags.Read);
                }
                passData.material = material;
                passData.cameraData = cameraData;
                passData.isActiveTargetBackBuffer = isActiveTargetBackBuffer;
                passData.shaderPass = (int)presentationMode;

                builder.SetRenderFunc(static (PresentationPassData data, RasterGraphContext context) =>
                {
                    if (data.lutTexture.IsValid())
                    {
                        data.material.SetTexture(PostProcessShaderConstants._InternalLut, data.lutTexture);
                        data.material.SetTexture(PostProcessShaderConstants._UserLut, data.userLutTexture);
                    }
                    if (data.bloomTexture.IsValid())
                    {
                        data.material.SetTexture(PostProcessShaderConstants._Bloom_Texture, data.bloomTexture);
                        Vector4 bloomScaleBias = RenderingUtils.GetFinalBlitScaleBias(in context, in data.bloomTexture, in data.destinationTexture);
                        data.material.SetVector(PostProcessShaderConstants._Bloom_TextureScaleBias, bloomScaleBias);
                    }
                    if (data.screenSpaceLensFlareTexture.IsValid())
                    {
                        data.material.SetTexture(PostProcessShaderConstants._ScreenSpaceLensFlareTexture, data.screenSpaceLensFlareTexture);
                        Vector4 flareScaleBias = RenderingUtils.GetFinalBlitScaleBias(in context, in data.screenSpaceLensFlareTexture, in data.destinationTexture);
                        data.material.SetVector(PostProcessShaderConstants._ScreenSpaceLensFlareTextureScaleBias, flareScaleBias);
                    }
                    if (data.lensDirtTexture.IsValid())
                        data.material.SetTexture(PostProcessShaderConstants._LensDirt_Texture, data.lensDirtTexture);
                    if (data.grainTexture.IsValid())
                        data.material.SetTexture(PostProcessShaderConstants._Grain_Texture, data.grainTexture);
                    if (data.blueNoiseTexture.IsValid())
                        data.material.SetTexture(PostProcessShaderConstants._BlueNoise_Texture, data.blueNoiseTexture);
                    if (data.overlayUITexture.IsValid())
                        data.material.SetTexture(PostProcessShaderConstants._OverlayUITexture, data.overlayUITexture);
                    if (data.exposureTexture.IsValid())
                        data.material.SetTexture(PostProcessShaderConstants._AutoExposureTexture, data.exposureTexture);

                    PostProcessBlitUtils.ScaleViewportAndBlit(in context, in data.sourceTexture, in data.destinationTexture, data.cameraData, data.material, data.isActiveTargetBackBuffer, data.shaderPass);
                });
            }
        }
    }
}
