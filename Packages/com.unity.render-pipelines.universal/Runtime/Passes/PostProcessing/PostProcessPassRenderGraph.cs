using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal.Internal;

namespace UnityEngine.Rendering.Universal
{
    internal sealed class PostProcessPassRenderGraph
    {
        private readonly PostProcessMaterialLibrary m_Materials;
        private readonly AutoExposurePass m_AutoExposure;

        private readonly StopNaNsPostProcessPass m_StopNaNsPass;
        private readonly SMAAPostProcessPass m_SMAAPass;
        private readonly BloomPostProcessPass m_BloomPass;
        private readonly DepthOfFieldPostProcessPass m_DepthOfFieldPass;
        private readonly TemporalAAPostProcessPass m_TemporalAAPass;
        private readonly STPPostProcessPass m_STPPass;
        private readonly Fsr3UpscalePass m_Fsr3UpscalePass;
        private readonly MotionBlurPostProcessPass m_MotionBlurPass;
        private readonly PaniniProjectionPostProcessPass m_PaniniProjectionPass;
        private readonly LensFlareDataDrivenPostProcessPass m_LensFlareDataDrivenPass;
        private readonly LensFlareScreenSpacePostProcessPass m_LensFlareScreenSpacePass;
        private readonly UberPostProcessPass m_PresentationPass;
        private readonly FinalPostProcessPass m_FinalPass;
        private readonly PostProcessPass[] m_Passes;

        private readonly LensDistortionPostProcessEffect m_LensDistortionEffect;
        private readonly FilmGrainPostProcessEffect m_FilmGrainEffect;
        private readonly DitheringPostProcessEffect m_DitheringEffect;

