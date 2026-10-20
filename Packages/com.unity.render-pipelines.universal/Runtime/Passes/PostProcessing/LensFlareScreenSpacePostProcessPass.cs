using System;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering.RenderGraphModule;

namespace UnityEngine.Rendering.Universal
{
    internal sealed class LensFlareScreenSpacePostProcessPass : PostProcessPass<ScreenSpaceLensFlare>
    {
        private const int k_GroupSizeX = 16;
        private const int k_GroupSizeY = 8;

        private static readonly int s_BloomMipTexture = Shader.PropertyToID("_LensFlareScreenSpaceBloomMipTexture");
        private static readonly int s_StreakTexture = Shader.PropertyToID("_LensFlareScreenSpaceStreakTex");
        private static readonly int s_MipLevel = Shader.PropertyToID("_LensFlareScreenSpaceMipLevel");
        private static readonly int s_TintColor = Shader.PropertyToID("_LensFlareScreenSpaceTintColor");
        private static readonly int s_Params1 = Shader.PropertyToID("_LensFlareScreenSpaceParams1");
        private static readonly int s_Params2 = Shader.PropertyToID("_LensFlareScreenSpaceParams2");
        private static readonly int s_Params3 = Shader.PropertyToID("_LensFlareScreenSpaceParams3");
        private static readonly int s_Params4 = Shader.PropertyToID("_LensFlareScreenSpaceParams4");
        private static readonly int s_Params5 = Shader.PropertyToID("_LensFlareScreenSpaceParams5");
        private static readonly int s_BloomMipTexelSize = Shader.PropertyToID("_LensFlareScreenSpaceBloomMipTexture_TexelSize");
        private static readonly int s_StreakTexelSize = Shader.PropertyToID("_LensFlareScreenSpaceStreakTex_TexelSize");
        private static readonly int s_FeatureTexture = Shader.PropertyToID("_LensFlareFeatureTexture");
        private static readonly int s_FeatureTexelSize = Shader.PropertyToID("_LensFlareFeatureTexture_TexelSize");
        private static readonly int s_OutputTexture = Shader.PropertyToID("_LensFlareOutputTexture");
        private static readonly int s_LogicalSize = Shader.PropertyToID("_LensFlareLogicalSize");
        private static readonly int s_HasStreak = Shader.PropertyToID("_LensFlareHasStreak");
        private static readonly int s_FlipY = Shader.PropertyToID("_LensFlareFlipY");

        private readonly GraphicsFormat m_ColorFormat;
        private readonly ComputeShader m_ComposeShader;
        private readonly int m_ComposeKernel;
        private readonly int m_BlurKernel;

        private readonly struct Parameters
        {
            internal readonly Color tint;
            internal readonly Vector4 value1;
            internal readonly Vector4 value2;
            internal readonly Vector4 value3;
            internal readonly Vector4 value4;
            internal readonly Vector4 value5;

            internal Parameters(ScreenSpaceLensFlare settings, int width, int height, int downsample)
            {
                tint = settings.tintColor.value;
                value1 = new Vector4(
                    settings.intensity.value,
                    settings.firstFlareIntensity.value,
                    settings.secondaryFlareIntensity.value,
                    settings.warpedFlareIntensity.value);
                value2 = new Vector4(
                    Mathf.Pow(settings.vignetteEffect.value, 0.25f),
                    settings.startingPosition.value,
                    settings.scale.value,
                    0f);
                value3 = new Vector4(
                    settings.samples.value,
                    settings.sampleDimmer.value,
                    settings.chromaticAbberationIntensity.value / 20f,
                    0f);
                value4 = new Vector4(
                    settings.streaksIntensity.value,
                    settings.streaksLength.value * width * 0.005f,
                    settings.streaksOrientation.value / 90f,
                    settings.streaksThreshold.value);
                value5 = new Vector4(
                    downsample,
                    width / (float)Mathf.Max(1, height) / Mathf.Max(settings.warpedFlareScale.value.x, 1e-6f),
                    1f / Mathf.Max(settings.warpedFlareScale.value.y, 1e-6f),
                    0f);
            }
        }

        private sealed class PrefilterPassData
        {
            internal TextureHandle bloomMip;
            internal Material material;
            internal int passIndex;
            internal Parameters parameters;
        }

        private sealed class StreakPassData
        {
            internal TextureHandle source;
            internal Material material;
            internal int passIndex;
            internal int mipLevel;
            internal Parameters parameters;
        }

