using Aetheria.Runtime.Overrides;
using Aetheria.Runtime.Utils;
using UnityEngine;

namespace Aetheria.Runtime.Rendering
{
    internal struct FogParameters : IShaderValueSource
    {
        public Color Color;
        public Vector4 MipParameters;
        public Vector4 PlanetUpAltitude;
        public Vector4 HeightExponents;
        public float MaxDistance;
        public float BaseExtinction;
        public Vector3 BaseScattering;
        public float BaseHeight;
        public float ColorMode;
        public float ConvolutionMipCount;

        public static FogParameters Build(
            FogVolume fog,
            Vector3 planetUp,
            float cameraAltitude,
            int convolutionMipCount)
        {
            float layerDepth = Mathf.Max(
                0.01f,
                fog.MaximumHeight.value - fog.BaseHeight.value);
            
            // Exp[-depth / H] = 0.001, therefore H = depth / -Log[0.001].
            float scaleHeight = layerDepth * 0.144765f;
            float extinction = 1.0f / Mathf.Max(fog.MeanFreePath.value, 1.0e-6f);
            
            if (fog.IsVolumetricActive())
                extinction *= Mathf.Max(0.0f, fog.VolumetricExtinctionScale.value);
            
            var linearAlbedo = fog.Albedo.value.linear;
            var baseScattering = new Vector3(
                Mathf.Clamp01(linearAlbedo.r),
                Mathf.Clamp01(linearAlbedo.g),
                Mathf.Clamp01(linearAlbedo.b)) * extinction;
            var colorMode = fog.ColorMode.value;
            var color = colorMode == FogColorMode.SkyColor ? fog.Tint.value : fog.Color.value;
            
            float mipNear = Mathf.Max(0.0f, fog.MipFogNear.value);
            float mipFar = Mathf.Max(mipNear + 1.0e-4f, fog.MipFogFar.value);

            return new FogParameters
            {
                Color = color,
                MipParameters = new Vector4(
                    mipNear,
                    mipFar,
                    Mathf.Clamp01(fog.MipFogMaxMip.value),
                    0.0f),
                PlanetUpAltitude = new Vector4(planetUp.x, planetUp.y, planetUp.z, cameraAltitude),
                HeightExponents = new Vector4(1.0f / scaleHeight, scaleHeight, 0.0f, 0.0f),
                MaxDistance = Mathf.Max(0.0f, fog.MaxFogDistance.value),
                BaseExtinction = extinction,
                BaseScattering = baseScattering,
                BaseHeight = fog.BaseHeight.value,
                ColorMode = (float)colorMode,
                ConvolutionMipCount = Mathf.Max(2, convolutionMipCount)
            };
        }

        public void WriteTo(ref ShaderValueWriter writer)
        {
            writer.SetFloat(ShaderProperties.Fog.Enabled, 1.0f);
            writer.SetFloat(ShaderProperties.Fog.ColorMode, ColorMode);
            writer.SetColor(ShaderProperties.Fog.Color, Color);
            writer.SetVector(ShaderProperties.Fog.MipParameters, MipParameters);
            writer.SetFloat(
                ShaderProperties.Fog.ConvolutionMipCount,
                ConvolutionMipCount);
            writer.SetFloat(ShaderProperties.Fog.MaxDistance, MaxDistance);
            writer.SetFloat(ShaderProperties.Fog.BaseExtinction, BaseExtinction);
            writer.SetVector(ShaderProperties.Fog.BaseScattering, BaseScattering);
            writer.SetFloat(ShaderProperties.Fog.BaseHeight, BaseHeight);
            writer.SetVector(ShaderProperties.Fog.HeightExponents, HeightExponents);
            writer.SetVector(ShaderProperties.Fog.PlanetUpAltitude, PlanetUpAltitude);
        }
    }
}