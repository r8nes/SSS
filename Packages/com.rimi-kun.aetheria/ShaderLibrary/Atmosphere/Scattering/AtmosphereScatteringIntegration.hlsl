#ifndef ATMOSPHERE_SCATTERING_INTEGRATION_INCLUDED
#define ATMOSPHERE_SCATTERING_INTEGRATION_INCLUDED

struct SingleScatteringResult
{
    float3 L;
    float3 OpticalDepth;
    float3 Transmittance;
    float3 MultiScatAs1;
};

struct AtmospherePathEnd
{
    float pathEnd;
    float tBottom;
    bool valid;
};

float3 GetAtmosphereTransmittanceToSun(float viewHeight, float sunZenithCosAngle)
{
    float2 uv = 0.0f;
    LutTransmittanceParamsToUv(viewHeight, sunZenithCosAngle, uv);
    return SAMPLE_TEXTURE2D_LOD(ATMOSPHERE_TRANSMITTANCE_LUT, ATMOSPHERE_TRANSMITTANCE_LUT_SAMPLER, uv, 0).rgb;
}

SingleScatteringResult IntegrateScatteredLuminance(in float3 viewPos, in float3 viewDir,
                                                   bool ground, float densityScale,
                                                   in float sampleCountIni, in float linearDepth,
                                                   bool useMieRayPhase,
                                                   bool renderSunDiskScattering, float celestialEvalDistance);
SingleScatteringResult IntegrateScatteredLuminanceSegment(in float3 viewPos, in float3 viewDir,
                                                          float densityScale, in float sampleCountIni,
                                                          in float segmentStart, in float segmentEnd,
                                                          bool useMieRayPhase,
                                                          bool renderSunDiskScattering,
                                                          float celestialEvalDistance);
SingleScatteringResult IntegrateResolvedScatteredLuminanceSegment(
    in float3 viewPos,
    in float3 viewDir,
    in float resolvedPathEnd,
    in bool resolvedPathValid,
    float densityScale,
    in float sampleCountIni,
    in float segmentStart,
    in float segmentEnd,
    bool useMieRayPhase,
    bool renderSunDiskScattering,
    float celestialEvalDistance);

float3 RenderTransmittanceLut(float2 uv);

float3 ComputeRGBDirectGroundIrradiance(float pHeight, float sunZenithCosAngle, float nDotL, float3 lightColor)
{
    float3 transmittanceToSun = GetAtmosphereTransmittanceToSun(pHeight, sunZenithCosAngle);
    return lightColor * transmittanceToSun * nDotL;
}

void AccumulateRGBDirectScattering(
    float pHeight, float sunZenithCosAngle, float totalShadow,
    bool useMieRayPhase, float miePhaseValue, float rayleighPhaseValue, float uniformPhase,
    float3 lightColor,
    float3 scatteringRay,
    float3 scatteringMie,
    float3 extinction,
    float3 sampleTransmittance,
    float3 transmittanceAccu,
    float densityScale,
    inout float3 directIntegratedRgb)
{
    float3 transmittanceToSun = GetAtmosphereTransmittanceToSun(pHeight, sunZenithCosAngle);
    float3 phaseTimesScattering = useMieRayPhase
        ? scatteringMie * miePhaseValue + scatteringRay * rayleighPhaseValue
        : (scatteringMie + scatteringRay) * uniformPhase;

    float3 S = totalShadow * transmittanceToSun * phaseTimesScattering;
    float3 Sint = (S - S * sampleTransmittance) / max(extinction, 1e-10f);

    directIntegratedRgb += transmittanceAccu * lightColor * Sint;
}

float GetAtmosphereRaymarchSampleOffset()
{
    return 0.5f;
}

