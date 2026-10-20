using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal.Internal;

namespace UnityEngine.Rendering.Universal
{
    internal sealed class BloomPostProcessPass : PostProcessPass<Bloom>
    {
        private const int k_PrefilterThreadGroupSize = 8;
        private const int k_DownsampleThreadGroupSizeX = 16;
        private const int k_DownsampleThreadGroupSizeY = 16;
        private const int k_UpsampleThreadGroupSizeX = 16;
        private const int k_UpsampleThreadGroupSizeY = 8;
        private const int k_DownsampleTailMaxWidth = 16;
        private const int k_DownsampleTailMaxHeight = 16;
        private const int k_UpsampleTailFirstMaxWidth = 16;
        private const int k_UpsampleTailFirstMaxHeight = 8;
        private const int k_UpsampleTailSecondMaxWidth = 32;
        private const int k_UpsampleTailSecondMaxHeight = 24;
        private const float k_ThresholdSoftness = 0.5f;

        private readonly GraphicsFormat m_BloomColorFormat;
        private readonly string[] m_BloomMipDownNames;
        private readonly int[] m_PrefilterKernels = new int[16];
        private const string k_BloomResolvedOutputName = "Bloom Resolved";

        private int m_DownsampleKernel;
        private int m_Downsample2xKernel;
        private int m_DownsampleTail2Kernel;
        private int m_UpsampleLowQualityKernel;
        private int m_UpsampleHighQualityKernel;
        private int m_UpsampleTail2LowQualityKernel;
        private int m_UpsampleTail2HighQualityKernel;
        private int m_ResolveKernel;
        private bool m_KernelsInitialized;

        public BloomPostProcessPass(PostProcessMaterialLibrary materials, GraphicsFormat bloomColorFormat)
            : base(materials)
        {
            m_BloomColorFormat = bloomColorFormat;
            m_BloomMipDownNames = new string[PostProcessConstants.k_MaxPyramidSize];

            for (int i = 0; i < PostProcessConstants.k_MaxPyramidSize; i++)
                m_BloomMipDownNames[i] = "Bloom Mip Down " + i;
        }

        private void InitializeKernels()
        {
            if (m_KernelsInitialized)
                return;

            for (int index = 0; index < m_PrefilterKernels.Length; index++)
            {
                bool highQuality = (index & 1) != 0;
                bool alpha = (index & 2) != 0;
                bool panini = (index & 4) != 0;
                bool noThreshold = (index & 8) != 0;

                string name = highQuality ? "KMainHighQuality" : "KMainLowQuality";
                if (alpha)
                    name += "Alpha";
                if (panini)
                    name += "Panini";
                if (noThreshold)
                    name += "NoThreshold";

                m_PrefilterKernels[index] = Materials.bloomPrefilterCS.FindKernel(name);
            }

            m_DownsampleKernel = Materials.bloomBlurCS.FindKernel("KDownsample");
            m_Downsample2xKernel = Materials.bloomBlurCS.FindKernel("KDownsample2x");
            m_DownsampleTail2Kernel = Materials.bloomBlurCS.FindKernel("KDownsampleTail2");
            m_UpsampleLowQualityKernel = Materials.bloomUpsampleCS.FindKernel("KMainLowQuality");
            m_UpsampleHighQualityKernel = Materials.bloomUpsampleCS.FindKernel("KMainHighQuality");
            m_UpsampleTail2LowQualityKernel = Materials.bloomUpsampleCS.FindKernel("KMainTail2LowQuality");
            m_UpsampleTail2HighQualityKernel = Materials.bloomUpsampleCS.FindKernel("KMainTail2HighQuality");
            m_ResolveKernel = Materials.bloomUpsampleCS.FindKernel("KResolve");
            m_KernelsInitialized = true;
        }

        private int GetPrefilterKernel(UniversalCameraData cameraData, in PaniniProjectionParameters panini)
        {
            int index = 0;
            if (Settings.highQualityPrefiltering)
                index |= 1;
            if (cameraData.isAlphaOutputEnabled)
                index |= 2;
            if (panini.enabled)
                index |= 4;
            if (Settings.threshold.value <= 0f)
                index |= 8;
            return m_PrefilterKernels[index];
        }

