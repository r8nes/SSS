#ifndef UNIVERSAL_SHADOWS_INCLUDED
#define UNIVERSAL_SHADOWS_INCLUDED

#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Shadow/ShadowSamplingTent.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/GlobalSamplers.hlsl"
#include "Core.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/CommonMaterial.hlsl"
#include "Shadows.deprecated.hlsl"
#include "ContactShadows.hlsl"

#if defined(_CONTACT_SHADOW_RECEIVER_OFF) && !defined(LIGHT_EVALUATION_NO_CONTACT_SHADOWS)
    #define LIGHT_EVALUATION_NO_CONTACT_SHADOWS
#elif defined(_CONTACT_SHADOW_RECEIVER_FORCE) && !defined(LIGHT_EVALUATION_CONTACT_SHADOW_DISABLE_NDOTL)
    #define LIGHT_EVALUATION_CONTACT_SHADOW_DISABLE_NDOTL
#endif

#ifndef CONTACT_SHADOW_RECEIVER_DEFAULT
#define CONTACT_SHADOW_RECEIVER_DEFAULT 0u
#define CONTACT_SHADOW_RECEIVER_OFF     1u
#define CONTACT_SHADOW_RECEIVER_FORCE   2u
#endif

#if defined(LIGHT_EVALUATION_NO_CONTACT_SHADOWS)
    #define MATERIAL_CONTACT_SHADOW_RECEIVER_MODE CONTACT_SHADOW_RECEIVER_OFF
#elif defined(LIGHT_EVALUATION_CONTACT_SHADOW_DISABLE_NDOTL)
    #define MATERIAL_CONTACT_SHADOW_RECEIVER_MODE CONTACT_SHADOW_RECEIVER_FORCE
#else
    #define MATERIAL_CONTACT_SHADOW_RECEIVER_MODE CONTACT_SHADOW_RECEIVER_DEFAULT
#endif

#define MAX_SHADOW_CASCADES 4

// HDRP uses a small fixed comparison bias in addition to raster slope-scale bias.
// HDRP targets reversed-Z; preserve the same light-relative direction on non-reversed platforms.
#if UNITY_REVERSED_Z
    #define MAIN_LIGHT_FIXED_UNIFORM_BIAS (1.0f / 65536.0f)
#else
    #define MAIN_LIGHT_FIXED_UNIFORM_BIAS (-1.0f / 65536.0f)
#endif

#if !defined(_RECEIVE_SHADOWS_OFF)
    #if defined(_MAIN_LIGHT_SHADOWS) || defined(_MAIN_LIGHT_SHADOWS_CASCADE) || defined(_MAIN_LIGHT_SHADOWS_SCREEN)
        #define MAIN_LIGHT_CALCULATE_SHADOWS

        #if defined(_MAIN_LIGHT_SHADOWS) || (defined(_MAIN_LIGHT_SHADOWS_SCREEN) && !defined(_SURFACE_TYPE_TRANSPARENT))
            #define REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR
        #endif
    #endif

    #if defined(_ADDITIONAL_LIGHT_SHADOWS)
        #define ADDITIONAL_LIGHT_CALCULATE_SHADOWS
    #endif
#endif

#if defined(UNITY_DOTS_INSTANCING_ENABLED) && !defined(USE_LEGACY_LIGHTMAPS)
// ^ GPU-driven rendering is enabled, and we haven't opted-out from lightmap
// texture arrays. This minimizes batch breakages, but texture arrays aren't
// supported in a performant way on all GPUs.
#define SHADOWMASK_NAME unity_ShadowMasks
#define SHADOWMASK_SAMPLER_NAME samplerunity_ShadowMasks
#define SHADOWMASK_SAMPLE_EXTRA_ARGS , unity_LightmapIndex.x
#else
// ^ Lightmaps are not bound as texture arrays, but as individual textures. The
// batch is broken every time lightmaps are changed, but this is well-supported
// on all GPUs.
#define SHADOWMASK_NAME unity_ShadowMask
#define SHADOWMASK_SAMPLER_NAME samplerunity_ShadowMask
#define SHADOWMASK_SAMPLE_EXTRA_ARGS
#endif

#if defined(SHADOWS_SHADOWMASK) && defined(LIGHTMAP_ON)
    #if defined(LIGHTMAP_BICUBIC_SAMPLING)
    #define SAMPLE_SHADOWMASK(uv) SampleLightmapBicubic(SHADOWMASK_NAME, SHADOWMASK_SAMPLER_NAME, uv SHADOWMASK_SAMPLE_EXTRA_ARGS);
    #else
    #define SAMPLE_SHADOWMASK(uv) SAMPLE_TEXTURE2D_LIGHTMAP(SHADOWMASK_NAME, SHADOWMASK_SAMPLER_NAME, uv SHADOWMASK_SAMPLE_EXTRA_ARGS);
    #endif
#elif !defined (LIGHTMAP_ON)
    #define SAMPLE_SHADOWMASK(uv) unity_ProbesOcclusion;
#else
    #define SAMPLE_SHADOWMASK(uv) half4(1, 1, 1, 1);
#endif

#define REQUIRES_WORLD_SPACE_POS_INTERPOLATOR

#if defined(LIGHTMAP_ON) || defined(LIGHTMAP_SHADOW_MIXING) || defined(SHADOWS_SHADOWMASK)
#define CALCULATE_BAKED_SHADOWS
#endif

