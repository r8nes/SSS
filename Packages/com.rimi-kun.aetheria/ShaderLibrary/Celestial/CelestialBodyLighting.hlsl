#ifndef CELESTIAL_BODY_LIGHTING_INCLUDED
#define CELESTIAL_BODY_LIGHTING_INCLUDED

#if !defined(ATMOSPHERE_PRECOMPUTE_ONLY)
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/GlobalSamplers.hlsl"
#endif
#include "../Atmosphere/AtmosphereShaderBindings.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/GeometricTools.hlsl"

struct CelestialBodyData
{
    float3 color;
    float radius;

    float3 forward;
    float distanceFromCamera;

    float3 right;
    float angularRadius;

    float3 up;
    int type; // 0 = star/emission, 1 = moon/reflected

    float3 surfaceColor;
    float earthshine;

    float4 cookieAtlasScaleOffset;

    float3 sunDirection;
    float limbDarkening;
    float cookieRotation;
    float lunarLambertWeight;
    float padding;

    float3 flareColor;
    float flareFalloff;
    float flareCosInner;
    float flareCosOuter;
    float flareSize;
};

#define MAX_CELESTIAL_BODIES ATMOSPHERESHADERCONFIG_MAX_CELESTIAL_LIGHTS

float4 ATMOSPHERE_CELESTIAL_COLOR_RADIUS[MAX_CELESTIAL_BODIES];
float4 ATMOSPHERE_CELESTIAL_FORWARD_DISTANCE[MAX_CELESTIAL_BODIES];
float4 ATMOSPHERE_CELESTIAL_RIGHT_ANGULAR_RADIUS[MAX_CELESTIAL_BODIES];
float4 ATMOSPHERE_CELESTIAL_UP_TYPE[MAX_CELESTIAL_BODIES];
float4 ATMOSPHERE_CELESTIAL_SURFACE_EARTHSHINE[MAX_CELESTIAL_BODIES];
float4 ATMOSPHERE_CELESTIAL_COOKIE_ATLAS_SCALE_OFFSET[MAX_CELESTIAL_BODIES];
float4 ATMOSPHERE_CELESTIAL_SUN_DIRECTION_LIMB_DARKENING[MAX_CELESTIAL_BODIES];
float4 ATMOSPHERE_CELESTIAL_COOKIE_PARAMS[MAX_CELESTIAL_BODIES];
float4 ATMOSPHERE_CELESTIAL_FLARE_COLOR[MAX_CELESTIAL_BODIES];
float4 ATMOSPHERE_CELESTIAL_FLARE_PARAMS[MAX_CELESTIAL_BODIES];

uint ATMOSPHERE_CELESTIAL_BODY_COUNT;
uint ATMOSPHERE_CELESTIAL_LIGHT_COUNT;

#if !defined(ATMOSPHERE_PRECOMPUTE_ONLY)
TEXTURE2D(ATMOSPHERE_CELESTIAL_COOKIE_ATLAS);
#endif

#define INV_PI 0.31830988618379067154
#define INV_SQRT2 0.70710678118654752440

CelestialBodyData GetCelestialBody(uint index)
{
    CelestialBodyData body;

    float4 colorRadius = ATMOSPHERE_CELESTIAL_COLOR_RADIUS[index];
    float4 forwardDistance = ATMOSPHERE_CELESTIAL_FORWARD_DISTANCE[index];
    float4 rightAngularRadius = ATMOSPHERE_CELESTIAL_RIGHT_ANGULAR_RADIUS[index];
    float4 upType = ATMOSPHERE_CELESTIAL_UP_TYPE[index];
    float4 surfaceEarthshine = ATMOSPHERE_CELESTIAL_SURFACE_EARTHSHINE[index];
    float4 sunDirectionLimbDarkening = ATMOSPHERE_CELESTIAL_SUN_DIRECTION_LIMB_DARKENING[index];
    float4 cookieParams = ATMOSPHERE_CELESTIAL_COOKIE_PARAMS[index];
    float4 flareColor = ATMOSPHERE_CELESTIAL_FLARE_COLOR[index];
    float4 flareParams = ATMOSPHERE_CELESTIAL_FLARE_PARAMS[index];

    body.color = colorRadius.xyz;
    body.radius = colorRadius.w;
    body.forward = forwardDistance.xyz;
    body.distanceFromCamera = forwardDistance.w;
    body.right = rightAngularRadius.xyz;
    body.angularRadius = rightAngularRadius.w;
    body.up = upType.xyz;
    body.type = (int)round(upType.w);
    body.surfaceColor = surfaceEarthshine.xyz;
    body.earthshine = surfaceEarthshine.w;
    body.cookieAtlasScaleOffset = ATMOSPHERE_CELESTIAL_COOKIE_ATLAS_SCALE_OFFSET[index];
    body.sunDirection = sunDirectionLimbDarkening.xyz;
    body.limbDarkening = sunDirectionLimbDarkening.w;
    body.cookieRotation = cookieParams.x;
    body.lunarLambertWeight = cookieParams.y;
    body.padding = cookieParams.z;
    body.flareColor = flareColor.xyz;
    body.flareFalloff = flareColor.w;
    body.flareCosInner = flareParams.x;
    body.flareCosOuter = flareParams.y;
    body.flareSize = flareParams.z;
    return body;
}

