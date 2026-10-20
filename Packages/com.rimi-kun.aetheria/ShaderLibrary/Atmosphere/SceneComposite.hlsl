#ifndef SCENE_COMPOSITE_INCLUDED
#define SCENE_COMPOSITE_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "AtmosphereLutCoordinates.hlsl"

#if !defined(ATMOSPHERE_CORE_INCLUDED)
#include "../../Runtime/Data/ShaderConfigs/AtmosphereShaderConfig.cs.hlsl"
#include "AtmosphereShaderBindings.hlsl"

#ifndef _DistanceUnitMeter
#define _DistanceUnitMeter ATMOSPHERESHADERCONFIG_DISTANCE_UNIT_METERS
#endif
#define _AtmosphericScatteringLutResolution \
    float3(ATMOSPHERESHADERCONFIG_ATMOSPHERIC_SCATTERING_LUT_WIDTH, \
           ATMOSPHERESHADERCONFIG_ATMOSPHERIC_SCATTERING_LUT_HEIGHT, \
           ATMOSPHERESHADERCONFIG_ATMOSPHERIC_SCATTERING_LUT_DEPTH)

float _AtmosphericScatteringMaxDistance;
TEXTURE3D(ATMOSPHERE_AERIAL_PERSPECTIVE_LUT);
#endif

bool IsFarRawDepth(float rawDepth)
{
#if UNITY_REVERSED_Z
    return rawDepth <= 1.0e-6f;
#else
    return rawDepth >= 1.0f - 1.0e-6f;
#endif
}

struct SceneCompositeCoefficients
{
    float3 additiveRadiance;
    float transmittance;
};

SceneCompositeCoefficients IdentitySceneCompositeCoefficients()
{
    SceneCompositeCoefficients coefficients;
    coefficients.additiveRadiance = 0.0f;
    coefficients.transmittance = 1.0f;
    return coefficients;
}

SceneCompositeCoefficients SampleAtmosphereCompositeCoefficients(float2 screenUv, float viewDepthKm)
{
    float clampedViewDepthKm = min(viewDepthKm, _AtmosphericScatteringMaxDistance);
    float3 uvw = float3(
        screenUv,
        EncodeAerialPerspectiveDepth(
            clampedViewDepthKm,
            _AtmosphericScatteringMaxDistance,
            _AtmosphericScatteringLutResolution.z));
    float4 aerialPerspective = SAMPLE_TEXTURE3D_LOD(
        ATMOSPHERE_AERIAL_PERSPECTIVE_LUT,
        ATMOSPHERE_AERIAL_PERSPECTIVE_LUT_SAMPLER,
        uvw,
        0);

    SceneCompositeCoefficients coefficients;
    coefficients.additiveRadiance = max(aerialPerspective.rgb, 0.0f);
    coefficients.transmittance = saturate(aerialPerspective.a);
    return coefficients;
}

SceneCompositeCoefficients EvaluateOpaqueSceneCompositeCoefficients(float2 screenUv, float rawDepth)
{
    float viewDepthKm = LinearEyeDepth(rawDepth, _ZBufferParams) * rcp(_DistanceUnitMeter);
    return SampleAtmosphereCompositeCoefficients(screenUv, viewDepthKm);
}

#endif