TEXTURE2D_X(_ScreenSpaceShadowmapTexture);

TEXTURE2D_SHADOW(_MainLightShadowmapTexture);
TEXTURE2D(_MainLightShadowmapTextureRaw);
TEXTURE2D_SHADOW(_AdditionalLightsShadowmapTexture);
SAMPLER_CMP(sampler_LinearClampCompare);

// GLES3 causes a performance regression in some devices when using CBUFFER.
#ifndef LIGHT_SHADOWS_NO_CBUFFER
CBUFFER_START(LightShadows)
#endif

// Last cascade is initialized with a no-op matrix. It always transforms
// shadow coord to half3(0, 0, NEAR_PLANE). We use this trick to avoid
// branching since ComputeCascadeIndex can return cascade index = MAX_SHADOW_CASCADES
float4x4    _MainLightWorldToShadow[MAX_SHADOW_CASCADES + 1];
float4      _CascadeShadowSplitSpheres0;
float4      _CascadeShadowSplitSpheres1;
float4      _CascadeShadowSplitSpheres2;
float4      _CascadeShadowSplitSpheres3;
float4      _CascadeShadowSplitSphereRadii;

float4      _MainLightShadowOffset0; // xy: offset0, zw: offset1
float4      _MainLightShadowOffset1; // xy: offset2, zw: offset3
float4      _MainLightShadowParams;   // (x: shadowStrength, y: >= 1.0 if soft shadows, 0.0 otherwise, z: main light fade scale, w: main light fade bias)
float4      _MainLightShadowmapSize;  // (xy: 1/width and 1/height, zw: width and height)

float4      _AdditionalShadowOffset0; // xy: offset0, zw: offset1
float4      _AdditionalShadowOffset1; // xy: offset2, zw: offset3
float4      _AdditionalShadowFadeParams; // x: additional light fade scale, y: additional light fade bias, z: 0.0, w: 0.0)
float4      _AdditionalShadowmapSize; // (xy: 1/width and 1/height, zw: width and height)

#if defined(ADDITIONAL_LIGHT_CALCULATE_SHADOWS)
#if !USE_STRUCTURED_BUFFER_FOR_LIGHT_DATA
// Point lights can use 6 shadow slices. Some mobile GPUs performance decrease drastically with uniform
// blocks bigger than 8kb while others have a 64kb max uniform block size. This number ensures size of buffer
// AdditionalLightShadows stays reasonable. It also avoids shader compilation errors on SHADER_API_GLES30
// devices where max number of uniforms per shader GL_MAX_FRAGMENT_UNIFORM_VECTORS is low (224)
float4      _AdditionalShadowParams[MAX_VISIBLE_LIGHTS];         // Per-light data: (x: shadowStrength, y: softShadows, z: light type (Spot: 0, Point: 1), w: perLightFirstShadowSliceIndex)
float4x4    _AdditionalLightsWorldToShadow[MAX_VISIBLE_LIGHTS];  // Per-shadow-slice-data
#endif
#endif

#ifndef LIGHT_SHADOWS_NO_CBUFFER
CBUFFER_END
#endif

#if defined(ADDITIONAL_LIGHT_CALCULATE_SHADOWS)
    #if USE_STRUCTURED_BUFFER_FOR_LIGHT_DATA
        StructuredBuffer<float4>   _AdditionalShadowParams_SSBO;        // Per-light data - TODO: test if splitting _AdditionalShadowParams_SSBO[lightIndex].w into a separate StructuredBuffer<int> buffer is faster
        StructuredBuffer<float4x4> _AdditionalLightsWorldToShadow_SSBO; // Per-shadow-slice-data - A shadow casting light can have 6 shadow slices (if it's a point light)
    #endif
#endif

// x: depth bias,
// y: normal bias,
// z: light type (Spot = 0, Directional = 1, Point = 2, Area/Rectangle = 3, Disc = 4, Pyramid = 5, Box = 6, Tube = 7)
// w: unused
float4 _ShadowBias;

half IsSpotLight()
{
    return round(_ShadowBias.z) == 0.0 ? 1 : 0;
}

half IsDirectionalLight()
{
    return round(_ShadowBias.z) == 1.0 ? 1 : 0;
}

half IsPointLight()
{
    return round(_ShadowBias.z) == 2.0 ? 1 : 0;
}

#define BEYOND_SHADOW_FAR(shadowCoord) shadowCoord.z <= 0.0 || shadowCoord.z >= 1.0

// Should match: UnityEngine.Rendering.Universal + 1
#define SOFT_SHADOW_QUALITY_OFF    half(0.0)
#define SOFT_SHADOW_QUALITY_LOW    half(1.0)
#define SOFT_SHADOW_QUALITY_MEDIUM half(2.0)
#define SOFT_SHADOW_QUALITY_HIGH   half(3.0)

struct ShadowSamplingData
{
    half4 shadowOffset0;
    half4 shadowOffset1;
    float4 shadowmapSize;
    half softShadowQuality;
    // Normalized screen UV [0,1] — used for quarter-res penumbra mask lookup (PCSS_USE_BLOCKER_MASK).
    // Set to (0,0) when not available (caller has no screen-space position).
    // Normalized screen UV [0,1], converted to HDRP's pixel-space posSS for PCSS temporal jitter.
    float2 screenUV;
};

