#ifndef UNIVERSAL_EXTERNAL_FOG_INCLUDED
#define UNIVERSAL_EXTERNAL_FOG_INCLUDED

#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Filtering.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/VolumeRendering.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RefractionBoundary.hlsl"

TEXTURE3D(_UniversalExternalFogTexture);
TEXTURECUBE(_UniversalExternalFogFilteredSky);

float _UniversalExternalFogEnabled;
float _UniversalExternalFogVolumeEnabled;
float _UniversalExternalFogLastSliceDistance;
float4 _UniversalExternalFogGridParams;
float4 _UniversalExternalFogDepthEncodingParams;
float4 _UniversalExternalFogVBufferViewportSize;
float _UniversalExternalFogUseBiquadraticReconstruction;
float4 _UniversalExternalFogColor;
float4 _UniversalExternalFogMipParameters;
float _UniversalExternalFogConvolutionMipCount;
float _UniversalExternalFogSkyColorEnabled;
float _UniversalExternalFogBaseExtinction;
float3 _UniversalExternalFogBaseScattering;
float _UniversalExternalFogBaseHeight;
float2 _UniversalExternalFogHeightExponents;
float4 _UniversalExternalFogPlanetUpAltitude;
float _UniversalExternalMediumEnabled;
float _UniversalExternalMediumCameraInside;
float3 _UniversalExternalMediumExtinction;
float3 _UniversalExternalMediumScatteringAlbedo;

struct UniversalExternalFogSample
{
    float3 radiance;
    float opacity;
};

UniversalExternalFogSample CreateUniversalExternalFogSample(float3 radiance, float opacity)
{
    UniversalExternalFogSample fog;
    fog.radiance = max(radiance, 0.0);
    fog.opacity = saturate(opacity);
    return fog;
}

