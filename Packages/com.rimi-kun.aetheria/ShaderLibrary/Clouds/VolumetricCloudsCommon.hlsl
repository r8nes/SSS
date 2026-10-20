#ifndef VOLUMETRIC_CLOUDS_COMMON_INCLUDED
#define VOLUMETRIC_CLOUDS_COMMON_INCLUDED
#include "../../Runtime/Data/ShaderConfigs/CloudShaderConfig.cs.hlsl"

#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
#include "../Common/AtmosphereSunTransmittance.hlsl"
#include "./VolumetricCloudsBlueNoise.hlsl"
#include "../Common/VolumetricDepth.hlsl"
#include "../Common/PhaseFunctions.hlsl"

CBUFFER_START(UnityPerMaterial)
float _MaxStepSize;
int _NumPrimarySteps;
int _NumLightSteps;
float _HighestAltitude;
float _LowestAltitude;
float4 _CloudAltitudeSquaredParams;
float4 _CloudPlanetCenterWS;
float _CloudPlanetRadius;
float _CameraSpace;
float4 _ShapeNoiseOffset;
float _VerticalShapeNoiseOffset;
float4 _WindDirection;
float4 _WindVector;
float _VerticalShapeWindDisplacement;
float _VerticalErosionWindDisplacement;
float _MediumWindSpeed;
float _SmallWindSpeed;
float _AltitudeDistortion;
float _DensityMultiplier;
float _ShapeFactor;
float _ErosionFactor;
float _MicroErosionFactor;
float _PowderEffectIntensity;
float _MultiScattering;
float _ErosionOcclusion;
float _FadeInStart;
float4 _CloudNoiseScales;
float4 _CloudDistanceReciprocals;
float4 _ScatteringTint;
CBUFFER_END

#include "../Common/SphericalShellIntersection.hlsl"

int _AccumulationFrameIndex;
int _SubPixelIndex;
float4 _CloudTraceSize;
float4 _CloudIntermediateSize;
float4 _CloudFinalSize;
float _CloudNearPlane;

float3 _SunColor;
float4 _CloudSunDirectionWS;
float3 _CloudAmbientTop;
float3 _CloudAmbientBottom;
float4 _CloudAmbientSHAr;
float4 _CloudAmbientSHAg;
float4 _CloudAmbientSHAb;
float4 _CloudAmbientSHBr;
float4 _CloudAmbientSHBg;
float4 _CloudAmbientSHBb;
float4 _CloudAmbientSHC;

#define PHASE_FUNCTION_STRUCTURE float2
#define CLOUD_DETAIL_MIP_OFFSET 0.0
#define CLOUD_DENSITY_THRESHOLD ((float)CLOUDSHADERCONFIG_DENSITY_THRESHOLD_NUMERATOR / (float)CLOUDSHADERCONFIG_DENSITY_THRESHOLD_DENOMINATOR)
#define CLOUD_FORWARD_LOBE_ECCENTRICITY 0.7
#define CLOUD_BACKWARD_LOBE_ECCENTRICITY 0.7
#define MIN_EROSION_DISTANCE 3000.0
#define MAX_EROSION_DISTANCE 100000.0
#define MAX_SKYBOX_VOLUMETRIC_CLOUDS_DISTANCE FLT_MAX
#define LIGHT_STEP_MAXIMAL_SIZE 1000.0f
#define CLOUD_TRANSFER_LUT_RESOLUTION CLOUDSHADERCONFIG_TRANSFER_LUT_RESOLUTION
#define CLOUD_BLOCKED_OPTICAL_DEPTH 80.0f
#define EMPTY_STEPS_BEFORE_LARGE_STEPS 8
#define CLOUD_COVERAGE ((float)CLOUDSHADERCONFIG_COVERAGE_NUMERATOR / (float)CLOUDSHADERCONFIG_COVERAGE_DENOMINATOR)
#define CLOUD_MAX_BASE_DENSITY (CLOUD_COVERAGE * CLOUD_COVERAGE)
#define CLOUD_EXTINCTION 0.04f

#define _PlanetCenterPosition _CloudPlanetCenterWS
#define _PlanetRadius _CloudPlanetRadius

float3 ConvertToPS(float3 positionWS)
{
    return positionWS - _PlanetCenterPosition.xyz;
}

