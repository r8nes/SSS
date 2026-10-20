using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.RenderGraphModule;

namespace Aetheria.Runtime.Water
{
    internal static class WaterWaveMipGenerationPass
    {
        internal sealed class PassData
        {
            public WaterSimulationFrame frame;
        }

        internal static void Record(RenderGraph renderGraph, WaterSimulationFrame frame)
        {
            bool hasDirtySurface = false;
            for (int surfaceIndex = 0; surfaceIndex < frame.surfaceCount; ++surfaceIndex)
            {
                ref WaterSurfaceSimulationFrame surfaceFrame = ref frame.surfaces[surfaceIndex];
                surfaceFrame.waveMipGenerationRequired = surfaceFrame.spectralUpdateScheduled
                    && frame.WaveField(surfaceIndex, 0).IsValid();
                hasDirtySurface |= surfaceFrame.waveMipGenerationRequired;
            }
            if (!hasDirtySurface)
                return;

            using (var profiledPass = WaterProfiling.WaveMipGeneration.AddUnsafePass<PassData>(
                renderGraph, out var passData))
            {
                var builder = profiledPass.Builder;
                passData.frame = frame;
                for (int surfaceIndex = 0; surfaceIndex < frame.surfaceCount; ++surfaceIndex)
                {
                    ref WaterSurfaceSimulationFrame surfaceFrame = ref frame.surfaces[surfaceIndex];
                    if (!surfaceFrame.waveMipGenerationRequired)
                        continue;
                    for (int cascadeIndex = 0; cascadeIndex < surfaceFrame.cascadeCount; ++cascadeIndex)
                    {
                        builder.UseTexture(frame.WaveField(surfaceIndex, cascadeIndex), AccessFlags.ReadWrite);
                    }
                }

                builder.SetRenderFunc(static (PassData data, UnsafeGraphContext context) =>
                {
                    WaterSimulationFrame frame = data.frame;
                    for (int surfaceIndex = 0; surfaceIndex < frame.surfaceCount; ++surfaceIndex)
                    {
                        ref WaterSurfaceSimulationFrame surfaceFrame = ref frame.surfaces[surfaceIndex];
                        if (!surfaceFrame.waveMipGenerationRequired)
                            continue;
                        WaterGpuSimulationResources gpu = surfaceFrame.surface?.simulation?.gpuResources;
                        if (gpu == null)
                            continue;
                        for (int cascadeIndex = 0; cascadeIndex < surfaceFrame.cascadeCount; ++cascadeIndex)
                        {
                            WaterWaveCascadeResources cascade = gpu.waveCascades[cascadeIndex];
                            if (cascade?.waveField != null)
                                context.cmd.GenerateMips(cascade.waveField.rt);
                        }
                    }
                });
            }
        }
    }
}
