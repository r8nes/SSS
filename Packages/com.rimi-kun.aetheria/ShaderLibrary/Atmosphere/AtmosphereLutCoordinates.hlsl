#ifndef ATMOSPHERE_LUT_COORDINATES_INCLUDED
#define ATMOSPHERE_LUT_COORDINATES_INCLUDED

#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"

float EncodeAerialPerspectiveDepth(float viewDepth, float maxViewDepth, float sliceCount)
{
    float encodedDepth = 0.0f;
    if (maxViewDepth > 1.0e-5f && sliceCount > 0.0f)
    {
        float normalizedDepth = saturate(viewDepth / maxViewDepth);
        encodedDepth = Remap01ToHalfTexelCoord(sqrt(normalizedDepth).xx, sliceCount.xx).x;
    }
    return encodedDepth;
}

float DecodeAerialPerspectiveDepth(float uvz, float maxViewDepth, float sliceCount)
{
    float viewDepth = 0.0f;
    if (maxViewDepth > 1.0e-5f && sliceCount > 0.0f)
    {
        float depthCoord = RemapHalfTexelCoordTo01(uvz.xx, sliceCount.xx).x;
        viewDepth = saturate(depthCoord * depthCoord) * maxViewDepth;
    }
    return viewDepth;
}

#endif
