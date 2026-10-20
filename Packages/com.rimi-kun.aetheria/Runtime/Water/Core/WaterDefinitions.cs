using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Unity.Mathematics;

namespace Aetheria.Runtime.Water
{
    internal static class WaterConstants
    {
        public const int k_DirectionalNormalizationLutSize = 1024;

        public const float k_GravityConstant = 9.81f;
        public const float k_KilometerPerHourToMeterPerSecond = 1.0f / 3.6f;
        public const int k_GridMeshResolution = 128;
        public const float k_MinScatteringAmplitude = 1.0f;
        public const float k_EnvironmentRoughnessWindSpeedMetersPerSecond = 30.0f * k_KilometerPerHourToMeterPerSecond;
        public const float k_ScatteringRange = 0.75f;

        private const float k_InvSqrt2 = 0.70710678118f;
        internal const int k_CurrentSectorSwizzleCount = 16;

        internal static float4 GetCurrentSectorSwizzle(int index)
        {
            switch (index)
            {
                case 0: return new float4(1, 0, k_InvSqrt2, k_InvSqrt2);
                case 1: return new float4(0, 1, k_InvSqrt2, k_InvSqrt2);
                case 2: return new float4(0, 1, -k_InvSqrt2, k_InvSqrt2);
                case 3: return new float4(-1, 0, -k_InvSqrt2, k_InvSqrt2);
                case 4: return new float4(-1, 0, -k_InvSqrt2, -k_InvSqrt2);
                case 5: return new float4(0, -1, -k_InvSqrt2, -k_InvSqrt2);
                case 6: return new float4(0, -1, k_InvSqrt2, -k_InvSqrt2);
                case 7: return new float4(1, 0, k_InvSqrt2, -k_InvSqrt2);
                case 8: return new float4(1, 0, k_InvSqrt2, -k_InvSqrt2);
                case 9: return new float4(0, -1, k_InvSqrt2, -k_InvSqrt2);
                case 10: return new float4(0, -1, -k_InvSqrt2, -k_InvSqrt2);
                case 11: return new float4(-1, 0, -k_InvSqrt2, -k_InvSqrt2);
                case 12: return new float4(-1, 0, -k_InvSqrt2, k_InvSqrt2);
                case 13: return new float4(0, 1, -k_InvSqrt2, k_InvSqrt2);
                case 14: return new float4(0, 1, k_InvSqrt2, k_InvSqrt2);
                default: return new float4(1, 0, k_InvSqrt2, k_InvSqrt2);
            }
        }

        internal static readonly float4[] k_CurrentSectorSwizzles = CreateCurrentSectorSwizzles();

        private static float4[] CreateCurrentSectorSwizzles()
        {
            var swizzles = new float4[k_CurrentSectorSwizzleCount];
            for (int index = 0; index < swizzles.Length; ++index)
                swizzles[index] = GetCurrentSectorSwizzle(index);
            return swizzles;
        }
    }

    [GenerateHLSL(PackingRules.Exact)]
    public enum WaterDebugMode
    {
        None = 0,
        SimulationMask = 1,
        Current = 2,
        Displacement = 3,
        ShadingNormal = 4,
        GeometryNormal = 5,
        CascadeContribution = 6,
        Foam = 7,
        Breaking = 8,
    }

    [GenerateHLSL(PackingRules.Exact)]
    public enum WaterSimulationMaskDebugMode
    {
        RedChannel = 0,
        GreenChannel = 1,
        BlueChannel = 2,
    }


    [GenerateHLSL(PackingRules.Exact)]
    internal enum WaterShaderLimits
    {
        CascadeCount = WaterLimits.CascadeCount,
        MaxLodCount = WaterLimits.MaxLodCount,
    }

    [GenerateHLSL(PackingRules.Exact)]
    internal enum WaterRenderMode
    {
        Physical = 0,
        Debug = 1,
    }

    [GenerateHLSL(PackingRules.Exact, false)]
    internal struct WaterSurfaceProfile
    {
        public Vector3 upDirection;
        public float maxRefractionDistance;

        public Vector3 extinction;
        public float environmentPerceptualRoughness;

        public Vector3 scatteringAlbedo;
        public float scatteringAlbedoPadding;

        public Vector3 ambientLighting;
        public float causticsDirectionalShadowDimmer;

        public Vector4 probeLightingFadeParameters;

        public Vector3 foamColor;
        public float foamSmoothness;

        public Vector3 deepFoamColor;
        public float deepFoamWeight;

        public Vector3 bubbleFoamColor;
        public float bubbleFoamWeight;

        public float nearSmoothness;
        public float farRoughness;
        public float smoothnessFadeStart;
        public float smoothnessFadeDistance;

        public float directLightTipScattering;
        public float directLightBodyScattering;
        public uint renderMode;
        public float surfaceLevel;

        public Vector4 surfaceColorParameters;
        public Vector4 shallowSurfaceColor;
        public Vector4 deepSurfaceColor;
        public Vector4 shorelineSmoothnessParameters;
        public Vector4 reflectionParameters;

        public Vector4 causticsAppearanceParameters;
        public Vector4 causticsProjectionParameters;
    }
}
