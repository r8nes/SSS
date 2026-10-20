#ifndef RIMIKUN_SCREEN_SPACE_LIGHT_SHAFTS_COMMON_INCLUDED
#define RIMIKUN_SCREEN_SPACE_LIGHT_SHAFTS_COMMON_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

float _LightShaftsSunViewportPadding;
float3 _LightShaftsLightDirectionWS;

bool IsFarDepth(float rawDepth)
{
#if UNITY_REVERSED_Z
    return rawDepth <= 1.0e-6;
#else
    return rawDepth >= 1.0 - 1.0e-6;
#endif
}

float NormalizeRawDepth(float rawDepth, float depthRange)
{
    float safeDepthRange = max(depthRange, 1.0e-4);
    float sceneDepth = IsFarDepth(rawDepth) ? safeDepthRange : LinearEyeDepth(rawDepth, _ZBufferParams);
    return saturate(sceneDepth / safeDepthRange);
}

float2 ProjectMainLightUv(float3 directionWS, out float visibility)
{
    float3 directionVS = mul((float3x3)UNITY_MATRIX_V, directionWS);
    float4 positionCS = mul(UNITY_MATRIX_P, float4(directionVS, 1.0));

    float2 uv = float2(0.5, 0.5);
    visibility = 0.0;
    if (positionCS.w > 1.0e-6)
    {
        float2 ndc = positionCS.xy / positionCS.w;
        uv = ndc * float2(0.5, 0.5 * _ProjectionParams.x) + 0.5;

        float offscreenDistance = max(
            max(-uv.x, uv.x - 1.0),
            max(-uv.y, uv.y - 1.0));

        if (offscreenDistance < _LightShaftsSunViewportPadding)
        {
            float fade = saturate(max(offscreenDistance, 0.0) / _LightShaftsSunViewportPadding);
            visibility = 1.0 - smoothstep(0.0, 1.0, fade);
        }
    }

    return uv;
}

float2 GetMainLightUv(out float visibility)
{
    return ProjectMainLightUv(_LightShaftsLightDirectionWS, visibility);
}

float2 GetMainLightUv()
{
    float visibility;
    return GetMainLightUv(visibility);
}

float2 GetAspectCorrection()
{
    float2 screenSize = max(GetScaledScreenParams().xy, float2(1.0, 1.0));
    return float2(1.0, screenSize.y / screenSize.x);
}

float3 GetViewRayWS(float2 uv)
{
    float3 positionWS = ComputeWorldSpacePosition(uv, 0.5, UNITY_MATRIX_I_VP);
    return SafeNormalize(positionWS - GetCameraPositionWS());
}

#endif
