
#ifndef WATERDEFINITIONS_CS_HLSL
#define WATERDEFINITIONS_CS_HLSL

#define WATERDEBUGMODE_NONE (0)
#define WATERDEBUGMODE_SIMULATION_MASK (1)
#define WATERDEBUGMODE_CURRENT (2)
#define WATERDEBUGMODE_DISPLACEMENT (3)
#define WATERDEBUGMODE_SHADING_NORMAL (4)
#define WATERDEBUGMODE_GEOMETRY_NORMAL (5)
#define WATERDEBUGMODE_CASCADE_CONTRIBUTION (6)
#define WATERDEBUGMODE_FOAM (7)
#define WATERDEBUGMODE_BREAKING (8)

#define WATERSIMULATIONMASKDEBUGMODE_RED_CHANNEL (0)
#define WATERSIMULATIONMASKDEBUGMODE_GREEN_CHANNEL (1)
#define WATERSIMULATIONMASKDEBUGMODE_BLUE_CHANNEL (2)

#define WATERSHADERLIMITS_CASCADE_COUNT (3)
#define WATERSHADERLIMITS_MAX_LOD_COUNT (15)

#define WATERRENDERMODE_PHYSICAL (0)
#define WATERRENDERMODE_DEBUG (1)

struct WaterSurfaceProfile
{
    float3 upDirection;
    float maxRefractionDistance;
    float3 extinction;
    float environmentPerceptualRoughness;
    float3 scatteringAlbedo;
    float scatteringAlbedoPadding;
    float3 ambientLighting;
    float causticsDirectionalShadowDimmer;
    float4 probeLightingFadeParameters;
    float3 foamColor;
    float foamSmoothness;
    float3 deepFoamColor;
    float deepFoamWeight;
    float3 bubbleFoamColor;
    float bubbleFoamWeight;
    float nearSmoothness;
    float farRoughness;
    float smoothnessFadeStart;
    float smoothnessFadeDistance;
    float directLightTipScattering;
    float directLightBodyScattering;
    uint renderMode;
    float surfaceLevel;
    float4 surfaceColorParameters; 
    float4 shallowSurfaceColor;
    float4 deepSurfaceColor;
    float4 shorelineSmoothnessParameters; 
    float4 reflectionParameters;
    float4 causticsAppearanceParameters;
    float4 causticsProjectionParameters;
};

#endif
