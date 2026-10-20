#ifndef SURFACE_MEDIUM_INCLUDED
#define SURFACE_MEDIUM_INCLUDED

#if defined(SURFACE_MEDIUM_FOG)
#include "../Fog/Fog.hlsl"
#endif
#if defined(SURFACE_MEDIUM_ATMOSPHERE)
#include "../Atmosphere/SceneComposite.hlsl"
#endif

struct SurfaceMediumComposite
{
    float3 additiveRadiance;
    float transmittance;
};

SurfaceMediumComposite IdentitySurfaceMedium()
{
    SurfaceMediumComposite medium;
    medium.additiveRadiance = 0.0f;
    medium.transmittance = 1.0f;
    return medium;
}

SurfaceMediumComposite EvaluateSurfaceAtmosphereMedium(
    float2 screenUv,
    float rawDepth,
    int enabled)
{
    SurfaceMediumComposite medium = IdentitySurfaceMedium();
#if defined(SURFACE_MEDIUM_ATMOSPHERE)
    if (enabled != 0 && !IsFarRawDepth(rawDepth))
    {
        SceneCompositeCoefficients atmosphere =
            EvaluateOpaqueSceneCompositeCoefficients(screenUv, rawDepth);
        medium.additiveRadiance = atmosphere.additiveRadiance;
        medium.transmittance = atmosphere.transmittance;
    }
#endif
    return medium;
}

SurfaceMediumComposite EvaluateSurfaceFogMedium(
    float2 screenUv,
    float rawDepth,
    int enabled,
    int skyColorEnabled,
    int volumetricFogEnabled)
{
    SurfaceMediumComposite medium = IdentitySurfaceMedium();
#if defined(SURFACE_MEDIUM_FOG)
    if (enabled != 0)
    {
        FogComposite fog = EvaluateFogRuntime(
            rawDepth,
            screenUv,
            skyColorEnabled != 0,
            volumetricFogEnabled != 0);
        medium.additiveRadiance = fog.radiance;
        medium.transmittance = 1.0f - fog.opacity;
    }
#endif
    return medium;
}

SurfaceMediumComposite EvaluateSurfaceMedium(
    float2 screenUv,
    float rawDepth,
    int atmosphereEnabled,
    int fogEnabled,
    int fogSkyColorEnabled,
    int volumetricFogEnabled)
{
    SurfaceMediumComposite atmosphere = EvaluateSurfaceAtmosphereMedium(
        screenUv,
        rawDepth,
        atmosphereEnabled);
    SurfaceMediumComposite fog = EvaluateSurfaceFogMedium(
        screenUv,
        rawDepth,
        fogEnabled,
        fogSkyColorEnabled,
        volumetricFogEnabled);

    SurfaceMediumComposite medium;
    medium.additiveRadiance =
        fog.additiveRadiance + atmosphere.additiveRadiance * fog.transmittance;
    medium.transmittance = atmosphere.transmittance * fog.transmittance;
    return medium;
}

#endif
