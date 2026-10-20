#ifndef WATER_NORMAL_MAPS_INCLUDED
#define WATER_NORMAL_MAPS_INCLUDED

#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/CommonMaterial.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/GlobalSamplers.hlsl"
#include "../Rendering/WaterSurfaceTransform.hlsl"

TEXTURE2D(_WaterNormalMap);
float4 _WaterNormalMapParameters;

half2 SampleWaterNormalPair(float2 waterPosition, float scale, float scrollSpeed)
{
    float sineDirection;
    float cosineDirection;
    sincos(_WaterReferenceOrientation.x, sineDirection, cosineDirection);
    float2 windDirection = float2(cosineDirection, sineDirection);

    float invScale = rcp(max(scale, 1e-4));
    float2 scroll = windDirection * (_TimeParameters.x * scrollSpeed);

    half2 forwardNormal = UnpackNormal(SAMPLE_TEXTURE2D(
        _WaterNormalMap, sampler_LinearRepeat,
        (waterPosition + scroll) * invScale)).xy;
    half2 backwardNormal = UnpackNormal(SAMPLE_TEXTURE2D(
        _WaterNormalMap, sampler_LinearRepeat,
        (waterPosition - scroll) * invScale)).xy;
    return forwardNormal + backwardNormal;
}

float3 EvaluateWaterDetailNormal(float2 waterPosition, float3 spectralNormalWS, float4 normalLod)
{
    float strength = max(_WaterNormalMapParameters.z, 0.0);
    if (_WaterNormalMapParameters.w < 0.5 || strength <= 1e-5)
        return SafeNormalize(spectralNormalWS);

    float cascadeScale = max(normalLod.x, 1e-4);
    float scale = max(_WaterNormalMapParameters.x, 0.01) * cascadeScale / 10.0;
    float normalMapSpeed = max(_WaterNormalMapParameters.y, 0.0);

    half2 detail = SampleWaterNormalPair(
        waterPosition,
        scale,
        normalLod.z * normalMapSpeed);

    float nextLodBlend = saturate(normalLod.y);
    if (nextLodBlend > 0.001)
    {
        half2 nextDetail = SampleWaterNormalPair(
            waterPosition,
            scale * 2.0,
            normalLod.w * normalMapSpeed);
        detail = lerp(detail, nextDetail, nextLodBlend);
    }

    detail *= strength;

    float3 normalOS = SafeNormalize(mul(
        transpose((float3x3)_WaterSurfaceTransform),
        SafeNormalize(spectralNormalWS)));
    normalOS.xz += detail;
    return SafeNormalize(TransformWaterDirectionToWorld(normalOS));
}

float3 EvaluateWaterDetailNormal(float2 waterPosition, float3 spectralNormalWS)
{
    return EvaluateWaterDetailNormal(waterPosition, spectralNormalWS, float4(10.0, 0.0, 1.0, 1.0));
}

#endif