        private Vector4 GetThresholdParameters()
        {
            float threshold = Mathf.GammaToLinearSpace(Settings.threshold.value);
            float knee = threshold * k_ThresholdSoftness + 1e-5f;
            return new Vector4(threshold, threshold - knee, knee * 2f, 0.25f / knee);
        }

        private static Color NormalizeTint(Color tint)
        {
            tint = tint.linear;
            float luminance = ColorUtils.Luminance(tint);
            return luminance > 0f ? tint * (1f / luminance) : Color.white;
        }

        private static Vector4 GetLensDirtScaleOffset(Texture dirtTexture, in TextureDesc sourceDescriptor)
        {
            float dirtRatio = dirtTexture.width / (float)dirtTexture.height;
            float screenRatio = sourceDescriptor.width / (float)sourceDescriptor.height;
            var scaleOffset = new Vector4(1f, 1f, 0f, 0f);

            if (dirtRatio > screenRatio)
            {
                scaleOffset.x = screenRatio / dirtRatio;
                scaleOffset.z = (1f - scaleOffset.x) * 0.5f;
            }
            else if (screenRatio > dirtRatio)
            {
                scaleOffset.y = dirtRatio / screenRatio;
                scaleOffset.w = (1f - scaleOffset.y) * 0.5f;
            }

            return scaleOffset;
        }

        internal readonly struct BloomUberPostParameters
        {
            internal readonly Vector4 bloomParams;
            internal readonly Vector4 bloomTint;
            internal readonly Vector4 bloomBicubicParams;
            internal readonly Vector4 bloomTextureScaleBias;
            internal readonly Vector4 lensDirtParams;
            internal readonly Texture lensDirtTexture;
            internal readonly bool hasLensDirt;

            internal BloomUberPostParameters(Vector4 bloomParams, Vector4 bloomTint,
                Vector4 bloomBicubicParams, Vector4 bloomTextureScaleBias, Vector4 lensDirtParams,
                Texture lensDirtTexture, bool hasLensDirt)
            {
                this.bloomParams = bloomParams;
                this.bloomTint = bloomTint;
                this.bloomBicubicParams = bloomBicubicParams;
                this.bloomTextureScaleBias = bloomTextureScaleBias;
                this.lensDirtParams = lensDirtParams;
                this.lensDirtTexture = lensDirtTexture;
                this.hasLensDirt = hasLensDirt;
            }
        }

        internal BloomUberPostParameters GetUberPostParameters(BloomFrameData bloomFrameData, in TextureDesc sourceDescriptor)
        {
            float intensity = Mathf.Pow(2f, Settings.intensity.value) - 1f;
            Color tint = NormalizeTint(Settings.tint.value);
            Texture dirtTexture = Settings.dirtTexture.value != null ? Settings.dirtTexture.value : Texture2D.blackTexture;
            bool hasLensDirt = Settings.dirtTexture.value != null && Settings.dirtIntensity.value > 0f;

            return new BloomUberPostParameters(
                new Vector4(intensity, Settings.dirtIntensity.value * intensity, 0f, 0f),
                new Vector4(tint.r, tint.g, tint.b, tint.a),
                bloomFrameData.bicubicParams,
                new Vector4(1f, 1f, 0f, 0f),
                GetLensDirtScaleOffset(dirtTexture, in sourceDescriptor),
                dirtTexture,
                hasLensDirt: hasLensDirt);
        }

        private sealed class PrefilterPassData
        {
            internal ComputeShader shader;
            internal int kernel;
            internal TextureHandle source;
            internal TextureHandle destination;
            internal Vector4 texelSize;
            internal Vector4 sourceSize;
            internal Vector4 threshold;
            internal Vector4 paniniParams;
            internal Vector4 parameters;
        }

        private sealed class BlurPassData
        {
            internal ComputeShader shader;
            internal int kernel;
            internal TextureHandle source;
            internal TextureHandle destination;
            internal Vector4 texelSize;
            internal Vector4 sourceSize;
        }

