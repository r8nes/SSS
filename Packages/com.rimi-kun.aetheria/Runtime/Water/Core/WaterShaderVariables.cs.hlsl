#ifndef WATERSHADERVARIABLES_CS_HLSL
#define WATERSHADERVARIABLES_CS_HLSL

struct WaterCameraShaderVariables
{
    float2 _PatchOffset;
    float2 _GridSize;
    float2 _RegionExtent;
    float2 _LodCenter;
    float2 _LodCameraPosition;
    float _LodAlphaBlackPointFade;
    float _LodAlphaBlackPointWhitePointFade;
};

CBUFFER_START(WaterSimulationShaderVariables)
    float4 _WaterCascade0ScaleOffset;
    float4 _WaterCascade1ScaleOffset;
    float4 _WaterCascade2ScaleOffset;
    float4 _WaterCascade0ScalesFade;
    float4 _WaterCascade1ScalesFade;
    float4 _WaterCascade2ScalesFade;
    float4 _WaterFoamSimulationParameters;
    float4 _WaterFoamLodParameters;
    float4 _WaterFoamLodCenter;
    float4 _WaterFoamLodScale;
    float4 _WaterFoamTemporalParameters;
    float4 _WaterFoamAdvectionVelocity;
    float4 _WaterCurrentRegionScaleOffset;
    float2 _WaterReferenceOrientation;
    uint _WaterSimulationResolution;
    uint _WaterFoamEnabled;
    uint _WaterActiveCascadeCount;
    float _WaterCurrentMapInfluence;
    float _WaterScatteringWaveHeight;
    float _WaterSimulationTime;
    float _WaterSimulationPadding0;
    float _WaterSimulationPadding1;
    float _WaterSimulationPadding2;
    float _WaterSimulationPadding3;
CBUFFER_END

CBUFFER_START(WaterSpectrumShaderVariables)
    float4 _WaterCascade0Grid;
    float4 _WaterCascade1Grid;
    float4 _WaterCascade2Grid;
    float4 _WaterCascade0Spectrum;
    float4 _WaterCascade1Spectrum;
    float4 _WaterCascade2Spectrum;
    float4 _WaterCascade0Shape;
    float4 _WaterCascade1Shape;
    float4 _WaterCascade2Shape;
    uint _WaterCascade0Seed;
    uint _WaterCascade1Seed;
    uint _WaterCascade2Seed;
    float _WaterSpectrumBlend;
    uint _WaterSpectrumBlendRequiresNormalization;
    uint _WaterSpectrumPadding0;
    uint _WaterSpectrumPadding1;
    uint _WaterSpectrumPadding2;
CBUFFER_END

CBUFFER_START(WaterSurfaceShaderVariables)
    float4x4 _WaterSurfaceTransform;
    float4 _WaterFoamAppearanceParameters;
    float4 _WaterFoamVolumeAppearanceParameters;
    float4 _WaterFoamColor;
    float4 _WaterDeepFoamColor;
    float4 _WaterBubbleFoamColor;
    float4 _WaterShorelineFoamParameters;
    float4 _WaterShorelineFoamNoiseParameters;
    float4 _WaterShorelineSmoothnessParameters;
    float4 _WaterUpDirection;
    float4 _WaterExtinction;
    float4 _WaterScatteringAlbedo;
    float2 _WaterSimulationMaskScale;
    float2 _WaterSimulationMaskOffset;
    float2 _WaterSimulationMaskRemap;
    float _WaterMaxRefractionDistance;
    float _WaterAmbientScattering;
    float _WaterHeightScattering;
    float _WaterDisplacementScattering;
    float _WaterNearSmoothness;
    float _WaterFarSmoothness;
    float _WaterSmoothnessFadeStart;
    float _WaterSmoothnessFadeDistance;
    float _WaterDirectLightTipScattering;
    float _WaterDirectLightBodyScattering;
    float _WaterEnvironmentPerceptualRoughness;
    uint _WaterSimulationMaskEnabled;
    uint _WaterSurfaceIndex;
    float _WaterSurfacePadding;
    int _WaterDebugMode;
    int _WaterSimulationMaskDebugMode;
    float _WaterCurrentDebugArrowDensity;
CBUFFER_END

#endif
