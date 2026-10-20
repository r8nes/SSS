#ifndef ATMOSPHERE_CORE_INCLUDED
#define ATMOSPHERE_CORE_INCLUDED

#include "../../Runtime/Data/ShaderConfigs/AtmosphereShaderConfig.cs.hlsl"
#include "AtmosphereShaderBindings.hlsl"
#include "AtmosphereLutCoordinates.hlsl"
#include "../Common/TransmittanceLut.hlsl"
#include "../Common/PhaseFunctions.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/GeometricTools.hlsl"
#include "../Celestial/CelestialBodyLighting.hlsl"

float _RayleighDensityExpScale;
float3 _RayleighScattering;

float _MieDensityExpScale;
float3 _MieScattering;
float3 _MieAbsorption;

float _MiePhaseG;

float3 _OzoneAbsorptionExtinction;

float3 _GroundAlbedo;

float3 _CameraPosForAtmosphere;

#define _MultiScatteringLutRes ATMOSPHERESHADERCONFIG_MULTI_SCATTERING_LUT_WIDTH
#define _GroundIrradianceLutResolution \
    float2(ATMOSPHERESHADERCONFIG_GROUND_IRRADIANCE_LUT_WIDTH, \
           ATMOSPHERESHADERCONFIG_GROUND_IRRADIANCE_LUT_HEIGHT)
float _AtmosphericScatteringMaxDistance;

#define _SkyViewLutResolution \
    float2(ATMOSPHERESHADERCONFIG_SKY_VIEW_LUT_WIDTH, \
           ATMOSPHERESHADERCONFIG_SKY_VIEW_LUT_HEIGHT)
#define _AtmosphericScatteringLutResolution \
    float3(ATMOSPHERESHADERCONFIG_ATMOSPHERIC_SCATTERING_LUT_WIDTH, \
           ATMOSPHERESHADERCONFIG_ATMOSPHERIC_SCATTERING_LUT_HEIGHT, \
           ATMOSPHERESHADERCONFIG_ATMOSPHERIC_SCATTERING_LUT_DEPTH)

int _PerPixelRaymarchSamples;

#define _PLANET_RADIUS_OFFSET 10.0 / _DistanceUnitMeter

float3 _PlanetCenterWS;

int _SpaceRaymarchSamples;

static const float ATMOSPHERE_MULTI_SCATTERING_PRE_EXPOSURE = 100.0f;
static const float ATMOSPHERE_MULTI_SCATTERING_INV_PRE_EXPOSURE = 1.0f / ATMOSPHERE_MULTI_SCATTERING_PRE_EXPOSURE;

TEXTURE2D(ATMOSPHERE_GROUND_IRRADIANCE_LUT);

TEXTURE2D(ATMOSPHERE_SKY_VIEW_LUT);
SAMPLER(ATMOSPHERE_SKY_VIEW_LUT_SAMPLER);

TEXTURE3D(ATMOSPHERE_AERIAL_PERSPECTIVE_LUT);

TEXTURECUBE(ATMOSPHERE_SPACE_CUBEMAP);
SAMPLER(ATMOSPHERE_SPACE_CUBEMAP_SAMPLER);

float4 _SpaceParams0;
float4 _SpaceParams1;
float3 _SpaceTint;
float4 _SkyArtisticParams;
float4 _SkyArtisticPhaseParams;
float3 _SkyHueColor;
float3 _HorizonHueColor;
float4 _SkySunsetParams;
float4 _SkyPatternParams;
float4 _SkyPatternParams2;
float _SkyArtisticAffectEnvironment;

struct MediumSampleRGB
{
    float3 scatteringRay;
    float3 scatteringMie;
    float3 scattering;
    float3 extinction;
};

float SampleOzoneDensity(float altitude);

void ComputeDensities(float viewHeight,
                      out float densityRay,
                      out float densityMie,
                      out float densityOzo)
{
    densityRay = exp(_RayleighDensityExpScale * viewHeight);
    densityMie = exp(_MieDensityExpScale * viewHeight);
    densityOzo = SampleOzoneDensity(viewHeight);
}


float ComputeSunPenumbra(float viewHeight, float3 viewDir, float3 sunDir, float sunAngularRadius)
{
    float cosHorizon = clamp(ComputeSphericalHorizonCosine(viewHeight, _BottomRadius), -1.0f, 1.0f);
    float cosTheta = clamp(dot(normalize(viewDir), normalize(sunDir)), -1.0f, 1.0f);
    float theta = acos(cosTheta);
    float horizonAngle = acos(cosHorizon);
    float distToHorizon = horizonAngle - theta;
    float penumbra = saturate(distToHorizon / max(abs(sunAngularRadius), 1e-6f) * 0.5f + 0.5f);
    return penumbra;
}

