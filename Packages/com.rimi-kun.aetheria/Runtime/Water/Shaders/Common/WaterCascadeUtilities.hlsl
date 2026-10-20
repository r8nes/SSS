#ifndef WATER_CASCADE_UTILITIES_INCLUDED
#define WATER_CASCADE_UTILITIES_INCLUDED

#include "../../Core/WaterDefinitions.cs.hlsl"

#define WATER_CASCADE_COUNT WATERSHADERLIMITS_CASCADE_COUNT

float4 GetWaterCascadeScaleOffset(int cascadeIndex)
{
    float4 value = 0.0;
    if (cascadeIndex == 0)
        value = _WaterCascade0ScaleOffset;
    else if (cascadeIndex == 1)
        value = _WaterCascade1ScaleOffset;
    else
        value = _WaterCascade2ScaleOffset;
    return value;
}

float4 GetWaterCascadeScalesFade(int cascadeIndex)
{
    float4 value = 0.0;
    if (cascadeIndex == 0)
        value = _WaterCascade0ScalesFade;
    else if (cascadeIndex == 1)
        value = _WaterCascade1ScalesFade;
    else
        value = _WaterCascade2ScalesFade;
    return value;
}

float EvaluateWaterCascadeDistanceFade(float distanceToCamera, int cascadeIndex)
{
#ifndef IGNORE_WATER_FADE
    float2 scaleBias = GetWaterCascadeScalesFade(cascadeIndex).zw;
    float fade = saturate(distanceToCamera * scaleBias.x + scaleBias.y);
    return Smoothstep01(fade * fade);
#else
    return 1.0;
#endif
}

float GetWaterCascadeDisplacementScale(int cascadeIndex)
{
    return GetWaterCascadeScalesFade(cascadeIndex).x;
}

float GetWaterCascadeNormalScale(int cascadeIndex)
{
    return GetWaterCascadeScalesFade(cascadeIndex).y;
}

#endif