ShadowSamplingData GetMainLightShadowSamplingData()
{
    ShadowSamplingData shadowSamplingData = (ShadowSamplingData)0;

    // shadowOffsets are used in SampleShadowmapFiltered for low quality soft shadows.
    shadowSamplingData.shadowOffset0 = half4(_MainLightShadowOffset0);
    shadowSamplingData.shadowOffset1 = half4(_MainLightShadowOffset1);

    // shadowmapSize is used in SampleShadowmapFiltered otherwise
    shadowSamplingData.shadowmapSize = _MainLightShadowmapSize;
    shadowSamplingData.softShadowQuality = half(_MainLightShadowParams.y);
    shadowSamplingData.screenUV = float2(0.0, 0.0);

    return shadowSamplingData;
}

ShadowSamplingData GetMainLightShadowSamplingData(float2 screenUV)
{
    ShadowSamplingData shadowSamplingData = GetMainLightShadowSamplingData();
    shadowSamplingData.screenUV = screenUV;
    return shadowSamplingData;
}

ShadowSamplingData GetAdditionalLightShadowSamplingData(int index)
{
    ShadowSamplingData shadowSamplingData = (ShadowSamplingData)0;

    #if defined(ADDITIONAL_LIGHT_CALCULATE_SHADOWS)
        // shadowOffsets are used in SampleShadowmapFiltered for low quality soft shadows.
        shadowSamplingData.shadowOffset0 = _AdditionalShadowOffset0;
        shadowSamplingData.shadowOffset1 = _AdditionalShadowOffset1;

        // shadowmapSize is used in SampleShadowmapFiltered otherwise.
        shadowSamplingData.shadowmapSize = _AdditionalShadowmapSize;
        shadowSamplingData.softShadowQuality = _AdditionalShadowParams[index].y;
    #endif

    return shadowSamplingData;
}

// ShadowParams
// x: ShadowStrength
// y: 1.0 if shadow is soft, 0.0 otherwise
half4 GetMainLightShadowParams()
{
    return half4(_MainLightShadowParams);
}


// ShadowParams
// x: ShadowStrength
// y: >= 1.0 if shadow is soft, 0.0 otherwise. Higher value for higher quality. (1.0 == low, 2.0 == medium, 3.0 == high)
// z: 1.0 if cast by a point light (6 shadow slices), 0.0 if cast by a spot light (1 shadow slice)
// w: first shadow slice index for this light, there can be 6 in case of point lights. (-1 for non-shadow-casting-lights)
half4 GetAdditionalLightShadowParams(int lightIndex)
{
    half4 results;
    #if defined(ADDITIONAL_LIGHT_CALCULATE_SHADOWS)
        #if USE_STRUCTURED_BUFFER_FOR_LIGHT_DATA
            results = _AdditionalShadowParams_SSBO[lightIndex];
        #else
            results = _AdditionalShadowParams[lightIndex];
            // workaround: Avoid failing the graphics test using Terrain Shader on Android Vulkan when using dynamic branching for fog keywords.
            #if !SKIP_SHADOWS_LIGHT_INDEX_CHECK
                results.w = lightIndex < 0 ? -1 : results.w;
            #endif
        #endif
    #else
        // Same defaults as set in AdditionalLightsShadowCasterPass.cs
        return half4(0, 0, 0, -1);
    #endif

    return results;
}

#include "PcssMain.hlsl"
#include "PenumbraTint.hlsl"

half SampleScreenSpaceShadowmap(float4 shadowCoord)
{
    shadowCoord.xy /= max(0.00001, shadowCoord.w); // Prevent division by zero.

    // The stereo transform has to happen after the manual perspective divide
    shadowCoord.xy = UnityStereoTransformScreenSpaceTex(shadowCoord.xy);

#if defined(UNITY_STEREO_INSTANCING_ENABLED) || defined(UNITY_STEREO_MULTIVIEW_ENABLED)
    half attenuation = SAMPLE_TEXTURE2D_ARRAY(_ScreenSpaceShadowmapTexture, sampler_PointClamp, shadowCoord.xy, unity_StereoEyeIndex).x;
#else
    half attenuation = half(SAMPLE_TEXTURE2D(_ScreenSpaceShadowmapTexture, sampler_PointClamp, shadowCoord.xy).x);
#endif

    return attenuation;
}

real SampleShadowmapFilteredLowQuality(TEXTURE2D_SHADOW_PARAM(ShadowMap, sampler_ShadowMap), float4 shadowCoord, ShadowSamplingData samplingData)
{
    // 4-tap hardware comparison
    real4 attenuation4;
    attenuation4.x = real(SAMPLE_TEXTURE2D_SHADOW(ShadowMap, sampler_ShadowMap, shadowCoord.xyz + float3(samplingData.shadowOffset0.xy, 0)));
    attenuation4.y = real(SAMPLE_TEXTURE2D_SHADOW(ShadowMap, sampler_ShadowMap, shadowCoord.xyz + float3(samplingData.shadowOffset0.zw, 0)));
    attenuation4.z = real(SAMPLE_TEXTURE2D_SHADOW(ShadowMap, sampler_ShadowMap, shadowCoord.xyz + float3(samplingData.shadowOffset1.xy, 0)));
    attenuation4.w = real(SAMPLE_TEXTURE2D_SHADOW(ShadowMap, sampler_ShadowMap, shadowCoord.xyz + float3(samplingData.shadowOffset1.zw, 0)));
    return dot(attenuation4, real(0.25));
}

