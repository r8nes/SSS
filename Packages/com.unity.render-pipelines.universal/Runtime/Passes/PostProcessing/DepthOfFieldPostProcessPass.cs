using System;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using System.Runtime.CompilerServices;

namespace UnityEngine.Rendering.Universal
{
    internal sealed class DepthOfFieldPostProcessPass : PostProcessPass<DepthOfField>
    {
        private readonly GraphicsFormat m_GaussianCoCFormat;
        private readonly GraphicsFormat m_GaussianDoFColorFormat;
        private bool m_UseFastSRGBLinearConversion;

        private Vector4[] m_BokehKernel;
        private int m_BokehHash;
        private float m_BokehMaxRadius;
        private float m_BokehRCPAspect;
        private Vector4 m_BokehRingMin;

        private readonly ComputeShader m_BokehCompute;
        private readonly int m_BokehPrefilterClassifyKernel;
        private readonly int m_BokehPrefilterClassifyFastKernel;
        private readonly int m_BokehBlurKernel;
        private readonly GraphicsFormat m_BokehTileFormat;

        private static readonly int s_BokehSourceColorTexture = Shader.PropertyToID("_SourceColorTexture");
        private static readonly int s_BokehInputTexture = Shader.PropertyToID("_InputTexture");
        private static readonly int s_BokehPrefilterOutputTexture = Shader.PropertyToID("_PrefilterOutputTexture");
        private static readonly int s_BokehOutputTexture = Shader.PropertyToID("_OutputTexture");
        private static readonly int s_BokehTileTexture = Shader.PropertyToID("_TileTexture");
        private static readonly int s_BokehTileOutputTexture = Shader.PropertyToID("_TileOutputTexture");
        private static readonly int s_BokehSourceSize = Shader.PropertyToID("_SourceSize");
        private static readonly int s_BokehFullSourceSize = Shader.PropertyToID("_FullSourceSize");
        private static readonly int s_BokehRingMin = Shader.PropertyToID("_BokehRingMin");
        private static readonly int s_DofTextureTexelSize = Shader.PropertyToID("_DofTexture_TexelSize");

        public DepthOfFieldPostProcessPass(PostProcessMaterialLibrary materials, GraphicsFormat gaussianCoCFormat, GraphicsFormat gaussianDoFColorFormat)
            : base(materials)
        {
            m_GaussianCoCFormat = gaussianCoCFormat;
            m_GaussianDoFColorFormat = gaussianDoFColorFormat;

            m_BokehCompute = materials.bokehDepthOfFieldBlur;
            if (m_BokehCompute != null)
            {
                m_BokehPrefilterClassifyKernel = m_BokehCompute.FindKernel("KBokehPrefilterClassify");
                m_BokehPrefilterClassifyFastKernel = m_BokehCompute.FindKernel("KBokehPrefilterClassifyFast");
                m_BokehBlurKernel = m_BokehCompute.FindKernel("KBokehBlur");
            }

            m_BokehTileFormat = GraphicsFormat.R32G32_SFloat;
        }

        public void SetSettings(DepthOfField depthOfField, bool useFastSRGBLinearConversion)
        {
            SetSettings(depthOfField);
            m_UseFastSRGBLinearConversion = useFastSRGBLinearConversion;
        }

        public TextureHandle Render(RenderGraph renderGraph, UniversalResourceData resourceData, UniversalCameraData cameraData, in TextureHandle source)
        {
            renderGraph.BeginProfilingSampler(ProfilingSampler.Get(URPProfileId.DepthOfField));

            TextureHandle destination;
            destination = CreateCompatibleTexture(renderGraph, source, "_DoFTarget", false, FilterMode.Bilinear);

            switch (Settings.mode.value)
            {
                case DepthOfFieldMode.Gaussian:
                {
                    var key = PostProcessMaterialKey.DepthOfField(cameraData.isAlphaOutputEnabled,
                        Settings.highQualitySampling.value, false);
                    var material = Materials.GetKeywordVariant(Materials.gaussianDepthOfField, key);
                    RenderDoFGaussian(renderGraph, resourceData, source, destination, material);
                    break;
                }
                case DepthOfFieldMode.Bokeh:
                {
                    RenderDoFBokeh(renderGraph, resourceData, source, destination, cameraData.isAlphaOutputEnabled);
                    break;
                }
            }

            renderGraph.EndProfilingSampler(ProfilingSampler.Get(URPProfileId.DepthOfField));
            return destination;
        }

