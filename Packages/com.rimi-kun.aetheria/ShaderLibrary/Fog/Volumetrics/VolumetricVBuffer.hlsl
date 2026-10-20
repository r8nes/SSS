#ifndef VOLUMETRIC_VBUFFER_INCLUDED
#define VOLUMETRIC_VBUFFER_INCLUDED

#include "VolumetricFroxel.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Filtering.hlsl"


void ComputeVolumetricVBufferBiquadraticFootprint(
    float2 positionNDC,
    float linearDistance,
    bool biasLookup,
    out float4 sampleWeights,
    out float3 sampleCoord0,
    out float3 sampleCoord1,
    out float3 sampleCoord2,
    out float3 sampleCoord3)
{
    float2 viewportSize = max(_VolumetricVBufferViewportSize.xy, float2(1.0, 1.0));
    float depthSliceCount = max(_VolumetricGridParams.z, 1.0);
    float2 xy = positionNDC * viewportSize;
    float2 integerCoord = floor(xy);
    float2 fractionCoord = frac(xy);

    float2 axisWeights[2];
    float2 axisOffsets[2];
    BiquadraticFilter(1.0 - fractionCoord, axisWeights, axisOffsets);

    float depthCoord = EncodeLogarithmicDepthGeneralized(linearDistance, _VolumetricDepthEncodingParams);
    if (biasLookup)
        depthCoord -= (sqrt(3.0) * 0.5) * rcp(depthSliceCount);

    float2 invViewportSize = _VolumetricVBufferViewportSize.zw;
    float2 viewportLimit = (viewportSize - 0.5) * invViewportSize;
    depthCoord = min(depthCoord, (depthSliceCount - 0.5) * rcp(depthSliceCount));

    float2 uv0 = min((integerCoord + float2(axisOffsets[0].x, axisOffsets[0].y)) * invViewportSize,
        viewportLimit);
    float2 uv1 = min((integerCoord + float2(axisOffsets[1].x, axisOffsets[0].y)) * invViewportSize,
        viewportLimit);
    float2 uv2 = min((integerCoord + float2(axisOffsets[0].x, axisOffsets[1].y)) * invViewportSize,
        viewportLimit);
    float2 uv3 = min((integerCoord + float2(axisOffsets[1].x, axisOffsets[1].y)) * invViewportSize,
        viewportLimit);

    sampleWeights = float4(
        axisWeights[0].x * axisWeights[0].y,
        axisWeights[1].x * axisWeights[0].y,
        axisWeights[0].x * axisWeights[1].y,
        axisWeights[1].x * axisWeights[1].y);
    sampleCoord0 = float3(uv0, depthCoord);
    sampleCoord1 = float3(uv1, depthCoord);
    sampleCoord2 = float3(uv2, depthCoord);
    sampleCoord3 = float3(uv3, depthCoord);
}

float4 SampleVolumetricVBufferBiquadratic(
    TEXTURE3D_PARAM(vBuffer, clampSampler),
    float4 sampleWeights,
    float3 sampleCoord0,
    float3 sampleCoord1,
    float3 sampleCoord2,
    float3 sampleCoord3)
{
    return sampleWeights.x * SAMPLE_TEXTURE3D_LOD(vBuffer, clampSampler, sampleCoord0, 0) +
        sampleWeights.y * SAMPLE_TEXTURE3D_LOD(vBuffer, clampSampler, sampleCoord1, 0) +
        sampleWeights.z * SAMPLE_TEXTURE3D_LOD(vBuffer, clampSampler, sampleCoord2, 0) +
        sampleWeights.w * SAMPLE_TEXTURE3D_LOD(vBuffer, clampSampler, sampleCoord3, 0);
}

float4 SampleVolumetricVBuffer(TEXTURE3D_PARAM(vBuffer, clampSampler),
    float2 positionNDC,
    float linearDistance,
    float3 viewportScale,
    float3 viewportLimit,
    float4 distanceEncodingParams,
    bool biasLookup,
    bool clampToBorder)
{
    float2 uv = positionNDC;
    float w = EncodeLogarithmicDepthGeneralized(linearDistance, distanceEncodingParams);
    if (biasLookup)
        w -= (sqrt(3.0) * 0.5) * rcp(max((float)_VolumetricGridParams.z, 1.0));

    bool inside = true;
    if (clampToBorder)
    {
        float3 positionCS = float3(uv, w) * 2.0 - 1.0;
        inside = Max3(abs(positionCS.x), abs(positionCS.y), abs(positionCS.z)) < 1.0;
    }

    float4 result = 0.0;
    if (inside)
    {
        float3 tc = min(float3(uv, w) * viewportScale, viewportLimit);
        result = SAMPLE_TEXTURE3D_LOD(vBuffer, clampSampler, tc, 0);
    }
    return result;
}

float4 SampleVolumetricVBuffer(TEXTURE3D_PARAM(vBuffer, clampSampler),
    float3 positionWS,
    float3 cameraPositionWS,
    float4x4 viewProjection,
    float3 viewportScale,
    float3 viewportLimit,
    float4 distanceEncodingParams,
    bool biasLookup,
    bool clampToBorder)
{
    float4 result = 0.0;
    float4 positionCS = mul(viewProjection, float4(positionWS, 1.0));
    if (positionCS.w > 1.0e-6)
    {
        float2 positionNDC = positionCS.xy * rcp(positionCS.w) * 0.5 + 0.5;
        float linearDistance = distance(positionWS, cameraPositionWS);
        result = SampleVolumetricVBuffer(TEXTURE3D_ARGS(vBuffer, clampSampler),
            positionNDC,
            linearDistance,
            viewportScale,
            viewportLimit,
            distanceEncodingParams,
            biasLookup,
            clampToBorder);
    }
    return result;
}

float4 SampleVolumetricVBuffer(TEXTURE3D_PARAM(vBuffer, clampSampler),
    float2 positionNDC,
    float linearDistance,
    bool biasLookup,
    bool clampToBorder)
{
    float3 viewportSize = max(_VolumetricGridParams.xyz, float3(1.0, 1.0, 1.0));
    float3 viewportScale = float3(1.0, 1.0, 1.0);
    float3 viewportLimit = (viewportSize - 0.5) / viewportSize;

    return SampleVolumetricVBuffer(TEXTURE3D_ARGS(vBuffer, clampSampler),
        positionNDC,
        linearDistance,
        viewportScale,
        viewportLimit,
        _VolumetricDepthEncodingParams,
        biasLookup,
        clampToBorder);
}

#endif
