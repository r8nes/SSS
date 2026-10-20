using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering.RenderGraphModule;

namespace Aetheria.Runtime.Water
{
    internal static class WaterSpectralSimulationPass
    {
        private static readonly string[] k_EvolvedSpectrumRealNames =
        {
            "Water FFT Spatial Real Cascade 0",
            "Water FFT Spatial Real Cascade 1",
            "Water FFT Spatial Real Cascade 2"
        };

        private static readonly string[] k_EvolvedSpectrumImaginaryNames =
        {
            "Water FFT Spatial Imaginary Cascade 0",
            "Water FFT Spatial Imaginary Cascade 1",
            "Water FFT Spatial Imaginary Cascade 2"
        };

        private static readonly string[] k_FftRowRealNames =
        {
            "Water FFT Row Real Cascade 0",
            "Water FFT Row Real Cascade 1",
            "Water FFT Row Real Cascade 2"
        };

        private static readonly string[] k_FftRowImaginaryNames =
        {
            "Water FFT Row Imaginary Cascade 0",
            "Water FFT Row Imaginary Cascade 1",
            "Water FFT Row Imaginary Cascade 2"
        };

        internal sealed class PassData
        {
            public WaterSimulationPipeline system;
            public WaterSimulationFrame frame;
        }

        internal static void Record(WaterSimulationPipeline system, RenderGraph renderGraph, WaterSimulationFrame frame)
        {
            if (frame.surfaceCount == 0)
                return;

            bool canSimulate = system.m_SpectralSimulationShader != null && system.m_FourierTransformShader != null;
            bool hasUpdate = false;
            int usedCascadeMask = 0;

            for (int surfaceIndex = 0; surfaceIndex < frame.surfaceCount; ++surfaceIndex)
            {
                ref WaterSurfaceSimulationFrame surfaceFrame = ref frame.surfaces[surfaceIndex];
                WaterSurface surface = surfaceFrame.surface;
                WaterSimulationState simulation = surface?.simulation;
                if (surface == null || !surface.simulationEnabled)
                    continue;

                WaterGpuSimulationResources gpu = simulation?.gpuResources;
                int cascadeCount = surfaceFrame.cascadeCount;
                if (gpu == null
                    || gpu.waveCascades == null
                    || gpu.spectrumCascades == null
                    || gpu.waveCascades.Length < cascadeCount
                    || gpu.spectrumCascades.Length < cascadeCount)
                    continue;

                for (int cascadeIndex = 0; cascadeIndex < cascadeCount; ++cascadeIndex)
                {
                    int index = frame.CascadeIndex(surfaceIndex, cascadeIndex);
                    WaterWaveCascadeResources waveCascade = gpu.waveCascades[cascadeIndex];
                    if (waveCascade != null && waveCascade.waveField != null)
                        frame.waveField[index] = renderGraph.ImportTexture(waveCascade.waveField);
                }

                if (!canSimulate
                    || !simulation.waveFieldUpdateRequired
                    || (!simulation.currentSpectrumValid && gpu.directionalNormalizationLut == null)
                    || gpu.fallbackPreviousSpectrum == null)
                    continue;

                bool validSpectrumCascades = true;
                for (int cascadeIndex = 0; cascadeIndex < cascadeCount; ++cascadeIndex)
                {
                    if (gpu.spectrumCascades[cascadeIndex]?.IsValid() != true)
                    {
                        validSpectrumCascades = false;
                        break;
                    }
                }
                if (!validSpectrumCascades)
                    continue;

                surfaceFrame.spectralUpdateScheduled = true;
                hasUpdate = true;
                for (int cascadeIndex = 0; cascadeIndex < cascadeCount; ++cascadeIndex)
                    usedCascadeMask |= 1 << cascadeIndex;
            }

            if (!hasUpdate)
                return;

            using (var profiledPass = WaterProfiling.SpectralSimulation.AddComputePass<PassData>(renderGraph, out var passData))
            {
                var builder = profiledPass.Builder;
                passData.system = system;
                passData.frame = frame;
                int resolution = system.GetCascadeResolution();

                for (int cascadeIndex = 0; cascadeIndex < WaterLimits.CascadeCount; ++cascadeIndex)
                {
                    if ((usedCascadeMask & (1 << cascadeIndex)) == 0)
                        continue;

                    TextureDesc scratchDesc = new TextureDesc(resolution, resolution)
                    {
                        colorFormat = GraphicsFormat.R16G16B16A16_SFloat,
                        useMipMap = false,
                        autoGenerateMips = false,
                        enableRandomWrite = true,
                        filterMode = FilterMode.Point,
                        wrapMode = TextureWrapMode.Repeat,
                        clearBuffer = false
                    };

                    scratchDesc.name = k_EvolvedSpectrumRealNames[cascadeIndex];
                    frame.evolvedSpectrumReal[cascadeIndex] = renderGraph.CreateTexture(scratchDesc);
                    scratchDesc.name = k_EvolvedSpectrumImaginaryNames[cascadeIndex];
                    frame.evolvedSpectrumImaginary[cascadeIndex] = renderGraph.CreateTexture(scratchDesc);
                    scratchDesc.name = k_FftRowRealNames[cascadeIndex];
                    frame.fftRowReal[cascadeIndex] = renderGraph.CreateTexture(scratchDesc);
                    scratchDesc.name = k_FftRowImaginaryNames[cascadeIndex];
                    frame.fftRowImaginary[cascadeIndex] = renderGraph.CreateTexture(scratchDesc);

                    builder.UseTexture(frame.evolvedSpectrumReal[cascadeIndex], AccessFlags.ReadWrite);
                    builder.UseTexture(frame.evolvedSpectrumImaginary[cascadeIndex], AccessFlags.ReadWrite);
                    builder.UseTexture(frame.fftRowReal[cascadeIndex], AccessFlags.ReadWrite);
                    builder.UseTexture(frame.fftRowImaginary[cascadeIndex], AccessFlags.ReadWrite);
                }

                for (int surfaceIndex = 0; surfaceIndex < frame.surfaceCount; ++surfaceIndex)
                {
                    ref WaterSurfaceSimulationFrame surfaceFrame = ref frame.surfaces[surfaceIndex];
                    if (!surfaceFrame.spectralUpdateScheduled)
                        continue;

                    WaterSurface surface = surfaceFrame.surface;
                    WaterSimulationState simulation = surface.simulation;
                    WaterGpuSimulationResources gpu = simulation.gpuResources;

                    for (int cascadeIndex = 0; cascadeIndex < surfaceFrame.cascadeCount; ++cascadeIndex)
                    {
                        int index = frame.CascadeIndex(surfaceIndex, cascadeIndex);
                        WaterSpectrumCascadeResources spectrumCascade = gpu.spectrumCascades[cascadeIndex];
                        frame.spectrum[index] = renderGraph.ImportTexture(spectrumCascade.spectrum);
                        frame.previousSpectrum[index] = renderGraph.ImportTexture(
                            spectrumCascade.previousSpectrum ?? gpu.fallbackPreviousSpectrum);
                        builder.UseTexture(frame.spectrum[index], AccessFlags.ReadWrite);
                        builder.UseTexture(frame.previousSpectrum[index], AccessFlags.ReadWrite);
                        builder.UseTexture(frame.WaveField(surfaceIndex, cascadeIndex), AccessFlags.Write);
                    }

                    if (!simulation.currentSpectrumValid && gpu.directionalNormalizationLut != null)
                    {
                        surfaceFrame.directionalNormalizationLut = renderGraph.ImportTexture(gpu.directionalNormalizationLut);
                        surfaceFrame.requiresDirectionalNormalizationUpdate = simulation.RequiresDirectionalNormalizationUpdate();
                    }

                    if (surfaceFrame.directionalNormalizationLut.IsValid())
                    {
                        builder.UseTexture(surfaceFrame.directionalNormalizationLut,
                            surfaceFrame.requiresDirectionalNormalizationUpdate ? AccessFlags.ReadWrite : AccessFlags.Read);
                    }

                    if (surface.simulationConstantBuffer != null)
                        builder.UseBuffer(renderGraph.ImportBuffer(surface.simulationConstantBuffer), AccessFlags.Read);
                    if (surface.spectrumConstantBuffer != null)
                        builder.UseBuffer(renderGraph.ImportBuffer(surface.spectrumConstantBuffer), AccessFlags.Read);
                }

                builder.SetRenderFunc(static (PassData data, ComputeGraphContext context) =>
                {
                    data.system.DispatchSpectralSimulation(context.cmd, data);
                });
            }
        }
    }
}