        private sealed class DoFGaussianPrefilterPassData
        {
            internal TextureHandle sourceTexture;
            internal TextureHandle depthTexture;
            internal Material material;
            internal Vector3 cocParams;
            internal Vector4 downSampleScaleFactor;
        }

        private sealed class DoFBlurPassData : PostProcessFullscreenPassData
        {
            internal TextureHandle auxiliaryTexture;
        }

        private sealed class DoFCompositePassData : PostProcessFullscreenPassData
        {
            internal TextureHandle colorTexture;
            internal TextureHandle depthTexture;
            internal Vector3 cocParams;
        }

        private sealed class DoFBokehPrefilterClassifyPassData
        {
            internal ComputeShader shader;
            internal int kernel;
            internal TextureHandle sourceTexture;
            internal TextureHandle depthTexture;
            internal TextureHandle outputTexture;
            internal TextureHandle tileTexture;
            internal Vector4 sourceSize;
            internal Vector4 fullSourceSize;
            internal Vector4 cocParams;
            internal int width;
            internal int height;
        }

        private sealed class DoFBokehComputeBlurPassData
        {
            internal ComputeShader shader;
            internal int kernel;
            internal TextureHandle sourceTexture;
            internal TextureHandle tileTexture;
            internal TextureHandle destinationTexture;
            internal Vector4 sourceSize;
            internal Vector4[] bokehKernel;
            internal Vector4 bokehConstants;
            internal Vector4 bokehRingMin;
            internal int width;
            internal int height;
        }

        private sealed class DoFBokehFusedCompositePassData : PostProcessFullscreenPassData
        {
            internal TextureHandle depthTexture;
            internal TextureHandle blurTexture;
            internal Vector4 cocParams;
            internal Vector4 blurTexelSize;
        }