struct EnvironmentLighting
{
    float3 sunDirection;
    float cosAngle;
    float powderDirectionalFactor;
    PHASE_FUNCTION_STRUCTURE phaseFunction;
};

struct CloudRay
{
    float3 originWS;
    float3 direction;
    float maxRayLength;
    float integrationNoise;
};

struct RayMarchRange
{
    float start;
    float end;
};

struct VolumetricRayResult
{
    float3 scattering;
    float ambient;
    float transmittance;
    float meanDistance;
    bool invalidRay;
};

groupshared float gs_cloudLutDensity[CLOUD_TRANSFER_LUT_RESOLUTION];
groupshared float gs_cloudLutErosion[CLOUD_TRANSFER_LUT_RESOLUTION];
groupshared float gs_cloudLutAO[CLOUD_TRANSFER_LUT_RESOLUTION];
groupshared float gs_cloudLutShapeBias[CLOUD_TRANSFER_LUT_RESOLUTION];

void LoadCloudLutToLDS(uint groupThreadId)
{
    float4 cloudLut = LOAD_TEXTURE2D_LOD(
        _CloudLutTexture,
        int2(0, groupThreadId),
        0);
    gs_cloudLutDensity[groupThreadId] = cloudLut.x;
    gs_cloudLutErosion[groupThreadId] = cloudLut.y;
    gs_cloudLutAO[groupThreadId] = cloudLut.z;
    gs_cloudLutShapeBias[groupThreadId] = cloudLut.w;
    GroupMemoryBarrierWithGroupSync();
}

void ComputeCloudLutInterpolation(
    float radialDistanceSquared,
    out uint floorTap,
    out uint ceilTap,
    out float interpolation)
{
    float squaredRadiusCoordinate = saturate(
        (radialDistanceSquared - _CloudAltitudeSquaredParams.x)
        * _CloudAltitudeSquaredParams.y);
    float tapCoordinate = squaredRadiusCoordinate
        * (CLOUD_TRANSFER_LUT_RESOLUTION - 1.0f);
    floorTap = (uint)floor(tapCoordinate);
    ceilTap = min(floorTap + 1u, CLOUD_TRANSFER_LUT_RESOLUTION - 1u);
    interpolation = tapCoordinate - floorTap;
}

void SampleCloudShapeLDS(
    float radialDistanceSquared,
    out float densityProfile,
    out float erosionProfile,
    out float shapeBias)
{
    uint floorTap;
    uint ceilTap;
    float interpolation;
    ComputeCloudLutInterpolation(
        radialDistanceSquared, floorTap, ceilTap, interpolation);
    densityProfile = lerp(
        gs_cloudLutDensity[floorTap],
        gs_cloudLutDensity[ceilTap],
        interpolation);
    erosionProfile = lerp(
        gs_cloudLutErosion[floorTap],
        gs_cloudLutErosion[ceilTap],
        interpolation);
    shapeBias = lerp(
        gs_cloudLutShapeBias[floorTap],
        gs_cloudLutShapeBias[ceilTap],
        interpolation);
}

float SampleCloudAOLDS(float radialDistanceSquared)
{
    uint floorTap;
    uint ceilTap;
    float interpolation;
    ComputeCloudLutInterpolation(
        radialDistanceSquared, floorTap, ceilTap, interpolation);
    return lerp(
        gs_cloudLutAO[floorTap],
        gs_cloudLutAO[ceilTap],
        interpolation);
}

EnvironmentLighting EvaluateEnvironmentLighting(float3 rayDirection)
{
    EnvironmentLighting lighting = (EnvironmentLighting)0;
    lighting.sunDirection = _CloudSunDirectionWS.xyz;
    lighting.cosAngle = dot(rayDirection, lighting.sunDirection);
    lighting.powderDirectionalFactor = smoothstep(0.5, -0.5, lighting.cosAngle);

    lighting.phaseFunction[0] = ApproximateMiePhase(
        CLOUDSHADERCONFIG_DROPLET_DIAMETER_MICROMETERS,
        lighting.cosAngle);

    float higherOrderScattering = saturate(_MultiScattering);
    float forwardLobe = HenyeyGreensteinPhaseFunction(
        CLOUD_FORWARD_LOBE_ECCENTRICITY * higherOrderScattering,
        lighting.cosAngle);
    float backwardLobe = HenyeyGreensteinPhaseFunction(
        -CLOUD_BACKWARD_LOBE_ECCENTRICITY * higherOrderScattering,
        lighting.cosAngle);
    lighting.phaseFunction[1] = forwardLobe + backwardLobe;

    return lighting;
}