        private sealed class DownsampleTailPassData
        {
            internal ComputeShader shader;
            internal int kernel;
            internal TextureHandle source;
            internal TextureHandle firstDestination;
            internal TextureHandle secondDestination;
            internal Vector4 sourceSize;
            internal Vector4 firstSize;
            internal Vector4 secondSize;
        }

        private sealed class UpsamplePassData
        {
            internal ComputeShader shader;
            internal int kernel;
            internal TextureHandle lowTexture;
            internal TextureHandle highOutputTexture;
            internal Vector4 parameters;
            internal Vector4 bicubicParameters;
            internal Vector4 texelSize;
        }

        private sealed class ResolvePassData
        {
            internal ComputeShader shader;
            internal int kernel;
            internal TextureHandle lowTexture;
            internal TextureHandle destination;
            internal Vector4 texelSize;
            internal Vector4 bicubicParameters;
        }

        private sealed class UpsampleTailPassData
        {
            internal ComputeShader shader;
            internal int kernel;
            internal TextureHandle lowTexture;
            internal TextureHandle firstHighOutputTexture;
            internal TextureHandle secondHighOutputTexture;
            internal Vector4 parameters;
            internal Vector4 bicubicParameters;
            internal Vector4 firstSize;
            internal Vector4 secondSize;
        }

        private int PrepareMipInfo(Camera camera, in TextureDesc sourceDescriptor, BloomFrameData bloomFrameData)
        {
            int resolution = (int)Settings.resolution;
            float scaleWidth = 1f / (resolution / 2f);
            float scaleHeight = 1f / (resolution / 2f);

            if (sourceDescriptor.width < 800 || sourceDescriptor.height < 450)
            {
                scaleWidth = 1f;
                scaleHeight = 1f;
            }

            if (Settings.anamorphic.value)
            {
                float anamorphism = camera.anamorphism * 0.5f;
                scaleWidth *= anamorphism < 0f ? 1f + anamorphism : 1f;
                scaleHeight *= anamorphism > 0f ? 1f - anamorphism : 1f;
            }

            float resolveWidthScale = scaleWidth * 0.5f;
            float resolveHeightScale = scaleHeight * 0.5f;
            int resolveWidth = Mathf.Max(1, Mathf.RoundToInt(resolveWidthScale * sourceDescriptor.width));
            int resolveHeight = Mathf.Max(1, Mathf.RoundToInt(resolveHeightScale * sourceDescriptor.height));
            bloomFrameData.resolveInfo = new Vector4(resolveWidth, resolveHeight, resolveWidthScale, resolveHeightScale);
            bloomFrameData.bicubicParams = BuildSizeVector(resolveWidth, resolveHeight);

            int maxSize = Mathf.Max(sourceDescriptor.width, sourceDescriptor.height);
            int iterations = Mathf.FloorToInt(Mathf.Log(maxSize, 2f) - 2f
                - (Settings.resolution == BloomResolution.Half ? 0f : 1f));
            int mipCount = Mathf.Clamp(iterations, 1, PostProcessConstants.k_MaxPyramidSize);

            for (int i = 0; i < mipCount; i++)
            {
                float pyramidScale = 1f / Mathf.Pow(2f, i + 1f);
                float widthScale = scaleWidth * pyramidScale;
                float heightScale = scaleHeight * pyramidScale;
                int width = Mathf.Max(1, Mathf.RoundToInt(widthScale * sourceDescriptor.width));
                int height = Mathf.Max(1, Mathf.RoundToInt(heightScale * sourceDescriptor.height));
                bloomFrameData.mipInfo[i] = new Vector4(width, height, widthScale, heightScale);
            }

            return mipCount;
        }

