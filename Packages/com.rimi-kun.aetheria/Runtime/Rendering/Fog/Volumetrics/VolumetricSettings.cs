using Aetheria.Runtime.Overrides;
using UnityEngine;
using UnityEngine.Experimental.Rendering;

namespace Aetheria.Runtime.Rendering.Fog.Volumetrics
{
    internal readonly struct VolumetricQualitySettings
    {
        public readonly int GridWidth;
        public readonly int GridHeight;
        public readonly int GridDepth;
        public readonly float ScreenFraction;
        public readonly bool GaussianFilterEnabled;
        public readonly bool UseBiquadraticReconstruction;
        public readonly float SliceDistributionUniformity;
        public readonly bool ShadowsEnabled;
        public readonly GraphicsFormat VBufferGraphicsFormat;

        public VolumetricQualitySettings(FogVolume volume)
        {
            int width = Mathf.Clamp(volume.FroxelWidth.value, 16, 512);
            int height = Mathf.Clamp(volume.FroxelHeight.value, 16, 512);
            int depth = Mathf.Clamp(volume.FroxelDepth.value, 8, 256);

            switch (volume.GridPreset.value)
            {
                case VolumetricFogGridPreset.Low:
                    width = 96;
                    height = 54;
                    depth = 48;
                    break;
                case VolumetricFogGridPreset.Medium:
                    width = 128;
                    height = 72;
                    depth = 56;
                    break;
                case VolumetricFogGridPreset.High:
                    width = 160;
                    height = 90;
                    depth = 64;
                    break;
                case VolumetricFogGridPreset.Ultra:
                    width = 240;
                    height = 135;
                    depth = 72;
                    break;
            }

            GridWidth = width;
            GridHeight = height;
            GridDepth = depth;
            ScreenFraction = Mathf.Clamp(
                Mathf.Min(width / 1920.0f, height / 1080.0f),
                1.0f / 512.0f,
                1.0f);
            GaussianFilterEnabled = volume.GaussianFilterEnabled.value;
            UseBiquadraticReconstruction =
                volume.ReconstructionFilter.value == VolumetricFogReconstructionFilter.Biquadratic;
            SliceDistributionUniformity = Mathf.Clamp01(volume.SliceDistributionUniformity.value);
            ShadowsEnabled = volume.MainLightShadowsEnabled.value;
            VBufferGraphicsFormat = GraphicsFormat.R16G16B16A16_SFloat;
        }

        public Vector3Int ComputeViewportSize(int pixelWidth, int pixelHeight)
        {
            float screenScale = Mathf.Min(
                Mathf.Max(1, pixelWidth) / 1920.0f,
                Mathf.Max(1, pixelHeight) / 1080.0f);
            float gridScale = screenScale <= 1.0f
                ? screenScale
                : Mathf.Min(1.5f, 1.0f + (screenScale - 1.0f) * 0.5f);

            return new Vector3Int(
                Mathf.Max(1, Mathf.RoundToInt(GridWidth * gridScale)),
                Mathf.Max(1, Mathf.RoundToInt(GridHeight * gridScale)),
                GridDepth);
        }
    }

    internal readonly struct VolumetricRuntimeSettings
    {
        public readonly VolumetricQualitySettings Quality;
        public readonly VolumetricFogVolumeData FogData;
        public readonly bool Enabled;
        public readonly float MaxDistance;

        public VolumetricRuntimeSettings(FogVolume volume)
        {
            Enabled = volume && volume.IsVolumetricActive();
            Quality = new VolumetricQualitySettings(volume);
            FogData = new VolumetricFogVolumeData(volume, Quality);
            MaxDistance = volume
                ? Mathf.Max(1.0f, volume.DepthExtent.value)
                : 1.0f;
        }
    }
}