        /// <summary>
        /// Creates a new <c>PostProcessPassRenderGraph</c> instance.
        /// </summary>
        /// <param name="data">The <c>PostProcessData</c> resources to use.</param>
        /// <param name="requestPostProColorFormat">Requested <c>GraphicsFormat</c> for postprocess rendering.</param>
        /// <seealso cref="RenderPassEvent"/>
        /// <seealso cref="PostProcessData"/>
        /// <seealso cref="GraphicsFormat"/>
        public PostProcessPassRenderGraph(PostProcessData data, GraphicsFormat requestPostProColorFormat)
        {
            Assertions.Assert.IsNotNull(data, "PostProcessData and resources cannot be null.");

            m_Materials = new PostProcessMaterialLibrary(data);
            m_AutoExposure = new AutoExposurePass(data);

            // NOTE: Request color format is the back-buffer color format. It can be HDR or SDR (when HDR disabled).
            // Request color might have alpha or might not have alpha.
            // The actual post-process target can be different. A RenderTexture with a custom format. Not necessarily a back-buffer.
            // A RenderTexture with a custom format can have an alpha channel, regardless of the back-buffer setting,
            // so the post-processing should just use the current target format/alpha to toggle alpha output.
            //
            // However, we want to filter out the alpha shader variants when not used (common case).
            // The rule is that URP post-processing format follows the back-buffer format setting.

            bool requestHDR = PostProcessPassUtils.IsHDRFormat(requestPostProColorFormat);
            var defaultFormat = GraphicsFormat.None;

            // Texture format pre-lookup
            // UUM-41070: We require `Linear | Render` but with the deprecated FormatUsage this was checking `Blend`
            // For now, we keep checking for `Blend` until the performance hit of doing the correct checks is evaluated
            if (requestHDR)
            {
                const GraphicsFormatUsage usage = GraphicsFormatUsage.Blend;
                if (SystemInfo.IsFormatSupported(requestPostProColorFormat, usage))
                {
                    defaultFormat = requestPostProColorFormat;
                }
                else if (SystemInfo.IsFormatSupported(GraphicsFormat.B10G11R11_UFloatPack32, usage))
                {
                    defaultFormat = GraphicsFormat.B10G11R11_UFloatPack32;
                }
                else
                {
                    defaultFormat = QualitySettings.activeColorSpace == ColorSpace.Linear
                        ? GraphicsFormat.R8G8B8A8_SRGB
                        : GraphicsFormat.R8G8B8A8_UNorm;
                }
            }
            else
            {
                defaultFormat = QualitySettings.activeColorSpace == ColorSpace.Linear
                    ? GraphicsFormat.R8G8B8A8_SRGB
                    : GraphicsFormat.R8G8B8A8_UNorm;
            }

            var bloomColorFormat = SystemInfo.IsFormatSupported(GraphicsFormat.B10G11R11_UFloatPack32, GraphicsFormatUsage.LoadStore)
                ? GraphicsFormat.B10G11R11_UFloatPack32
                : defaultFormat;

            const GraphicsFormat smaaEdgeFormat = GraphicsFormat.R8G8B8A8_UNorm;

            GraphicsFormat gaussianCoCFormat;
            if (SystemInfo.IsFormatSupported(GraphicsFormat.R16_UNorm, GraphicsFormatUsage.Blend))
                gaussianCoCFormat = GraphicsFormat.R16_UNorm;
            else if (SystemInfo.IsFormatSupported(GraphicsFormat.R16_SFloat, GraphicsFormatUsage.Blend))
                gaussianCoCFormat = GraphicsFormat.R16_SFloat;
            else
                gaussianCoCFormat = GraphicsFormat.R8_UNorm;

            var gaussianDoFColorFormat = defaultFormat;
            var lensFlareScreenSpaceColorFormat = defaultFormat;

            m_LensDistortionEffect = new LensDistortionPostProcessEffect();
            m_FilmGrainEffect = new FilmGrainPostProcessEffect();
            m_DitheringEffect = new DitheringPostProcessEffect();

            m_StopNaNsPass = new StopNaNsPostProcessPass(m_Materials);
            m_SMAAPass = new SMAAPostProcessPass(m_Materials, smaaEdgeFormat);
            m_BloomPass = new BloomPostProcessPass(m_Materials, bloomColorFormat);
            m_DepthOfFieldPass = new DepthOfFieldPostProcessPass(m_Materials, gaussianCoCFormat, gaussianDoFColorFormat);
            m_TemporalAAPass = new TemporalAAPostProcessPass(m_Materials);
            m_STPPass = new STPPostProcessPass(m_Materials);
            m_Fsr3UpscalePass = new Fsr3UpscalePass(data);
            m_MotionBlurPass = new MotionBlurPostProcessPass(m_Materials);
            m_PaniniProjectionPass = new PaniniProjectionPostProcessPass(m_Materials);
            m_LensFlareDataDrivenPass = new LensFlareDataDrivenPostProcessPass(m_Materials);
            m_LensFlareScreenSpacePass = new LensFlareScreenSpacePostProcessPass(m_Materials, lensFlareScreenSpaceColorFormat);
            m_PresentationPass = new UberPostProcessPass(m_Materials);
            m_FinalPass = new FinalPostProcessPass(m_Materials, m_FilmGrainEffect, m_DitheringEffect);

            m_Passes = new PostProcessPass[]
            {
                m_AutoExposure,
                m_StopNaNsPass,
                m_SMAAPass,
                m_BloomPass,
                m_DepthOfFieldPass,
                m_TemporalAAPass,
                m_STPPass,
                m_Fsr3UpscalePass,
                m_MotionBlurPass,
                m_PaniniProjectionPass,
                m_LensFlareDataDrivenPass,
                m_LensFlareScreenSpacePass,
                m_PresentationPass,
                m_FinalPass
            };
        }

        public void Cleanup()
        {
            Dispose();
            m_Materials.Cleanup();
        }

        /// <summary>
        /// Disposes used resources.
        /// </summary>
        public void Dispose()
        {
            foreach (var pass in m_Passes)
                pass.Dispose();
        }