bool MoveToTopAtmosphere(inout float3 viewPos, in float3 viewDir, in float atmosphereTopRadius)
{
    float3 resolvedViewPos = viewPos;
    bool intersectsAtmosphere = true;
    float viewHeight = length(resolvedViewPos);

    if (viewHeight > atmosphereTopRadius)
    {
        float2 topIntersections = 0.0f;
        bool hitTop = IntersectRaySphere(resolvedViewPos, viewDir, atmosphereTopRadius, topIntersections);
        float tTop = hitTop && topIntersections.y >= 0.0f
            ? (topIntersections.x >= 0.0f ? topIntersections.x : topIntersections.y)
            : -1.0f;

        if (tTop >= 0.0f)
        {
            tTop = max(tTop, 0.0f);

            if (tTop > 0.0f)
                resolvedViewPos += viewDir * tTop;

            float viewPosLength = length(resolvedViewPos);

            if (viewPosLength > 1e-6f)
                resolvedViewPos *= atmosphereTopRadius / viewPosLength;
        }
        else
        {
            intersectsAtmosphere = false;
        }
    }

    viewPos = resolvedViewPos;

    return intersectsAtmosphere;
}

void TransformToEarthSpace(float3 cameraWorldPos, float earthRadius,
                           out float3 viewPos, out float viewHeight, out float3 upVector)
{
    float3 relativePosKm = (cameraWorldPos - _PlanetCenterWS) / _DistanceUnitMeter;
    float distKm = length(relativePosKm);

    float r = max(distKm, earthRadius + _PLANET_RADIUS_OFFSET);

    upVector = distKm > 1e-4 ? relativePosKm / distKm : float3(0.0, 1.0, 0.0);

    viewPos = upVector * r;
    viewHeight = r;
}

MediumSampleRGB BuildMediumSampleRGB(float viewHeight)
{
    float densityRay = 0.0f;
    float densityMie = 0.0f;
    float densityOzo = 0.0f;
    ComputeDensities(viewHeight, densityRay, densityMie, densityOzo);

    MediumSampleRGB s;
    s.scatteringMie = densityMie * _MieScattering;
    s.scatteringRay = densityRay * _RayleighScattering;
    s.scattering = s.scatteringMie + s.scatteringRay;

    s.extinction = s.scatteringMie + densityMie * _MieAbsorption
                    + s.scatteringRay
                    + densityOzo * _OzoneAbsorptionExtinction;
    return s;
}

MediumSampleRGB SampleMediumRGBAtHeight(float worldRadius)
{
    return BuildMediumSampleRGB(worldRadius - _BottomRadius);
}

MediumSampleRGB SampleMediumRGB(in float3 WorldPos)
{
    return BuildMediumSampleRGB(length(WorldPos) - _BottomRadius);
}

float SampleOzoneDensity(float altitude)
{
    const float ozoneCenterKm = 25.0f;
    const float ozoneHalfWidthKm = 15.0f;
    return max(0.0f, 1.0f - abs(altitude - ozoneCenterKm) / ozoneHalfWidthKm);
}

float3 GetCameraFrustumRayWS(float2 uv)
{
#if (defined(ATMOSPHERE_PRECOMPUTE_ONLY) && !defined(BUILDING_AERIAL_PERSPECTIVE_LUT)) || defined(BUILDING_SKY_VIEW_LUT) || defined(BUILDING_MULTI_SCATTER_LUT)
    return float3(0.0f, 1.0f, 0.0f);
#else
    float3 positionWS = ComputeWorldSpacePosition(
        uv,
        UNITY_RAW_FAR_CLIP_VALUE,
        UNITY_MATRIX_I_VP);
    return -GetWorldSpaceNormalizeViewDir(positionWS);
#endif
}