bool GetCloudVolumeIntersection(CloudRay ray, out RayMarchRange rayMarchRange)
{
    rayMarchRange = (RayMarchRange)0;
    return IntersectSphericalShellVolume(
        ConvertToPS(ray.originWS),
        ray.direction,
        _LowestAltitude,
        _HighestAltitude,
        _CloudPlanetRadius,
        rayMarchRange.start,
        rayMarchRange.end);
}

float DensityRemap(float x, float a, float b, float c, float d)
{
    return (((x - a) / (b - a)) * (d - c)) + c;
}

float EvaluateCloudDetailErosion(float detailNoise, float normalizedHeight)
{
    return lerp(detailNoise, 1.0 - detailNoise, saturate(normalizedHeight * 10.0));
}

float PowderEffect(float cloudDensity, float directionalFactor, float intensity)
{
    float powderEffect = saturate((1.0 - exp(-cloudDensity * 4.0)) * 2.0);
    return lerp(1.0, powderEffect, directionalFactor * intensity);
}

float EvaluateNormalizedCloudHeightFromRadiusSquared(float radialDistanceSquared)
{
    return saturate(
        (sqrt(max(radialDistanceSquared, 0.0f)) - _CloudAltitudeSquaredParams.z)
        * _CloudAltitudeSquaredParams.w);
}

float3 AnimateShapeNoisePosition(float3 positionPS)
{
    positionPS.y += positionPS.x / 3.0f + positionPS.z / 7.0f;
    return positionPS
        + float3(_WindVector.x, 0.0, _WindVector.y) * _MediumWindSpeed
        + float3(0.0, _VerticalShapeWindDisplacement, 0.0);
}

float3 AnimateErosionNoisePosition(float3 positionPS)
{
    return positionPS
        + float3(_WindVector.x, 0.0, _WindVector.y) * _SmallWindSpeed
        + float3(0.0, _VerticalErosionWindDisplacement, 0.0);
}

float3 BuildShapeBaseCoordinate(float3 positionPS)
{
    positionPS.xz += _WorldSpaceCameraPos.xz * _CameraSpace;
    float3 animatedPosition = AnimateShapeNoisePosition(positionPS);
    return float3(animatedPosition.x, animatedPosition.z, animatedPosition.y)
        * _CloudNoiseScales.x
        - float3(_ShapeNoiseOffset.x, _ShapeNoiseOffset.y, _VerticalShapeNoiseOffset);
}

float3 BuildShapeBaseStep(float3 direction)
{
    float3 transformedDirection = float3(
        direction.x,
        direction.z,
        direction.y + direction.x / 3.0f + direction.z / 7.0f);
    return transformedDirection * _CloudNoiseScales.x;
}

float3 BuildErosionPosition(float3 positionPS)
{
    positionPS.xz += _WorldSpaceCameraPos.xz * _CameraSpace;
    return AnimateErosionNoisePosition(positionPS);
}

float EvaluateCloudShapeFactor(float erosionProfile)
{
    return lerp(0.1f, 1.0f, _ShapeFactor) * erosionProfile;
}

bool EvaluateBaseCloudFromNoise(
    float shapeNoise,
    float densityProfile,
    float erosionProfile,
    float shapeBias,
    float baseCloudThreshold,
    out float baseCloud)
{
    baseCloud = 0.0f;

    float shapeFactor = EvaluateCloudShapeFactor(erosionProfile);
    float densitySpan = densityProfile * CLOUD_COVERAGE * (1.0f - shapeFactor);
    bool hasBaseCloud = densitySpan > 0.0f && shapeFactor > 0.0f;
    if (hasBaseCloud)
    {
        // Reconstruct the old shape response without ever materializing
        // slope = shapeFactor / densitySpan. The extra shapeBias channel keeps
        // the original empty-slice sentinel behavior at zero-density LUT rows,
        // which prevents isolated noise peaks from surviving at the layer bounds.
        float baseNumerator = shapeBias * densitySpan
            - shapeFactor * (1.0f - shapeNoise);
        float thresholdNumerator = saturate(
            baseCloudThreshold / CLOUD_MAX_BASE_DENSITY) * densitySpan;
        hasBaseCloud = baseNumerator > thresholdNumerator;
        if (hasBaseCloud)
        {
            baseCloud = saturate(baseNumerator / densitySpan) * CLOUD_MAX_BASE_DENSITY;
            hasBaseCloud = baseCloud > baseCloudThreshold;
        }
    }

    return hasBaseCloud;
}

