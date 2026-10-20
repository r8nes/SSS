using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.RenderGraphModule;

namespace Aetheria.Runtime.Water
{
    internal static class WaterFoamSimulationPass
    {
        internal sealed class PassData
        {
            public WaterSimulationPipeline system;
            public WaterSimulationFrame frame;
            public BufferHandle currentSectorSwizzleBuffer;
        }

        internal static void Record(
            WaterSimulationPipeline system,
            RenderGraph renderGraph,
            WaterSimulationFrame frame,
            GraphicsBuffer currentSectorSwizzleBuffer)
        {
            bool hasUpdate = false;
            for (int surfaceIndex = 0; surfaceIndex < frame.surfaceCount; ++surfaceIndex)
            {
                ref WaterSurfaceSimulationFrame surfaceFrame = ref frame.surfaces[surfaceIndex];
                WaterSurface surface = surfaceFrame.surface;
                WaterSimulationState simulation = surface?.simulation;
                WaterFoamResources foam = simulation?.gpuResources?.foam;
                if (foam == null || !foam.IsValid())
                    continue;

                surfaceFrame.foam = renderGraph.ImportTexture(foam.current);
                surfaceFrame.foamCascades = renderGraph.ImportBuffer(simulation.gpuResources.foamCascadesBuffer);
                if (!surface.HasSimulationFoam())
                {
                    simulation.foam.Pause();
                    continue;
                }

                bool simulateWaves = simulation.foam.stepDeltaTime > 0.0f
                    || (surface.foamPrewarmingEnabled && simulation.foam.needsPrewarmingThisStep);
                bool validWaves = true;
                if (simulateWaves)
                {
                    for (int cascadeIndex = 0; cascadeIndex < surfaceFrame.cascadeCount; ++cascadeIndex)
                        validWaves &= frame.WaveField(surfaceIndex, cascadeIndex).IsValid();
                }

                hasUpdate |= simulation.foam.simulationStepRequired && validWaves;
            }

            if (!hasUpdate || system.m_FoamSimulationShader == null)
                return;

            using (var profiledPass = WaterProfiling.FoamSimulation.AddComputePass<PassData>(
                renderGraph, out var passData))
            {
                var builder = profiledPass.Builder;
                passData.system = system;
                passData.frame = frame;
                passData.currentSectorSwizzleBuffer = currentSectorSwizzleBuffer != null
                    ? renderGraph.ImportBuffer(currentSectorSwizzleBuffer)
                    : default;
                if (passData.currentSectorSwizzleBuffer.IsValid())
                    builder.UseBuffer(passData.currentSectorSwizzleBuffer, AccessFlags.Read);

                for (int surfaceIndex = 0; surfaceIndex < frame.surfaceCount; ++surfaceIndex)
                {
                    ref WaterSurfaceSimulationFrame surfaceFrame = ref frame.surfaces[surfaceIndex];
                    WaterSurface surface = surfaceFrame.surface;
                    WaterSimulationState simulation = surface?.simulation;
                    WaterFoamResources foam = simulation?.gpuResources?.foam;
                    if (foam == null
                        || !foam.IsValid()
                        || !surface.HasSimulationFoam()
                        || !simulation.foam.simulationStepRequired)
                        continue;

                    bool foamPrewarmingRequired = surface.foamPrewarmingEnabled && simulation.foam.needsPrewarmingThisStep;
                    bool simulateWaves = simulation.foam.stepDeltaTime > 0.0f || foamPrewarmingRequired;
                    bool validWaves = true;
                    if (simulateWaves)
                    {
                        for (int cascadeIndex = 0; cascadeIndex < surfaceFrame.cascadeCount; ++cascadeIndex)
                            validWaves &= frame.WaveField(surfaceIndex, cascadeIndex).IsValid();
                    }
                    if (!validWaves)
                        continue;

                    foam.PrepareSimulationStep();

                    surfaceFrame.foamUpdateScheduled = true;
                    surfaceFrame.foamSourceValid = simulation.foam.historyValid;
                    surfaceFrame.foamPrewarmingRequired = foamPrewarmingRequired;
                    surfaceFrame.waveDrivenFoamUpdateRequired = simulateWaves;
                    surfaceFrame.foamUsesCurrent = simulateWaves
                        && surface.currentMap != null
                        && surface.currentMapInfluence > 1e-4f;
                    surfaceFrame.foamLodScaleShift = simulation.foam.lodScaleShift;
                    surfaceFrame.foamSubstepCount = Mathf.Max(simulation.foam.substepCount, 1);
                    surfaceFrame.foamSource = renderGraph.ImportTexture(foam.history);
                    surfaceFrame.foamTarget = renderGraph.ImportTexture(foam.current);
                    surfaceFrame.foamCascades = renderGraph.ImportBuffer(simulation.gpuResources.foamCascadesBuffer);
                    surfaceFrame.currentMap = system.m_TextureCache.Import(
                        renderGraph,
                        surface.GetCurrentMapTexture(Texture2D.blackTexture));

                    if (surfaceFrame.waveDrivenFoamUpdateRequired)
                    {
                        for (int cascadeIndex = 0; cascadeIndex < surfaceFrame.cascadeCount; ++cascadeIndex)
                            builder.UseTexture(frame.WaveField(surfaceIndex, cascadeIndex), AccessFlags.Read);
                    }
                    builder.UseTexture(
                        surfaceFrame.foamSource,
                        surfaceFrame.foamSubstepCount > 1 ? AccessFlags.ReadWrite : AccessFlags.Read);
                    builder.UseTexture(surfaceFrame.foamTarget, AccessFlags.ReadWrite);
                    builder.UseBuffer(surfaceFrame.foamCascades, AccessFlags.Read);
                    if (surfaceFrame.foamUsesCurrent && surfaceFrame.currentMap.IsValid())
                        builder.UseTexture(surfaceFrame.currentMap, AccessFlags.Read);
                    if (surface.simulationConstantBuffer != null)
                        builder.UseBuffer(renderGraph.ImportBuffer(surface.simulationConstantBuffer), AccessFlags.Read);

                    if ((surfaceFrame.foamSubstepCount & 1) == 0)
                    {
                        foam.CommitHistoryAsCurrent();
                        surfaceFrame.foam = surfaceFrame.foamSource;
                    }
                    else
                    {
                        surfaceFrame.foam = surfaceFrame.foamTarget;
                    }

                    simulation.foam.CommitStep();
                }

                builder.SetRenderFunc(static (PassData data, ComputeGraphContext context) =>
                {
                    data.system.DispatchFoamSimulation(context.cmd, data);
                });
            }
        }
    }
}