        private sealed class ComposePassData
        {
            internal ComputeShader shader;
            internal int kernel;
            internal TextureHandle bloomMip;
            internal TextureHandle streak;
            internal TextureHandle destination;
            internal Parameters parameters;
            internal Vector4 bloomMipTexelSize;
            internal Vector4 streakTexelSize;
            internal Vector4 logicalSize;
            internal int hasStreak;
            internal int flipY;
            internal int width;
            internal int height;
        }

        private sealed class BlurPassData
        {
            internal ComputeShader shader;
            internal int kernel;
            internal TextureHandle source;
            internal TextureHandle destination;
            internal Vector4 sourceTexelSize;
            internal Vector4 logicalSize;
            internal int width;
            internal int height;
        }

        public LensFlareScreenSpacePostProcessPass(PostProcessMaterialLibrary materials, GraphicsFormat colorFormat)
            : base(materials)
        {
            m_ColorFormat = colorFormat;
            m_ComposeShader = materials.lensFlareScreenSpaceCompose;
            if (m_ComposeShader != null)
            {
                m_ComposeKernel = m_ComposeShader.FindKernel("KComposeFeatures");
                m_BlurKernel = m_ComposeShader.FindKernel("KBlurFeatures");
            }
        }

        public TextureHandle RenderLensFlareScreenSpace(RenderGraph renderGraph, Camera camera, in TextureDesc srcDesc,
            TextureHandle screenSpaceLensFlareBloomMipTexture)
        {
            if (!IsEnabledForCamera(camera))
                return TextureHandle.nullHandle;
            if (m_ComposeShader == null)
                throw new InvalidOperationException("LensFlareScreenSpaceCompose.compute is required for screen-space lens flare.");

            var material = Materials.lensFlareScreenSpace;
            int prefilterPass = material.FindPass("LensFlareScreenSpac Prefilter");
            int downsamplePass = material.FindPass("LensFlareScreenSpace Downsample");
            int upsamplePass = material.FindPass("LensFlareScreenSpace Upsample");

            int downsample = (int)Settings.resolution.value;
            int flareWidth = Math.Max(srcDesc.width / downsample, 1);
            int flareHeight = Math.Max(srcDesc.height / downsample, 1);
            var parameters = new Parameters(Settings, srcDesc.width, srcDesc.height, downsample);
            var flareDesc = GetCompatibleDescriptor(srcDesc, flareWidth, flareHeight, m_ColorFormat);

            TextureHandle finalStreak = TextureHandle.nullHandle;
            if (parameters.value4.x > 0f)
            {
                var streakA = CreateCompatibleTexture(renderGraph, flareDesc, "_StreakTmpTexture", false, FilterMode.Bilinear);
                var streakB = CreateCompatibleTexture(renderGraph, flareDesc, "_StreakTmpTexture2", false, FilterMode.Bilinear);

                AddPrefilterPass(renderGraph, screenSpaceLensFlareBloomMipTexture, streakA, material,
                    prefilterPass, parameters);

                int maxLevel = Mathf.FloorToInt(Mathf.Log(Mathf.Max(srcDesc.height, srcDesc.width), 2f));
                int downsampleCount = Mathf.Max(1, maxLevel);
                TextureHandle streakSource = streakA;
                TextureHandle streakDestination = streakB;

                for (int i = 0; i < downsampleCount; i++)
                {
                    AddStreakPass(renderGraph, "Lens Flare Streak Downsample", streakSource, streakDestination,
                        material, downsamplePass, i, parameters);
                    (streakSource, streakDestination) = (streakDestination, streakSource);
                }

                for (int i = 0; i < 2; i++)
                {
                    AddStreakPass(renderGraph, "Lens Flare Streak Upsample", streakSource, streakDestination,
                        material, upsamplePass, i, parameters);
                    (streakSource, streakDestination) = (streakDestination, streakSource);
                }

                finalStreak = streakSource;
            }

            // Regular screen-space flare ghosts retain recognizable source-image structure unless
            // feature generation is followed by a low-pass stage. Keep generation at the configured
            // low resolution, blur it there, then let presentation reconstruct it together with Bloom.
            var featureDesc = flareDesc;
            MakeComputeCompatible(ref featureDesc);
            var rawFeatures = CreateCompatibleTexture(renderGraph, featureDesc, "_LensFlareFeaturesRaw", false, FilterMode.Bilinear);
            var blurredFeatures = CreateCompatibleTexture(renderGraph, featureDesc, "_LensFlareFeaturesBlurred", false, FilterMode.Bilinear);

            AddComposePass(renderGraph, screenSpaceLensFlareBloomMipTexture, finalStreak, rawFeatures,
                parameters, flareWidth, flareHeight);
            AddBlurPass(renderGraph, rawFeatures, blurredFeatures, flareWidth, flareHeight);

            return blurredFeatures;
        }