void GetCelestialLightReferenceFrame(float3 upVector, out float3 forwardVector, out float3 sideVector)
{
    float3 celestialLightDir = ATMOSPHERE_CELESTIAL_LIGHT_COUNT > 0
        ? -GetCelestialBody(0).forward.xyz
        : float3(0.0f, 1.0f, 0.0f);

    float3 lightOnPlane = celestialLightDir - upVector * dot(celestialLightDir, upVector);
    float lightOnPlaneLength = length(lightOnPlane);

    if (lightOnPlaneLength > 1e-4f)
    {
        forwardVector = lightOnPlane / lightOnPlaneLength;
        sideVector = normalize(cross(upVector, forwardVector));
    }
    else
    {
        float3 referenceAxis = abs(upVector.y) < 0.999f ? float3(0.0f, 1.0f, 0.0f) : float3(0.0f, 0.0f, 1.0f);
        sideVector = normalize(cross(upVector, referenceAxis));
        forwardVector = normalize(cross(sideVector, upVector));
    }
}

void GetViewDirectionLutParams(float3 viewDirWS, float3 upVector,
                               out float viewZenithCosAngle, out float lightViewCosAngle)
{
    viewZenithCosAngle = dot(viewDirWS, upVector);
    lightViewCosAngle = 0.0f;

    float3 viewOnPlane = viewDirWS - upVector * viewZenithCosAngle;
    float viewOnPlaneLength = length(viewOnPlane);

    if (viewOnPlaneLength < 1e-4f)
        return;

    float3 forwardVector = float3(0.0f, 0.0f, 1.0f);
    float3 sideVector = float3(1.0f, 0.0f, 0.0f);
    GetCelestialLightReferenceFrame(upVector, forwardVector, sideVector);
    float2 viewOnPlaneBasis = float2(dot(viewOnPlane / viewOnPlaneLength, forwardVector),
                                     dot(viewOnPlane / viewOnPlaneLength, sideVector));
    lightViewCosAngle = atan2(viewOnPlaneBasis.y, viewOnPlaneBasis.x) * rcp(PI);
}

float3 ReconstructViewDirectionFromLutParams(float viewZenithCosAngle, float lightViewCosAngle, float3 upVector)
{
    float viewZenithSinAngle = SinFromCos(viewZenithCosAngle);
    float relativeAzimuth = lightViewCosAngle * PI;
    float sinAzimuth = 0.0f;
    float cosAzimuth = 1.0f;

    sincos(relativeAzimuth, sinAzimuth, cosAzimuth);

    float3 forwardVector = float3(0.0f, 0.0f, 1.0f);
    float3 sideVector = float3(1.0f, 0.0f, 0.0f);

    GetCelestialLightReferenceFrame(upVector, forwardVector, sideVector);

    return normalize(sideVector * (viewZenithSinAngle * sinAzimuth)
                   + upVector * viewZenithCosAngle
                   + forwardVector * (viewZenithSinAngle * cosAzimuth));
}

float3 ReconstructCanonicalViewDirectionFromLutParams(float viewZenithCosAngle, float lightViewCosAngle)
{
    float viewZenithSinAngle = SinFromCos(viewZenithCosAngle);
    float relativeAzimuth = lightViewCosAngle * PI;
    float sinAzimuth = 0.0f;
    float cosAzimuth = 1.0f;

    sincos(relativeAzimuth, sinAzimuth, cosAzimuth);

    return float3(viewZenithSinAngle * sinAzimuth,
                  viewZenithCosAngle,
                  viewZenithSinAngle * cosAzimuth);
}

void UvToSkyViewLutParams(out float viewZenithCosAngle, out float lightViewCosAngle, in float viewHeight, in float2 uv,
                          in float2 resolution);
void SkyViewLutParamsToUv(bool IntersectGround, in float viewZenithCosAngle, in float lightViewCosAngle,
                          in float viewHeight, out float2 uv, in float2 resolution);
void ComputeZenithHorizonParams(float viewHeight, out float Beta, out float ZenithHorizonAngle)
{
    float Vhorizon = sqrt(max(viewHeight * viewHeight - _BottomRadius * _BottomRadius, 0.0f));
    float ratio = saturate(Vhorizon / max(viewHeight, 1e-6f));
    Beta = acos(ratio);
    ZenithHorizonAngle = PI - Beta;
}

float DecodeLightViewCosAngle(float uvx)
{
    float coord = uvx * 2.0f - 1.0f;
    return sign(coord) * coord * coord;
}

float EncodeLightViewCosAngleToUvX(float lightViewCosAngle)
{
    return sign(lightViewCosAngle) * sqrt(saturate(abs(lightViewCosAngle))) * 0.5f + 0.5f;
}

void UvToSkyViewLutParams(out float viewZenithCosAngle, out float lightViewCosAngle, in float viewHeight, in float2 uv)
{
    UvToSkyViewLutParams(viewZenithCosAngle, lightViewCosAngle, viewHeight, uv, _SkyViewLutResolution);
}

