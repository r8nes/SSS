#ifndef ATMOSPHERE_SCATTERING_RUNTIME_INCLUDED
#define ATMOSPHERE_SCATTERING_RUNTIME_INCLUDED

#include "../OuterSpace.hlsl"


bool ResolveGroundIntersection(float3 viewPos, float3 viewDir, out float tGround)
{
    float2 intersections = 0.0f;
    bool hit = IntersectRaySphere(viewPos, viewDir, _BottomRadius, intersections);
    tGround = hit && intersections.y >= 0.0f
        ? (intersections.x >= 0.0f ? intersections.x : intersections.y)
        : -1.0f;
    return tGround >= 0.0f;
}

float ComputeSpaceRaymarchSampleCount(float3 viewPos, float3 viewDir, bool intersectGround)
{
    float tGround = -1.0f;
    if (intersectGround)
        ResolveGroundIntersection(viewPos, viewDir, tGround);

    float2 topIntersections = 0.0f;
    bool hitTop = IntersectRaySphere(viewPos, viewDir, _TopRadius, topIntersections);
    float tTopExit = hitTop && topIntersections.y >= 0.0f ? topIntersections.y : -1.0f;

    float segmentLength = tTopExit > 0.0f ? tTopExit : 0.0f;
    if (intersectGround && tGround > 0.0f)
        segmentLength = min(segmentLength, tGround);

    float h = sqrt(max(_TopRadius * _TopRadius - _BottomRadius * _BottomRadius, 0.0f));
    float maxTraversalLength = max(2.0f * h, 1e-5f);
    float normalizedTraversal = saturate(segmentLength / maxTraversalLength);
    float sampleCount = ceil(max(8.0f, _SpaceRaymarchSamples * normalizedTraversal));
    return max(sampleCount, 1.0f);
}

#if !defined(BUILDING_SKY_VIEW_LUT) && !defined(BUILDING_MULTI_SCATTER_LUT)
#endif

float3 RenderSkyViewLut(float2 uv)
{
    float3 skyRadiance = 0.0f;
    bool intersectGround = uv.y >= 0.5f;

    float viewHeight = _BottomRadius;

    float3 upVector = float3(0.0f, 1.0f, 0.0f);
    float3 viewPos = upVector * viewHeight;

    TransformToEarthSpace(_CameraPosForAtmosphere, _BottomRadius, viewPos, viewHeight, upVector);

    float viewZenithCosAngle = 1.0f;
    float lightViewCosAngle = 0.0f;

    UvToSkyViewLutParams(viewZenithCosAngle, lightViewCosAngle, viewHeight, uv);

#ifdef BUILDING_SKY_VIEW_LUT
    viewPos = float3(0.0, viewHeight, 0.0);
    float3 viewDir = ReconstructCanonicalViewDirectionFromLutParams(viewZenithCosAngle, lightViewCosAngle);
#else
    float3 viewDir = ReconstructViewDirectionFromLutParams(viewZenithCosAngle, lightViewCosAngle, upVector);
#endif
#ifdef BUILDING_SKY_VIEW_LUT
    {
        float3 canonForward = float3(0.0f, 0.0f, 1.0f);
        float3 canonSide = float3(1.0f, 0.0f, 0.0f);
        GetCelestialLightReferenceFrame(upVector, canonForward, canonSide);

        for (uint li = 0; li < ATMOSPHERE_CELESTIAL_LIGHT_COUNT && li < MAX_LUT_BUILD_LIGHTS; ++li)
        {
            float3 Li = -GetCelestialBody(li).forward.xyz;
            _LutBuildSunDirs[li] = normalize(float3(dot(Li, canonSide),
                                                    dot(Li, upVector),
                                                    dot(Li, canonForward)));
        }
    }
#endif
    if (MoveToTopAtmosphere(viewPos, viewDir, _TopRadius))
    {
        float raymarchSampleCount = ComputeAdaptiveAtmosphereRaymarchSampleCount(
            viewPos,
            viewDir,
            (float)_PerPixelRaymarchSamples,
            64.0f);

        SingleScatteringResult ss = IntegrateScatteredLuminance(viewPos, viewDir,
                                                                intersectGround, 1.0f,
                                                                raymarchSampleCount, -1.0f,
                                                                true, !intersectGround, 1e30f);

        skyRadiance = ss.L;
    }

    return skyRadiance;
}