        public void RenderBloomTexture(RenderGraph renderGraph, UniversalCameraData cameraData,
            in TextureHandle source, BloomFrameData bloomFrameData, in PaniniProjectionParameters panini)
        {
            InitializeKernels();
            renderGraph.BeginProfilingSampler(ProfilingSampler.Get(URPProfileId.Bloom));

            TextureDesc sourceDescriptor = source.GetDescriptor(renderGraph);
            int mipCount = PrepareMipInfo(cameraData.camera, in sourceDescriptor, bloomFrameData);
            bloomFrameData.mipCount = mipCount;

            TextureDesc descriptor = GetCompatibleDescriptor(sourceDescriptor,
                (int)bloomFrameData.mipInfo[0].x, (int)bloomFrameData.mipInfo[0].y, m_BloomColorFormat);
            MakeComputeCompatible(ref descriptor);

            for (int i = 0; i < mipCount; i++)
            {
                descriptor.width = (int)bloomFrameData.mipInfo[i].x;
                descriptor.height = (int)bloomFrameData.mipInfo[i].y;
                bloomFrameData.mipDown[i] = CreateCompatibleTexture(renderGraph, descriptor,
                    m_BloomMipDownNames[i], false, FilterMode.Bilinear);
            }

            float sourceToWideX = sourceDescriptor.width / Mathf.Max(1f, bloomFrameData.mipInfo[0].x);
            float sourceToWideY = sourceDescriptor.height / Mathf.Max(1f, bloomFrameData.mipInfo[0].y);
            float widePrefilterSpread = Mathf.Max(1f, Mathf.Min(sourceToWideX, sourceToWideY) * 0.5f);

            AddPrefilterPass(renderGraph, cameraData, source, bloomFrameData.mipDown[0], sourceDescriptor,
                (int)bloomFrameData.mipInfo[0].x, (int)bloomFrameData.mipInfo[0].y, widePrefilterSpread, in panini);

            bool fusedDownsampleTail = CanFuseDownsampleTail(bloomFrameData, mipCount);
            int regularDownsampleCount = fusedDownsampleTail ? mipCount - 3 : mipCount - 1;

            for (int i = 0; i < regularDownsampleCount; i++)
            {
                AddBlurPass(renderGraph, bloomFrameData.mipDown[i], bloomFrameData.mipDown[i + 1],
                    (int)bloomFrameData.mipInfo[i].x, (int)bloomFrameData.mipInfo[i].y,
                    (int)bloomFrameData.mipInfo[i + 1].x, (int)bloomFrameData.mipInfo[i + 1].y);
            }

            if (fusedDownsampleTail)
            {
                int sourceIndex = mipCount - 3;
                AddDownsampleTailPass(renderGraph,
                    bloomFrameData.mipDown[sourceIndex],
                    bloomFrameData.mipDown[sourceIndex + 1],
                    bloomFrameData.mipDown[sourceIndex + 2],
                    (int)bloomFrameData.mipInfo[sourceIndex].x,
                    (int)bloomFrameData.mipInfo[sourceIndex].y,
                    (int)bloomFrameData.mipInfo[sourceIndex + 1].x,
                    (int)bloomFrameData.mipInfo[sourceIndex + 1].y,
                    (int)bloomFrameData.mipInfo[sourceIndex + 2].x,
                    (int)bloomFrameData.mipInfo[sourceIndex + 2].y);
            }

            float scatter = Mathf.Lerp(0.05f, 0.95f, Settings.scatter.value);
            bool bicubicUpsampling = Settings.highQualityFiltering
                && sourceDescriptor.width >= 800
                && sourceDescriptor.height >= 450;

            bool fusedUpsampleTail = CanFuseUpsampleTail(bloomFrameData, mipCount);
            int regularUpsampleStart;

            if (fusedUpsampleTail)
            {
                int lowIndex = mipCount - 1;
                int firstHighIndex = mipCount - 2;
                int secondHighIndex = mipCount - 3;

                AddUpsampleTailPass(renderGraph,
                    bloomFrameData.mipDown[lowIndex],
                    bloomFrameData.mipDown[firstHighIndex],
                    bloomFrameData.mipDown[secondHighIndex],
                    scatter,
                    bicubicUpsampling,
                    (int)bloomFrameData.mipInfo[lowIndex].x,
                    (int)bloomFrameData.mipInfo[lowIndex].y,
                    (int)bloomFrameData.mipInfo[firstHighIndex].x,
                    (int)bloomFrameData.mipInfo[firstHighIndex].y,
                    (int)bloomFrameData.mipInfo[secondHighIndex].x,
                    (int)bloomFrameData.mipInfo[secondHighIndex].y);

                regularUpsampleStart = mipCount - 4;
            }
            else
            {
                regularUpsampleStart = mipCount - 2;
            }

            for (int i = regularUpsampleStart; i >= 0; i--)
            {
                TextureHandle lowSource = bloomFrameData.mipDown[i + 1];
                int kernel = bicubicUpsampling ? m_UpsampleHighQualityKernel : m_UpsampleLowQualityKernel;

                AddUpsamplePass(renderGraph, kernel, lowSource, bloomFrameData.mipDown[i],
                    scatter,
                    (int)bloomFrameData.mipInfo[i + 1].x, (int)bloomFrameData.mipInfo[i + 1].y,
                    (int)bloomFrameData.mipInfo[i].x, (int)bloomFrameData.mipInfo[i].y);
            }

            bloomFrameData.texture = bloomFrameData.mipDown[0];
            renderGraph.EndProfilingSampler(ProfilingSampler.Get(URPProfileId.Bloom));
        }

