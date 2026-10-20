using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Unity.Mathematics;

namespace Aetheria.Runtime.Water
{
    [GenerateHLSL(needAccessors = false, generateCBuffer = true)]
    internal unsafe struct WaterSimulationShaderVariables
    {
        public Vector4 _WaterCascade0ScaleOffset;
        public Vector4 _WaterCascade1ScaleOffset;
        public Vector4 _WaterCascade2ScaleOffset;
        public Vector4 _WaterCascade0ScalesFade;
        public Vector4 _WaterCascade1ScalesFade;
        public Vector4 _WaterCascade2ScalesFade;
        public Vector4 _WaterFoamSimulationParameters;
        public Vector4 _WaterFoamLodParameters;
        public Vector4 _WaterFoamLodCenter;
        public Vector4 _WaterFoamLodScale;
        public Vector4 _WaterFoamTemporalParameters;
        public Vector4 _WaterFoamAdvectionVelocity;
        public Vector4 _WaterCurrentRegionScaleOffset;
        public float2 _WaterReferenceOrientation;
        public uint _WaterSimulationResolution;
        public uint _WaterFoamEnabled;
        public uint _WaterActiveCascadeCount;
        public float _WaterCurrentMapInfluence;
        public float _WaterScatteringWaveHeight;
        public float _WaterSimulationTime;
        public float _WaterSimulationPadding0;
        public float _WaterSimulationPadding1;
        public float _WaterSimulationPadding2;
        public float _WaterSimulationPadding3;
    }

    [GenerateHLSL(needAccessors = false, generateCBuffer = true)]
    internal unsafe struct WaterSpectrumShaderVariables
    {
        public Vector4 _WaterCascade0Grid;
        public Vector4 _WaterCascade1Grid;
        public Vector4 _WaterCascade2Grid;
        public Vector4 _WaterCascade0Spectrum;
        public Vector4 _WaterCascade1Spectrum;
        public Vector4 _WaterCascade2Spectrum;
        public Vector4 _WaterCascade0Shape;
        public Vector4 _WaterCascade1Shape;
        public Vector4 _WaterCascade2Shape;
        public uint _WaterCascade0Seed;
        public uint _WaterCascade1Seed;
        public uint _WaterCascade2Seed;
        public float _WaterSpectrumBlend;
        public uint _WaterSpectrumBlendRequiresNormalization;
        public uint _WaterSpectrumPadding0;
        public uint _WaterSpectrumPadding1;
        public uint _WaterSpectrumPadding2;
    }

    [GenerateHLSL(needAccessors = false, generateCBuffer = true)]
    internal unsafe struct WaterSurfaceShaderVariables
    {
        public Matrix4x4 _WaterSurfaceTransform;
        public Vector4 _WaterFoamAppearanceParameters;
        public Vector4 _WaterFoamVolumeAppearanceParameters;
        public Vector4 _WaterFoamColor;
        public Vector4 _WaterDeepFoamColor;
        public Vector4 _WaterBubbleFoamColor;
        public Vector4 _WaterShorelineFoamParameters;
        public Vector4 _WaterShorelineFoamNoiseParameters;
        public Vector4 _WaterShorelineSmoothnessParameters;
        public float4 _WaterUpDirection;
        public float4 _WaterExtinction;
        public Vector4 _WaterScatteringAlbedo;
        public Vector2 _WaterSimulationMaskScale;
        public Vector2 _WaterSimulationMaskOffset;
        public Vector2 _WaterSimulationMaskRemap;
        public float _WaterMaxRefractionDistance;
        public float _WaterAmbientScattering;
        public float _WaterHeightScattering;
        public float _WaterDisplacementScattering;
        public float _WaterNearSmoothness;
        public float _WaterFarSmoothness;
        public float _WaterSmoothnessFadeStart;
        public float _WaterSmoothnessFadeDistance;
        public float _WaterDirectLightTipScattering;
        public float _WaterDirectLightBodyScattering;
        public float _WaterEnvironmentPerceptualRoughness;
        public uint _WaterSimulationMaskEnabled;
        public uint _WaterSurfaceIndex;
        public float _WaterSurfacePadding;
        public int _WaterDebugMode;
        public int _WaterSimulationMaskDebugMode;
        public float _WaterCurrentDebugArrowDensity;
    }

    [GenerateHLSL(needAccessors = false, generateCBuffer = false)]
    internal unsafe struct WaterCameraShaderVariables
    {
        public float2 _PatchOffset;
        public float2 _GridSize;
        public float2 _RegionExtent;
        public float2 _LodCenter;
        public float2 _LodCameraPosition;
        public float _LodAlphaBlackPointFade;
        public float _LodAlphaBlackPointWhitePointFade;
    }
}
