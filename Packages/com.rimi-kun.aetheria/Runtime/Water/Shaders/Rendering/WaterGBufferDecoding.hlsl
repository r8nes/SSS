#ifndef WATER_GBUFFER_DECODING_INCLUDED
#define WATER_GBUFFER_DECODING_INCLUDED

#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Packing.hlsl"

uint DecodeWaterPackedMetadata(float encodedMetadata)
{
    return (uint)floor(saturate(encodedMetadata) * 255.0 + 0.5);
}

int DecodeWaterSurfaceIndex(uint packedMetadata)
{
    return (int)(packedMetadata >> 1) - 1;
}

float3 DecodeWaterNormal(float3 encodedNormal)
{
    float2 octNormal = Unpack888ToFloat2(encodedNormal);
    return SafeNormalize(UnpackNormalOctQuadEncode(octNormal * 2.0 - 1.0));
}

float3 DecodeWaterNormalOct2(float2 encodedNormal)
{
    return SafeNormalize(UnpackNormalOctQuadEncode(encodedNormal * 2.0 - 1.0));
}

#endif
