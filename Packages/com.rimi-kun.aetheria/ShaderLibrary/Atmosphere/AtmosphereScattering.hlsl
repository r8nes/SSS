#ifndef ATMOSPHERE_SCATTERING_INCLUDED
#define ATMOSPHERE_SCATTERING_INCLUDED

#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
#include "AtmosphereCore.hlsl"

#define MAX_LUT_BUILD_LIGHTS ATMOSPHERESHADERCONFIG_MAX_CELESTIAL_LIGHTS

#if defined(BUILDING_MULTI_SCATTER_LUT) || defined(BUILDING_GROUND_IRRADIANCE_LUT) || defined(BUILDING_SKY_VIEW_LUT)
static float3 _LutBuildSunDirs[MAX_LUT_BUILD_LIGHTS];
#endif


#include "Scattering/AtmosphereScatteringIntegration.hlsl"
#include "Scattering/AtmosphereArtistic.hlsl"

#if !defined(ATMOSPHERE_PRECOMPUTE_ONLY)
#include "Scattering/AtmosphereScatteringRuntime.hlsl"
#endif

#endif
