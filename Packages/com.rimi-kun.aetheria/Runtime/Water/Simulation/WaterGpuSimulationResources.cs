using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Experimental.Rendering;

namespace Aetheria.Runtime.Water
{
    internal sealed class WaterSpectrumCascadeResources
    {
        internal readonly int resolution;
        internal RTHandle spectrum;
        internal RTHandle previousSpectrum;

        private WaterSpectrumCascadeResources(int resolution, int cascadeIndex)
        {
            this.resolution = resolution;
            spectrum = AllocateSpectrumTexture(resolution, $"Water Spectrum Cascade {cascadeIndex}");
        }

        internal static WaterSpectrumCascadeResources Create(int resolution, int cascadeIndex) =>
            new(resolution, cascadeIndex);

        internal bool IsValid() => spectrum != null;

        internal void EnsurePrevious(int cascadeIndex)
        {
            if (previousSpectrum != null)
                return;
            previousSpectrum = AllocateSpectrumTexture(resolution, $"Water Previous Spectrum Cascade {cascadeIndex}");
        }

        internal void ReleasePrevious()
        {
            RTHandles.Release(previousSpectrum);
            previousSpectrum = null;
        }

        internal void SwapCurrentAndPrevious() => (spectrum, previousSpectrum) = (previousSpectrum, spectrum);

        internal void Release()
        {
            ReleasePrevious();
            RTHandles.Release(spectrum);
            spectrum = null;
        }

        private static RTHandle AllocateSpectrumTexture(int resolution, string name)
        {
            RTHandle texture = RTHandles.Alloc(
                resolution,
                resolution,
                colorFormat: GraphicsFormat.R32G32B32A32_SFloat,
                enableRandomWrite: true,
                useMipMap: false,
                autoGenerateMips: false,
                filterMode: FilterMode.Point,
                wrapMode: TextureWrapMode.Repeat,
                name: name);
            texture.rt.Create();
            return texture;
        }
    }

    internal sealed class WaterGpuSimulationResources
    {
        internal WaterSpectrumCascadeResources[] spectrumCascades;
        internal RTHandle fallbackPreviousSpectrum;
        internal RTHandle directionalNormalizationLut;
        internal WaterWaveCascadeResources[] waveCascades;
        internal GraphicsBuffer foamCascadesBuffer;
        internal WaterFoamResources foam;

        internal static WaterGpuSimulationResources Create(int simulationResolution, int cascadeCount)
        {
            return new WaterGpuSimulationResources
            {
                spectrumCascades = AllocateSpectrumCascades(simulationResolution, cascadeCount),
                fallbackPreviousSpectrum = AllocateInactiveSpectrumTexture("Water Inactive Previous Spectrum Binding"),
                directionalNormalizationLut = AllocateTexture(
                    WaterConstants.k_DirectionalNormalizationLutSize,
                    cascadeCount,
                    GraphicsFormat.R32_SFloat,
                    "Water Directional Spreading Normalization",
                    FilterMode.Bilinear,
                    TextureWrapMode.Clamp,
                    false),
                waveCascades = AllocateWaveCascades(simulationResolution, cascadeCount),
                foamCascadesBuffer = new GraphicsBuffer(
                    GraphicsBuffer.Target.Structured,
                    WaterFoamState.k_CascadeSetStride * 2,
                    sizeof(float) * 4)
            };
        }

        internal bool IsValid(int expectedCascadeCount) =>
            AreSpectrumCascadesValid(spectrumCascades, expectedCascadeCount)
            && directionalNormalizationLut != null
            && foamCascadesBuffer != null
            && AreWaveCascadesValid(waveCascades, expectedCascadeCount);

        internal void EnsurePreviousSpectrumResources()
        {
            if (spectrumCascades == null)
                return;
            for (int cascadeIndex = 0; cascadeIndex < spectrumCascades.Length; ++cascadeIndex)
                spectrumCascades[cascadeIndex]?.EnsurePrevious(cascadeIndex);
        }

        internal void ReleasePreviousSpectrumResources()
        {
            if (spectrumCascades == null)
                return;
            for (int cascadeIndex = 0; cascadeIndex < spectrumCascades.Length; ++cascadeIndex)
                spectrumCascades[cascadeIndex]?.ReleasePrevious();
        }

        internal void SwapSpectrumHistory()
        {
            if (spectrumCascades == null)
                return;
            for (int cascadeIndex = 0; cascadeIndex < spectrumCascades.Length; ++cascadeIndex)
                spectrumCascades[cascadeIndex]?.SwapCurrentAndPrevious();
        }

