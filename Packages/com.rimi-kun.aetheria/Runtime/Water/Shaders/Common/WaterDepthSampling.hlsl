#ifndef WATER_DEPTH_SAMPLING_INCLUDED
#define WATER_DEPTH_SAMPLING_INCLUDED

#if defined(WATER_USE_CAMERA_DEPTH)
Texture2D<float> _WaterOpaqueDepthTexture;
#endif

float WaterSampleCameraDepth(float2 positionNDC)
{
#if defined(WATER_USE_CAMERA_DEPTH)
    uint2 size = max((uint2)_ScaledScreenParams.xy, uint2(1u, 1u));
    uint2 positionSS = min((uint2)(saturate(positionNDC) * size), size - 1u);
    return _WaterOpaqueDepthTexture.Load(int3(positionSS, 0));
#else
    return UNITY_RAW_FAR_CLIP_VALUE;
#endif
}

#endif