real SampleShadowmapFilteredMediumQuality(TEXTURE2D_SHADOW_PARAM(ShadowMap, sampler_ShadowMap), float4 shadowCoord, ShadowSamplingData samplingData)
{
    float fetchesWeights[9];
    float2 fetchesUV[9];
    SampleShadow_ComputeSamples_Tent_Filter_5x5(float, samplingData.shadowmapSize, shadowCoord, fetchesWeights, fetchesUV);

    return fetchesWeights[0] * SAMPLE_TEXTURE2D_SHADOW(ShadowMap, sampler_ShadowMap, float3(fetchesUV[0].xy, shadowCoord.z))
                + fetchesWeights[1] * SAMPLE_TEXTURE2D_SHADOW(ShadowMap, sampler_ShadowMap, float3(fetchesUV[1].xy, shadowCoord.z))
                + fetchesWeights[2] * SAMPLE_TEXTURE2D_SHADOW(ShadowMap, sampler_ShadowMap, float3(fetchesUV[2].xy, shadowCoord.z))
                + fetchesWeights[3] * SAMPLE_TEXTURE2D_SHADOW(ShadowMap, sampler_ShadowMap, float3(fetchesUV[3].xy, shadowCoord.z))
                + fetchesWeights[4] * SAMPLE_TEXTURE2D_SHADOW(ShadowMap, sampler_ShadowMap, float3(fetchesUV[4].xy, shadowCoord.z))
                + fetchesWeights[5] * SAMPLE_TEXTURE2D_SHADOW(ShadowMap, sampler_ShadowMap, float3(fetchesUV[5].xy, shadowCoord.z))
                + fetchesWeights[6] * SAMPLE_TEXTURE2D_SHADOW(ShadowMap, sampler_ShadowMap, float3(fetchesUV[6].xy, shadowCoord.z))
                + fetchesWeights[7] * SAMPLE_TEXTURE2D_SHADOW(ShadowMap, sampler_ShadowMap, float3(fetchesUV[7].xy, shadowCoord.z))
                + fetchesWeights[8] * SAMPLE_TEXTURE2D_SHADOW(ShadowMap, sampler_ShadowMap, float3(fetchesUV[8].xy, shadowCoord.z));
}

real SampleShadowmapFilteredHighQuality(TEXTURE2D_SHADOW_PARAM(ShadowMap, sampler_ShadowMap), float4 shadowCoord, ShadowSamplingData samplingData)
{
    float fetchesWeights[16];
    float2 fetchesUV[16];
    SampleShadow_ComputeSamples_Tent_Filter_7x7(float, samplingData.shadowmapSize, shadowCoord, fetchesWeights, fetchesUV);

    return fetchesWeights[0] * SAMPLE_TEXTURE2D_SHADOW(ShadowMap, sampler_ShadowMap, float3(fetchesUV[0].xy, shadowCoord.z))
                + fetchesWeights[1] * SAMPLE_TEXTURE2D_SHADOW(ShadowMap, sampler_ShadowMap, float3(fetchesUV[1].xy, shadowCoord.z))
                + fetchesWeights[2] * SAMPLE_TEXTURE2D_SHADOW(ShadowMap, sampler_ShadowMap, float3(fetchesUV[2].xy, shadowCoord.z))
                + fetchesWeights[3] * SAMPLE_TEXTURE2D_SHADOW(ShadowMap, sampler_ShadowMap, float3(fetchesUV[3].xy, shadowCoord.z))
                + fetchesWeights[4] * SAMPLE_TEXTURE2D_SHADOW(ShadowMap, sampler_ShadowMap, float3(fetchesUV[4].xy, shadowCoord.z))
                + fetchesWeights[5] * SAMPLE_TEXTURE2D_SHADOW(ShadowMap, sampler_ShadowMap, float3(fetchesUV[5].xy, shadowCoord.z))
                + fetchesWeights[6] * SAMPLE_TEXTURE2D_SHADOW(ShadowMap, sampler_ShadowMap, float3(fetchesUV[6].xy, shadowCoord.z))
                + fetchesWeights[7] * SAMPLE_TEXTURE2D_SHADOW(ShadowMap, sampler_ShadowMap, float3(fetchesUV[7].xy, shadowCoord.z))
                + fetchesWeights[8] * SAMPLE_TEXTURE2D_SHADOW(ShadowMap, sampler_ShadowMap, float3(fetchesUV[8].xy, shadowCoord.z))
                + fetchesWeights[9] * SAMPLE_TEXTURE2D_SHADOW(ShadowMap, sampler_ShadowMap, float3(fetchesUV[9].xy, shadowCoord.z))
                + fetchesWeights[10] * SAMPLE_TEXTURE2D_SHADOW(ShadowMap, sampler_ShadowMap, float3(fetchesUV[10].xy, shadowCoord.z))
                + fetchesWeights[11] * SAMPLE_TEXTURE2D_SHADOW(ShadowMap, sampler_ShadowMap, float3(fetchesUV[11].xy, shadowCoord.z))
                + fetchesWeights[12] * SAMPLE_TEXTURE2D_SHADOW(ShadowMap, sampler_ShadowMap, float3(fetchesUV[12].xy, shadowCoord.z))
                + fetchesWeights[13] * SAMPLE_TEXTURE2D_SHADOW(ShadowMap, sampler_ShadowMap, float3(fetchesUV[13].xy, shadowCoord.z))
                + fetchesWeights[14] * SAMPLE_TEXTURE2D_SHADOW(ShadowMap, sampler_ShadowMap, float3(fetchesUV[14].xy, shadowCoord.z))
                + fetchesWeights[15] * SAMPLE_TEXTURE2D_SHADOW(ShadowMap, sampler_ShadowMap, float3(fetchesUV[15].xy, shadowCoord.z));
}

