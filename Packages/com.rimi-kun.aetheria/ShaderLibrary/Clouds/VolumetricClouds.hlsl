#ifndef URP_VOLUMETRIC_CLOUDS_HLSL
#define URP_VOLUMETRIC_CLOUDS_HLSL

#include "./VolumetricCloudsCommon.hlsl"

CloudRay BuildCloudsRay(
    uint2 pixelCoord,
    float3 viewDirectionWS,
    float maxRayLength)
{
    CloudRay ray = (CloudRay)0;
    ray.originWS = GetCameraPositionWS();
    ray.direction = viewDirectionWS;
    ray.maxRayLength = maxRayLength;
    ray.integrationNoise = EvaluateCloudIntegrationNoise(
        pixelCoord,
        (uint)_AccumulationFrameIndex);
    return ray;
}

void TraceVolumetricRay(CloudRay cloudRay, inout VolumetricRayResult volumetricRay)
{
    volumetricRay.scattering = 0.0f;
    volumetricRay.ambient = 0.0f;
    volumetricRay.transmittance = 1.0f;
    volumetricRay.meanDistance = FLT_MAX;
    volumetricRay.invalidRay = true;

    RayMarchRange rayMarchRange = (RayMarchRange)0;
    if (!GetCloudVolumeIntersection(cloudRay, rayMarchRange)
        || cloudRay.maxRayLength < rayMarchRange.start)
    {
        return;
    }

    volumetricRay.meanDistance = 0.0f;

    float totalDistance =
        min(rayMarchRange.end, cloudRay.maxRayLength) - rayMarchRange.start;
    float stepSize = min(totalDistance / (float)_NumPrimarySteps, _MaxStepSize);
    totalDistance = stepSize * _NumPrimarySteps;
    float rayMarchEndDistance = rayMarchRange.start + totalDistance;

    EnvironmentLighting envLighting = EvaluateEnvironmentLighting(cloudRay.direction);
    float3 lightShapeBaseStep = BuildShapeBaseStep(envLighting.sunDirection);

    float3 rayOriginPS = ConvertToPS(cloudRay.originWS);

    float currentDistanceToCamera =
        rayMarchRange.start + cloudRay.integrationNoise * stepSize;
    float3 firstPositionPS =
        rayOriginPS + currentDistanceToCamera * cloudRay.direction;
    float currentRadialProjection = dot(firstPositionPS, cloudRay.direction);
    float currentRadialDistanceSquared = dot(firstPositionPS, firstPositionPS);
    float3 currentShapeBaseCoordinate = BuildShapeBaseCoordinate(firstPositionPS);
    float3 primaryShapeBaseStep = BuildShapeBaseStep(cloudRay.direction);
    float3 primaryShapeStep = primaryShapeBaseStep * stepSize;

    float meanDistanceDivider = 0.0f;
    int currentIndex = 0;

    // HDRP empty-space stepping.
    bool activeSampling = true;
    int sequentialEmptySamples = 0;

    [loop]
    while (currentIndex < _NumPrimarySteps
        && currentDistanceToCamera < rayMarchEndDistance)
    {
        float radialDistanceSquared = currentRadialDistanceSquared;
        float densityAttenuationValue = DensityFadeValue(currentDistanceToCamera);

        if (activeSampling)
        {
            float baseCloud;
            float normalizedHeight;
            float erosionProfile;
            bool hasShape = EvaluatePrimaryShape(
                radialDistanceSquared,
                currentShapeBaseCoordinate,
                densityAttenuationValue,
                baseCloud,
                normalizedHeight,
                erosionProfile);

            bool hasDensity = false;
            float cloudDensity = 0.0f;
            float cloudAmbientOcclusion = 1.0f;

            if (hasShape)
            {
                float densityScale = _DensityMultiplier * densityAttenuationValue;
                float baseCloudThreshold =
                    CLOUD_DENSITY_THRESHOLD / max(densityScale, 1e-20f);
                float3 currentPositionPS =
                    rayOriginPS + currentDistanceToCamera * cloudRay.direction;
                hasDensity = ApplyPrimaryDetailErosion(
                    currentPositionPS,
                    radialDistanceSquared,
                    normalizedHeight,
                    erosionProfile,
                    baseCloud,
                    baseCloudThreshold,
                    densityScale,
                    ErosionMipOffset(currentDistanceToCamera),
                    cloudDensity,
                    cloudAmbientOcclusion);

                if (hasDensity)
                {
                    float3 sunOpticalDepth = EvaluateSunOpticalDepth(
                        currentPositionPS,
                        radialDistanceSquared,
                        currentShapeBaseCoordinate,
                        lightShapeBaseStep,
                        envLighting.sunDirection);

                    if (IntegrateCloudSample(
                        cloudDensity,
                        cloudAmbientOcclusion,
                        currentDistanceToCamera,
                        sunOpticalDepth,
                        envLighting,
                        stepSize,
                        volumetricRay,
                        meanDistanceDivider))
                    {
                        break;
                    }
                }
            }

            if (hasDensity)
                sequentialEmptySamples = 0;
            else
                sequentialEmptySamples++;

            if (sequentialEmptySamples == EMPTY_STEPS_BEFORE_LARGE_STEPS)
                activeSampling = false;

            currentRadialDistanceSquared +=
                stepSize * (2.0f * currentRadialProjection + stepSize);
            currentRadialProjection += stepSize;
            currentDistanceToCamera += stepSize;
            currentShapeBaseCoordinate += primaryShapeStep;
        }
        else
        {
            float cheapDensity = SampleCheapCloudDensity(
                radialDistanceSquared,
                currentShapeBaseCoordinate,
                densityAttenuationValue);

            // Match HDRP: the mip-1 probe only controls stepping and uses the
            // same density threshold as full sampling.
            if (cheapDensity < CLOUD_DENSITY_THRESHOLD)
            {
                float largeStep = stepSize * 2.0f;
                currentRadialDistanceSquared +=
                    largeStep * (2.0f * currentRadialProjection + largeStep);
                currentRadialProjection += largeStep;
                currentDistanceToCamera += largeStep;
                currentShapeBaseCoordinate += primaryShapeStep * 2.0f;
            }
            else
            {
                float rewindStep = -stepSize;
                currentRadialDistanceSquared +=
                    rewindStep * (2.0f * currentRadialProjection + rewindStep);
                currentRadialProjection += rewindStep;
                currentDistanceToCamera += rewindStep;
                currentShapeBaseCoordinate -= primaryShapeStep;
                currentIndex -= 1;
                activeSampling = true;
                sequentialEmptySamples = 0;
            }
        }

        currentIndex++;
    }

    if (meanDistanceDivider <= 0.0f)
        return;

    volumetricRay.invalidRay = false;
    volumetricRay.meanDistance = min(
        volumetricRay.meanDistance / meanDistanceDivider,
        cloudRay.maxRayLength);

    float3 meanPositionPS =
        rayOriginPS + volumetricRay.meanDistance * cloudRay.direction;
    float3 sunColor = _SunColor.xyz * EvaluateSunColorAttenuation(
        meanPositionPS,
        envLighting.sunDirection,
        true);

    volumetricRay.scattering = sunColor * volumetricRay.scattering;
    volumetricRay.scattering +=
        EvaluateCloudFinalAmbient(meanPositionPS, cloudRay.direction)
        * volumetricRay.ambient;
}

#endif
