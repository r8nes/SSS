#ifndef CLOUD_LAYER_COMMON_INCLUDED
#define CLOUD_LAYER_COMMON_INCLUDED

#include "../Common/PhaseFunctions.hlsl"
#include "./CloudLayerUtilities.hlsl"
#include "../Atmosphere/AtmosphereCore.hlsl"
#include "../Common/AtmosphereSunTransmittance.hlsl"
#include "../../Runtime/Data/ShaderConfigs/CloudShaderConfig.cs.hlsl"

TEXTURE2D_ARRAY(_CloudLayerTexture);
SAMPLER(sampler_CloudLayerTexture);

float4 _CloudLayerGlobalParams;
float3 _CloudLayerSunDirection;
float4 _CloudLayerLightParamA;
float4 _CloudLayerLightParamB;
float4 _CloudLayerAmbientColor;
float4 _CloudLayerAmbientColorB;
float4 _CloudLayerAmbientProbeDimmers;

#ifndef CLOUD_LAYER_AMBIENT_PROBE_BUFFER
#define CLOUD_LAYER_AMBIENT_PROBE_BUFFER 1
#endif

#if CLOUD_LAYER_AMBIENT_PROBE_BUFFER
StructuredBuffer<float> _CloudLayerAmbientProbeBuffer;
int _CloudLayerUseAmbientProbeBuffer;
#endif
float4 _CloudLayerAnimationParamA;
float4 _CloudLayerAnimationParamB;
float3 _CloudLayerViewForwardWS;
float4x4 _CloudLayerPixelCoordToViewDirWS;
float _CloudLayerPlanetaryRadius;

#define CLOUD_LAYER_DISTORTION_NONE 0

bool CloudLayerIsEnabled()
{
    return _CloudLayerGlobalParams.x > 0.5f;
}

bool CloudLayerUpperHemisphereOnly()
{
    return _CloudLayerGlobalParams.y > 0.5f;
}

float CloudLayerOpacity()
{
    return _CloudLayerGlobalParams.z;
}

uint CloudLayerCount()
{
    return _CloudLayerGlobalParams.w > 1.5f ? 2u : 1u;
}

float4 GetCloudLayerLightParam(uint layerIndex)
{
    return layerIndex == 0u ? _CloudLayerLightParamA : _CloudLayerLightParamB;
}

#if CLOUD_LAYER_AMBIENT_PROBE_BUFFER
float EvaluateCloudLayerAmbientProbeChannel(
    uint offset,
    float shL0,
    float3 shL1,
    float3 shL2A,
    float2 shL2B,
    float band0,
    float band1,
    float band2)
{
    return
        _CloudLayerAmbientProbeBuffer[offset] * shL0 * band0 +
        dot(float3(
            _CloudLayerAmbientProbeBuffer[offset + 1u],
            _CloudLayerAmbientProbeBuffer[offset + 2u],
            _CloudLayerAmbientProbeBuffer[offset + 3u]), shL1) * band1 +
        dot(float3(
            _CloudLayerAmbientProbeBuffer[offset + 4u],
            _CloudLayerAmbientProbeBuffer[offset + 5u],
            _CloudLayerAmbientProbeBuffer[offset + 6u]), shL2A) * band2 +
        dot(float2(
            _CloudLayerAmbientProbeBuffer[offset + 7u],
            _CloudLayerAmbientProbeBuffer[offset + 8u]), shL2B) * band2;
}

float3 EvaluateCloudLayerAmbientProbe(float3 direction)
{
    float x = direction.x;
    float y = direction.y;
    float z = direction.z;
    float shL0 = 0.28209479177387814;
    float3 shL1 = float3(
        -0.4886025119029199 * y,
         0.4886025119029199 * z,
        -0.4886025119029199 * x);
    float3 shL2A = float3(
         1.0925484305920792 * x * y,
        -1.0925484305920792 * y * z,
         0.31539156525252005 * (3.0 * z * z - 1.0));
    float2 shL2B = float2(
        -1.0925484305920792 * x * z,
         0.5462742152960396 * (x * x - y * y));

    float2 mieMoments = ApproximateMieLegendreMomentsL1L2(
        CLOUDSHADERCONFIG_DROPLET_DIAMETER_MICROMETERS);
    const float band0 = 1.0;
    float band1 = mieMoments.x;
    float band2 = mieMoments.y;

    float3 irradiance = float3(
        EvaluateCloudLayerAmbientProbeChannel(0u, shL0, shL1, shL2A, shL2B, band0, band1, band2),
        EvaluateCloudLayerAmbientProbeChannel(9u, shL0, shL1, shL2A, shL2B, band0, band1, band2),
        EvaluateCloudLayerAmbientProbeChannel(18u, shL0, shL1, shL2A, shL2B, band0, band1, band2));

    return max(irradiance, 0.0);
}

