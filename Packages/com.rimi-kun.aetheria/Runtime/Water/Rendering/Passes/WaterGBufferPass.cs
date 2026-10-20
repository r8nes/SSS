using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.RenderGraphModule;

namespace Aetheria.Runtime.Water
{
    internal static class WaterGBufferPass
    {
        private sealed class PassData
        {
            public WaterCameraRenderData renderData;
            public TextureHandle opaqueDepth;
        }

        internal static void Record(
            WaterRenderSystem system,
            RenderGraph renderGraph,
            WaterFrameResources frame,
            WaterCameraRenderData renderData,
            WaterSimulationFrame simulationFrame)
        {
            using (var profiledPass = WaterProfiling.SurfaceGBuffer.AddRasterPass<PassData>(
                renderGraph, out var passData))
            {
                var builder = profiledPass.Builder;
                passData.renderData = renderData;
                passData.opaqueDepth = frame.opaqueDepth;

                builder.SetRenderAttachment(frame.materialGBuffer, 0, AccessFlags.Write);
                builder.SetRenderAttachment(frame.surfaceGBuffer, 1, AccessFlags.Write);
                builder.SetRenderAttachment(frame.geometryGBuffer, 2, AccessFlags.Write);
                builder.SetRenderAttachment(frame.metadataGBuffer, 3, AccessFlags.Write);
                builder.SetRenderAttachmentDepth(frame.waterDepth, AccessFlags.ReadWrite);

                for (int surfaceIndex = 0; surfaceIndex < renderData.surfaceCount; ++surfaceIndex)
                {
                    ref var surface = ref renderData.surfaces[surfaceIndex];
                    if (!surface.shouldRender)
                        continue;

                    if (surface.lodPatchBuffer != null)
                        builder.UseBuffer(renderGraph.ImportBuffer(surface.lodPatchBuffer), AccessFlags.Read);
                    if (surface.simulationConstantBuffer != null)
                        builder.UseBuffer(renderGraph.ImportBuffer(surface.simulationConstantBuffer), AccessFlags.Read);
                    if (surface.surfaceConstantBuffer != null)
                        builder.UseBuffer(renderGraph.ImportBuffer(surface.surfaceConstantBuffer), AccessFlags.Read);
                    if (surface.foamCascadesBuffer != null)
                        builder.UseBuffer(renderGraph.ImportBuffer(surface.foamCascadesBuffer), AccessFlags.Read);

                    for (int cascadeIndex = 0; cascadeIndex < surface.activeCascadeCount; ++cascadeIndex)
                    {
                        if (simulationFrame.WaveField(surfaceIndex, cascadeIndex).IsValid())
                            builder.UseTexture(simulationFrame.WaveField(surfaceIndex, cascadeIndex), AccessFlags.Read);
                    }

                    if (simulationFrame.surfaces[surfaceIndex].foam.IsValid())
                        builder.UseTexture(simulationFrame.surfaces[surfaceIndex].foam, AccessFlags.Read);
                }

                if (system.currentSectorSwizzleBuffer != null)
                    builder.UseBuffer(renderGraph.ImportBuffer(system.currentSectorSwizzleBuffer), AccessFlags.Read);
                if (renderData.cameraVariablesBuffer != null)
                    builder.UseBuffer(renderGraph.ImportBuffer(renderData.cameraVariablesBuffer), AccessFlags.Read);
                builder.UseTexture(passData.opaqueDepth, AccessFlags.Read);
                builder.AllowGlobalStateModification(true);

                builder.SetRenderFunc(static (PassData data, RasterGraphContext context) =>
                {
                    context.cmd.SetGlobalTexture(WaterShaderIDs._WaterOpaqueDepthTexture, data.opaqueDepth);

                    for (int surfaceIndex = 0; surfaceIndex < data.renderData.surfaceCount; ++surfaceIndex)
                    {
                        ref var surface = ref data.renderData.surfaces[surfaceIndex];
                        if (!surface.shouldRender)
                            continue;

                        WaterSurfaceRenderer.RenderGBuffer(context.cmd, data.renderData, ref surface);
                    }
                });
            }
        }
    }
}
