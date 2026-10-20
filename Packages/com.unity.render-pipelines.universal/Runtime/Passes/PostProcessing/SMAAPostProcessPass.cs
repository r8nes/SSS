using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering.RenderGraphModule;

namespace UnityEngine.Rendering.Universal
{
    internal sealed class SMAAPostProcessPass : PostProcessPass
    {
        private readonly GraphicsFormat m_SMAAEdgeFormat;

        public SMAAPostProcessPass(PostProcessMaterialLibrary materials, GraphicsFormat edgeFormat)
            : base(materials)
        {
            m_SMAAEdgeFormat = edgeFormat;
        }

        private sealed class SMAAPassData : PostProcessFullscreenPassData
        {
            internal TextureHandle blendTexture;
        }

        private static GraphicsFormat GetSMAAStencilFormat()
        {
            var format = GraphicsFormatUtility.GetDepthStencilFormat(24, 8);
            if (format != GraphicsFormat.None)
                return format;

            format = GraphicsFormatUtility.GetDepthStencilFormat(32, 8);
            if (format != GraphicsFormat.None)
                return format;

            if (SystemInfo.IsFormatSupported(GraphicsFormat.D24_UNorm_S8_UInt, GraphicsFormatUsage.Render))
                return GraphicsFormat.D24_UNorm_S8_UInt;

            if (SystemInfo.IsFormatSupported(GraphicsFormat.D32_SFloat_S8_UInt, GraphicsFormatUsage.Render))
                return GraphicsFormat.D32_SFloat_S8_UInt;

            return GraphicsFormat.D24_UNorm_S8_UInt;
        }

        public TextureHandle Render(RenderGraph renderGraph, UniversalResourceData resourceData, AntialiasingQuality antialiasingQuality, in TextureHandle source)
        {
            renderGraph.BeginProfilingSampler(ProfilingSampler.Get(URPProfileId.SMAA));

            var destDesc = source.GetDescriptor(renderGraph);

            var smaaTarget = CreateCompatibleTexture(renderGraph, destDesc, "_SMAATarget", false, FilterMode.Bilinear);

            destDesc.clearColor = Color.black;
            destDesc.clearColor.a = 0.0f;

            var edgeTextureDesc = destDesc;
            edgeTextureDesc.format = m_SMAAEdgeFormat;
            var edgeTexture = CreateCompatibleTexture(renderGraph, edgeTextureDesc, "_EdgeStencilTexture", false, FilterMode.Bilinear);

            var edgeTextureStencil = CreateCompatibleDepthStencilTexture(renderGraph, destDesc, "_EdgeTexture",
                GetSMAAStencilFormat(), true, FilterMode.Point);

            var blendTextureDesc = destDesc;
            blendTextureDesc.format = GraphicsFormat.R8G8B8A8_UNorm;
            var blendTexture = CreateCompatibleTexture(renderGraph, blendTextureDesc, "_BlendTexture", true, FilterMode.Point);

            // Anti-aliasing. Select immutable keyword variant while recording the graph.
            const int kStencilBit = 64;
            var material = Materials.GetKeywordVariant(Materials.subpixelMorphologicalAntialiasing,
                PostProcessMaterialKey.SMAA(antialiasingQuality));
            material.SetVector(PostProcessShaderConstants._Metrics, new Vector4(1f / destDesc.width, 1f / destDesc.height, destDesc.width, destDesc.height));
            material.SetTexture(PostProcessShaderConstants._AreaTexture, Materials.resources.textures.smaaAreaTex);
            material.SetTexture(PostProcessShaderConstants._SearchTexture, Materials.resources.textures.smaaSearchTex);
            material.SetFloat(PostProcessShaderConstants._StencilRef, (float)kStencilBit);
            material.SetFloat(PostProcessShaderConstants._StencilMask, (float)kStencilBit);

            using (var builder = renderGraph.AddRasterRenderPass<SMAAPassData>("SMAA Edge Detection", out var passData, ProfilingSampler.Get(URPProfileId.SMAAEdgeDetection)))
            {
                PostProcessGraphPasses.SetupFullscreenPass(builder, passData, source, edgeTexture, material, 0);
                builder.SetRenderAttachmentDepth(edgeTextureStencil);
                builder.UseTexture(resourceData.cameraDepth, AccessFlags.Read);

                builder.SetRenderFunc(static (SMAAPassData data, RasterGraphContext context) =>
                {
                    // Pass 1: Edge detection
                    PostProcessGraphPasses.DrawFullscreen(data, context);
                });
            }

            using (var builder = renderGraph.AddRasterRenderPass<SMAAPassData>("SMAA Blend weights", out var passData, ProfilingSampler.Get(URPProfileId.SMAABlendWeight)))
            {
                PostProcessGraphPasses.SetupFullscreenPass(builder, passData, edgeTexture, blendTexture, material, 1);
                builder.SetRenderAttachmentDepth(edgeTextureStencil, AccessFlags.Read);

                builder.SetRenderFunc(static (SMAAPassData data, RasterGraphContext context) =>
                {
                    // Pass 2: Blend weights
                    PostProcessGraphPasses.DrawFullscreen(data, context);
                });
            }

            using (var builder = renderGraph.AddRasterRenderPass<SMAAPassData>("SMAA Neighborhood blending", out var passData, ProfilingSampler.Get(URPProfileId.SMAANeighborhoodBlend)))
            {
                PostProcessGraphPasses.SetupFullscreenPass(builder, passData, source, smaaTarget, material, 2);
                passData.blendTexture = blendTexture;
                builder.UseTexture(blendTexture, AccessFlags.Read);

                builder.SetRenderFunc(static (SMAAPassData data, RasterGraphContext context) =>
                {
                    // Pass 3: Neighborhood blending
                    data.material.SetTexture(PostProcessShaderConstants._BlendTexture, data.blendTexture);
                    PostProcessGraphPasses.DrawFullscreen(data, context);
                });
            }

            renderGraph.EndProfilingSampler(ProfilingSampler.Get(URPProfileId.SMAA));
            return smaaTarget;
        }
    }
}