void ComputeUniversalExternalFogBiquadraticFootprint(
    float2 positionNDC,
    float linearDistance,
    out float4 sampleWeights,
    out float3 sampleCoord0,
    out float3 sampleCoord1,
    out float3 sampleCoord2,
    out float3 sampleCoord3)
{
    float2 viewportSize = max(_UniversalExternalFogVBufferViewportSize.xy, float2(1.0, 1.0));
    float depthSliceCount = max(_UniversalExternalFogGridParams.z, 1.0);
    float2 xy = positionNDC * viewportSize;
    float2 integerCoord = floor(xy);
    float2 fractionCoord = frac(xy);

    float2 axisWeights[2];
    float2 axisOffsets[2];
    BiquadraticFilter(1.0 - fractionCoord, axisWeights, axisOffsets);

    float depthCoord = EncodeLogarithmicDepthGeneralized(linearDistance, _UniversalExternalFogDepthEncodingParams);
    depthCoord -= (sqrt(3.0) * 0.5) * rcp(depthSliceCount);

    float2 invViewportSize = _UniversalExternalFogVBufferViewportSize.zw;
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

float4 SampleUniversalExternalFogVolume(float2 positionNDC, float linearDistance)
{
    float4 encodedFog;

    [branch]
    if (_UniversalExternalFogUseBiquadraticReconstruction > 0.5)
    {
        float4 sampleWeights;
        float3 sampleCoord0;
        float3 sampleCoord1;
        float3 sampleCoord2;
        float3 sampleCoord3;
        ComputeUniversalExternalFogBiquadraticFootprint(
            positionNDC,
            linearDistance,
            sampleWeights,
            sampleCoord0,
            sampleCoord1,
            sampleCoord2,
            sampleCoord3);
        encodedFog =
            sampleWeights.x * SAMPLE_TEXTURE3D_LOD(_UniversalExternalFogTexture, sampler_LinearClamp, sampleCoord0, 0) +
            sampleWeights.y * SAMPLE_TEXTURE3D_LOD(_UniversalExternalFogTexture, sampler_LinearClamp, sampleCoord1, 0) +
            sampleWeights.z * SAMPLE_TEXTURE3D_LOD(_UniversalExternalFogTexture, sampler_LinearClamp, sampleCoord2, 0) +
            sampleWeights.w * SAMPLE_TEXTURE3D_LOD(_UniversalExternalFogTexture, sampler_LinearClamp, sampleCoord3, 0);
    }
    else
    {
        float3 viewportSize = max(_UniversalExternalFogGridParams.xyz, float3(1.0, 1.0, 1.0));
        float depthCoord = EncodeLogarithmicDepthGeneralized(linearDistance, _UniversalExternalFogDepthEncodingParams);
        depthCoord -= (sqrt(3.0) * 0.5) * rcp(viewportSize.z);
        float3 viewportLimit = (viewportSize - 0.5) / viewportSize;
        float3 sampleCoord = min(float3(positionNDC, depthCoord), viewportLimit);
        encodedFog = SAMPLE_TEXTURE3D_LOD(_UniversalExternalFogTexture, sampler_LinearClamp, sampleCoord, 0);
    }

    float4 fog = DelinearizeRGBA(encodedFog);
    return float4(max(fog.rgb, 0.0), saturate(fog.a));
}

float3 GetUniversalExternalFogColor(float3 viewDirectionWS, float fragmentDistance)
{
    float3 color = _UniversalExternalFogColor.rgb;

    if (_UniversalExternalFogSkyColorEnabled > 0.5)
    {
        float mipRange = max(_UniversalExternalFogMipParameters.y - _UniversalExternalFogMipParameters.x, 1.0e-5);
        float mipBlend = saturate((fragmentDistance - _UniversalExternalFogMipParameters.x) * rcp(mipRange));
        float mipLevel = (1.0 - _UniversalExternalFogMipParameters.z * mipBlend) * max(_UniversalExternalFogConvolutionMipCount - 1.0, 0.0);
        color *= SAMPLE_TEXTURECUBE_LOD(_UniversalExternalFogFilteredSky, sampler_TrilinearClamp, -viewDirectionWS, mipLevel).rgb;
    }

    return color;
}

UniversalExternalFogSample EvaluateUniversalExternalFogTail(
    float3 viewDirectionWS,
    float segmentStart,
    float segmentEnd,
    float fogColorDistance)
{
    UniversalExternalFogSample fog;
    fog.radiance = float3(0.0, 0.0, 0.0);
    fog.opacity = 0.0;
    float segmentLength = max(segmentEnd - segmentStart, 0.0);
    if (segmentLength <= 0.0 || _UniversalExternalFogBaseExtinction <= 0.0)
        return fog;

    float cosZenith = -dot(viewDirectionWS, _UniversalExternalFogPlanetUpAltitude.xyz);
    float startHeight = _UniversalExternalFogPlanetUpAltitude.w + segmentStart * cosZenith;
    float opticalDepth = OpticalDepthHeightFog(
        _UniversalExternalFogBaseExtinction,
        _UniversalExternalFogBaseHeight,
        _UniversalExternalFogHeightExponents,
        cosZenith,
        startHeight,
        segmentLength);
    float transmittance = TransmittanceFromOpticalDepth(opticalDepth);
    float opacity = 1.0 - transmittance;
    float3 scatteringAlbedo = _UniversalExternalFogBaseScattering * rcp(max(_UniversalExternalFogBaseExtinction, 1.0e-6));
    float3 radiance = GetUniversalExternalFogColor(viewDirectionWS, fogColorDistance) * scatteringAlbedo * opacity;
    return CreateUniversalExternalFogSample(radiance, opacity);
}

UniversalExternalFogSample SampleUniversalExternalFog(float2 normalizedScreenSpaceUV, float3 positionWS)
{
    float3 cameraToFragment = positionWS - GetCameraPositionWS();
    float rayDistance = length(cameraToFragment);
    float3 viewDirectionWS = -SafeNormalize(cameraToFragment);

    UniversalExternalFogSample fog = CreateUniversalExternalFogSample(0.0, 0.0);
    float volumetricEnd = 0.0;

    if (_UniversalExternalFogVolumeEnabled > 0.5 && rayDistance > 0.0)
    {
        float4 volumetricFog = SampleUniversalExternalFogVolume(normalizedScreenSpaceUV, rayDistance);
        fog = CreateUniversalExternalFogSample(volumetricFog.rgb, volumetricFog.a);
        volumetricEnd = _UniversalExternalFogLastSliceDistance;
    }

    if (rayDistance > volumetricEnd)
    {
        UniversalExternalFogSample tail = EvaluateUniversalExternalFogTail(
            viewDirectionWS,
            volumetricEnd,
            rayDistance,
            rayDistance);
        float cameraTransmittance = 1.0 - fog.opacity;
        fog.radiance += cameraTransmittance * tail.radiance;
        fog.opacity = 1.0 - cameraTransmittance * (1.0 - tail.opacity);
    }

    return CreateUniversalExternalFogSample(fog.radiance, fog.opacity);
}

#define UNIVERSAL_EXTERNAL_FOG_BLEND_ALPHA 0
#define UNIVERSAL_EXTERNAL_FOG_BLEND_PREMULTIPLIED 1
#define UNIVERSAL_EXTERNAL_FOG_BLEND_ADDITIVE 2
#define UNIVERSAL_EXTERNAL_FOG_BLEND_MULTIPLY 3

float GetUniversalExternalMediumDistance(float2 normalizedScreenSpaceUV, float3 positionWS)
{
    if (_UniversalExternalMediumEnabled <= 0.5 || _UniversalExternalMediumCameraInside <= 0.5)
        return 0.0;

    uint2 positionSS = GetRefractionBoundaryPixel(normalizedScreenSpaceUV);
    if (HasRefractionBoundary(positionSS))
    {
        float fragmentDepth = GetFragmentDeviceDepth(positionWS);
        float boundaryDepth = LoadRefractionBoundaryDepth(positionSS);
        if (!IsFragmentBeforeRefractionBoundary(fragmentDepth, boundaryDepth))
            return 0.0;
    }

    return length(positionWS - GetCameraPositionWS());
}

half3 MixUniversalExternalMedium(
    half3 fragColor,
    half alpha,
    float2 normalizedScreenSpaceUV,
    float3 positionWS,
    int blendMode)
{
    float mediumDistance = GetUniversalExternalMediumDistance(normalizedScreenSpaceUV, positionWS);
    if (mediumDistance <= 0.0)
        return fragColor;

    float3 transmittance = exp(-max(_UniversalExternalMediumExtinction, 0.0) * mediumDistance);
    float3 radiance = saturate(_UniversalExternalMediumScatteringAlbedo) * (1.0 - transmittance);

    if (blendMode == UNIVERSAL_EXTERNAL_FOG_BLEND_MULTIPLY)
        return (half3)(1.0 + ((float3)fragColor - 1.0) * transmittance);

    if (blendMode == UNIVERSAL_EXTERNAL_FOG_BLEND_ADDITIVE)
        return (half3)((float3)fragColor * transmittance);

    if (blendMode == UNIVERSAL_EXTERNAL_FOG_BLEND_PREMULTIPLIED)
        return (half3)((float3)fragColor * transmittance + radiance * alpha);

    return (half3)((float3)fragColor * transmittance + radiance);
}

half3 MixTransparentFragmentFog(
    half3 fragColor,
    half alpha,
    float2 normalizedScreenSpaceUV,
    float3 positionWS,
    half legacyFogFactor,
    int blendMode)
{
    float mediumDistance = GetUniversalExternalMediumDistance(normalizedScreenSpaceUV, positionWS);
    if (mediumDistance > 0.0)
        return MixUniversalExternalMedium(fragColor, alpha, normalizedScreenSpaceUV, positionWS, blendMode);

    [branch]
    if (_UniversalExternalFogEnabled > 0.5)
    {
        UniversalExternalFogSample fog = SampleUniversalExternalFog(normalizedScreenSpaceUV, positionWS);
        half transmittance = (half)(1.0 - fog.opacity);

        if (blendMode == UNIVERSAL_EXTERNAL_FOG_BLEND_MULTIPLY)
            return lerp(half3(1.0, 1.0, 1.0), fragColor, transmittance);

        if (blendMode == UNIVERSAL_EXTERNAL_FOG_BLEND_ADDITIVE)
            return fragColor * transmittance;

        if (blendMode == UNIVERSAL_EXTERNAL_FOG_BLEND_PREMULTIPLIED)
            return fragColor * transmittance + (half3)fog.radiance * alpha;

        return fragColor * transmittance + (half3)fog.radiance;
    }

    return MixFog(fragColor, legacyFogFactor);
}

float3 MixTransparentFragmentFog(
    float3 fragColor,
    float alpha,
    float2 normalizedScreenSpaceUV,
    float3 positionWS,
    float legacyFogFactor,
    int blendMode)
{
    return (float3)MixTransparentFragmentFog(
        (half3)fragColor,
        (half)alpha,
        normalizedScreenSpaceUV,
        positionWS,
        (half)legacyFogFactor,
        blendMode);
}

half3 MixFragmentFog(
    half3 fragColor,
    half alpha,
    float2 normalizedScreenSpaceUV,
    float3 positionWS,
    half legacyFogFactor)
{
#if defined(_SURFACE_TYPE_TRANSPARENT)
    #if defined(_ALPHAMODULATE_ON) || defined(_BLENDMODE_MULTIPLY)
        return MixTransparentFragmentFog(fragColor, alpha, normalizedScreenSpaceUV, positionWS, legacyFogFactor, UNIVERSAL_EXTERNAL_FOG_BLEND_MULTIPLY);
    #elif defined(_BLENDMODE_ADD)
        return MixTransparentFragmentFog(fragColor, alpha, normalizedScreenSpaceUV, positionWS, legacyFogFactor, UNIVERSAL_EXTERNAL_FOG_BLEND_ADDITIVE);
    #elif defined(_ALPHAPREMULTIPLY_ON)
        return MixTransparentFragmentFog(fragColor, alpha, normalizedScreenSpaceUV, positionWS, legacyFogFactor, UNIVERSAL_EXTERNAL_FOG_BLEND_PREMULTIPLIED);
    #else
        return MixTransparentFragmentFog(fragColor, alpha, normalizedScreenSpaceUV, positionWS, legacyFogFactor, UNIVERSAL_EXTERNAL_FOG_BLEND_ALPHA);
    #endif
#else
    return MixFog(fragColor, legacyFogFactor);
#endif
}

float3 MixFragmentFog(
    float3 fragColor,
    float alpha,
    float2 normalizedScreenSpaceUV,
    float3 positionWS,
    float legacyFogFactor)
{
    return (float3)MixFragmentFog((half3)fragColor, (half)alpha, normalizedScreenSpaceUV, positionWS, (half)legacyFogFactor);
}

#endif
