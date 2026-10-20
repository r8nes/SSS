#ifndef UNIVERSAL_REFRACTION_BOUNDARY_INCLUDED
#define UNIVERSAL_REFRACTION_BOUNDARY_INCLUDED

Texture2D<float> _WaterSurfaceDepthTexture;
Texture2D<uint2> _WaterStencilTexture;

uint2 GetRefractionBoundaryPixel(float2 normalizedScreenSpaceUV)
{
    float2 size = max(_ScaledScreenParams.xy, float2(1.0, 1.0));
    return min((uint2)(saturate(normalizedScreenSpaceUV) * size), (uint2)size - 1u);
}

bool HasRefractionBoundary(uint2 positionSS)
{
    return (GetStencilValue(_WaterStencilTexture.Load(int3(positionSS, 0))) & 128u) != 0u;
}

float LoadRefractionBoundaryDepth(uint2 positionSS)
{
    return _WaterSurfaceDepthTexture.Load(int3(positionSS, 0));
}

bool IsFragmentBeforeRefractionBoundary(float fragmentDepth, float boundaryDepth)
{
#if UNITY_REVERSED_Z
    return fragmentDepth >= boundaryDepth;
#else
    return fragmentDepth <= boundaryDepth;
#endif
}

float GetFragmentDeviceDepth(float3 positionWS)
{
    float4 positionCS = TransformWorldToHClip(positionWS);
    return positionCS.z * rcp(max(abs(positionCS.w), 1.0e-6));
}

#endif