        private static bool CanFuseDownsampleTail(BloomFrameData bloomFrameData, int mipCount)
        {
            if (mipCount < 3)
                return false;

            Vector4 first = bloomFrameData.mipInfo[mipCount - 2];
            Vector4 second = bloomFrameData.mipInfo[mipCount - 1];
            return first.x <= k_DownsampleTailMaxWidth && first.y <= k_DownsampleTailMaxHeight
                && second.x <= k_DownsampleTailMaxWidth && second.y <= k_DownsampleTailMaxHeight;
        }

        private static bool CanFuseUpsampleTail(BloomFrameData bloomFrameData, int mipCount)
        {
            if (mipCount < 3)
                return false;

            Vector4 firstHigh = bloomFrameData.mipInfo[mipCount - 2];
            Vector4 secondHigh = bloomFrameData.mipInfo[mipCount - 3];
            return firstHigh.x <= k_UpsampleTailFirstMaxWidth && firstHigh.y <= k_UpsampleTailFirstMaxHeight
                && secondHigh.x <= k_UpsampleTailSecondMaxWidth && secondHigh.y <= k_UpsampleTailSecondMaxHeight;
        }

        private void AddPrefilterPass(RenderGraph renderGraph, UniversalCameraData cameraData,
            TextureHandle source, TextureHandle destination, in TextureDesc sourceDescriptor, int width, int height,
            float spread, in PaniniProjectionParameters panini)
        {
            int kernel = GetPrefilterKernel(cameraData, in panini);

            using (var builder = renderGraph.AddComputePass<PrefilterPassData>("Bloom Prefilter",
                       out var passData, ProfilingSampler.Get(URPProfileId.BloomPrefilter)))
            {
                passData.shader = Materials.bloomPrefilterCS;
                passData.kernel = kernel;
                passData.source = source;
                passData.destination = destination;
                passData.texelSize = BuildSizeVector(width, height);
                passData.sourceSize = BuildSizeVector(sourceDescriptor.width, sourceDescriptor.height);
                passData.threshold = GetThresholdParameters();
                passData.paniniParams = panini.shaderParameters;
                passData.parameters = new Vector4(spread, 0f, 0f, 0f);

                builder.UseTexture(source, AccessFlags.Read);
                builder.UseTexture(destination, AccessFlags.Write);
                builder.SetRenderFunc(static (PrefilterPassData data, ComputeGraphContext context) =>
                    ExecutePrefilterPass(data, context.cmd));
            }
        }

        private void AddBlurPass(RenderGraph renderGraph, TextureHandle source, TextureHandle destination,
            int sourceWidth, int sourceHeight, int width, int height)
        {
            using (var builder = renderGraph.AddComputePass<BlurPassData>("Bloom Downsample",
                       out var passData, ProfilingSampler.Get(URPProfileId.BloomDownsample)))
            {
                passData.shader = Materials.bloomBlurCS;
                passData.kernel = sourceWidth == width * 2 && sourceHeight == height * 2
                    ? m_Downsample2xKernel
                    : m_DownsampleKernel;
                passData.source = source;
                passData.destination = destination;
                passData.sourceSize = BuildSizeVector(sourceWidth, sourceHeight);
                passData.texelSize = BuildSizeVector(width, height);

                builder.UseTexture(source, AccessFlags.Read);
                builder.UseTexture(destination, AccessFlags.Write);
                builder.SetRenderFunc(static (BlurPassData data, ComputeGraphContext context) =>
                    ExecuteBlurPass(data, context.cmd));
            }
        }