        public void RenderDoFGaussian(RenderGraph renderGraph, UniversalResourceData resourceData, in TextureHandle source, TextureHandle destination, Material material)
        {
            var srcDesc = source.GetDescriptor(renderGraph);

            const int downSample = 2;
            int wh = Mathf.Max(1, srcDesc.width / downSample);
            int hh = Mathf.Max(1, srcDesc.height / downSample);

            float farStart = Settings.gaussianStart.value;
            float farEnd = Mathf.Max(farStart, Settings.gaussianEnd.value);
            float maxRadius = Settings.gaussianMaxRadius.value * (wh / 1080f);
            maxRadius = Mathf.Min(maxRadius, 2f);
            var cocParams = new Vector3(farStart, farEnd, maxRadius);
            var downSampleScale = new Vector4(1.0f / downSample, 1.0f / downSample, downSample, downSample);

            var halfCoCTextureDesc = GetCompatibleDescriptor(srcDesc, wh, hh, m_GaussianCoCFormat);
            var halfCoCTexture = CreateCompatibleTexture(renderGraph, halfCoCTextureDesc, "_HalfCoCTexture", false, FilterMode.Bilinear);
            var pingTextureDesc = GetCompatibleDescriptor(srcDesc, wh, hh, m_GaussianDoFColorFormat);
            var pingTexture = CreateCompatibleTexture(renderGraph, pingTextureDesc, "_PingTexture", false, FilterMode.Bilinear);
            var pongTextureDesc = GetCompatibleDescriptor(srcDesc, wh, hh, m_GaussianDoFColorFormat);
            var pongTexture = CreateCompatibleTexture(renderGraph, pongTextureDesc, "_PongTexture", false, FilterMode.Bilinear);


            using (var builder = renderGraph.AddRasterRenderPass<DoFGaussianPrefilterPassData>("Depth of Field - Gaussian Prefilter", out var passData, ProfilingSampler.Get(URPProfileId.DOFDownscalePrefilter)))
            {
                passData.sourceTexture = source;
                passData.depthTexture = resourceData.cameraDepthTexture;
                passData.material = material;
                passData.cocParams = cocParams;
                passData.downSampleScaleFactor = downSampleScale;

                builder.UseTexture(source, AccessFlags.Read);
                builder.UseTexture(resourceData.cameraDepthTexture, AccessFlags.Read);
                builder.SetRenderAttachment(halfCoCTexture, 0, AccessFlags.Write);
                builder.SetRenderAttachment(pingTexture, 1, AccessFlags.Write);

                builder.SetRenderFunc(static (DoFGaussianPrefilterPassData data, RasterGraphContext context) =>
                {
                    data.material.SetVector(PostProcessShaderConstants._CoCParams, data.cocParams);
                    data.material.SetVector(PostProcessShaderConstants._DownSampleScaleFactor, data.downSampleScaleFactor);
                    data.material.SetTexture(PostProcessShaderConstants._CameraDepthTextureID, data.depthTexture);
                    PostProcessPassUtils.SetMaterialSourceSize(data.material, data.sourceTexture);
                    Blitter.BlitTexture(context.cmd, data.sourceTexture, GetViewportScale((RTHandle)data.sourceTexture), data.material, PostProcessConstants.k_GaussianDoFPassDownscalePrefilter);
                });
            }

            using (var builder = renderGraph.AddRasterRenderPass<DoFBlurPassData>("Depth of Field - Gaussian Blur H", out var passData, ProfilingSampler.Get(URPProfileId.DOFBlurH)))
            {
                PostProcessGraphPasses.SetupFullscreenPass(builder, passData, pingTexture, pongTexture, material, PostProcessConstants.k_GaussianDoFPassBlurH);
                passData.auxiliaryTexture = halfCoCTexture;
                builder.UseTexture(halfCoCTexture, AccessFlags.Read);

                builder.SetRenderFunc(static (DoFBlurPassData data, RasterGraphContext context) =>
                {
                    data.material.SetTexture(PostProcessShaderConstants._HalfCoCTexture, data.auxiliaryTexture);
                    PostProcessGraphPasses.DrawFullscreen(data, context);
                });
            }

            using (var builder = renderGraph.AddRasterRenderPass<DoFBlurPassData>("Depth of Field - Gaussian Blur V", out var passData, ProfilingSampler.Get(URPProfileId.DOFBlurV)))
            {
                PostProcessGraphPasses.SetupFullscreenPass(builder, passData, pongTexture, pingTexture, material, PostProcessConstants.k_GaussianDoFPassBlurV);

                builder.SetRenderFunc(static (DoFBlurPassData data, RasterGraphContext context) =>
                {
                    PostProcessGraphPasses.DrawFullscreen(data, context);
                });
            }

            using (var builder = renderGraph.AddRasterRenderPass<DoFCompositePassData>("Depth of Field - Gaussian Composite", out var passData, ProfilingSampler.Get(URPProfileId.DOFComposite)))
            {
                PostProcessGraphPasses.SetupFullscreenPass(builder, passData, source, destination, material, PostProcessConstants.k_GaussianDoFPassComposite);
                passData.colorTexture = pingTexture;
                passData.depthTexture = resourceData.cameraDepthTexture;
                passData.cocParams = cocParams;
                builder.UseTexture(pingTexture, AccessFlags.Read);
                builder.UseTexture(resourceData.cameraDepthTexture, AccessFlags.Read);

                builder.SetRenderFunc(static (DoFCompositePassData data, RasterGraphContext context) =>
                {
                    data.material.SetTexture(PostProcessShaderConstants._ColorTexture, data.colorTexture);
                    data.material.SetTexture(PostProcessShaderConstants._CameraDepthTextureID, data.depthTexture);
                    data.material.SetVector(PostProcessShaderConstants._CoCParams, data.cocParams);
                    PostProcessPassUtils.SetMaterialSourceSize(data.material, data.sourceTexture);
                    PostProcessGraphPasses.DrawFullscreen(data, context);
                });
            }
        }

