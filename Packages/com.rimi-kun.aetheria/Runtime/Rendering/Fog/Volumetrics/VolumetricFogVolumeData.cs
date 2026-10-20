using System.Runtime.InteropServices;
using Aetheria.Runtime.Overrides;
using UnityEngine;

namespace Aetheria.Runtime.Rendering.Fog.Volumetrics
{
    [StructLayout(LayoutKind.Sequential)]
    internal readonly struct VolumetricFogVolumeData
    {
        public readonly Vector4 DensityParams; // base extinction, extinction scale, unused, unused
        public readonly Vector4 LayerParams; // base altitude, scale height, inverse scale height, unused
        public readonly Vector4 ScatteringPhaseParams; // scattering albedo.rgb, anisotropy
        public readonly Vector4 LightingParams; // main shadow dimmer, GI dimmer, receive shadows, runtime shadow availability
        public readonly Vector4 AmbientLightingParams; // rgb source tint/color, w = use environment probe
        public readonly Vector4 ShadowParams; // reserved, reserved, reserved, near fade enabled
        public readonly Vector4 FogControlParams; // near fade start, fade distance, exponential layer enabled, unused

        public VolumetricFogVolumeData(FogVolume volume, VolumetricQualitySettings quality)
        {
            bool active = volume && volume.IsVolumetricActive();
            float baseExtinction = active
                ? 1.0f / Mathf.Max(volume.MeanFreePath.value, 1.0e-6f)
                : 0.0f;
            float extinctionScale = active
                ? Mathf.Max(0.0f, volume.VolumetricExtinctionScale.value)
                : 0.0f;
            float layerDepth = active
                ? Mathf.Max(0.01f, volume.MaximumHeight.value - volume.BaseHeight.value)
                : 1.0f;
            float scaleHeight = layerDepth * 0.144765f;
            
            var albedo = active ? volume.Albedo.value.linear : Color.white;
            
            float anisotropy = active ? volume.Anisotropy.value : 0.0f;
            
            bool useEnvironmentProbe = active && volume.ColorMode.value == FogColorMode.SkyColor;
            
            var ambientLightingColor = active
                ? (useEnvironmentProbe ? volume.Tint.value.linear : volume.Color.value.linear)
                : Color.black;

            DensityParams = new Vector4(baseExtinction, extinctionScale, 0.0f, 0.0f);
            LayerParams = new Vector4(
                active ? volume.BaseHeight.value : 0.0f,
                scaleHeight,
                1.0f / Mathf.Max(scaleHeight, 1.0e-6f),
                0.0f);
            ScatteringPhaseParams = new Vector4(
                Mathf.Clamp01(albedo.r),
                Mathf.Clamp01(albedo.g),
                Mathf.Clamp01(albedo.b),
                anisotropy);

            bool receiveShadows = active &&
                                  volume.ReceiveMainLightShadows.value &&
                                  quality.ShadowsEnabled;
            
            LightingParams = new Vector4(
                receiveShadows ? Mathf.Clamp01(volume.MainLightShadowDimmer.value) : 0.0f,
                active ? Mathf.Clamp01(volume.GlobalLightProbeDimmer.value) : 0.0f,
                receiveShadows ? 1.0f : 0.0f,
                0.0f);
            
            AmbientLightingParams = new Vector4(
                ambientLightingColor.r,
                ambientLightingColor.g,
                ambientLightingColor.b,
                useEnvironmentProbe ? 1.0f : 0.0f);

            ShadowParams = new Vector4(
                0.0f,
                0.0f,
                0.0f,
                active && volume.NearDampEnable.value ? 1.0f : 0.0f);
            
            FogControlParams = new Vector4(
                active ? volume.FogStartDistance.value : 0.0f,
                active ? Mathf.Max(0.0001f, volume.DampDistance.value) : 1.0f,
                active ? 1.0f : 0.0f,
                0.0f);
        }
    }
}