        public void RenderFinalPassRenderGraph(RenderGraph renderGraph, ContextContainer frameData, in TextureHandle source, in TextureHandle overlayUITexture, in TextureHandle postProcessingTarget, bool enableColorEncodingIfNeeded)
        {
            renderGraph.BeginProfilingSampler(ProfilingSampler.Get(URPProfileId.PostProcessing));
            renderGraph.BeginProfilingSampler(ProfilingSampler.Get(URPProfileId.FinalPostProcessing));
            m_FinalPass.RenderFinalPassRenderGraph(renderGraph, frameData, in source, in overlayUITexture, in postProcessingTarget, enableColorEncodingIfNeeded);
            renderGraph.EndProfilingSampler(ProfilingSampler.Get(URPProfileId.FinalPostProcessing));
            renderGraph.EndProfilingSampler(ProfilingSampler.Get(URPProfileId.PostProcessing));
        }

        public void RenderPostProcessingRenderGraph(RenderGraph renderGraph, ContextContainer frameData, in TextureHandle activeCameraColorTexture, in TextureHandle lutTexture, in TextureHandle overlayUITexture, in TextureHandle postProcessingTarget, bool isActiveTargetBackBuffer, bool hasFinalPass, bool resolveToDebugScreen, bool enableColorEndingIfNeeded, bool allowFinalPassDirectSource = false)
        {
            renderGraph.BeginProfilingSampler(ProfilingSampler.Get(URPProfileId.PostProcessing));

            var resourceData = frameData.Get<UniversalResourceData>();
            var cameraData = frameData.Get<UniversalCameraData>();
            var postProcessingData = frameData.Get<UniversalPostProcessingData>();

            var stack = VolumeManager.instance.stack;
            var depthOfField = stack.GetComponent<DepthOfField>();
            var motionBlur = stack.GetComponent<MotionBlur>();
            var paniniProjection = stack.GetComponent<PaniniProjection>();
            var bloom = stack.GetComponent<Bloom>();
            var lensFlareScreenSpace = stack.GetComponent<ScreenSpaceLensFlare>();
            var lensDistortion = stack.GetComponent<LensDistortion>();
            var chromaticAberration = stack.GetComponent<ChromaticAberration>();
            var vignette = stack.GetComponent<Vignette>();
            var colorLookup = stack.GetComponent<ColorLookup>();
            var colorAdjustments = stack.GetComponent<ColorAdjustments>();
            var tonemapping = stack.GetComponent<Tonemapping>();
            var filmGrain = stack.GetComponent<FilmGrain>();
            bool useFastSRGBLinearConversion = postProcessingData.useFastSRGBLinearConversion;
            bool supportDataDrivenLensFlare = postProcessingData.supportDataDrivenLensFlare;
            bool supportScreenSpaceLensFlare = postProcessingData.supportScreenSpaceLensFlare;

            
            m_DepthOfFieldPass.SetSettings(depthOfField, useFastSRGBLinearConversion);
            m_MotionBlurPass.SetSettings(motionBlur);
            m_PaniniProjectionPass.SetSettings(paniniProjection);
            m_BloomPass.SetSettings(bloom);
            m_LensFlareScreenSpacePass.SetSettings(lensFlareScreenSpace);
            m_PresentationPass.SetSettings(lensDistortion, chromaticAberration, vignette, colorLookup, colorAdjustments, tonemapping, filmGrain, hasFinalPass, enableColorEndingIfNeeded);

            ref var renderer = ref cameraData.renderer;
            bool isSceneViewCamera = cameraData.isSceneViewCamera;

            //We blit back and forth without msaa untill the last blit.
            bool useStopNan = cameraData.isStopNaNEnabled && m_Materials.stopNaN != null;
            bool useSubPixelMorpAA = cameraData.effectiveAntialiasing == AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            var dofMaterial = depthOfField.mode.value == DepthOfFieldMode.Gaussian ? m_Materials.gaussianDepthOfField : m_Materials.bokehDepthOfField;
            bool useDepthOfField = depthOfField.IsActive() && !isSceneViewCamera && dofMaterial != null;
            bool useLensFlare = !LensFlareCommonSRP.Instance.IsEmpty() && supportDataDrivenLensFlare;
            bool useLensFlareScreenSpace = lensFlareScreenSpace.IsActive() && supportScreenSpaceLensFlare;
            bool useMotionBlur = motionBlur.IsActive() && !isSceneViewCamera;
            bool usePaniniProjection = paniniProjection.IsActive() && !isSceneViewCamera;
            // Disable MotionBlurMode.CameraAndObjects on renderers that do not support motion vectors
            if (useMotionBlur && motionBlur.mode.value == MotionBlurMode.CameraAndObjects)
            {
                useMotionBlur &= renderer.SupportsMotionVectors();
                if (!useMotionBlur)
                {
                    var warning = "Disabling Motion Blur for Camera And Objects because the renderer does not implement motion vectors.";
                    const int warningThrottleFrames = 60 * 1; // 60 FPS * 1 sec
                    if (Time.frameCount % warningThrottleFrames == 0)
                        Debug.LogWarning(warning);
                }
            }

            // Note that enabling jitters uses the same CameraData::IsTemporalAAEnabled(). So if we add any other kind of overrides (like
            // disable useTemporalAA if another feature is disabled) then we need to put it in CameraData::IsTemporalAAEnabled() as opposed
            // to tweaking the value here.
            bool useTemporalAA = cameraData.IsTemporalAAEnabled();
            bool useFsr3 = cameraData.IsFsr3Enabled();

            // STP is only enabled when TAA is enabled and all of its runtime requirements are met.
            // Using IsSTPRequested() vs IsSTPEnabled() for perf reason here, as we already know TAA status
            bool isSTPRequested = cameraData.IsSTPRequested();
            bool useSTP = useTemporalAA && isSTPRequested;

            // Warn users if TAA and STP are disabled despite being requested
            if (!useTemporalAA && cameraData.antialiasing == AntialiasingMode.TemporalAntiAliasing)
                TemporalAA.ValidateAndWarn(cameraData, isSTPRequested);

            var currentSource = activeCameraColorTexture;

            // Optional NaN killer before post-processing kicks in. Dynamic exposure is updated from
            // the sanitized source before temporal resolve or presentation-only passes.
            if (useStopNan)
                currentSource = m_StopNaNsPass.Render(renderGraph, in currentSource);

            var autoExposure = m_AutoExposure.Render(renderGraph, frameData, in currentSource);

            if (useSubPixelMorpAA)
                currentSource = m_SMAAPass.Render(renderGraph, resourceData, cameraData.antialiasingQuality, in currentSource);

            // Depth of Field
            if (useDepthOfField)
                currentSource = m_DepthOfFieldPass.Render(renderGraph, resourceData, cameraData, in currentSource);

            // Temporal anti-aliasing and temporal upscaling share one arbitration point.
#if ENABLE_UPSCALER_FRAMEWORK
            if (useTemporalAA && !useFsr3 && postProcessingData.activeUpscaler != null)
            {
                // Create a context item containing upscaling inputs
                UpscalingIO io = frameData.Create<UpscalingIO>();
                io.cameraColor = currentSource;
                io.cameraDepth = resourceData.cameraDepth;
                io.motionVectorColor = resourceData.motionVectorColor;
                io.motionVectorDomain = UpscalingIO.MotionVectorDomain.NDC;
                io.motionVectorDirection = UpscalingIO.MotionVectorDirection.PreviousFrameToCurrentFrame;
                io.jitteredMotionVectors = false; // URP has no jittering in MVs
                io.exposureTexture = autoExposure.Texture;
                io.preExposureValue = 1.0f; // URP color is not pre-exposed before the upscaler.
                io.hdrDisplayInformation = cameraData.isHDROutputActive ? cameraData.hdrDisplayInformation : new HDROutputUtils.HDRDisplayInformation(-1, -1, -1, 160.0f);
                io.preUpscaleResolution = new Vector2Int(
                    cameraData.cameraTargetDescriptor.width,
                    cameraData.cameraTargetDescriptor.height
                );
                io.previousPreUpscaleResolution = io.preUpscaleResolution; // URP doesn't support Dynamic Resolution Scaling (DRS).
                io.postUpscaleResolution = new Vector2Int(cameraData.pixelWidth, cameraData.pixelHeight);
                io.motionVectorTextureSize = io.preUpscaleResolution;
                io.enableTexArray = false;

                MotionVectorsPersistentData motionData = null;
                {
                    cameraData.camera.TryGetComponent<UniversalAdditionalCameraData>(out var additionalCameraData);
                    Debug.Assert(additionalCameraData != null);
                    motionData = additionalCameraData.motionVectorsPersistentData;
                    Debug.Assert(motionData != null);
                }
                io.cameraInstanceID = cameraData.camera.GetInstanceID();
                io.nearClipPlane = cameraData.camera.nearClipPlane;
                io.farClipPlane = cameraData.camera.farClipPlane;
                io.fieldOfViewDegrees = cameraData.camera.fieldOfView;
                io.invertedDepth = SystemInfo.usesReversedZBuffer;
                io.flippedY = SystemInfo.graphicsUVStartsAtTop;
                io.flippedX = false;
                io.hdrInput = GraphicsFormatUtility.IsHDRFormat(currentSource.GetDescriptor(renderGraph).format);
                io.numActiveViews = 1;
                io.eyeIndex = 0;
                io.worldSpaceCameraPositions = new Vector3[io.numActiveViews];
                io.previousWorldSpaceCameraPositions = new Vector3[io.numActiveViews];
                io.previousPreviousWorldSpaceCameraPositions = new Vector3[io.numActiveViews];
                for (int i = 0; i < io.numActiveViews; i++)
                {
                    io.worldSpaceCameraPositions[i] = motionData.worldSpaceCameraPos;
                    io.previousWorldSpaceCameraPositions[i] = motionData.previousWorldSpaceCameraPos;
                    io.previousPreviousWorldSpaceCameraPositions[i] = motionData.previousPreviousWorldSpaceCameraPos;
                }
                io.projectionMatrices = new[] { motionData.projection };
                io.previousProjectionMatrices = new[] { motionData.previousProjection };
                io.previousPreviousProjectionMatrices = new[] { motionData.previousPreviousProjection };
                io.viewMatrices = new[] { motionData.view };
                io.previousViewMatrices = new[] { motionData.previousView };
                io.previousPreviousViewMatrices = new[] { motionData.previousPreviousView };
                io.resetHistory = cameraData.resetHistory;
                // Keep external upscalers on the same deterministic jitter sequence as URP TAA.
                io.frameIndex = Time.frameCount + cameraData.taaSettings.jitterFrameCountOffset;
                io.deltaTime = motionData.deltaTime;
                io.previousDeltaTime = motionData.lastDeltaTime;
                io.blueNoiseTextureSet = m_Materials.resources.textures.blueNoise16LTex;

                // The motion scaling feature is only active outside of test environments. If we allowed it to run
                // during automated graphics tests, the results of each test run would be dependent on system
                // performance.
#if LWRP_DEBUG_STATIC_POSTFX
                io.enableMotionScaling = false;
#else
                io.enableMotionScaling = true;
#endif
                io.enableHwDrs = false; // URP doesn't support hardware dynamic resolution scaling
                // Insert the active upscaler's render graph passes
                postProcessingData.activeUpscaler.RecordRenderGraph(renderGraph, frameData);

                // Update the camera resolution to reflect the upscaled size
                var desc = io.cameraColor.GetDescriptor(renderGraph);
                PostProcessPassUtils.UpdateCameraResolution(renderGraph, cameraData, new Vector2Int(desc.width, desc.height));

                // Use the output texture of upscaling
                currentSource = io.cameraColor;
            }
            else
#endif
            if (useTemporalAA)
            {
                if (useFsr3)
                {
                    currentSource = m_Fsr3UpscalePass.Render(renderGraph, resourceData, cameraData, in currentSource, autoExposure.Texture);
                }
                else if (useSTP)
                {
                    currentSource = m_STPPass.Render(renderGraph, resourceData, cameraData, in currentSource);
                }
                else
                {
                    currentSource = m_TemporalAAPass.Render(renderGraph, resourceData, cameraData, in currentSource);
                }
            }

            if (useMotionBlur)
                currentSource = m_MotionBlurPass.Render(renderGraph, resourceData, cameraData, in currentSource);

            // Panini is a virtual sampling transform. It is consumed by Bloom prefilter and presentation
            // instead of materializing a full-resolution intermediate texture.
            var presentationSourceDesc = currentSource.GetDescriptor(renderGraph);
            var paniniParameters = usePaniniProjection
                ? m_PaniniProjectionPass.GetParameters(cameraData.camera, presentationSourceDesc.width, presentationSourceDesc.height)
                : PaniniProjectionParameters.Disabled;

            // Final presentation
            {
                var srcDesc = presentationSourceDesc;
                var bloomFrameData = frameData.GetOrCreate<BloomFrameData>();

                if (bloom.IsActive())
                {
                    m_BloomPass.RenderBloomTexture(renderGraph, cameraData, currentSource, bloomFrameData, in paniniParameters);
                    bloomFrameData.textureActive = true;

                    if (useLensFlareScreenSpace && bloomFrameData.textureActive && bloomFrameData.texture.IsValid())
                    {
                        // We need to take into account how many valid mips the bloom pass produced.
                        int maxBloomMip = Mathf.Max(0, bloomFrameData.mipCount - 2);
                        int useBloomMip = Mathf.Clamp(lensFlareScreenSpace.bloomMip.value, 0, maxBloomMip);

                        var bloomMipFlareSource = useBloomMip == 0 ? bloomFrameData.texture : bloomFrameData.GetReconstructedMip(useBloomMip);

                        bloomFrameData.screenSpaceLensFlareTexture = m_LensFlareScreenSpacePass.RenderLensFlareScreenSpace(
                            renderGraph, cameraData.camera, srcDesc, bloomMipFlareSource);
                        bloomFrameData.screenSpaceLensFlareActive = bloomFrameData.screenSpaceLensFlareTexture.IsValid();
                    }

                    if (bloomFrameData.textureActive)
                        bloomFrameData.uberPostParameters = m_BloomPass.GetUberPostParameters(bloomFrameData, srcDesc);
                }

                if (useLensFlare)
                {
                    renderGraph.BeginProfilingSampler(ProfilingSampler.Get(URPProfileId.LensFlareDataDriven));
                    m_LensFlareDataDrivenPass.LensFlareDataDrivenComputeOcclusion(renderGraph, resourceData, cameraData, srcDesc);
                    m_LensFlareDataDrivenPass.RenderLensFlareDataDriven(renderGraph, resourceData, cameraData, in currentSource, in srcDesc);
                    renderGraph.EndProfilingSampler(ProfilingSampler.Get(URPProfileId.LensFlareDataDriven));
                }

                bool requireHDROutput = PostProcessPassUtils.RequireHDROutput(cameraData);
                bool enableAlphaOutput = cameraData.isAlphaOutputEnabled;

                var debugHandler = ScriptableRenderPass.GetActiveDebugHandler(cameraData);
                debugHandler?.UpdateShaderGlobalPropertiesForFinalValidationPass(renderGraph, cameraData, !hasFinalPass && !resolveToDebugScreen);

                bool skipPresentation = allowFinalPassDirectSource && hasFinalPass &&
                    m_PresentationPass.CanFinalPassConsumeSourceDirectly(
                        in srcDesc, in lutTexture, bloomFrameData, in paniniParameters, autoExposure, requireHDROutput);

                if (skipPresentation)
                    resourceData.cameraColor = currentSource;
                else
                    m_PresentationPass.RenderPresentation(renderGraph, frameData, cameraData, postProcessingData, in currentSource, in postProcessingTarget, in lutTexture, bloomFrameData, in paniniParameters, in overlayUITexture, autoExposure, requireHDROutput, enableAlphaOutput, isActiveTargetBackBuffer, hasFinalPass);
            }

            renderGraph.EndProfilingSampler(ProfilingSampler.Get(URPProfileId.PostProcessing));
        }

    }
}