        internal void UploadFoamCascades(Vector4[] cascades)
        {
            if (foamCascadesBuffer != null && cascades != null)
                foamCascadesBuffer.SetData(cascades);
        }

        internal void EnsureFoamResources(int resolution, int lodCount)
        {
            if (foam != null && foam.IsValid())
                return;
            foam?.Release();
            foam = WaterFoamResources.Create(resolution, lodCount);
        }

        internal void ReleaseFoamResources()
        {
            foam?.Release();
            foam = null;
        }

        internal void Release()
        {
            ReleaseFoamResources();
            CoreUtils.SafeRelease(foamCascadesBuffer);
            foamCascadesBuffer = null;
            ReleaseWaveCascades(waveCascades);
            ReleaseSpectrumCascades(spectrumCascades);
            RTHandles.Release(directionalNormalizationLut);
            RTHandles.Release(fallbackPreviousSpectrum);
            waveCascades = null;
            spectrumCascades = null;
            directionalNormalizationLut = null;
            fallbackPreviousSpectrum = null;
        }

        private static RTHandle AllocateInactiveSpectrumTexture(string name)
        {
            RTHandle texture = RTHandles.Alloc(
                1,
                1,
                colorFormat: GraphicsFormat.R32G32B32A32_SFloat,
                enableRandomWrite: true,
                useMipMap: false,
                autoGenerateMips: false,
                filterMode: FilterMode.Point,
                wrapMode: TextureWrapMode.Clamp,
                name: name);
            texture.rt.Create();
            return texture;
        }

        private static RTHandle AllocateTexture(int width, int height, GraphicsFormat format, string name,
            FilterMode filterMode, TextureWrapMode wrapMode, bool useMipMap)
        {
            RTHandle texture = RTHandles.Alloc(
                width,
                height,
                colorFormat: format,
                filterMode: filterMode,
                enableRandomWrite: true,
                useMipMap: useMipMap,
                autoGenerateMips: false,
                wrapMode: wrapMode,
                name: name);
            texture.rt.Create();
            return texture;
        }

        private static WaterSpectrumCascadeResources[] AllocateSpectrumCascades(int resolution, int cascadeCount)
        {
            var cascades = new WaterSpectrumCascadeResources[cascadeCount];
            for (int cascadeIndex = 0; cascadeIndex < cascadeCount; ++cascadeIndex)
                cascades[cascadeIndex] = WaterSpectrumCascadeResources.Create(resolution, cascadeIndex);
            return cascades;
        }

        private static WaterWaveCascadeResources[] AllocateWaveCascades(int resolution, int cascadeCount)
        {
            var cascades = new WaterWaveCascadeResources[cascadeCount];
            for (int cascadeIndex = 0; cascadeIndex < cascadeCount; ++cascadeIndex)
                cascades[cascadeIndex] = WaterWaveCascadeResources.Create(resolution, cascadeIndex);
            return cascades;
        }

        private static bool AreSpectrumCascadesValid(WaterSpectrumCascadeResources[] cascades, int expectedCount)
        {
            if (cascades == null || cascades.Length != expectedCount)
                return false;
            for (int cascadeIndex = 0; cascadeIndex < expectedCount; ++cascadeIndex)
                if (cascades[cascadeIndex] == null || !cascades[cascadeIndex].IsValid())
                    return false;
            return true;
        }

        private static bool AreWaveCascadesValid(WaterWaveCascadeResources[] cascades, int expectedCount)
        {
            if (cascades == null || cascades.Length != expectedCount)
                return false;
            for (int cascadeIndex = 0; cascadeIndex < expectedCount; ++cascadeIndex)
                if (cascades[cascadeIndex] == null || !cascades[cascadeIndex].IsValid())
                    return false;
            return true;
        }

        private static void ReleaseSpectrumCascades(WaterSpectrumCascadeResources[] cascades)
        {
            if (cascades == null)
                return;
            for (int cascadeIndex = 0; cascadeIndex < cascades.Length; ++cascadeIndex)
                cascades[cascadeIndex]?.Release();
        }

        private static void ReleaseWaveCascades(WaterWaveCascadeResources[] cascades)
        {
            if (cascades == null)
                return;
            for (int cascadeIndex = 0; cascadeIndex < cascades.Length; ++cascadeIndex)
                cascades[cascadeIndex]?.Release();
        }
    }
}