bool TryGetMoonSurfaceData(CelestialBodyData moon, float3 V, out float3 N, out float muView, out float muSun, out float phaseCos)
{
    N = 0.0f;
    muView = 0.0f;
    muSun = 0.0f;
    phaseCos = 0.0f;
    bool hitSurface = false;

    float3 moonDir = -moon.forward.xyz;
    float radialDistance = moon.distanceFromCamera;

    float2 t = 0.0f;
    bool hitMoon = IntersectRaySphere(-moonDir * radialDistance, V, moon.radius, t);

    if (hitMoon && t.x >= 0.0f)
    {
        float3 moonCenter = moonDir * radialDistance;
        float3 hitPoint = t.x * V;
        N = normalize(hitPoint - moonCenter);

        muView = saturate(dot(N, -V));
        muSun = saturate(dot(N, -moon.sunDirection));
        phaseCos = clamp(dot(-V, -moon.sunDirection), -1.0f, 1.0f);

        hitSurface = true;
    }

    return hitSurface;
}

float ComputeEarthshine(CelestialBodyData moon);

float ComputeMoonDiskLighting(CelestialBodyData moon, float3 V)
{
    float3 N = 0.0f;
    float muView = 0.0f;
    float muSun = 0.0f;
    float phaseCos = 0.0f;
    float lighting = 0.0f;

    if (TryGetMoonSurfaceData(moon, V, N, muView, muSun, phaseCos))
    {
        // Lunar-Lambert model used in planetary photometry:
        // (1 - L) * Lambert + 2L * Lommel-Seeliger.
        float ls = muSun / max(muSun + muView, 1e-4f);
        float lambert = muSun;
        float L = saturate(moon.lunarLambertWeight);
        float lunarDiffuse = (1.0f - L) * lambert + (2.0f * L) * ls;

        float phaseVisibility = saturate(0.5f * (phaseCos + 1.0f));

        float oppositionMask = smoothstep(0.96f, 1.0f, phaseCos);
        float opposition = 1.0f + 0.10f * oppositionMask * oppositionMask;

        float limb = lerp(1.0f, sqrt(muView), saturate(moon.limbDarkening));
        float reflected = lunarDiffuse * limb * opposition;

        float earthshine = ComputeEarthshine(moon) * pow(saturate(muView), 0.35f) * (1.0f - phaseVisibility);

        lighting = reflected + earthshine;
    }

    return lighting;
}

float ComputeEarthshine(CelestialBodyData moon)
{
    // Cheaper approximation: https://www.desmos.com/calculator/11ny6d5j1b
    float sinPhase = sqrt(max(1.0 - dot(moon.sunDirection, moon.forward), 0.0)) * INV_SQRT2;
    float earthshine = 1.0 - sinPhase * sqrt(sinPhase);

    return earthshine * moon.earthshine;
}

float2 RotateCookiePlane(float2 v, float angleRad)
{
    float s = 0.0f;
    float c = 1.0f;
    sincos(angleRad, s, c);
    return float2(v.x * c - v.y * s, v.x * s + v.y * c);
}

float2 GetCelestialDiskUv(CelestialBodyData body, float3 viewDir)
{
    float2 projection = float2(dot(viewDir, body.right.xyz), dot(viewDir, body.up.xyz));
    projection = RotateCookiePlane(projection, body.cookieRotation);

    float2 angles = float2(asin(clamp(projection.x, -1.0f, 1.0f)),
                           asin(clamp(-projection.y, -1.0f, 1.0f)));
    return angles * rcp(max(body.angularRadius, 1e-6f)) * 0.5f + 0.5f;
}

float4 SampleCelestialCookie(CelestialBodyData body, float3 viewDir)
{
#if defined(ATMOSPHERE_PRECOMPUTE_ONLY)
    return 1.0f;
#else
    float2 localUv = saturate(GetCelestialDiskUv(body, viewDir));
    float2 atlasUv = body.cookieAtlasScaleOffset.zw + localUv * body.cookieAtlasScaleOffset.xy;
    return SAMPLE_TEXTURE2D_LOD(
        ATMOSPHERE_CELESTIAL_COOKIE_ATLAS,
        ATMOSPHERE_CELESTIAL_COOKIE_ATLAS_SAMPLER,
        atlasUv,
        0);
#endif
}

#endif