float EvaluateDiskCoverage(float3 viewDir, float3 L, float angularRadius, out float mu)
{
    float baseRadius = max(sin(angularRadius), 1e-6f);
    float centerCosine = clamp(dot(viewDir, L), -1.0f, 1.0f);
    float normalizedRadius = SinFromCos(centerCosine) / baseRadius;

    float aa = 1e-3f;
    float mask = 1.0f - smoothstep(1.0f - aa, 1.0f + aa, normalizedRadius);
    mu = SinFromCos(normalizedRadius);
    return mask;
}

float3 EvaluateSunDiskLimbDarkened(
    float3 viewDir,
    float3 sunDir,
    float angularRadius,
    float3 surfaceColor,
    float limbDarkening)
{
    const float limbA = 0.5442f;
    const float limbB = 0.1414f;

    float mu = 0.0f;
    float mask = EvaluateDiskCoverage(viewDir, sunDir, angularRadius, mu);
    float limb = saturate(1.0f - limbDarkening * limbA * (1.0f - mu)
                            - limbDarkening * limbB * (1.0f - mu) * (1.0f - mu));

    return surfaceColor * mask * limb;
}

float3 RenderSunDisk(inout float tFrag, float tExit, float3 viewDir)
{
    float3 radiance = 0.0f;

    [loop]
    for (uint i = 0; i < ATMOSPHERE_CELESTIAL_BODY_COUNT; ++i)
    {
        CelestialBodyData body = GetCelestialBody(i);
        float bodyDist = max(body.distanceFromCamera, tExit);

        if (asint(body.angularRadius) != 0 && bodyDist < tFrag)
        {
            float3 L = -body.forward.xyz;
            float flareSize = body.type == 0 ? max(body.flareSize, 0.0f) : 0.0f;
            float angularCull = body.angularRadius + max(flareSize, 0.01f);

            if (dot(L, viewDir) < cos(angularCull))
                continue;

            float lDotV = dot(L, viewDir);

            if (body.type == 0)
            {
                if (lDotV >= body.flareCosInner)
                {
                    tFrag = bodyDist;
                    float4 cookie = SampleCelestialCookie(body, viewDir);
                    radiance = EvaluateSunDiskLimbDarkened(viewDir, L, body.angularRadius,
                        body.surfaceColor.rgb * cookie.rgb, body.limbDarkening) * cookie.a;
                }
                else if (flareSize > 0.0f && lDotV >= body.flareCosOuter)
                {
                    float rad = acos(clamp(lDotV, -1.0f, 1.0f));
                    float r = max(0.0f, rad - body.angularRadius);
                    float w = saturate(1.0f - r / max(flareSize, 1e-6f));
                    radiance += body.flareColor * pow(w, max(body.flareFalloff, 1e-4f));
                }
            }
            else
            {
                tFrag = bodyDist;
                float4 cookie = SampleCelestialCookie(body, viewDir);
                float mu = 0.0f;
                float diskMask = EvaluateDiskCoverage(viewDir, L, body.angularRadius, mu);
                float moonLighting = ComputeMoonDiskLighting(body, viewDir);
                radiance = body.surfaceColor * cookie.rgb * (moonLighting * INV_PI) * diskMask * cookie.a;
            }
        }
    }

    return radiance;
}

float3 RenderCelestialDisk(uint index, inout float tFrag, float tExit, float3 viewDir)
{
    if (index >= ATMOSPHERE_CELESTIAL_BODY_COUNT)
        return 0.0f;

    CelestialBodyData body = GetCelestialBody(index);
    float bodyDist = max(body.distanceFromCamera, tExit);
    if (asint(body.angularRadius) == 0 || bodyDist >= tFrag)
        return 0.0f;

    float3 L = -body.forward.xyz;
    float flareSize = body.type == 0 ? max(body.flareSize, 0.0f) : 0.0f;
    float angularCull = body.angularRadius + max(flareSize, 0.01f);
    float lDotV = dot(L, viewDir);
    if (lDotV < cos(angularCull))
        return 0.0f;

    if (body.type == 0)
    {
        if (lDotV >= body.flareCosInner)
        {
            tFrag = bodyDist;
            float4 cookie = SampleCelestialCookie(body, viewDir);
            return EvaluateSunDiskLimbDarkened(viewDir, L, body.angularRadius,
                body.surfaceColor.rgb * cookie.rgb, body.limbDarkening) * cookie.a;
        }

        if (flareSize > 0.0f && lDotV >= body.flareCosOuter)
        {
            float rad = acos(clamp(lDotV, -1.0f, 1.0f));
            float r = max(0.0f, rad - body.angularRadius);
            float w = saturate(1.0f - r / max(flareSize, 1e-6f));
            return body.flareColor * pow(w, max(body.flareFalloff, 1e-4f));
        }

        return 0.0f;
    }

    tFrag = bodyDist;
    float4 moonCookie = SampleCelestialCookie(body, viewDir);
    float mu = 0.0f;
    float diskMask = EvaluateDiskCoverage(viewDir, L, body.angularRadius, mu);
    return body.surfaceColor * moonCookie.rgb *
           (ComputeMoonDiskLighting(body, viewDir) * INV_PI) * diskMask * moonCookie.a;
}