real SampleShadowmapFiltered(TEXTURE2D_SHADOW_PARAM(ShadowMap, sampler_ShadowMap), float4 shadowCoord, ShadowSamplingData samplingData)
{
    real attenuation = real(1.0);

    if (samplingData.softShadowQuality == SOFT_SHADOW_QUALITY_LOW)
    {
        attenuation = SampleShadowmapFilteredLowQuality(TEXTURE2D_SHADOW_ARGS(ShadowMap, sampler_ShadowMap), shadowCoord, samplingData);
    }
    else if(samplingData.softShadowQuality == SOFT_SHADOW_QUALITY_MEDIUM)
    {
        attenuation = SampleShadowmapFilteredMediumQuality(TEXTURE2D_SHADOW_ARGS(ShadowMap, sampler_ShadowMap), shadowCoord, samplingData);
    }
    else // SOFT_SHADOW_QUALITY_HIGH
    {
        attenuation = SampleShadowmapFilteredHighQuality(TEXTURE2D_SHADOW_ARGS(ShadowMap, sampler_ShadowMap), shadowCoord, samplingData);
    }

    return attenuation;
}

real SampleShadowmap(TEXTURE2D_SHADOW_PARAM(ShadowMap, sampler_ShadowMap), float4 shadowCoord, ShadowSamplingData samplingData, half4 shadowParams, bool isPerspectiveProjection = true)
{
    // Compiler will optimize this branch away as long as isPerspectiveProjection is known at compile time
    if (isPerspectiveProjection)
        shadowCoord.xyz /= shadowCoord.w;

    real attenuation;
    real shadowStrength = shadowParams.x;

    #if defined(_SHADOWS_SOFT_LOW)
        attenuation = SampleShadowmapFilteredLowQuality(TEXTURE2D_SHADOW_ARGS(ShadowMap, sampler_ShadowMap), shadowCoord, samplingData);
    #elif defined(_SHADOWS_SOFT_MEDIUM)
        attenuation = SampleShadowmapFilteredMediumQuality(TEXTURE2D_SHADOW_ARGS(ShadowMap, sampler_ShadowMap), shadowCoord, samplingData);
    #elif defined(_SHADOWS_SOFT_HIGH)
        attenuation = SampleShadowmapFilteredHighQuality(TEXTURE2D_SHADOW_ARGS(ShadowMap, sampler_ShadowMap), shadowCoord, samplingData);
    #elif defined(_SHADOWS_SOFT)
        if (shadowParams.y > SOFT_SHADOW_QUALITY_OFF)
        {
            attenuation = SampleShadowmapFiltered(TEXTURE2D_SHADOW_ARGS(ShadowMap, sampler_ShadowMap), shadowCoord, samplingData);
        }
        else
        {
            attenuation = real(SAMPLE_TEXTURE2D_SHADOW(ShadowMap, sampler_ShadowMap, shadowCoord.xyz));
        }
    #else
        attenuation = real(SAMPLE_TEXTURE2D_SHADOW(ShadowMap, sampler_ShadowMap, shadowCoord.xyz));
    #endif

    attenuation = LerpWhiteTo(attenuation, shadowStrength);

    // Shadow coords that fall out of the light frustum volume must always return attenuation 1.0
    // TODO: We could use branch here to save some perf on some platforms.
    return BEYOND_SHADOW_FAR(shadowCoord) ? 1.0 : attenuation;
}

#include "MainLightShadowCascades.hlsl"

half ComputeCascadeIndex(float3 positionWS)
{
    MainLightCascadeSelection selection = SelectMainLightCascade(positionWS);
    return selection.current >= 0 ? half(selection.current) : half(MAX_SHADOW_CASCADES);
}

float4 TransformWorldToShadowCoord(float3 positionWS)
{
#if defined(_MAIN_LIGHT_SHADOWS_SCREEN) && !defined(_SURFACE_TYPE_TRANSPARENT)
    float4 shadowCoord = float4(ComputeNormalizedDeviceCoordinatesWithZ(positionWS, GetWorldToHClipMatrix()), 1.0);
#else
    #ifdef _MAIN_LIGHT_SHADOWS_CASCADE
        half cascadeIndex = ComputeCascadeIndex(positionWS);
    #else
        half cascadeIndex = half(0.0);
    #endif

    float4 shadowCoord = float4(mul(_MainLightWorldToShadow[cascadeIndex], float4(positionWS, 1.0)).xyz, cascadeIndex);
#endif
    return shadowCoord;
}

half MainLightRealtimeShadowRaw(float4 shadowCoord, half4 shadowParams, ShadowSamplingData shadowSamplingData)
{
    #if !defined(MAIN_LIGHT_CALCULATE_SHADOWS)
        return half(1.0);
    #endif

    #if defined(_MAIN_LIGHT_SHADOWS_SCREEN) && !defined(_SURFACE_TYPE_TRANSPARENT)
        // The screen-space directional shadow texture stores the raw CSM result.
        // Shadow strength and Shadow Mask integration are resolved afterwards.
        return SampleScreenSpaceShadowmap(shadowCoord);
    #else
        return half(SampleMainLightDirectionalFilterRaw(shadowCoord, shadowParams, shadowSamplingData));
    #endif
}

