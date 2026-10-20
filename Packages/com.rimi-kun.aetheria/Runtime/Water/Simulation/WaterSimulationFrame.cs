using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using System;

namespace Aetheria.Runtime.Water
{
    internal struct WaterSurfaceSimulationFrame
    {
        internal WaterSurface surface;
        internal int cascadeCount;
        internal TextureHandle directionalNormalizationLut;
        internal bool requiresDirectionalNormalizationUpdate;
        internal TextureHandle foam;
        internal TextureHandle foamSource;
        internal TextureHandle foamTarget;
        internal BufferHandle foamCascades;
        internal TextureHandle currentMap;
        internal bool spectralUpdateScheduled;
        internal bool waveMipGenerationRequired;
        internal bool foamUpdateScheduled;
        internal bool foamSourceValid;
        internal bool foamPrewarmingRequired;
        internal bool waveDrivenFoamUpdateRequired;
        internal bool foamUsesCurrent;
        internal int foamLodScaleShift;
        internal int foamSubstepCount;
    }

    internal sealed class WaterSimulationFrame : ContextItem
    {
        private const int k_CascadeStride = WaterLimits.CascadeCount;
        internal int surfaceCount;
        internal readonly WaterSurfaceSimulationFrame[] surfaces = new WaterSurfaceSimulationFrame[WaterLimits.MaxSurfaceCount];
        internal readonly TextureHandle[] waveField = new TextureHandle[WaterLimits.MaxSurfaceCount * k_CascadeStride];
        internal readonly TextureHandle[] spectrum = new TextureHandle[WaterLimits.MaxSurfaceCount * k_CascadeStride];
        internal readonly TextureHandle[] previousSpectrum = new TextureHandle[WaterLimits.MaxSurfaceCount * k_CascadeStride];
        internal readonly TextureHandle[] evolvedSpectrumReal = new TextureHandle[k_CascadeStride];
        internal readonly TextureHandle[] evolvedSpectrumImaginary = new TextureHandle[k_CascadeStride];
        internal readonly TextureHandle[] fftRowReal = new TextureHandle[k_CascadeStride];
        internal readonly TextureHandle[] fftRowImaginary = new TextureHandle[k_CascadeStride];

        internal int CascadeIndex(int surfaceIndex, int cascadeIndex) => surfaceIndex * k_CascadeStride + cascadeIndex;
        internal TextureHandle WaveField(int surfaceIndex, int cascadeIndex) => waveField[CascadeIndex(surfaceIndex, cascadeIndex)];
        internal TextureHandle Spectrum(int surfaceIndex, int cascadeIndex) => spectrum[CascadeIndex(surfaceIndex, cascadeIndex)];
        internal TextureHandle PreviousSpectrum(int surfaceIndex, int cascadeIndex) => previousSpectrum[CascadeIndex(surfaceIndex, cascadeIndex)];

        internal void Initialize(WaterSurface[] activeSurfaces, int activeSurfaceCount)
        {
            surfaceCount = activeSurfaceCount;
            for (int surfaceIndex = 0; surfaceIndex < activeSurfaceCount; ++surfaceIndex)
            {
                WaterSurface surface = activeSurfaces[surfaceIndex];
                surfaces[surfaceIndex] = new WaterSurfaceSimulationFrame
                {
                    surface = surface,
                    cascadeCount = surface != null && surface.simulationEnabled
                        ? surface.simulation?.activeCascadeCount ?? 0
                        : 0
                };
            }
        }

        public override void Reset()
        {
            Array.Clear(surfaces, 0, surfaces.Length);
            Array.Clear(waveField, 0, waveField.Length);
            Array.Clear(spectrum, 0, spectrum.Length);
            Array.Clear(previousSpectrum, 0, previousSpectrum.Length);
            Array.Clear(evolvedSpectrumReal, 0, evolvedSpectrumReal.Length);
            Array.Clear(evolvedSpectrumImaginary, 0, evolvedSpectrumImaginary.Length);
            Array.Clear(fftRowReal, 0, fftRowReal.Length);
            Array.Clear(fftRowImaginary, 0, fftRowImaginary.Length);
            surfaceCount = 0;
        }
    }
}