float3 ClampLightDirectionToAngularDisk(float3 lightDir, float3 viewDir, float angularRadius)
{
    float3 clampedLightDir = lightDir;
    float c = dot(lightDir, viewDir);

    if (c > -0.99999f && c < 0.99999f)
    {
        float alpha = angularRadius;
        float beta = acos(c);
        float gamma = min(alpha, beta);
        gamma *= (PI - beta) * rcp(max(PI - gamma, 1e-6f));

        float3 axis = normalize(cross(lightDir, viewDir));

        float sinGamma = 0.0f;
        float cosGamma = 1.0f;

        sincos(gamma, sinGamma, cosGamma);

        clampedLightDir = lightDir * cosGamma
                        + cross(axis, lightDir) * sinGamma
                        + axis * dot(axis, lightDir) * (1.0f - cosGamma);
    }

    return clampedLightDir;
}

void ResolveAtmospherePathEnd(float3 viewPos, float3 viewDir, inout AtmospherePathEnd result)
{
    float3 earthO = float3(0.0f, 0.0f, 0.0f);
    result.pathEnd = 0.0f;

    float2 bottomIntersections = 0.0f;
    bool hitBottom = IntersectRaySphere(viewPos - earthO, viewDir, _BottomRadius, bottomIntersections);
    result.tBottom = hitBottom && bottomIntersections.y >= 0.0f
        ? (bottomIntersections.x >= 0.0f ? bottomIntersections.x : bottomIntersections.y)
        : -1.0f;
    result.valid = false;
    bool pathResolved = false;

    float2 topIntersections = 0.0f;
    bool hitTop = IntersectRaySphere(viewPos - earthO, viewDir, _TopRadius, topIntersections);
    float tTopNear = hitTop && topIntersections.y >= 0.0f
        ? (topIntersections.x >= 0.0f ? topIntersections.x : topIntersections.y)
        : -1.0f;
    float tTopFar = hitTop && topIntersections.y >= 0.0f ? topIntersections.y : -1.0f;
    float tTop = tTopNear;

    float viewHeight = length(viewPos);
    float3 upVectorAtView = viewPos / max(viewHeight, 1e-6f);
    bool onTopBoundary = abs(viewHeight - _TopRadius) <= _PLANET_RADIUS_OFFSET;
    bool startsInsideTop = viewHeight < _TopRadius || (onTopBoundary && dot(viewDir, upVectorAtView) < 0.0f);

    if (startsInsideTop)
        tTop = tTopFar;
    else if (tTopNear < 0.0f)
        tTop = tTopFar;

    if (result.tBottom < 0.0f)
    {
        if (tTop >= 0.0f)
        {
            result.pathEnd = tTop;
            pathResolved = true;
        }
    }
    else if (tTop > 0.0f)
    {
        result.pathEnd = min(tTop, result.tBottom);
        pathResolved = true;
    }
    else
    {
        result.pathEnd = result.tBottom;
        pathResolved = true;
    }

    result.valid = pathResolved;
    result.pathEnd = min(result.pathEnd, 9000000.0f);
}

float ComputeAdaptiveAtmosphereRaymarchSampleCount(
    float3 viewPos,
    float3 viewDir,
    float requestedSampleCount,
    float maxSampleCount)
{
    float minSamples = max(floor(requestedSampleCount), 1.0f);
    float sampleCount = minSamples;

    AtmospherePathEnd path;
    path.pathEnd = 0.0f;
    path.tBottom = -1.0f;
    path.valid = false;
    ResolveAtmospherePathEnd(viewPos, viewDir, path);

    if (path.valid)
    {
        float atmosphereThickness = sqrt(max(_TopRadius * _TopRadius - _BottomRadius * _BottomRadius, 0.0f));
        float normalizedPath = saturate(path.pathEnd / max(atmosphereThickness * 2.0f, 1e-4f));
        float adaptiveMaxSamples = max(floor(maxSampleCount), minSamples);
        sampleCount = ceil(lerp(minSamples, adaptiveMaxSamples, normalizedPath));
    }

    return sampleCount;
}