float3 BuildShapeCoordinates(
    float3 shapeBaseCoordinate,
    float normalizedHeight)
{
    return shapeBaseCoordinate
        + normalizedHeight
        * float3(_WindDirection.x, _WindDirection.y, 0.0f)
        * _AltitudeDistortion;
}

float SampleShapeNoise(float3 shapeCoordinates, float mipOffset)
{
    return SAMPLE_TEXTURE3D_LOD(
        _CloudShapeNoise,
        sampler_TrilinearRepeat,
        shapeCoordinates,
        mipOffset).r;
}

bool EvaluatePrimaryShape(
    float radialDistanceSquared,
    float3 shapeBaseCoordinate,
    float densityAttenuationValue,
    out float baseCloud,
    out float normalizedHeight,
    out float erosionProfile)
{
    baseCloud = 0.0f;
    normalizedHeight = EvaluateNormalizedCloudHeightFromRadiusSquared(
        radialDistanceSquared);
    erosionProfile = 0.0f;

    float densityScale = _DensityMultiplier * densityAttenuationValue;
    float baseCloudThreshold = CLOUD_DENSITY_THRESHOLD / max(densityScale, 1e-20f);
    bool hasShape = densityScale * CLOUD_MAX_BASE_DENSITY > CLOUD_DENSITY_THRESHOLD;
    if (hasShape)
    {
        float shapeNoise = SampleShapeNoise(
            BuildShapeCoordinates(shapeBaseCoordinate, normalizedHeight),
            0.0f);

        float densityProfile;
        float shapeBias;
        SampleCloudShapeLDS(
            radialDistanceSquared,
            densityProfile,
            erosionProfile,
            shapeBias);

        hasShape = EvaluateBaseCloudFromNoise(
            shapeNoise,
            densityProfile,
            erosionProfile,
            shapeBias,
            baseCloudThreshold,
            baseCloud);
    }

    return hasShape;
}

bool ApplyPrimaryDetailErosion(
    float3 positionPS,
    float radialDistanceSquared,
    float normalizedHeight,
    float erosionProfile,
    float baseCloud,
    float baseCloudThreshold,
    float densityScale,
    float erosionMipOffset,
    out float density,
    out float ambientOcclusion)
{
    density = 0.0f;

    float shapeFactor = EvaluateCloudShapeFactor(erosionProfile);
    float erosionFactor = _ErosionFactor * erosionProfile;
    float rawAmbientOcclusion = SampleCloudAOLDS(radialDistanceSquared);
    float ambientOcclusionBlend = saturate(
        1.0f - max(erosionFactor, shapeFactor) * 0.5f);
    ambientOcclusion = lerp(
        1.0f,
        rawAmbientOcclusion,
        ambientOcclusionBlend);

    float3 erosionPosition = BuildErosionPosition(positionPS);
    float erosionNoise = EvaluateCloudDetailErosion(
        SAMPLE_TEXTURE3D_LOD(
            _CloudDetailNoise,
            sampler_TrilinearRepeat,
            erosionPosition * _CloudNoiseScales.y,
            CLOUD_DETAIL_MIP_OFFSET + erosionMipOffset).r,
        normalizedHeight);
    erosionNoise = lerp(
        0.0f,
        erosionNoise,
        erosionFactor * 0.75f * CLOUD_COVERAGE);

    baseCloud = DensityRemap(baseCloud, erosionNoise, 1.0f, 0.0f, 1.0f);
    bool hasDensity = baseCloud > baseCloudThreshold;

#if defined(CLOUDS_MICRO_EROSION)
    if (hasDensity)
    {
        float microDetailFactor = _MicroErosionFactor * erosionProfile;
        float fineNoise = EvaluateCloudDetailErosion(
            SAMPLE_TEXTURE3D_LOD(
                _CloudDetailNoise,
                sampler_TrilinearRepeat,
                erosionPosition * _CloudNoiseScales.z,
                CLOUD_DETAIL_MIP_OFFSET + erosionMipOffset).r,
            normalizedHeight);
        fineNoise = lerp(
            0.0f,
            fineNoise,
            microDetailFactor * 0.5f * CLOUD_COVERAGE);
        baseCloud = DensityRemap(baseCloud, fineNoise, 1.0f, 0.0f, 1.0f);
        hasDensity = baseCloud > baseCloudThreshold;
    }
#endif

    if (hasDensity)
    {
        ambientOcclusion = saturate(
            ambientOcclusion - sqrt(erosionNoise * _ErosionOcclusion));
        density = max(0.0f, baseCloud) * densityScale;
        hasDensity = density > CLOUD_DENSITY_THRESHOLD;
    }

    return hasDensity;
}