        private static void AddPrefilterPass(RenderGraph renderGraph, TextureHandle bloomMip,
            TextureHandle destination, Material material, int passIndex, Parameters parameters)
        {
            using (var builder = renderGraph.AddRasterRenderPass<PrefilterPassData>("Lens Flare Streak Prefilter",
                       out var passData, ProfilingSampler.Get(URPProfileId.LensFlareScreenSpace)))
            {
                passData.bloomMip = bloomMip;
                passData.material = material;
                passData.passIndex = passIndex;
                passData.parameters = parameters;
                builder.UseTexture(bloomMip, AccessFlags.Read);
                builder.SetRenderAttachment(destination, 0, AccessFlags.WriteAll);
                builder.SetRenderFunc(static (PrefilterPassData data, RasterGraphContext context) =>
                {
                    SetCommonParameters(data.material, data.parameters);
                    data.material.SetTexture(s_BloomMipTexture, data.bloomMip);
                    DrawFullscreenQuad(context, data.material, data.passIndex);
                });
            }
        }

        private static void AddStreakPass(RenderGraph renderGraph, string name, TextureHandle source,
            TextureHandle destination, Material material, int passIndex, int mipLevel, Parameters parameters)
        {
            using (var builder = renderGraph.AddRasterRenderPass<StreakPassData>(name, out var passData))
            {
                passData.source = source;
                passData.material = material;
                passData.passIndex = passIndex;
                passData.mipLevel = mipLevel;
                passData.parameters = parameters;
                builder.UseTexture(source, AccessFlags.Read);
                builder.SetRenderAttachment(destination, 0, AccessFlags.WriteAll);
                builder.SetRenderFunc(static (StreakPassData data, RasterGraphContext context) =>
                {
                    SetCommonParameters(data.material, data.parameters);
                    data.material.SetInt(s_MipLevel, data.mipLevel);
                    data.material.SetTexture(s_StreakTexture, data.source);
                    DrawFullscreenQuad(context, data.material, data.passIndex);
                });
            }
        }

        private void AddComposePass(RenderGraph renderGraph, TextureHandle bloomMip, TextureHandle streak,
            TextureHandle destination, Parameters parameters, int width, int height)
        {
            var bloomDesc = bloomMip.GetDescriptor(renderGraph);
            var streakHandle = streak.IsValid() ? streak : bloomMip;
            var streakDesc = streakHandle.GetDescriptor(renderGraph);

            using (var builder = renderGraph.AddComputePass<ComposePassData>(
                       "Lens Flare Feature Generation", out var passData,
                       ProfilingSampler.Get(URPProfileId.LensFlareScreenSpace)))
            {
                passData.shader = m_ComposeShader;
                passData.kernel = m_ComposeKernel;
                passData.bloomMip = bloomMip;
                passData.streak = streakHandle;
                passData.destination = destination;
                passData.parameters = parameters;
                passData.bloomMipTexelSize = new Vector4(1f / Mathf.Max(1, bloomDesc.width), 1f / Mathf.Max(1, bloomDesc.height), bloomDesc.width, bloomDesc.height);
                passData.streakTexelSize = new Vector4(1f / Mathf.Max(1, streakDesc.width), 1f / Mathf.Max(1, streakDesc.height), streakDesc.width, streakDesc.height);
                passData.logicalSize = BuildSizeVector(width, height);
                passData.hasStreak = streak.IsValid() ? 1 : 0;
                passData.flipY = SystemInfo.graphicsUVStartsAtTop ? 1 : 0;
                passData.width = width;
                passData.height = height;

                builder.UseTexture(bloomMip, AccessFlags.Read);
                if (streak.IsValid() && streak != bloomMip)
                    builder.UseTexture(streak, AccessFlags.Read);
                builder.UseTexture(destination, AccessFlags.Write);
                builder.SetRenderFunc(static (ComposePassData data, ComputeGraphContext context) =>
                {
                    var cmd = context.cmd;
                    cmd.SetComputeTextureParam(data.shader, data.kernel, s_BloomMipTexture, data.bloomMip);
                    cmd.SetComputeTextureParam(data.shader, data.kernel, s_StreakTexture, data.streak);
                    cmd.SetComputeTextureParam(data.shader, data.kernel, s_OutputTexture, data.destination);
                    cmd.SetComputeVectorParam(data.shader, s_BloomMipTexelSize, data.bloomMipTexelSize);
                    cmd.SetComputeVectorParam(data.shader, s_StreakTexelSize, data.streakTexelSize);
                    cmd.SetComputeVectorParam(data.shader, s_LogicalSize, data.logicalSize);
                    cmd.SetComputeIntParam(data.shader, s_HasStreak, data.hasStreak);
                    cmd.SetComputeIntParam(data.shader, s_FlipY, data.flipY);
                    cmd.SetComputeVectorParam(data.shader, s_TintColor, (Vector4)data.parameters.tint);
                    cmd.SetComputeVectorParam(data.shader, s_Params1, data.parameters.value1);
                    cmd.SetComputeVectorParam(data.shader, s_Params2, data.parameters.value2);
                    cmd.SetComputeVectorParam(data.shader, s_Params3, data.parameters.value3);
                    cmd.SetComputeVectorParam(data.shader, s_Params4, data.parameters.value4);
                    cmd.SetComputeVectorParam(data.shader, s_Params5, data.parameters.value5);
                    cmd.DispatchCompute(data.shader, data.kernel,
                        (data.width + k_GroupSizeX - 1) / k_GroupSizeX,
                        (data.height + k_GroupSizeY - 1) / k_GroupSizeY,
                        1);
                });
            }
        }

