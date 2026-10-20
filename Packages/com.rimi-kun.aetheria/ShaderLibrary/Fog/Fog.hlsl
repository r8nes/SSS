#ifndef FOG_COMMON_INCLUDED
#define FOG_COMMON_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "../Atmosphere/AtmosphereCore.hlsl"
#include "FogEvaluation.hlsl"

#define FOG_COLOR_MODE_CONSTANT 0
#define FOG_COLOR_MODE_SKY 1

FogComposite EvaluateFogRuntime(
    float rawDepth,
    float2 positionNdc,
    bool skyColorEnabled,
    bool volumetricFogEnabled)
{
    bool isSky = rawDepth == UNITY_RAW_FAR_CLIP_VALUE;
    float3 rayDirectionWS = GetCameraFrustumRayWS(positionNdc);
    float3 V = -rayDirectionWS;
    float projection = max(dot(-V, GetViewForwardDir()), 1.0e-5);
    float rayDistance = isSky
        ? _FogMaxDistance
        : LinearEyeDepth(rawDepth, _ZBufferParams) * rcp(projection);
    return EvaluateFogByRayDistance(
        positionNdc,
        V,
        rayDistance,
        skyColorEnabled,
        volumetricFogEnabled);
}

FogComposite EvaluateFog(float rawDepth, float2 positionNdc)
{
#if defined(FOG_SKY_COLOR)
    const bool skyColorEnabled = true;
#else
    const bool skyColorEnabled = false;
#endif
#if defined(VOLUMETRIC_FOG)
    const bool volumetricFogEnabled = true;
#else
    const bool volumetricFogEnabled = false;
#endif
    return EvaluateFogRuntime(rawDepth, positionNdc, skyColorEnabled, volumetricFogEnabled);
}

#endif