half MainLightRealtimeShadow(float4 shadowCoord, half4 shadowParams, ShadowSamplingData shadowSamplingData)
{
    return LerpWhiteTo(
        MainLightRealtimeShadowRaw(shadowCoord, shadowParams, shadowSamplingData),
        shadowParams.x);
}

half MainLightRealtimeShadow(float4 shadowCoord)
{
    #if !defined(MAIN_LIGHT_CALCULATE_SHADOWS)
        return half(1.0);
    #endif

    return MainLightRealtimeShadow(shadowCoord, GetMainLightShadowParams(), GetMainLightShadowSamplingData());
}

// Receiver-aware directional path. It recomputes per-cascade coordinates after applying
// receiver-side normal bias and evaluates HDRP-style cascade transitions.
half MainLightRealtimeShadowRaw(float4 shadowCoord, float3 positionWS, float3 normalWS,
    half4 shadowParams, ShadowSamplingData shadowSamplingData)
{
    #if !defined(MAIN_LIGHT_CALCULATE_SHADOWS)
        return half(1.0);
    #endif

    #if defined(_MAIN_LIGHT_SHADOWS_SCREEN) && !defined(_SURFACE_TYPE_TRANSPARENT)
        return SampleScreenSpaceShadowmap(shadowCoord);
    #else
        return EvaluateMainLightDirectionalShadowRaw(positionWS, normalWS, shadowParams, shadowSamplingData);
    #endif
}

half MainLightRealtimeShadow(float4 shadowCoord, float3 positionWS, float3 normalWS,
    half4 shadowParams, ShadowSamplingData shadowSamplingData)
{
    return LerpWhiteTo(
        MainLightRealtimeShadowRaw(shadowCoord, positionWS, normalWS, shadowParams, shadowSamplingData),
        shadowParams.x);
}

// returns 0.0 if position is in light's shadow
// returns 1.0 if position is in light
half AdditionalLightRealtimeShadow(int lightIndex, float3 positionWS, half3 lightDirection, half4 shadowParams, ShadowSamplingData shadowSamplingData)
{
    #if defined(ADDITIONAL_LIGHT_CALCULATE_SHADOWS)
        int shadowSliceIndex = shadowParams.w;
        if (shadowSliceIndex < 0)
            return 1.0;

        half isPointLight = shadowParams.z;

        UNITY_BRANCH
        if (isPointLight)
        {
            // This is a point light, we have to find out which shadow slice to sample from
            const int cubeFaceOffset = CubeMapFaceID(-lightDirection);
            shadowSliceIndex += cubeFaceOffset;
        }

        #if USE_STRUCTURED_BUFFER_FOR_LIGHT_DATA
            float4 shadowCoord = mul(_AdditionalLightsWorldToShadow_SSBO[shadowSliceIndex], float4(positionWS, 1.0));
        #else
            float4 shadowCoord = mul(_AdditionalLightsWorldToShadow[shadowSliceIndex], float4(positionWS, 1.0));
        #endif

        return SampleShadowmap(TEXTURE2D_ARGS(_AdditionalLightsShadowmapTexture, sampler_LinearClampCompare), shadowCoord, shadowSamplingData, shadowParams, true);
    #else
        return half(1.0);
    #endif
}

half AdditionalLightRealtimeShadow(int lightIndex, float3 positionWS, half3 lightDirection)
{
    #if !defined(ADDITIONAL_LIGHT_CALCULATE_SHADOWS)
        return half(1.0);
    #endif

    return AdditionalLightRealtimeShadow(lightIndex, positionWS, lightDirection, GetAdditionalLightShadowParams(lightIndex), GetAdditionalLightShadowSamplingData(lightIndex));
}

// Camera-distance fade used by the distance-shadowmask fallback. The realtime
// cascade transition itself is evaluated from the selected cascade border.
half GetMainLightShadowFade(float3 positionWS)
{
    float3 camToPixel = positionWS - _WorldSpaceCameraPos;
    float distanceCamToPixel2 = dot(camToPixel, camToPixel);

    float fade = saturate(distanceCamToPixel2 * float(_MainLightShadowParams.z) + float(_MainLightShadowParams.w));
    return half(fade);
}

half GetAdditionalLightShadowFade(float3 positionWS)
{
    #if defined(ADDITIONAL_LIGHT_CALCULATE_SHADOWS)
        float3 camToPixel = positionWS - _WorldSpaceCameraPos;
        float distanceCamToPixel2 = dot(camToPixel, camToPixel);

        float fade = saturate(distanceCamToPixel2 * float(_AdditionalShadowFadeParams.x) + float(_AdditionalShadowFadeParams.y));
        return half(fade);
    #else
        return half(1.0);
    #endif
}

half MixRealtimeAndBakedShadows(half realtimeShadow, half bakedShadow, half shadowFade)
{
#if defined(LIGHTMAP_SHADOW_MIXING)
    return min(lerp(realtimeShadow, 1, shadowFade), bakedShadow);
#else
    return lerp(realtimeShadow, bakedShadow, shadowFade);
#endif
}

