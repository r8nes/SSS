#ifndef COMMON_VOLUMETRIC_DEPTH_INCLUDED
#define COMMON_VOLUMETRIC_DEPTH_INCLUDED

#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"

float EncodeInfiniteVolumetricDepth(float linearDepth, float nearPlane)
{
    float encodedDepth = EncodeInfiniteDepth(max(linearDepth, nearPlane), nearPlane);
#if UNITY_REVERSED_Z
    return encodedDepth;
#else
    return 1.0f - encodedDepth;
#endif
}

float DecodeInfiniteVolumetricDepth(float deviceDepth, float nearPlane)
{
#if !UNITY_REVERSED_Z
    deviceDepth = 1.0f - deviceDepth;
#endif
    return DecodeInfiniteDepth(deviceDepth, nearPlane);
}

float LinearizeClampedVolumetricDepth(float rawDepth, float maxDistance)
{
#if UNITY_REVERSED_Z
    bool isSky = rawDepth <= 0.000001;
#else
    bool isSky = rawDepth >= 0.999999;
#endif
    return isSky
        ? maxDistance
        : min(LinearEyeDepth(rawDepth, _ZBufferParams), maxDistance);
}

#endif