        private void AddDownsampleTailPass(RenderGraph renderGraph, TextureHandle source,
            TextureHandle firstDestination, TextureHandle secondDestination,
            int sourceWidth, int sourceHeight, int firstWidth, int firstHeight, int secondWidth, int secondHeight)
        {
            using (var builder = renderGraph.AddComputePass<DownsampleTailPassData>("Bloom Downsample Tail",
                       out var passData, ProfilingSampler.Get(URPProfileId.BloomDownsample)))
            {
                passData.shader = Materials.bloomBlurCS;
                passData.kernel = m_DownsampleTail2Kernel;
                passData.source = source;
                passData.firstDestination = firstDestination;
                passData.secondDestination = secondDestination;
                passData.sourceSize = BuildSizeVector(sourceWidth, sourceHeight);
                passData.firstSize = BuildSizeVector(firstWidth, firstHeight);
                passData.secondSize = BuildSizeVector(secondWidth, secondHeight);

                builder.UseTexture(source, AccessFlags.Read);
                builder.UseTexture(firstDestination, AccessFlags.Write);
                builder.UseTexture(secondDestination, AccessFlags.Write);
                builder.SetRenderFunc(static (DownsampleTailPassData data, ComputeGraphContext context) =>
                    ExecuteDownsampleTailPass(data, context.cmd));
            }
        }

        private void AddUpsamplePass(RenderGraph renderGraph, int kernel, TextureHandle lowSource, TextureHandle highOutput,
            float scatter, int lowWidth, int lowHeight, int width, int height)
        {
            using (var builder = renderGraph.AddComputePass<UpsamplePassData>("Bloom Upsample",
                       out var passData, ProfilingSampler.Get(URPProfileId.BloomUpsample)))
            {
                passData.shader = Materials.bloomUpsampleCS;
                passData.kernel = kernel;
                passData.lowTexture = lowSource;
                passData.highOutputTexture = highOutput;
                passData.parameters = new Vector4(scatter, 0f, 0f, 0f);
                passData.bicubicParameters = BuildSizeVector(lowWidth, lowHeight);
                passData.texelSize = BuildSizeVector(width, height);

                builder.UseTexture(lowSource, AccessFlags.Read);
                builder.UseTexture(highOutput, AccessFlags.ReadWrite);
                builder.SetRenderFunc(static (UpsamplePassData data, ComputeGraphContext context) =>
                    ExecuteUpsamplePass(data, context.cmd));
            }
        }

        private void AddResolvePass(RenderGraph renderGraph,
            TextureHandle lowTexture, TextureHandle destination,
            int lowWidth, int lowHeight, int width, int height)
        {
            using (var builder = renderGraph.AddComputePass<ResolvePassData>("Bloom Resolve",
                       out var passData, ProfilingSampler.Get(URPProfileId.BloomUpsample)))
            {
                passData.shader = Materials.bloomUpsampleCS;
                passData.kernel = m_ResolveKernel;
                passData.lowTexture = lowTexture;
                passData.destination = destination;
                passData.texelSize = BuildSizeVector(width, height);
                passData.bicubicParameters = BuildSizeVector(lowWidth, lowHeight);

                builder.UseTexture(lowTexture, AccessFlags.Read);
                builder.UseTexture(destination, AccessFlags.Write);
                builder.SetRenderFunc(static (ResolvePassData data, ComputeGraphContext context) =>
                    ExecuteResolvePass(data, context.cmd));
            }
        }