half BakedShadow(half4 shadowMask, half4 occlusionProbeChannels, half4 shadowParams)
{
    // Here occlusionProbeChannels is the selector for the light's Shadow Mask channel.
    // If all components are zero, the default baked attenuation is fully lit.
    half bakedShadow = half(1.0) + dot(shadowMask - half(1.0), occlusionProbeChannels);
    return LerpWhiteTo(bakedShadow, shadowParams.x);
}

half ResolveMainLightShadow(half realtimeShadow, half bakedShadow, float3 positionWS)
{
#if defined(MAIN_LIGHT_CALCULATE_SHADOWS) && defined(CALCULATE_BAKED_SHADOWS)
    // Preserve URP's baked-lighting composition. The camera-distance fade is not used
    // for pure realtime CSM; HDRP's cascade-border evaluation already performs that fade.
    return MixRealtimeAndBakedShadows(realtimeShadow, bakedShadow, GetMainLightShadowFade(positionWS));
#elif defined(MAIN_LIGHT_CALCULATE_SHADOWS)
    return realtimeShadow;
#else
    return bakedShadow;
#endif
}

half MainLightShadow(float4 shadowCoord, float3 positionWS, half4 shadowMask, half4 occlusionProbeChannels)
{
    half4 shadowParams = GetMainLightShadowParams();
    half realtimeShadow = MainLightRealtimeShadow(
        shadowCoord, positionWS, float3(0.0, 0.0, 0.0), shadowParams, GetMainLightShadowSamplingData());

#ifdef CALCULATE_BAKED_SHADOWS
    half bakedShadow = BakedShadow(shadowMask, occlusionProbeChannels, shadowParams);
#else
    half bakedShadow = half(1.0);
#endif

    return ResolveMainLightShadow(realtimeShadow, bakedShadow, positionWS);
}

// HDRP contact-shadow receiver policy. Default performs the NdotL test, Off skips
// contact shadows, and Force skips the NdotL test. Transparent surfaces never receive
// raster contact shadows because they have no contact-shadow depth information.
half ApplyMainLightContactShadow(half shadow, float3 normalWS, float2 screenUV, uint receiverMode)
{
#if defined(_SURFACE_TYPE_TRANSPARENT)
    return shadow;
#else
    if (receiverMode == CONTACT_SHADOW_RECEIVER_OFF)
        return shadow;

    bool allowContactShadow = receiverMode == CONTACT_SHADOW_RECEIVER_FORCE ||
        dot(normalWS, _MainLightPosition.xyz) > 0.0;

    return min(shadow, allowContactShadow ? GetMainLightContactShadow(screenUV) : half(1.0));
#endif
}

half ApplyMainLightContactShadow(half shadow, float3 normalWS, float2 screenUV)
{
    return ApplyMainLightContactShadow(shadow, normalWS, screenUV, MATERIAL_CONTACT_SHADOW_RECEIVER_MODE);
}

// Common receiver-aware path used by both forward and deferred lighting.
half MainLightShadow(float4 shadowCoord, float3 positionWS, float3 normalWS, float2 screenUV,
    half4 shadowMask, half4 occlusionProbeChannels, uint contactShadowReceiverMode)
{
    half4 shadowParams = GetMainLightShadowParams();
    half realtimeShadow = MainLightRealtimeShadow(
        shadowCoord, positionWS, normalWS, shadowParams, GetMainLightShadowSamplingData(screenUV));

#ifdef CALCULATE_BAKED_SHADOWS
    half bakedShadow = BakedShadow(shadowMask, occlusionProbeChannels, shadowParams);
#else
    half bakedShadow = half(1.0);
#endif

    half shadow = ResolveMainLightShadow(realtimeShadow, bakedShadow, positionWS);
    return ApplyMainLightContactShadow(shadow, normalWS, screenUV, contactShadowReceiverMode);
}

// Forward/material-model overload. The receiver mode is selected by the exact HDRP macros.
half MainLightShadow(float4 shadowCoord, float3 positionWS, float3 normalWS, float2 screenUV,
    half4 shadowMask, half4 occlusionProbeChannels)
{
    return MainLightShadow(shadowCoord, positionWS, normalWS, screenUV, shadowMask,
        occlusionProbeChannels, MATERIAL_CONTACT_SHADOW_RECEIVER_MODE);
}

half MainLightShadow(float4 shadowCoord, float3 positionWS, float2 screenUV,
    half4 shadowMask, half4 occlusionProbeChannels)
{
    return MainLightShadow(shadowCoord, positionWS, float3(0.0, 0.0, 0.0),
        screenUV, shadowMask, occlusionProbeChannels, MATERIAL_CONTACT_SHADOW_RECEIVER_MODE);
}

half AdditionalLightShadow(int lightIndex, float3 positionWS, half3 lightDirection, half4 shadowMask, half4 occlusionProbeChannels)
{
    half4 shadowParams = GetAdditionalLightShadowParams(lightIndex);
    ShadowSamplingData samplingData = GetAdditionalLightShadowSamplingData(lightIndex);
    half realtimeShadow = AdditionalLightRealtimeShadow(lightIndex, positionWS, lightDirection, shadowParams, samplingData);

    #ifdef CALCULATE_BAKED_SHADOWS
        // This fading of the baked shadow using the light's shadow strength parameter needs
        // to be guarded against the Real-Time Shadow keyword as _AdditionalShadowParams is
        // only included in URP Shaders and updated when real time additional shadows are enabled.
        #ifndef ADDITIONAL_LIGHT_CALCULATE_SHADOWS
            shadowParams.x = half(1.0);
        #endif
        half bakedShadow = BakedShadow(shadowMask, occlusionProbeChannels, shadowParams);
    #else
        half bakedShadow = half(1.0);
    #endif

    #ifdef ADDITIONAL_LIGHT_CALCULATE_SHADOWS
        half shadowFade = GetAdditionalLightShadowFade(positionWS);
    #else
        half shadowFade = half(1.0);
    #endif

    return MixRealtimeAndBakedShadows(realtimeShadow, bakedShadow, shadowFade);
}