        private void AddBlurPass(RenderGraph renderGraph, TextureHandle source, TextureHandle destination,
            int width, int height)
        {
            using (var builder = renderGraph.AddComputePass<BlurPassData>(
                       "Lens Flare Feature Low Pass", out var passData,
                       ProfilingSampler.Get(URPProfileId.LensFlareScreenSpace)))
            {
                passData.shader = m_ComposeShader;
                passData.kernel = m_BlurKernel;
                passData.source = source;
                passData.destination = destination;
                passData.sourceTexelSize = new Vector4(1f / Mathf.Max(1, width), 1f / Mathf.Max(1, height), width, height);
                passData.logicalSize = BuildSizeVector(width, height);
                passData.width = width;
                passData.height = height;

                builder.UseTexture(source, AccessFlags.Read);
                builder.UseTexture(destination, AccessFlags.Write);
                builder.SetRenderFunc(static (BlurPassData data, ComputeGraphContext context) =>
                {
                    var cmd = context.cmd;
                    cmd.SetComputeTextureParam(data.shader, data.kernel, s_FeatureTexture, data.source);
                    cmd.SetComputeTextureParam(data.shader, data.kernel, s_OutputTexture, data.destination);
                    cmd.SetComputeVectorParam(data.shader, s_FeatureTexelSize, data.sourceTexelSize);
                    cmd.SetComputeVectorParam(data.shader, s_LogicalSize, data.logicalSize);
                    cmd.DispatchCompute(data.shader, data.kernel,
                        (data.width + k_GroupSizeX - 1) / k_GroupSizeX,
                        (data.height + k_GroupSizeY - 1) / k_GroupSizeY,
                        1);
                });
            }
        }

        private static Vector4 BuildSizeVector(int width, int height)
        {
            width = Mathf.Max(1, width);
            height = Mathf.Max(1, height);
            return new Vector4(width, height, 1f / width, 1f / height);
        }

        private static void DrawFullscreenQuad(RasterGraphContext context, Material material, int passIndex)
        {
            context.cmd.DrawProcedural(Matrix4x4.identity, material, passIndex, MeshTopology.Quads, 4, 1);
        }

        private static void SetCommonParameters(Material material, Parameters parameters)
        {
            material.SetColor(s_TintColor, parameters.tint);
            material.SetVector(s_Params1, parameters.value1);
            material.SetVector(s_Params2, parameters.value2);
            material.SetVector(s_Params3, parameters.value3);
            material.SetVector(s_Params4, parameters.value4);
            material.SetVector(s_Params5, parameters.value5);
        }

        private static bool IsEnabledForCamera(Camera camera)
        {
#if UNITY_EDITOR
            if (camera.cameraType == CameraType.SceneView)
            {
                for (int i = 0; i < UnityEditor.SceneView.sceneViews.Count; i++)
                {
                    if (UnityEditor.SceneView.sceneViews[i] is UnityEditor.SceneView sceneView &&
                        sceneView.camera == camera && !sceneView.sceneViewState.flaresEnabled)
                        return false;
                }
            }
#endif
            return true;
        }
    }
}