void ResolveAtmosphereViewContext(
    float3 viewDirWS,
    out float viewHeight,
    out float3 upVector,
    out float3 viewPos,
    out float3 spaceOuterRadiance)
{
    viewHeight = _BottomRadius;
    upVector = float3(0.0f, 1.0f, 0.0f);
    viewPos = upVector * viewHeight;
    spaceOuterRadiance = 0.0f;

    TransformToEarthSpace(_CameraPosForAtmosphere, _BottomRadius, viewPos, viewHeight, upVector);
    spaceOuterRadiance = SampleSpaceOuterRadiance(viewDirWS);
}


float3 RenderSkyForEnvironment(
    float3 viewDirWS,
    out float3 excludedDiffuseRadiance,
    bool renderSunDiskInSky,
    float artisticMix)
{
    float3 result = 0.0f;
    excludedDiffuseRadiance = 0.0f;
    bool hasSpace = IsSpaceEnabled();
    float viewHeight = _BottomRadius;
    float3 upVector = float3(0.0f, 1.0f, 0.0f);
    float3 viewPos = upVector * viewHeight;
    float3 spaceOuterRadiance = 0.0f;
    ResolveAtmosphereViewContext(viewDirWS, viewHeight, upVector, viewPos, spaceOuterRadiance);

#if defined(ATMOSPHERE_SKIP_CELESTIAL_DISKS)
    renderSunDiskInSky = false;
#endif

    if (viewHeight >= _TopRadius)
    {
        float3 spaceViewPos = viewPos;

        float tGround = -1.0f;
        bool intersectGround = ResolveGroundIntersection(spaceViewPos, viewDirWS, tGround);

        float2 atmosphereIntersections = 0.0f;
        bool hitAtmosphere = IntersectRaySphere(spaceViewPos, viewDirWS, _TopRadius, atmosphereIntersections);
        float tAtmEntry = hitAtmosphere && atmosphereIntersections.y >= 0.0f
            ? (atmosphereIntersections.x >= 0.0f ? atmosphereIntersections.x : atmosphereIntersections.y)
            : -1.0f;
        float tFrag = 1e30f;
        float3 sunDisk = 0.0f;

        if (renderSunDiskInSky && ATMOSPHERE_CELESTIAL_BODY_COUNT > 0u)
            sunDisk = RenderSunDisk(tFrag, max(tAtmEntry, 0.0f), viewDirWS);

        if (!MoveToTopAtmosphere(spaceViewPos, viewDirWS, _TopRadius))
        {
            result = max(sunDisk + spaceOuterRadiance, 0.0f) * _IntensityMultiplier;
            excludedDiffuseRadiance = result;
        }
        else
        {
            SingleScatteringResult ss = IntegrateScatteredLuminance(
                spaceViewPos, viewDirWS,
                intersectGround,
                1.0f,
                ComputeSpaceRaymarchSampleCount(spaceViewPos, viewDirWS, intersectGround),
                -1.0f,
                true,
                !intersectGround,
                1e30f
            );

            float3 col = ss.L;

            if (hasSpace && !intersectGround)
            {
                float3 attenuatedSpaceOuter = spaceOuterRadiance * ss.Transmittance;
                float visibility = ComputeSpaceVisibility(ss.L, attenuatedSpaceOuter);
                float3 spaceContribution = attenuatedSpaceOuter * visibility;
                col += spaceContribution;
                excludedDiffuseRadiance += spaceContribution;
            }

            if (any(sunDisk > 0.0f) && (tGround < 0.0f || tFrag < tGround))
            {
                float3 diskContribution = sunDisk * ss.Transmittance;
                col += diskContribution;
                excludedDiffuseRadiance += diskContribution;
            }

            result = max(col, 0.0f) * _IntensityMultiplier;
            excludedDiffuseRadiance *= _IntensityMultiplier;
        }
    }
    else
    {
        float viewZenithCosAngle = 1.0f;
        float lightViewCosAngle = 0.0f;

        GetViewDirectionLutParams(viewDirWS, upVector, viewZenithCosAngle, lightViewCosAngle);

        float tGround = -1.0f;
        bool intersectGround = ResolveGroundIntersection(viewPos, viewDirWS, tGround);

        float2 uv = 0.0f;

        SkyViewLutParamsToUv(intersectGround, viewZenithCosAngle, lightViewCosAngle, viewHeight, uv);

        float3 pureAtmCol = SAMPLE_TEXTURE2D_LOD(ATMOSPHERE_SKY_VIEW_LUT, ATMOSPHERE_SKY_VIEW_LUT_SAMPLER, uv, 0).rgb;
        float3 col = pureAtmCol;

        float3 spaceColForCelestial = 0.0f;
        float3 spaceContribution = 0.0f;

        if (hasSpace && !intersectGround)
        {
            float2 transUv = 0.0f;

            LutTransmittanceParamsToUv(viewHeight, viewZenithCosAngle, transUv);

            float3 transmittanceToSpace = SAMPLE_TEXTURE2D_LOD(ATMOSPHERE_TRANSMITTANCE_LUT, ATMOSPHERE_TRANSMITTANCE_LUT_SAMPLER, transUv, 0).rgb;
            float3 attenuatedSpaceOuter = spaceOuterRadiance * transmittanceToSpace;

            float visibility = ComputeSpaceVisibility(pureAtmCol, attenuatedSpaceOuter);

            spaceContribution = attenuatedSpaceOuter * visibility;
            col += spaceContribution;

            spaceColForCelestial = spaceOuterRadiance * visibility;
        }

        float3 sunDisk = 0.0f;
        float tFrag = 1e30f;

        if (renderSunDiskInSky && ATMOSPHERE_CELESTIAL_BODY_COUNT > 0u)
        {
            float2 atmosphereIntersections = 0.0f;
            bool hitAtmosphere = IntersectRaySphere(viewPos, viewDirWS, _TopRadius, atmosphereIntersections);
            float tExit = hitAtmosphere && atmosphereIntersections.y >= 0.0f
                ? (atmosphereIntersections.x >= 0.0f ? atmosphereIntersections.x : atmosphereIntersections.y)
                : -1.0f;
            sunDisk = RenderSunDisk(tFrag, tExit, viewDirWS);
        }

        if (any(sunDisk > 0.0f) && (tGround < 0.0f || tFrag < tGround))
        {
            float3 celestialRadiance = sunDisk + spaceColForCelestial;
            float2 transUv = 0.0f;

            LutTransmittanceParamsToUv(viewHeight, viewZenithCosAngle, transUv);

            float3 transmittanceToSky = SAMPLE_TEXTURE2D_LOD(ATMOSPHERE_TRANSMITTANCE_LUT, ATMOSPHERE_TRANSMITTANCE_LUT_SAMPLER, transUv, 0).rgb;

            col = pureAtmCol + celestialRadiance * transmittanceToSky;
            excludedDiffuseRadiance = celestialRadiance * transmittanceToSky;
        }
        else
            excludedDiffuseRadiance = spaceContribution;

        result = max(col, 0.0f) * _IntensityMultiplier;
        excludedDiffuseRadiance *= _IntensityMultiplier;
    }

    float3 artisticResult = ApplyArtisticSky(result, viewDirWS, upVector);
    return lerp(result, artisticResult, artisticMix);
}

float3 RenderSkyForEnvironment(float3 viewDirWS, out float3 excludedDiffuseRadiance, bool renderSunDiskInSky)
{
    return RenderSkyForEnvironment(
        viewDirWS,
        excludedDiffuseRadiance,
        renderSunDiskInSky,
        _SkyArtisticAffectEnvironment);
}

float3 RenderSkyForEnvironment(float3 viewDirWS, out float3 excludedDiffuseRadiance)
{
    return RenderSkyForEnvironment(viewDirWS, excludedDiffuseRadiance, true);
}

float3 RenderSky(float3 viewDirWS)
{
    float3 excludedDiffuseRadiance;
    float3 physicalSky = RenderSkyForEnvironment(
        viewDirWS,
        excludedDiffuseRadiance,
        true,
        0.0f);
    float viewHeight;
    float3 upVector;
    float3 viewPos;
    float3 spaceOuterRadiance;
    ResolveAtmosphereViewContext(viewDirWS, viewHeight, upVector, viewPos, spaceOuterRadiance);
    return ApplyArtisticSky(physicalSky, viewDirWS, upVector);
}

#endif