void AccumulateResolvedScatteringLight(
    float3 P,
    float pHeight,
    float3 upVector,
    float3 wo,
    MediumSampleRGB mediumRgb,
    float3 sampleTransmittanceRgb,
    float3 transmittanceAccu,
    float densityScale,
    bool useMieRayPhase,
    bool renderSunDiskScattering,
    float celestialEvalDistance,
    float uniformPhase,
    float3 sunDir,
    float3 lightColor,
    float angularRadius,
    float distanceFromCamera,
    bool useAngularDisk,
    bool accumulateMultiScatterSource,
    inout float3 directIntegratedRgb,
    inout float3 multiScatterSourceRgb)
{
    if (useAngularDisk &&
        renderSunDiskScattering &&
        asint(angularRadius) != 0 &&
        distanceFromCamera <= celestialEvalDistance)
        sunDir = ClampLightDirectionToAngularDisk(sunDir, wo, angularRadius);

    float cosTheta = dot(sunDir, wo);
    float miePhaseValue = useMieRayPhase ? CornetteShanksPhaseFunction(_MiePhaseG, cosTheta) : uniformPhase;
    float rayleighPhaseValue = useMieRayPhase ? RayleighPhaseFunction(cosTheta) : uniformPhase;
    float sunZenithCosAngle = dot(sunDir, upVector);

    float3 earthO = float3(0.0f, 0.0f, 0.0f);
    float2 earthIntersections = 0.0f;
    bool hitEarth = IntersectRaySphere(
        P - (earthO + _PLANET_RADIUS_OFFSET * upVector),
        sunDir,
        _BottomRadius,
        earthIntersections);
    float tEarth = hitEarth && earthIntersections.y >= 0.0f
        ? (earthIntersections.x >= 0.0f ? earthIntersections.x : earthIntersections.y)
        : -1.0f;
    float earthShadow = tEarth >= 0.0f ? 0.0f : 1.0f;

    if (useAngularDisk && renderSunDiskScattering && asint(angularRadius) != 0)
        earthShadow = ComputeSunPenumbra(pHeight, upVector, sunDir, angularRadius);

    AccumulateRGBDirectScattering(pHeight, sunZenithCosAngle, earthShadow,
        useMieRayPhase, miePhaseValue, rayleighPhaseValue, uniformPhase, lightColor,
        mediumRgb.scatteringRay, mediumRgb.scatteringMie, mediumRgb.extinction,
        sampleTransmittanceRgb, transmittanceAccu, densityScale,
        directIntegratedRgb);

    if (accumulateMultiScatterSource)
    {
        float3 multiScatteredLuminance = GetMultipleScattering(pHeight, sunZenithCosAngle);
        multiScatterSourceRgb += lightColor * multiScatteredLuminance * mediumRgb.scattering;
    }
}

void AccumulateScatteringLights(
    float3 P,
    float pHeight,
    float3 upVector,
    float3 wo,
    MediumSampleRGB mediumRgb,
    float3 sampleTransmittanceRgb,
    float3 transmittanceAccu,
    float densityScale,
    bool useMieRayPhase,
    bool renderSunDiskScattering,
    float celestialEvalDistance,
    float uniformPhase,
    inout float3 directIntegratedRgb,
    inout float3 multiScatterSourceRgb)
{
#if defined(BUILDING_MULTI_SCATTER_LUT) || defined(BUILDING_GROUND_IRRADIANCE_LUT)
    {
        AccumulateResolvedScatteringLight(
            P, pHeight, upVector, wo,
            mediumRgb, sampleTransmittanceRgb, transmittanceAccu, densityScale,
            useMieRayPhase, renderSunDiskScattering, celestialEvalDistance, uniformPhase,
            _LutBuildSunDirs[0], float3(1.0f, 1.0f, 1.0f), 0.0f, 0.0f,
            false,
#if defined(BUILDING_GROUND_IRRADIANCE_LUT)
            true,
#else
            false,
#endif
            directIntegratedRgb, multiScatterSourceRgb);
    }
#else
    [loop]
    for (uint lightIdx = 0; lightIdx < ATMOSPHERE_CELESTIAL_LIGHT_COUNT; ++lightIdx)
    {
        CelestialBodyData light = GetCelestialBody(lightIdx);
#if defined(BUILDING_SKY_VIEW_LUT)
        float3 sunDir = _LutBuildSunDirs[lightIdx];
#else
        float3 sunDir = -light.forward.xyz;
#endif
        AccumulateResolvedScatteringLight(
            P, pHeight, upVector, wo,
            mediumRgb, sampleTransmittanceRgb, transmittanceAccu, densityScale,
            useMieRayPhase, renderSunDiskScattering, celestialEvalDistance, uniformPhase,
            sunDir, light.color.rgb, light.angularRadius, light.distanceFromCamera,
            true, true,
            directIntegratedRgb, multiScatterSourceRgb);
    }
#endif
}