#endif

float3 GetCloudLayerAmbient(uint layerIndex)
{
    float3 result = layerIndex == 0u
        ? _CloudLayerAmbientColor.rgb
        : _CloudLayerAmbientColorB.rgb;

#if CLOUD_LAYER_AMBIENT_PROBE_BUFFER
    if (_CloudLayerUseAmbientProbeBuffer != 0)
    {
        float dimmer = layerIndex == 0u
            ? _CloudLayerAmbientProbeDimmers.x
            : _CloudLayerAmbientProbeDimmers.y;
        result = EvaluateCloudLayerAmbientProbe(float3(0.0, -1.0, 0.0)) * dimmer;
    }
#endif

    return result;
}

float4 GetCloudLayerAnimationParam(uint layerIndex)
{
    return layerIndex == 0u ? _CloudLayerAnimationParamA : _CloudLayerAnimationParamB;
}

int GetCloudLayerDistortionMode(uint layerIndex)
{
    return (int)round(GetCloudLayerAnimationParam(layerIndex).w);
}

float2 SampleCloudLayerMap(float3 direction, uint layerIndex)
{
    float2 uv = GetCloudLayerLatLongCoords(direction, CloudLayerUpperHemisphereOnly());
    return SAMPLE_TEXTURE2D_ARRAY_LOD(
        _CloudLayerTexture,
        sampler_CloudLayerTexture,
        uv,
        layerIndex,
        0).rg;
}

bool GetCloudLayerIntersection(
    float3 viewDirectionWS,
    float altitudeMeters,
    out float rayDistanceMeters,
    out float3 positionPS)
{
    float planetRadiusMeters = _CloudLayerPlanetaryRadius;
    float layerRadius = planetRadiusMeters + altitudeMeters;
    float2 intersections = 0.0f;
    bool hitLayer = IntersectRaySphere(
        float3(0.0f, planetRadiusMeters, 0.0f),
        viewDirectionWS,
        layerRadius,
        intersections);

    rayDistanceMeters = hitLayer ? intersections.y : -1.0f;
    positionPS = float3(0.0f, planetRadiusMeters, 0.0f) +
        viewDirectionWS * max(rayDistanceMeters, 0.0f);
    return rayDistanceMeters >= 0.0f;
}

float2 SampleAnimatedCloudLayer(
    float3 viewDirectionWS,
    float rayDistanceMeters,
    uint layerIndex)
{
    float2 cloud = 0.0f;
    int distortionMode = GetCloudLayerDistortionMode(layerIndex);

    if (distortionMode == CLOUD_LAYER_DISTORTION_NONE)
    {
        cloud = SampleCloudLayerMap(viewDirectionWS, layerIndex);
    }
    else
    {
        float altitude = GetCloudLayerLightParam(layerIndex).w;
        float scrollDistance = max(2.0f * altitude, 1.0e-3f);
        float4 animationParam = GetCloudLayerAnimationParam(layerIndex);
        float2 alpha = frac(animationParam.z / scrollDistance + float2(0.0f, 0.5f)) - 0.5f;
        float3 delta = float3(animationParam.x, 0.0f, animationParam.y);

        float3 position = viewDirectionWS * rayDistanceMeters;
        float2 cloudA = SampleCloudLayerMap(
            normalize(position + alpha.x * delta * scrollDistance),
            layerIndex);
        float2 cloudB = SampleCloudLayerMap(
            normalize(position + alpha.y * delta * scrollDistance),
            layerIndex);
        cloud = lerp(cloudA, cloudB, abs(2.0f * alpha.x));
    }

    return cloud;
}