float4 GetShadowCoord(VertexPositionInputs vertexInput)
{
#if defined(_MAIN_LIGHT_SHADOWS_SCREEN) && !defined(_SURFACE_TYPE_TRANSPARENT)
    return ComputeScreenPos(vertexInput.positionCS);
#else
    return TransformWorldToShadowCoord(vertexInput.positionWS);
#endif
}

float3 ApplyShadowBias(float3 positionWS, float3 normalWS, float3 lightDirection)
{
    float invNdotL = 1.0 - saturate(dot(lightDirection, normalWS));
    float scale = invNdotL * _ShadowBias.y;

    // normal bias is negative since we want to apply an inset normal offset
    positionWS = lightDirection * _ShadowBias.xxx + positionWS;
    positionWS = normalWS * scale.xxx + positionWS;
    return positionWS;
}

float4 ApplyShadowClamping(float4 positionCS)
{
#if UNITY_REVERSED_Z
    float clamped = min(positionCS.z, positionCS.w * UNITY_NEAR_CLIP_VALUE);
#else
    float clamped = max(positionCS.z, positionCS.w * UNITY_NEAR_CLIP_VALUE);
#endif

    // The current implementation of vertex clamping in Universal RP is the same as in Unity Built-In RP.
    // We follow the same convention in Universal RP where it's only enabled for Directional Lights
    // (see: Shadows.cpp::RenderShadowMaps() #L2161-L2162)
    // (see: Shadows.cpp::RenderShadowMaps() #L2086-L2102)
    // (see: Shadows.cpp::PrepareStateForShadowMap() #L1685-L1686)
    positionCS.z = lerp(positionCS.z, clamped, IsDirectionalLight());
    return positionCS;
}

///////////////////////////////////////////////////////////////////////////////
// Deprecated                                                                 /
///////////////////////////////////////////////////////////////////////////////

// Renamed -> _MainLightShadowParams
#define _MainLightShadowData _MainLightShadowParams

// Deprecated: Use GetMainLightShadowFade or GetAdditionalLightShadowFade instead.
float GetShadowFade(float3 positionWS)
{
    float3 camToPixel = positionWS - _WorldSpaceCameraPos;
    float distanceCamToPixel2 = dot(camToPixel, camToPixel);

    float fade = saturate(distanceCamToPixel2 * float(_MainLightShadowParams.z) + float(_MainLightShadowParams.w));
    return fade * fade;
}

// Deprecated: Use GetShadowFade instead.
float ApplyShadowFade(float shadowAttenuation, float3 positionWS)
{
    float fade = GetShadowFade(positionWS);
    return shadowAttenuation + (1 - shadowAttenuation) * fade * fade;
}

// Deprecated: Use GetMainLightShadowParams instead.
half GetMainLightShadowStrength()
{
    return half(_MainLightShadowData.x);
}

// Deprecated: Use GetAdditionalLightShadowParams instead.
half GetAdditionalLightShadowStrenth(int lightIndex)
{
    #if defined(ADDITIONAL_LIGHT_CALCULATE_SHADOWS)
        #if USE_STRUCTURED_BUFFER_FOR_LIGHT_DATA
            return half(_AdditionalShadowParams_SSBO[lightIndex].x);
        #else
            return half(_AdditionalShadowParams[lightIndex].x);
        #endif
    #else
        return half(1.0);
    #endif
}

// Deprecated: Use SampleShadowmap that takes shadowParams instead of strength.
real SampleShadowmap(float4 shadowCoord, TEXTURE2D_SHADOW_PARAM(ShadowMap, sampler_ShadowMap), ShadowSamplingData samplingData, half shadowStrength, bool isPerspectiveProjection = true)
{
    half4 shadowParams = half4(shadowStrength, 1.0, 0.0, 0.0);
    return SampleShadowmap(TEXTURE2D_SHADOW_ARGS(ShadowMap, sampler_ShadowMap), shadowCoord, samplingData, shadowParams, isPerspectiveProjection);
}

// Deprecated: Use AdditionalLightRealtimeShadow(int lightIndex, float3 positionWS, half3 lightDirection) in Shadows.hlsl instead, as it supports Point Light shadows
half AdditionalLightRealtimeShadow(int lightIndex, float3 positionWS)
{
    return AdditionalLightRealtimeShadow(lightIndex, positionWS, half3(1, 0, 0));
}

// Deprecated: Use BakedShadow(half4 shadowMask, half4 occlusionProbeChannels, half4 shadowParams) as it supports shadowStrength from the light
half BakedShadow(half4 shadowMask, half4 occlusionProbeChannels)
{
    #ifndef CALCULATE_BAKED_SHADOWS
        return half(1.0);
    #endif

    return BakedShadow(shadowMask, occlusionProbeChannels, half4(1,0,0,0));
}

#endif
