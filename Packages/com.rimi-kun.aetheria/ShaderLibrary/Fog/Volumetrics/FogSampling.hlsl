#ifndef FOG_SAMPLING_INCLUDED
#define FOG_SAMPLING_INCLUDED

#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Filtering.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/VolumeRendering.hlsl"

#include "VolumetricContextGlobals.hlsl"
Texture3D _VolumetricIntegratedFogTexture;

void ComputeIntegratedFogBiquadraticFootprint(
    float2 positionNDC,
    float linearDistance,
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
    depthCoord -= (sqrt(3.0) * 0.5) * rcp(depthSliceCount);

    float2 invViewportSize = _VolumetricVBufferViewportSize.zw;
    float2 viewportLimit = (viewportSize - 0.5) * invViewportSize;
    depthCoord = min(depthCoord, (depthSliceCount - 0.5) * rcp(depthSliceCount));

    float2 uv0 = min((integerCoord + float2(axisOffsets[0].x, axisOffsets[0].y)) * invViewportSize, viewportLimit);
    float2 uv1 = min((integerCoord + float2(axisOffsets[1].x, axisOffsets[0].y)) * invViewportSize, viewportLimit);
    float2 uv2 = min((integerCoord + float2(axisOffsets[0].x, axisOffsets[1].y)) * invViewportSize, viewportLimit);
    float2 uv3 = min((integerCoord + float2(axisOffsets[1].x, axisOffsets[1].y)) * invViewportSize, viewportLimit);

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

float4 SampleIntegratedFogBiquadratic(
    float4 sampleWeights,
    float3 sampleCoord0,
    float3 sampleCoord1,
    float3 sampleCoord2,
    float3 sampleCoord3)
{
    return sampleWeights.x * SAMPLE_TEXTURE3D_LOD(_VolumetricIntegratedFogTexture, sampler_LinearClamp, sampleCoord0, 0) +
        sampleWeights.y * SAMPLE_TEXTURE3D_LOD(_VolumetricIntegratedFogTexture, sampler_LinearClamp, sampleCoord1, 0) +
        sampleWeights.z * SAMPLE_TEXTURE3D_LOD(_VolumetricIntegratedFogTexture, sampler_LinearClamp, sampleCoord2, 0) +
        sampleWeights.w * SAMPLE_TEXTURE3D_LOD(_VolumetricIntegratedFogTexture, sampler_LinearClamp, sampleCoord3, 0);
}

float4 SampleIntegratedFogLinear(float2 positionNDC, float linearDistance)
{
    float3 viewportSize = max(_VolumetricGridParams.xyz, float3(1.0, 1.0, 1.0));
    float depthCoord = EncodeLogarithmicDepthGeneralized(linearDistance, _VolumetricDepthEncodingParams);
    depthCoord -= (sqrt(3.0) * 0.5) * rcp(viewportSize.z);
    float3 viewportLimit = (viewportSize - 0.5) / viewportSize;
    float3 sampleCoord = min(float3(positionNDC, depthCoord), viewportLimit);
    return SAMPLE_TEXTURE3D_LOD(_VolumetricIntegratedFogTexture, sampler_LinearClamp, sampleCoord, 0);
}

float4 SampleIntegratedFogByRayDistance(float2 screenUV, float rayDistance)
{
    float4 encodedFog;

    [branch]
    if (_VolumetricUseBiquadraticReconstruction > 0.5)
    {
        float4 sampleWeights;
        float3 sampleCoord0;
        float3 sampleCoord1;
        float3 sampleCoord2;
        float3 sampleCoord3;
        ComputeIntegratedFogBiquadraticFootprint(
            screenUV,
            rayDistance,
            sampleWeights,
            sampleCoord0,
            sampleCoord1,
            sampleCoord2,
            sampleCoord3);
        encodedFog = SampleIntegratedFogBiquadratic(
            sampleWeights,
            sampleCoord0,
            sampleCoord1,
            sampleCoord2,
            sampleCoord3);
    }
    else
    {
        encodedFog = SampleIntegratedFogLinear(screenUV, rayDistance);
    }

    float4 volumetricFog = DelinearizeRGBA(encodedFog);
    return float4(max(volumetricFog.rgb, 0.0), saturate(volumetricFog.a));
}

#endif