float SampleCheapCloudDensity(
    float radialDistanceSquared,
    float3 shapeBaseCoordinate,
    float densityAttenuationValue)
{
    float normalizedHeight = EvaluateNormalizedCloudHeightFromRadiusSquared(
        radialDistanceSquared);
    float shapeNoise = SampleShapeNoise(
        BuildShapeCoordinates(shapeBaseCoordinate, normalizedHeight),
        1.0f);

    float densityProfile;
    float erosionProfile;
    float shapeBias;
    SampleCloudShapeLDS(
        radialDistanceSquared,
        densityProfile,
        erosionProfile,
        shapeBias);

    float baseCloud;
    float density = 0.0f;
    if (EvaluateBaseCloudFromNoise(
        shapeNoise,
        densityProfile,
        erosionProfile,
        shapeBias,
        0.0f,
        baseCloud))
    {
        density = baseCloud * _DensityMultiplier * densityAttenuationValue;
    }

    return density;
}

float SampleLightExtinction(
    float radialDistanceSquared,
    float3 shapeBaseCoordinate,
    float shapeMipOffset)
{
    float normalizedHeight = EvaluateNormalizedCloudHeightFromRadiusSquared(
        radialDistanceSquared);
    float shapeNoise = SampleShapeNoise(
        BuildShapeCoordinates(shapeBaseCoordinate, normalizedHeight),
        shapeMipOffset);

    float densityProfile;
    float erosionProfile;
    float shapeBias;
    SampleCloudShapeLDS(
        radialDistanceSquared,
        densityProfile,
        erosionProfile,
        shapeBias);

    float erosionFactor = _ErosionFactor * erosionProfile;
    float erosionCompensation = erosionFactor * 0.1f;
#if defined(CLOUDS_MICRO_EROSION)
    erosionCompensation += _MicroErosionFactor * erosionProfile * 0.15f;
#endif

    float baseCloud;
    float extinction = 0.0f;
    if (EvaluateBaseCloudFromNoise(
        shapeNoise,
        densityProfile,
        erosionProfile,
        shapeBias,
        erosionCompensation,
        baseCloud))
    {
        float density = (baseCloud - erosionCompensation) * _DensityMultiplier;
        extinction = density * CLOUD_EXTINCTION;
    }

    return extinction;
}

float3 EvaluateSunOpticalDepth(
    float3 positionPS,
    float radialDistanceSquared,
    float3 primaryShapeBaseCoordinate,
    float3 lightShapeBaseStep,
    float3 sunDirection)
{
    float3 opticalDepthResult = float3(
        CLOUD_BLOCKED_OPTICAL_DEPTH,
        CLOUD_BLOCKED_OPTICAL_DEPTH,
        CLOUD_BLOCKED_OPTICAL_DEPTH);
    float radialProjection = dot(positionPS, sunDirection);
    float totalLightDistance = 0.0f;

    if (ExitSphericalShellVolume(
        radialProjection,
        radialDistanceSquared,
        _CloudPlanetRadius,
        _HighestAltitude,
        totalLightDistance))
    {
        // HDRP light-step layout.
        totalLightDistance = clamp(
            totalLightDistance,
            0.0f,
            (float)_NumLightSteps * LIGHT_STEP_MAXIMAL_SIZE);
        totalLightDistance += 5.0f;

        float intervalSize = totalLightDistance / (float)_NumLightSteps;
        float opticalDepth = 0.0f;

        [loop]
        for (int j = 0; j < _NumLightSteps; ++j)
        {
            float sampleDistance = intervalSize * (0.25f + (float)j);
            float sampleRadiusSquared = radialDistanceSquared
                + sampleDistance * (2.0f * radialProjection + sampleDistance);
            float3 sampleShapeBaseCoordinate = primaryShapeBaseCoordinate
                + lightShapeBaseStep * sampleDistance;
            float shapeMipOffset = 3.0f * (float)j / (float)_NumLightSteps;

            opticalDepth += SampleLightExtinction(
                sampleRadiusSquared,
                sampleShapeBaseCoordinate,
                shapeMipOffset);
        }

        opticalDepthResult = intervalSize * opticalDepth * _ScatteringTint.xyz;
    }

    return opticalDepthResult;
}

