using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace Aetheria.Runtime.Rendering.Passes
{
    internal sealed class EnvironmentLightingResolvePass
    {
        private readonly EnvironmentLightingPass _lightingPass;

        public EnvironmentLightingResolvePass(EnvironmentLightingPass lightingPass)
        {
            _lightingPass = lightingPass;
        }

        public void RecordRenderGraph(
            RenderGraph renderGraph,
            ContextContainer frameData)
        {
            if (!frameData.Contains<SkyFrameData>())
                return;

            var sky = frameData.Get<SkyFrameData>();
            ref var environment = ref sky.Environment;
            
            bool targetUpdated =
                environment.PendingTargetCubemap.IsValid() &&
                environment.PendingTargetAmbientProbe.IsValid();

            _lightingPass?.RecordTemporalBlend(
                renderGraph,
                ref environment,
                targetUpdated);

            environment.PendingTargetCubemap = TextureHandle.nullHandle;
            environment.PendingTargetAmbientProbe = BufferHandle.nullHandle;
        }
    }
}
