#ifndef AETHERIA_FOG_EVALUATION_INCLUDED
#define AETHERIA_FOG_EVALUATION_INCLUDED

#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/VolumeRendering.hlsl"
#include "FogGlobals.hlsl"
#include "Volumetrics/FogSampling.hlsl"

struct FogComposite
{
    float3 radiance;
    float opacity;
};

FogComposite CreateFogComposite(float3 radiance, float opacity)
{
    FogComposite fog = (FogComposite)0;
    fog.radiance = max(radiance, 0.0);
    fog.opacity = saturate(opacity);
    return fog;
}

float3 GetFogColor(float3 V, float fragDist, bool skyColorEnabled)
{
    float3 color = _FogColor.rgb;

    if (skyColorEnabled)
    {
        float mipRange = max(_FogMipParameters.y - _FogMipParameters.x, 1.0e-5);
        float mipBlend = saturate((fragDist - _FogMipParameters.x) * rcp(mipRange));
        float mipLevel = (1.0 - _FogMipParameters.z * mipBlend) * max(_FogConvolutionMipCount - 1.0, 0.0);
        color *= SAMPLE_TEXTURECUBE_LOD(_FogFilteredSky, sampler_TrilinearClamp, -V, mipLevel).rgb;
    }

    return color;
}

FogComposite EvaluateFogTail(
    float3 V,
    float segmentStart,
    float segmentEnd,
    float fogColorDistance,
    bool skyColorEnabled)
{
    FogComposite fog = CreateFogComposite(float3(0.0, 0.0, 0.0), 0.0);
    float segmentLength = max(segmentEnd - segmentStart, 0.0);
    if (segmentLength > 0.0 && _HeightFogBaseExtinction > 0.0)
    {
        float cosZenith = -dot(V, _PlanetUpAltitude.xyz);
        float startHeight = _PlanetUpAltitude.w + segmentStart * cosZenith;
        float opticalDepth = OpticalDepthHeightFog(
            _HeightFogBaseExtinction,
            _HeightFogBaseHeight,
            _HeightFogExponents,
            cosZenith,
            startHeight,
            segmentLength);
        float transmittance = TransmittanceFromOpticalDepth(opticalDepth);
        float opacity = 1.0 - transmittance;
        float3 scatteringAlbedo = _HeightFogBaseScattering * rcp(max(_HeightFogBaseExtinction, 1.0e-6));
        float3 radiance = GetFogColor(V, fogColorDistance, skyColorEnabled) * scatteringAlbedo * opacity;
        fog = CreateFogComposite(radiance, opacity);
    }

    return fog;
}

FogComposite EvaluateFogByRayDistance(
    float2 positionNdc,
    float3 V,
    float rayDistance,
    bool skyColorEnabled,
    bool volumetricFogEnabled)
{
    float tFrag = max(rayDistance, 0.0);
    FogComposite fog = CreateFogComposite(0.0, 0.0);
    float volumetricFogEnd = 0.0;

    if (volumetricFogEnabled && tFrag > 0.0)
    {
        float4 volumetric = SampleIntegratedFogByRayDistance(positionNdc, tFrag);
        fog = CreateFogComposite(volumetric.rgb, volumetric.a);
        volumetricFogEnd = _VolumetricLastSliceDistance;
    }

    if (tFrag > volumetricFogEnd)
    {
        FogComposite tail = EvaluateFogTail(
            V,
            volumetricFogEnd,
            tFrag,
            tFrag,
            skyColorEnabled);
        float cameraTransmittance = 1.0 - fog.opacity;
        fog.radiance += cameraTransmittance * tail.radiance;
        fog.opacity = 1.0 - cameraTransmittance * (1.0 - tail.opacity);
    }

    fog.radiance = max(fog.radiance, 0.0);
    fog.opacity = saturate(fog.opacity);
    return fog;
}

#endif