float3 EvaluateSunLightingFromOpticalDepth(
    float3 opticalDepth,
    PHASE_FUNCTION_STRUCTURE phaseFunction)
{
    float secondOctaveFactor = _MultiScattering;
    return TransmittanceFromOpticalDepth(opticalDepth) * phaseFunction[0]
        + TransmittanceFromOpticalDepth(opticalDepth * secondOctaveFactor)
        * (phaseFunction[1] * secondOctaveFactor);
}

float3 EvaluateSunColorAttenuation(float3 positionPS, float3 sunDirection, bool estimatePenumbra = false)
{
    return EvaluateAtmosphereSunTransmittance(positionPS, sunDirection, estimatePenumbra);
}

bool IntegrateCloudSample(
    float cloudDensity,
    float cloudAmbientOcclusion,
    float distanceToCamera,
    float3 sunOpticalDepth,
    EnvironmentLighting envLighting,
    float stepSize,
    inout VolumetricRayResult volumetricRay,
    inout float meanDistanceDivider)
{
    volumetricRay.meanDistance += distanceToCamera * volumetricRay.transmittance;
    meanDistanceDivider += volumetricRay.transmittance;

    float stepTransmittance = TransmittanceFromOpticalDepth(cloudDensity * CLOUD_EXTINCTION * stepSize);
    float powderEffect = PowderEffect(
        cloudDensity,
        envLighting.powderDirectionalFactor,
        _PowderEffectIntensity);
    float3 sunLighting = EvaluateSunLightingFromOpticalDepth(
        sunOpticalDepth,
        envLighting.phaseFunction);

    float integratedWeight =
        volumetricRay.transmittance * (1.0f - stepTransmittance);
    volumetricRay.scattering +=
        sunLighting * (powderEffect * PI * integratedWeight);
    volumetricRay.ambient += cloudAmbientOcclusion * integratedWeight / PI;
    volumetricRay.transmittance *= stepTransmittance;

    bool terminated = volumetricRay.transmittance < 0.003f;
    if (terminated)
        volumetricRay.transmittance = 0.0f;
    return terminated;
}

float DensityFadeValue(float distanceToCamera)
{
    return saturate(
        (distanceToCamera - _FadeInStart) * _CloudDistanceReciprocals.x);
}

float ErosionMipOffset(float distanceToCamera)
{
    return 4.0f * saturate(
        (distanceToCamera - MIN_EROSION_DISTANCE) * _CloudDistanceReciprocals.y);
}

float3 EvaluateCloudAmbientRadiance(float3 rayDirection)
{
    // The ambient probe has already been deconvolved from Unity's diffuse cosine
    // kernel and convolved with the cloud droplet phase function on the CPU.
    // Evaluate the resulting radiance field in the outgoing cloud-ray direction
    // instead of collapsing it to two up/down colors.
    float3 direction = SafeNormalize(rayDirection);
    float4 shL0L1 = float4(direction, 1.0);
    float4 shL2 = direction.xyzz * direction.yzzx;
    float shC = direction.x * direction.x - direction.y * direction.y;

    float3 radiance;
    radiance.r = dot(_CloudAmbientSHAr, shL0L1) + dot(_CloudAmbientSHBr, shL2) + _CloudAmbientSHC.r * shC;
    radiance.g = dot(_CloudAmbientSHAg, shL0L1) + dot(_CloudAmbientSHBg, shL2) + _CloudAmbientSHC.g * shC;
    radiance.b = dot(_CloudAmbientSHAb, shL0L1) + dot(_CloudAmbientSHBb, shL2) + _CloudAmbientSHC.b * shC;
    return max(0.0, radiance);
}

float3 EvaluateCloudFinalAmbient(float3 positionPS, float3 rayDirection)
{
    return EvaluateCloudAmbientRadiance(rayDirection);
}

#endif
