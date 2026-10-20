using UnityEngine.Rendering.RenderGraphModule;

namespace UnityEngine.Rendering.Universal
{
    internal sealed class StopNaNsPostProcessPass : PostProcessPass
    {
        public StopNaNsPostProcessPass(PostProcessMaterialLibrary materials)
            : base(materials)
        {
        }

        private sealed class StopNaNsPassData : PostProcessFullscreenPassData
        {
        }

        public TextureHandle Render(RenderGraph renderGraph, in TextureHandle activeCameraColor)
        {
            var stopNaNTarget = CreateCompatibleTexture(renderGraph, activeCameraColor, "_StopNaNsTarget", false, FilterMode.Bilinear);

            using (var builder = renderGraph.AddRasterRenderPass<StopNaNsPassData>("Stop NaNs", out var passData,
                       ProfilingSampler.Get(URPProfileId.StopNaNs)))
            {
                PostProcessGraphPasses.SetupFullscreenPass(builder, passData, activeCameraColor, stopNaNTarget,
                    Materials.stopNaN, 0);
                builder.SetRenderFunc(static (StopNaNsPassData data, RasterGraphContext context) =>
                    PostProcessGraphPasses.DrawFullscreen(data, context));
            }

            return stopNaNTarget;
        }
    }
}