void AccumulateScatteringStep(
    float3 viewDir,
    float3 P,
    float dt,
    float densityScale,
    bool useMieRayPhase,
    bool renderSunDiskScattering,
    float celestialEvalDistance,
    float uniformPhase,
    inout SingleScatteringResult result,
    inout float3 luminance,
    inout float3 opticalDepth,
    inout float3 transmittanceAccu)
{
    float pHeight = length(P);
    MediumSampleRGB mediumRgb = SampleMediumRGBAtHeight(pHeight);

    float3 sampleOpticalDepthRgb = mediumRgb.extinction * dt * densityScale;
    float3 sampleTransmittanceRgb = TransmittanceFromOpticalDepth(sampleOpticalDepthRgb);
    opticalDepth += sampleOpticalDepthRgb;

    float3 upVector = P / max(pHeight, 1e-6f);

    float3 directIntegratedRgb = 0.0f;
    float3 multiScatterSourceRgb = 0.0f;

    AccumulateScatteringLights(
        P, pHeight, upVector, viewDir,
        mediumRgb, sampleTransmittanceRgb, transmittanceAccu, densityScale,
        useMieRayPhase, renderSunDiskScattering, celestialEvalDistance, uniformPhase,
        directIntegratedRgb, multiScatterSourceRgb);

    float3 safeExtinctionMs1 = max(mediumRgb.extinction, 1e-10f);
    result.MultiScatAs1 += transmittanceAccu * mediumRgb.scattering * (1.0f - sampleTransmittanceRgb) / safeExtinctionMs1;

    float3 safeExtinctionRgb = max(mediumRgb.extinction, 1e-10f);
    float3 multiScatterIntegratedRgb = (multiScatterSourceRgb - multiScatterSourceRgb * sampleTransmittanceRgb) / safeExtinctionRgb;
    luminance += directIntegratedRgb;
    luminance += transmittanceAccu * multiScatterIntegratedRgb;

    transmittanceAccu *= sampleTransmittanceRgb;
}