        private void AddUpsampleTailPass(RenderGraph renderGraph, TextureHandle lowSource,
            TextureHandle firstHighOutput, TextureHandle secondHighOutput, float scatter, bool highQuality,
            int lowWidth, int lowHeight, int firstWidth, int firstHeight, int secondWidth, int secondHeight)
        {
            using (var builder = renderGraph.AddComputePass<UpsampleTailPassData>("Bloom Upsample Tail",
                       out var passData, ProfilingSampler.Get(URPProfileId.BloomUpsample)))
            {
                passData.shader = Materials.bloomUpsampleCS;
                passData.kernel = highQuality ? m_UpsampleTail2HighQualityKernel : m_UpsampleTail2LowQualityKernel;
                passData.lowTexture = lowSource;
                passData.firstHighOutputTexture = firstHighOutput;
                passData.secondHighOutputTexture = secondHighOutput;
                passData.parameters = new Vector4(scatter, 0f, 0f, 0f);
                passData.bicubicParameters = BuildSizeVector(lowWidth, lowHeight);
                passData.firstSize = BuildSizeVector(firstWidth, firstHeight);
                passData.secondSize = BuildSizeVector(secondWidth, secondHeight);

                builder.UseTexture(lowSource, AccessFlags.Read);
                builder.UseTexture(firstHighOutput, AccessFlags.ReadWrite);
                builder.UseTexture(secondHighOutput, AccessFlags.ReadWrite);
                builder.SetRenderFunc(static (UpsampleTailPassData data, ComputeGraphContext context) =>
                    ExecuteUpsampleTailPass(data, context.cmd));
            }
        }

        private static void ExecutePrefilterPass(PrefilterPassData data, ComputeCommandBuffer commandBuffer)
        {
            commandBuffer.SetComputeTextureParam(data.shader, data.kernel, PostProcessShaderConstants._InputTexture, data.source);
            commandBuffer.SetComputeTextureParam(data.shader, data.kernel, PostProcessShaderConstants._OutputTexture, data.destination);
            commandBuffer.SetComputeVectorParam(data.shader, PostProcessShaderConstants._TexelSize, data.texelSize);
            commandBuffer.SetComputeVectorParam(data.shader, PostProcessShaderConstants._SourceSize, data.sourceSize);
            commandBuffer.SetComputeVectorParam(data.shader, PostProcessShaderConstants._BloomThreshold, data.threshold);
            commandBuffer.SetComputeVectorParam(data.shader, PostProcessShaderConstants._PaniniParams, data.paniniParams);
            commandBuffer.SetComputeVectorParam(data.shader, PostProcessShaderConstants._Params, data.parameters);
            Dispatch(commandBuffer, data.shader, data.kernel, (int)data.texelSize.x, (int)data.texelSize.y,
                k_PrefilterThreadGroupSize, k_PrefilterThreadGroupSize);
        }

        private static void ExecuteBlurPass(BlurPassData data, ComputeCommandBuffer commandBuffer)
        {
            commandBuffer.SetComputeTextureParam(data.shader, data.kernel, PostProcessShaderConstants._InputTexture, data.source);
            commandBuffer.SetComputeTextureParam(data.shader, data.kernel, PostProcessShaderConstants._OutputTexture, data.destination);
            commandBuffer.SetComputeVectorParam(data.shader, PostProcessShaderConstants._TexelSize, data.texelSize);
            commandBuffer.SetComputeVectorParam(data.shader, PostProcessShaderConstants._SourceSize, data.sourceSize);
            Dispatch(commandBuffer, data.shader, data.kernel, (int)data.texelSize.x, (int)data.texelSize.y,
                k_DownsampleThreadGroupSizeX, k_DownsampleThreadGroupSizeY);
        }

        private static void ExecuteDownsampleTailPass(DownsampleTailPassData data, ComputeCommandBuffer commandBuffer)
        {
            commandBuffer.SetComputeTextureParam(data.shader, data.kernel, PostProcessShaderConstants._InputTexture, data.source);
            commandBuffer.SetComputeTextureParam(data.shader, data.kernel, PostProcessShaderConstants._OutputTexture, data.firstDestination);
            commandBuffer.SetComputeTextureParam(data.shader, data.kernel, PostProcessShaderConstants._BloomTailOutputTexture, data.secondDestination);
            commandBuffer.SetComputeVectorParam(data.shader, PostProcessShaderConstants._TexelSize, data.firstSize);
            commandBuffer.SetComputeVectorParam(data.shader, PostProcessShaderConstants._SourceSize, data.sourceSize);
            commandBuffer.SetComputeVectorParam(data.shader, PostProcessShaderConstants._BloomTailSize, data.secondSize);
            commandBuffer.DispatchCompute(data.shader, data.kernel, 1, 1, 1);
        }

