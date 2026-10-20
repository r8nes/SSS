#ifndef WATER_COMMON_INCLUDED
#define WATER_COMMON_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/GlobalSamplers.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/CommonMaterial.hlsl"

float _WaterAmbientProbeDimmer;
float _WaterEnvironmentReflectionIntensity;

float4 WaterTransformWorldToHClip(float3 positionWS)
{
    float3 cameraRelativeWS = positionWS - GetCameraPositionWS();
    float3 positionVS = mul((float3x3)UNITY_MATRIX_V, cameraRelativeWS);
    return mul(UNITY_MATRIX_P, float4(positionVS, 1.0));
}

float2 WaterComputeNormalizedDeviceCoordinates(float3 positionWS)
{
    float4 positionCS = WaterTransformWorldToHClip(positionWS);
    float2 positionNDC = positionCS.xy / max(positionCS.w, 1e-6);
    positionNDC.y *= _ProjectionParams.x;
    return positionNDC * 0.5 + 0.5;
}


#endif