SingleScatteringResult IntegrateScatteredLuminance(in float3 viewPos, in float3 viewDir,
                                                   bool ground, float densityScale,
                                                   in float sampleCountIni, in float linearDepth,
                                                   bool useMieRayPhase,
                                                   bool renderSunDiskScattering, float celestialEvalDistance)
{
    SingleScatteringResult result;
    result.L = 0.0f;
    result.OpticalDepth = 0.0f;
    result.Transmittance = 0.0f;
    result.MultiScatAs1 = 0.0f;

    AtmospherePathEnd path;
    path.pathEnd = 0.0f;
    path.tBottom = -1.0f;
    path.valid = false;
    ResolveAtmospherePathEnd(viewPos, viewDir, path);
    float tMax = path.pathEnd;

    if (path.valid && linearDepth >= 0.0f)
        tMax = min(tMax, linearDepth);

    float sampleCount = max(floor(sampleCountIni), 1.0f);
    const float uniformPhase = 1.0f / (4.0f * PI);

    float3 luminance = 0.0f;
    float3 opticalDepth = 0.0f;

    float3 transmittanceAccu = 1.0f;

    if (path.valid)
    {
        for (float s = 0.0f; s < sampleCount; s += 1.0f)
        {
            float sampleSegmentT = GetAtmosphereRaymarchSampleOffset();

            float t0 = s / sampleCount;
            float t1 = (s + 1.0f) / sampleCount;

            t0 = t0 * t0 * tMax;
            t1 = t1 * t1 * tMax;

            float t = lerp(t0, t1, sampleSegmentT);
            float dt = t1 - t0;

            float3 P = viewPos + t * viewDir;
            
            AccumulateScatteringStep(
                viewDir, P, dt, densityScale,
                useMieRayPhase, renderSunDiskScattering, celestialEvalDistance, uniformPhase,
                result, luminance, opticalDepth, transmittanceAccu);
        }
    }

    float3 transmittanceRgb = transmittanceAccu;

    if (path.valid && ground && tMax == path.tBottom && path.tBottom > 0.0f)
    {
        float3 P = viewPos + path.tBottom * viewDir;
        float pHeight = length(P);
        float3 upVector = P / pHeight;
        float3 totalGroundIrradiance = 0.0f;

        #ifdef BUILDING_MULTI_SCATTER_LUT
        {
            float3 sunDir = _LutBuildSunDirs[0];
            float sunZenithCosAngle = dot(sunDir, upVector);
            float nDotL = saturate(dot(normalize(upVector), normalize(sunDir)));

            float3 directIrradiance = ComputeRGBDirectGroundIrradiance(pHeight, sunZenithCosAngle, nDotL, 1.0f);
            totalGroundIrradiance += directIrradiance;
        }
        #else
        [loop]
        for (uint groundLightIdx = 0; groundLightIdx < ATMOSPHERE_CELESTIAL_LIGHT_COUNT; ++groundLightIdx)
        {
            CelestialBodyData light = GetCelestialBody(groundLightIdx);
            #if defined(BUILDING_SKY_VIEW_LUT)
            float3 sunDir = _LutBuildSunDirs[groundLightIdx];
            #else
            float3 sunDir = -light.forward.xyz;
            #endif
            float3 lightColor = light.color.rgb;
            float sunZenithCosAngle = dot(sunDir, upVector);
            float nDotL = saturate(dot(normalize(upVector), normalize(sunDir)));

            float3 directIrradiance = ComputeRGBDirectGroundIrradiance(
                pHeight, sunZenithCosAngle, nDotL, lightColor);
            float3 skyIrradianceAtGround = SampleGroundIrradiance(pHeight, sunZenithCosAngle) * lightColor;
            totalGroundIrradiance += directIrradiance + skyIrradianceAtGround;
        }
        #endif

        luminance += transmittanceRgb * totalGroundIrradiance * _GroundAlbedo / PI;
    }

    if (path.valid)
    {
        result.L = max(luminance, 0.0f);
        result.OpticalDepth = max(opticalDepth, 0.0f);
        result.Transmittance = max(transmittanceRgb, 0.0f);
        result.MultiScatAs1 = max(result.MultiScatAs1, 0.0f);
    }

    return result;
}