        private static void ExecuteUpsamplePass(UpsamplePassData data, ComputeCommandBuffer commandBuffer)
        {
            commandBuffer.SetComputeTextureParam(data.shader, data.kernel, PostProcessShaderConstants._InputLowTexture, data.lowTexture);
            commandBuffer.SetComputeTextureParam(data.shader, data.kernel, PostProcessShaderConstants._InputHighOutputTexture, data.highOutputTexture);
            commandBuffer.SetComputeVectorParam(data.shader, PostProcessShaderConstants._Params, data.parameters);
            commandBuffer.SetComputeVectorParam(data.shader, PostProcessShaderConstants._BloomBicubicParams, data.bicubicParameters);
            commandBuffer.SetComputeVectorParam(data.shader, PostProcessShaderConstants._TexelSize, data.texelSize);
            Dispatch(commandBuffer, data.shader, data.kernel, (int)data.texelSize.x, (int)data.texelSize.y,
                k_UpsampleThreadGroupSizeX, k_UpsampleThreadGroupSizeY);
        }

        private static void ExecuteResolvePass(ResolvePassData data, ComputeCommandBuffer commandBuffer)
        {
            commandBuffer.SetComputeTextureParam(data.shader, data.kernel, PostProcessShaderConstants._InputLowTexture, data.lowTexture);
            commandBuffer.SetComputeTextureParam(data.shader, data.kernel, PostProcessShaderConstants._OutputTexture, data.destination);
            commandBuffer.SetComputeVectorParam(data.shader, PostProcessShaderConstants._TexelSize, data.texelSize);
            commandBuffer.SetComputeVectorParam(data.shader, PostProcessShaderConstants._BloomBicubicParams, data.bicubicParameters);
            Dispatch(commandBuffer, data.shader, data.kernel, (int)data.texelSize.x, (int)data.texelSize.y,
                k_UpsampleThreadGroupSizeX, k_UpsampleThreadGroupSizeY);
        }

        private static void ExecuteUpsampleTailPass(UpsampleTailPassData data, ComputeCommandBuffer commandBuffer)
        {
            commandBuffer.SetComputeTextureParam(data.shader, data.kernel, PostProcessShaderConstants._InputLowTexture, data.lowTexture);
            commandBuffer.SetComputeTextureParam(data.shader, data.kernel, PostProcessShaderConstants._InputHighOutputTexture, data.firstHighOutputTexture);
            commandBuffer.SetComputeTextureParam(data.shader, data.kernel, PostProcessShaderConstants._BloomTailHighOutputTexture, data.secondHighOutputTexture);
            commandBuffer.SetComputeVectorParam(data.shader, PostProcessShaderConstants._Params, data.parameters);
            commandBuffer.SetComputeVectorParam(data.shader, PostProcessShaderConstants._BloomBicubicParams, data.bicubicParameters);
            commandBuffer.SetComputeVectorParam(data.shader, PostProcessShaderConstants._TexelSize, data.firstSize);
            commandBuffer.SetComputeVectorParam(data.shader, PostProcessShaderConstants._BloomTailSize, data.secondSize);
            commandBuffer.DispatchCompute(data.shader, data.kernel, 1, 1, 1);
        }

        private static Vector4 BuildSizeVector(int width, int height)
        {
            width = Mathf.Max(1, width);
            height = Mathf.Max(1, height);
            return new Vector4(width, height, 1f / width, 1f / height);
        }

        private static void Dispatch(ComputeCommandBuffer commandBuffer, ComputeShader shader, int kernel,
            int width, int height, int threadGroupSizeX, int threadGroupSizeY)
        {
            commandBuffer.DispatchCompute(shader, kernel,
                Mathf.Max(1, (width + threadGroupSizeX - 1) / threadGroupSizeX),
                Mathf.Max(1, (height + threadGroupSizeY - 1) / threadGroupSizeY),
                1);
        }
    }
}