float4 GetSingleCloudLayerColor(
    float3 viewDirectionWS,
    uint layerIndex,
    out float rayDistanceMeters)
{
    float4 result = 0.0f;
    float4 lightParam = GetCloudLayerLightParam(layerIndex);
    rayDistanceMeters = 0.0f;
    float3 positionPS = 0.0f;

    bool intersectsLayer = GetCloudLayerIntersection(
        viewDirectionWS,
        lightParam.w,
        rayDistanceMeters,
        positionPS);

    if (intersectsLayer)
    {
        float2 cloud = SampleAnimatedCloudLayer(viewDirectionWS, rayDistanceMeters, layerIndex);
        float3 lightColor = lightParam.rgb;
        lightColor *= EvaluateAtmosphereSunTransmittance(
            positionPS,
            _CloudLayerSunDirection,
            true);
        float3 ambient = GetCloudLayerAmbient(layerIndex);
        result = float4(cloud.x * lightColor + ambient * cloud.y, cloud.y) * CloudLayerOpacity();
    }

    return result;
}

float4 RenderCloudLayerRaw(float3 viewDirectionWS, out float cloudDistanceMeters)
{
    float4 clouds = 0.0f;
    cloudDistanceMeters = 0.0f;

    bool shouldRender = CloudLayerIsEnabled() &&
        (!CloudLayerUpperHemisphereOnly() || viewDirectionWS.y >= 0.0f);

    if (shouldRender)
    {
        clouds = GetSingleCloudLayerColor(
            viewDirectionWS,
            0u,
            cloudDistanceMeters);

        if (CloudLayerCount() > 1u)
        {
            float unusedLayerBDistance = 0.0f;
            float4 cloudsB = GetSingleCloudLayerColor(
                viewDirectionWS,
                1u,
                unusedLayerBDistance);
            clouds += cloudsB * (1.0f - clouds.a);
        }
    }

    return clouds;
}

float3 ApplyCloudLayerAerialPerspective(
    float3 cloudRadiance,
    float cloudOpacity,
    float3 viewDirectionWS,
    float2 screenUv,
    float rayDistanceMeters)
{
    float3 result = cloudRadiance;
    float viewForwardDistanceKm =
        rayDistanceMeters * dot(viewDirectionWS, normalize(_CloudLayerViewForwardWS)) /
        _DistanceUnitMeter;

    if (viewForwardDistanceKm > 0.0f && _AtmosphericScatteringMaxDistance > 0.0f)
    {
        float3 uvw = float3(
            screenUv,
            EncodeAerialPerspectiveDepth(
                min(viewForwardDistanceKm, _AtmosphericScatteringMaxDistance),
                _AtmosphericScatteringMaxDistance,
                _AtmosphericScatteringLutResolution.z));
        float4 aerialPerspective = SAMPLE_TEXTURE3D_LOD(
            ATMOSPHERE_AERIAL_PERSPECTIVE_LUT,
            ATMOSPHERE_AERIAL_PERSPECTIVE_LUT_SAMPLER,
            uvw,
            0);
        result = cloudRadiance * aerialPerspective.a + aerialPerspective.rgb * cloudOpacity;
    }
    return result;
}


float3 GetCloudLayerViewDirectionWS(float2 pixelCoord)
{
    float4 viewDirectionWS = mul(
        float4(pixelCoord, 1.0f, 1.0f),
        _CloudLayerPixelCoordToViewDirWS);
    // HDRP matrix produces V (towards the camera); Cloud Layer samples the ray into the scene.
    return -normalize(viewDirectionWS.xyz);
}

float4 RenderCloudLayer(float3 viewDirectionWS, float2 screenUv)
{
    float cloudDistanceMeters = 0.0f;
    float4 clouds = RenderCloudLayerRaw(viewDirectionWS, cloudDistanceMeters);

    if (clouds.a > 0.0f)
    {
        clouds.rgb = ApplyCloudLayerAerialPerspective(
            clouds.rgb,
            clouds.a,
            viewDirectionWS,
            screenUv,
            cloudDistanceMeters);
    }

    return clouds;
}

#endif