SingleScatteringResult IntegrateResolvedScatteredLuminanceSegment(
    in float3 viewPos,
    in float3 viewDir,
    in float resolvedPathEnd,
    in bool resolvedPathValid,
    float densityScale,
    in float sampleCountIni,
    in float segmentStart,
    in float segmentEnd,
    bool useMieRayPhase,
    bool renderSunDiskScattering,
    float celestialEvalDistance)
{
    SingleScatteringResult result;
    result.L = 0.0f;
    result.OpticalDepth = 0.0f;
    result.Transmittance = 1.0f;
    result.MultiScatAs1 = 0.0f;

    float tStart = max(segmentStart, 0.0f);
    float tEnd = min(max(segmentEnd, tStart), resolvedPathEnd);
    bool integrateSegment = resolvedPathValid && tEnd > tStart;

    float sampleCount = max(floor(sampleCountIni), 1.0f);
    const float uniformPhase = 1.0f / (4.0f * PI);

    float3 luminance = 0.0f;
    float3 opticalDepth = 0.0f;
    float3 transmittanceAccu = 1.0f;

    if (integrateSegment)
    {
        for (float s = 0.0f; s < sampleCount; s += 1.0f)
        {
            float t0 = lerp(tStart, tEnd, s / sampleCount);
            float t1 = lerp(tStart, tEnd, (s + 1.0f) / sampleCount);
            float t = lerp(t0, t1, GetAtmosphereRaymarchSampleOffset());
            float dt = t1 - t0;
            float3 P = viewPos + t * viewDir;

            AccumulateScatteringStep(
                viewDir, P, dt, densityScale,
                useMieRayPhase, renderSunDiskScattering, celestialEvalDistance, uniformPhase,
                result, luminance, opticalDepth, transmittanceAccu);
        }

        result.L = max(luminance, 0.0f);
        result.OpticalDepth = max(opticalDepth, 0.0f);
        result.Transmittance = max(transmittanceAccu, 0.0f);
        result.MultiScatAs1 = max(result.MultiScatAs1, 0.0f);
    }

    return result;
}

SingleScatteringResult IntegrateScatteredLuminanceSegment(
    in float3 viewPos,
    in float3 viewDir,
    float densityScale,
    in float sampleCountIni,
    in float segmentStart,
    in float segmentEnd,
    bool useMieRayPhase,
    bool renderSunDiskScattering,
    float celestialEvalDistance)
{
    AtmospherePathEnd path;
    path.pathEnd = 0.0f;
    path.tBottom = -1.0f;
    path.valid = false;
    ResolveAtmospherePathEnd(viewPos, viewDir, path);

    return IntegrateResolvedScatteredLuminanceSegment(
        viewPos,
        viewDir,
        path.pathEnd,
        path.valid,
        densityScale,
        sampleCountIni,
        segmentStart,
        segmentEnd,
        useMieRayPhase,
        renderSunDiskScattering,
        celestialEvalDistance);
}

float3 RenderTransmittanceLut(float2 uv)
{
    float3 transmittance = 1.0f;
    float viewHeight = _BottomRadius;
    float viewZenithCosAngle = 1.0f;

    UvToLutTransmittanceParams(viewHeight, viewZenithCosAngle, uv);

    float3 viewPos = float3(0.0f, viewHeight, 0.0f);
    float sinTheta = SinFromCos(viewZenithCosAngle);
    float3 viewDir = float3(sinTheta, viewZenithCosAngle, 0.0f);

    float2 topIntersections = 0.0f;
    bool hitTop = IntersectRaySphere(viewPos, viewDir, _TopRadius, topIntersections);
    float tTop = hitTop && topIntersections.y >= 0.0f
        ? (topIntersections.x >= 0.0f ? topIntersections.x : topIntersections.y)
        : -1.0f;

    float2 bottomIntersections = 0.0f;
    bool hitBottom = IntersectRaySphere(viewPos, viewDir, _BottomRadius, bottomIntersections);
    float tBottom = hitBottom && bottomIntersections.y >= 0.0f
        ? (bottomIntersections.x >= 0.0f ? bottomIntersections.x : bottomIntersections.y)
        : -1.0f;

    if (tBottom >= 0.0f && tBottom < tTop)
    {
        transmittance = 0.0f;
    }
    else if (tTop >= 0.0f)
    {
        const int SAMPLE_COUNT = 40;

        float dt = tTop / (float)SAMPLE_COUNT;
        float3 opticalDepth = 0.0f;

        for (int i = 0; i < SAMPLE_COUNT; ++i)
        {
            float t = ((float)i + 0.5f) * dt;
            float3 P = viewPos + t * viewDir;
            MediumSampleRGB medium = SampleMediumRGB(P);
            opticalDepth += medium.extinction * dt;
        }

        transmittance = max(TransmittanceFromOpticalDepth(opticalDepth), 0.0f);
    }

    return transmittance;
}

#endif