void UvToSkyViewLutParams(out float viewZenithCosAngle, out float lightViewCosAngle, in float viewHeight, in float2 uv,
                          in float2 resolution)
{
    uv = RemapHalfTexelCoordTo01(uv, resolution);

    float Beta, ZenithHorizonAngle;
    ComputeZenithHorizonParams(viewHeight, Beta, ZenithHorizonAngle);

    if (uv.y < 0.5f)
    {
        float coord = 2.0 * uv.y;
        coord = 1.0 - coord;
        coord *= coord;
        coord = 1.0 - coord;
        viewZenithCosAngle = cos(ZenithHorizonAngle * coord);
    }
    else
    {
        float coord = uv.y * 2.0 - 1.0;
        coord *= coord;
        viewZenithCosAngle = cos(ZenithHorizonAngle + Beta * coord);
    }

    lightViewCosAngle = DecodeLightViewCosAngle(uv.x);
}

void SkyViewLutParamsToUv(bool IntersectGround, in float viewZenithCosAngle, in float lightViewCosAngle,
                          in float viewHeight, out float2 uv)
{
    SkyViewLutParamsToUv(IntersectGround, viewZenithCosAngle, lightViewCosAngle, viewHeight, uv, _SkyViewLutResolution);
}

void SkyViewLutParamsToUv(bool IntersectGround, in float viewZenithCosAngle, in float lightViewCosAngle,
                          in float viewHeight, out float2 uv, in float2 resolution)
{
    float Beta, ZenithHorizonAngle;
    ComputeZenithHorizonParams(viewHeight, Beta, ZenithHorizonAngle);

    if (!IntersectGround)
    {
        float safeViewZenithCosAngle = clamp(viewZenithCosAngle, -1.0f, 1.0f);
        float coord = acos(safeViewZenithCosAngle) / max(ZenithHorizonAngle, 1e-6f);
        coord = 1.0 - coord;
        coord = sqrt(coord);
        coord = 1.0 - coord;
        uv.y = coord * 0.5f;
    }
    else
    {
        float safeViewZenithCosAngle = clamp(viewZenithCosAngle, -1.0f, 1.0f);
        float coord = (acos(safeViewZenithCosAngle) - ZenithHorizonAngle) / max(Beta, 1e-6f);
        coord = sqrt(coord);
        uv.y = coord * 0.5f + 0.5f;
    }

    uv.x = EncodeLightViewCosAngleToUvX(lightViewCosAngle);
    uv = Remap01ToHalfTexelCoord(uv, resolution);

    float horizonGuard = 0.5f / resolution.y;

    uv.y = IntersectGround
        ? max(uv.y, 0.5f + horizonGuard)
        : min(uv.y, 0.5f - horizonGuard);
}

TEXTURE2D(ATMOSPHERE_MULTI_SCATTER_LUT);

float _IntensityMultiplier;

float3 GetMultipleScattering(float viewHeight, float viewZenithCosAngle)
{
    float range = _TopRadius - _BottomRadius;
    float normalizedHeight = (viewHeight - _BottomRadius) / range;
    float2 uv = saturate(float2(viewZenithCosAngle * 0.5f + 0.5f, normalizedHeight));
    uv = Remap01ToHalfTexelCoord(uv, _MultiScatteringLutRes);

    return SAMPLE_TEXTURE2D_LOD(
        ATMOSPHERE_MULTI_SCATTER_LUT,
        ATMOSPHERE_MULTI_SCATTER_LUT_SAMPLER,
        uv,
        0).rgb * ATMOSPHERE_MULTI_SCATTERING_INV_PRE_EXPOSURE;
}


float3 SampleGroundIrradiance(float viewHeight, float sunZenithCosAngle)
{
    float range = _TopRadius - _BottomRadius;
    float normalizedHeight = (viewHeight - _BottomRadius) / max(range, 1e-6f);
    float2 uv = saturate(float2(sunZenithCosAngle * 0.5f + 0.5f, normalizedHeight));
    uv = Remap01ToHalfTexelCoord(uv, _GroundIrradianceLutResolution);

    return SAMPLE_TEXTURE2D_LOD(
        ATMOSPHERE_GROUND_IRRADIANCE_LUT,
        ATMOSPHERE_GROUND_IRRADIANCE_LUT_SAMPLER,
        uv,
        0).rgb;
}


#endif