        private void PrepareBokehKernel(float maxRadius, float rcpAspect)
        {
            const int kRings = 4;
            const int kPointsPerRing = 7;

            if (m_BokehKernel == null)
                m_BokehKernel = new Vector4[42];

            int idx = 0;
            Vector4 ringMin = new Vector4(float.MaxValue, float.MaxValue, float.MaxValue, 0f);
            float bladeCount = Settings.bladeCount.value;
            float curvature = 1f - Settings.bladeCurvature.value;
            float rotation = Settings.bladeRotation.value * Mathf.Deg2Rad;
            const float PI = Mathf.PI;
            const float TWO_PI = Mathf.PI * 2f;

            for (int ring = 1; ring < kRings; ring++)
            {
                float bias = 1f / kPointsPerRing;
                float radius = (ring + bias) / (kRings - 1f + bias);
                int points = ring * kPointsPerRing;
                int ringStart = idx;

                for (int point = 0; point < points; point++)
                {
                    float phi = 2f * PI * point / points;
                    float nt = Mathf.Cos(PI / bladeCount);
                    float dt = Mathf.Cos(phi - (TWO_PI / bladeCount) * Mathf.Floor((bladeCount * phi + Mathf.PI) / TWO_PI));
                    float r = radius * Mathf.Pow(nt / dt, curvature);
                    float u = r * Mathf.Cos(phi - rotation);
                    float v = r * Mathf.Sin(phi - rotation);

                    float uRadius = u * maxRadius;
                    float vRadius = v * maxRadius;
                    float kernelLength = Mathf.Sqrt(uRadius * uRadius + vRadius * vRadius);
                    float uRCP = uRadius * rcpAspect;

                    m_BokehKernel[idx++] = new Vector4(uRadius, vRadius, kernelLength, uRCP);
                }

                // Samples within a ring are order-independent. Sorting by radius lets the
                // compute blur terminate a complete workgroup's ring as soon as the next
                // sample is mathematically unable to contribute.
                for (int i = ringStart + 1; i < ringStart + points; ++i)
                {
                    Vector4 key = m_BokehKernel[i];
                    int j = i - 1;
                    while (j >= ringStart && m_BokehKernel[j].z > key.z)
                    {
                        m_BokehKernel[j + 1] = m_BokehKernel[j];
                        --j;
                    }
                    m_BokehKernel[j + 1] = key;
                }

                ringMin[ring - 1] = m_BokehKernel[ringStart].z;
            }

            m_BokehRingMin = ringMin;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float GetMaxBokehRadiusInPixels(float viewportHeight)
        {
            const float kRadiusInPixels = 14f;
            return Mathf.Min(0.05f, kRadiusInPixels / viewportHeight);
        }

        public void RenderDoFBokeh(RenderGraph renderGraph, UniversalResourceData resourceData, in TextureHandle source, in TextureHandle destination, bool alphaOutput)
        {
            var srcDesc = source.GetDescriptor(renderGraph);

            const int downSample = 2;
            int wh = Mathf.Max(1, srcDesc.width / downSample);
            int hh = Mathf.Max(1, srcDesc.height / downSample);

            var pingTextureDesc = GetCompatibleDescriptor(srcDesc, wh, hh, GraphicsFormat.R16G16B16A16_SFloat);
            MakeComputeCompatible(ref pingTextureDesc);
            var pingTexture = CreateCompatibleTexture(renderGraph, pingTextureDesc, "_PingTexture", false, FilterMode.Bilinear);

            var pongTextureDesc = GetCompatibleDescriptor(srcDesc, wh, hh, GraphicsFormat.R16G16B16A16_SFloat);
            MakeComputeCompatible(ref pongTextureDesc);
            var pongTexture = CreateCompatibleTexture(renderGraph, pongTextureDesc, "_PongTexture", false, FilterMode.Bilinear);

            const int bokehTileWidth = 8;
            const int bokehTileHeight = 8;
            int tileCountX = (wh + bokehTileWidth - 1) / bokehTileWidth;
            int tileCountY = (hh + bokehTileHeight - 1) / bokehTileHeight;
            var tileDesc = GetCompatibleDescriptor(srcDesc, tileCountX, tileCountY, m_BokehTileFormat);
            MakeComputeCompatible(ref tileDesc);
            var tileTexture = CreateCompatibleTexture(renderGraph, tileDesc, "_BokehCoCTiles", false, FilterMode.Point);

            float F = Settings.focalLength.value / 1000f;
            float A = Settings.focalLength.value / Settings.aperture.value;
            float P = Settings.focusDistance.value;
            float maxCoC = (A * F) / (P - F);
            float maxRadius = GetMaxBokehRadiusInPixels(srcDesc.height);
            float rcpAspect = 1f / (wh / (float)hh);

            int hash = Settings.GetHashCode();
            if (hash != m_BokehHash || !Mathf.Approximately(maxRadius, m_BokehMaxRadius) || !Mathf.Approximately(rcpAspect, m_BokehRCPAspect))
            {
                m_BokehHash = hash;
                m_BokehMaxRadius = maxRadius;
                m_BokehRCPAspect = rcpAspect;
                PrepareBokehKernel(maxRadius, rcpAspect);
            }

            float uvMargin = (1.0f / srcDesc.height) * downSample;
            var cocParams = new Vector4(P, maxCoC, maxRadius, rcpAspect);
            var bokehConstants = new Vector4(uvMargin, uvMargin * 2.0f);


            if (m_BokehCompute == null)
                throw new InvalidOperationException("BokehDepthOfFieldBlur.compute is required for Bokeh depth of field.");

            var halfSourceSize = new Vector4(wh, hh, 1.0f / wh, 1.0f / hh);
            var fullSourceSize = new Vector4(srcDesc.width, srcDesc.height, 1.0f / srcDesc.width, 1.0f / srcDesc.height);
            int prefilterKernel = m_UseFastSRGBLinearConversion
                ? m_BokehPrefilterClassifyFastKernel
                : m_BokehPrefilterClassifyKernel;

            using (var builder = renderGraph.AddComputePass<DoFBokehPrefilterClassifyPassData>(
                       "Depth of Field - Bokeh Prefilter + Tile Classification", out var passData,
                       ProfilingSampler.Get(URPProfileId.DOFDownscalePrefilter)))
            {
                passData.shader = m_BokehCompute;
                passData.kernel = prefilterKernel;
                passData.sourceTexture = source;
                passData.depthTexture = resourceData.cameraDepthTexture;
                passData.outputTexture = pingTexture;
                passData.tileTexture = tileTexture;
                passData.sourceSize = halfSourceSize;
                passData.fullSourceSize = fullSourceSize;
                passData.cocParams = cocParams;
                passData.width = wh;
                passData.height = hh;

                builder.UseTexture(source, AccessFlags.Read);
                builder.UseTexture(resourceData.cameraDepthTexture, AccessFlags.Read);
                builder.UseTexture(pingTexture, AccessFlags.Write);
                builder.UseTexture(tileTexture, AccessFlags.Write);
                builder.SetRenderFunc(static (DoFBokehPrefilterClassifyPassData data, ComputeGraphContext context) =>
                {
                    var cmd = context.cmd;
                    cmd.SetComputeTextureParam(data.shader, data.kernel, s_BokehSourceColorTexture, data.sourceTexture);
                    cmd.SetComputeTextureParam(data.shader, data.kernel, PostProcessShaderConstants._CameraDepthTextureID, data.depthTexture);
                    cmd.SetComputeTextureParam(data.shader, data.kernel, s_BokehPrefilterOutputTexture, data.outputTexture);
                    cmd.SetComputeTextureParam(data.shader, data.kernel, s_BokehTileOutputTexture, data.tileTexture);
                    cmd.SetComputeVectorParam(data.shader, s_BokehSourceSize, data.sourceSize);
                    cmd.SetComputeVectorParam(data.shader, s_BokehFullSourceSize, data.fullSourceSize);
                    cmd.SetComputeVectorParam(data.shader, PostProcessShaderConstants._CoCParams, data.cocParams);
                    cmd.DispatchCompute(data.shader, data.kernel,
                        (data.width + 7) / 8,
                        (data.height + 7) / 8,
                        1);
                });
            }

            using (var builder = renderGraph.AddComputePass<DoFBokehComputeBlurPassData>(
                       "Depth of Field - Bokeh Blur", out var passData,
                       ProfilingSampler.Get(URPProfileId.DOFBlurBokeh)))
            {
                passData.shader = m_BokehCompute;
                passData.kernel = m_BokehBlurKernel;
                passData.sourceTexture = pingTexture;
                passData.tileTexture = tileTexture;
                passData.destinationTexture = pongTexture;
                passData.sourceSize = halfSourceSize;
                passData.bokehKernel = m_BokehKernel;
                passData.bokehConstants = bokehConstants;
                passData.bokehRingMin = m_BokehRingMin;
                passData.width = wh;
                passData.height = hh;

                builder.UseTexture(pingTexture, AccessFlags.Read);
                builder.UseTexture(tileTexture, AccessFlags.Read);
                builder.UseTexture(pongTexture, AccessFlags.Write);
                builder.SetRenderFunc(static (DoFBokehComputeBlurPassData data, ComputeGraphContext context) =>
                {
                    var cmd = context.cmd;
                    cmd.SetComputeTextureParam(data.shader, data.kernel, s_BokehInputTexture, data.sourceTexture);
                    cmd.SetComputeTextureParam(data.shader, data.kernel, s_BokehTileTexture, data.tileTexture);
                    cmd.SetComputeTextureParam(data.shader, data.kernel, s_BokehOutputTexture, data.destinationTexture);
                    cmd.SetComputeVectorParam(data.shader, s_BokehSourceSize, data.sourceSize);
                    cmd.SetComputeVectorArrayParam(data.shader, PostProcessShaderConstants._BokehKernel, data.bokehKernel);
                    cmd.SetComputeVectorParam(data.shader, PostProcessShaderConstants._BokehConstants, data.bokehConstants);
                    cmd.SetComputeVectorParam(data.shader, s_BokehRingMin, data.bokehRingMin);
                    cmd.DispatchCompute(data.shader, data.kernel,
                        (data.width + 7) / 8,
                        (data.height + 7) / 8,
                        1);
                });
            }

            var bokehCompositeKey = PostProcessMaterialKey.DepthOfField(alphaOutput, false, m_UseFastSRGBLinearConversion);
            var bokehCompositeMaterial = Materials.GetKeywordVariant(Materials.bokehDepthOfField, bokehCompositeKey);
            int fusedCompositePass = bokehCompositeMaterial.FindPass("Bokeh Depth Of Field Fused Composite");

            using (var builder = renderGraph.AddRasterRenderPass<DoFBokehFusedCompositePassData>(
                       "Depth of Field - Bokeh Fused Reconstruction + Composite", out var passData,
                       ProfilingSampler.Get(URPProfileId.DOFComposite)))
            {
                PostProcessGraphPasses.SetupFullscreenPass(builder, passData, source, destination,
                    bokehCompositeMaterial, fusedCompositePass);
                passData.depthTexture = resourceData.cameraDepthTexture;
                passData.blurTexture = pongTexture;
                passData.cocParams = cocParams;
                passData.blurTexelSize = new Vector4(wh, hh, 1.0f / wh, 1.0f / hh);

                builder.UseTexture(resourceData.cameraDepthTexture, AccessFlags.Read);
                builder.UseTexture(pongTexture, AccessFlags.Read);
                builder.SetRenderFunc(static (DoFBokehFusedCompositePassData data, RasterGraphContext context) =>
                {
                    data.material.SetTexture(PostProcessShaderConstants._DofTexture, data.blurTexture);
                    data.material.SetVector(s_DofTextureTexelSize, data.blurTexelSize);
                    data.material.SetTexture(PostProcessShaderConstants._CameraDepthTextureID, data.depthTexture);
                    data.material.SetVector(PostProcessShaderConstants._CoCParams, data.cocParams);
                    PostProcessPassUtils.SetMaterialSourceSize(data.material, data.sourceTexture);
                    PostProcessGraphPasses.DrawFullscreen(data, context);
                });
            }
        }
    }
}
