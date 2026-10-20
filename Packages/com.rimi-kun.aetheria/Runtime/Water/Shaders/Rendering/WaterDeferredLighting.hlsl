#ifndef WATER_DEFERRED_LIGHTING_INCLUDED
#define WATER_DEFERRED_LIGHTING_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/SampleScreenSpaceReflection.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Packing.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/ImageBasedLighting.hlsl"
#include "../../Core/WaterDefinitions.cs.hlsl"
#include "../Common/WaterUtilities.hlsl"
#include "../Common/WaterCaustics.hlsl"
#include "WaterLightingTileClassification.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"

TEXTURE2D_X(_WaterMaterialGBuffer);
TEXTURE2D_X(_WaterSurfaceGBuffer);
TEXTURE2D_X(_WaterGeometryGBuffer);
TEXTURE2D_X(_WaterMetadataGBuffer);
TEXTURE2D_X_FLOAT(_WaterDepthTexture);
StructuredBuffer<WaterSurfaceProfile> _WaterSurfaceProfiles;

TEXTURE2D(_WaterSceneColorTexture);
#define SURFACE_MEDIUM_ATMOSPHERE 1
#define SURFACE_MEDIUM_FOG 1
#define SURFACE_MEDIUM_RUNTIME_FOG 1
#include "../../../../ShaderLibrary/Common/SurfaceMedium.hlsl"

int _WaterSurfaceMediumAtmosphereEnabled;
int _WaterSurfaceMediumFogEnabled;
int _WaterSurfaceMediumFogSkyEnabled;
int _WaterSurfaceMediumVolumetricFogEnabled;

float3 SampleWaterSourceColor(float2 positionNDC)
{
    uint2 size = max((uint2)_WaterRenderResolution.xy, uint2(1u, 1u));
    uint2 positionSS = min((uint2)(saturate(positionNDC) * size), size - 1u);
    return _WaterSceneColorTexture.Load(int3(positionSS, 0)).rgb;
}

uint SampleWaterSurfaceStencil(float2 positionNDC)
{
    uint2 size = max((uint2)_WaterRenderResolution.xy, uint2(1u, 1u));
    uint2 positionSS = min((uint2)(saturate(positionNDC) * size), size - 1u);
    return GetStencilValue(_WaterStencilTexture.Load(int3(positionSS, 0)));
}

half2 LoadWaterScreenSpaceShadows(float2 positionNDC)
{
    uint2 resolution = max((uint2)_WaterRenderResolution.xy, uint2(1u, 1u));
    uint2 positionSS = min((uint2)(saturate(positionNDC) * resolution), resolution - 1u);
    return half2(LOAD_TEXTURE2D_X(_ScreenSpaceShadowmapTexture, positionSS).xy);
}

#if defined(_LIGHT_COOKIES)
real3 SampleWaterMainLightCookie(float3 positionWS)
{
    if (!IsMainLightCookieEnabled())
        return real3(1.0, 1.0, 1.0);

    float2 uv = ComputeLightCookieUVDirectional(
        _MainLightWorldToLight,
        positionWS,
        float4(1.0, 1.0, 0.0, 0.0),
        URP_TEXTURE_WRAP_MODE_NONE);
    real4 color = SAMPLE_TEXTURE2D_LOD(_MainLightCookieTexture, sampler_LinearClamp, uv, 0);
    return IsMainLightCookieTextureRGBFormat() ? color.rgb
        : IsMainLightCookieTextureAlphaFormat() ? color.aaa
        : color.rrr;
}
#endif

float4 LoadWaterMaterialGBuffer(uint2 positionSS)
{
    return LOAD_TEXTURE2D_X(_WaterMaterialGBuffer, positionSS);
}

float4 LoadWaterSurfaceGBuffer(uint2 positionSS)
{
    return LOAD_TEXTURE2D_X(_WaterSurfaceGBuffer, positionSS);
}

float4 LoadWaterGeometryGBuffer(uint2 positionSS)
{
    return LOAD_TEXTURE2D_X(_WaterGeometryGBuffer, positionSS);
}

float4 LoadWaterMetadataGBuffer(uint2 positionSS)
{
    return LOAD_TEXTURE2D_X(_WaterMetadataGBuffer, positionSS);
}

uint DecodeWaterPackedMetadata(float encodedMetadata)
{
    return (uint)floor(saturate(encodedMetadata) * 255.0 + 0.5);
}

int DecodeWaterSurfaceIndex(uint packedMetadata)
{
    return (int)(packedMetadata >> 1) - 1;
}

float3 DecodeWaterNormal(float3 encodedNormal)
{
    float2 octNormal = Unpack888ToFloat2(encodedNormal);
    return SafeNormalize(UnpackNormalOctQuadEncode(octNormal * 2.0 - 1.0));
}

void InitializeWaterBRDFDataFromPerceptualRoughness(float perceptualRoughness, out BRDFData brdfData)
{
    half reflectivity = half(WATER_FRESNEL_ZERO);
    half oneMinusReflectivity = 1.0h - reflectivity;
    half3 specular = half3(reflectivity, reflectivity, reflectivity);
    half smoothness = 1.0h - half(saturate(perceptualRoughness));
    half alpha = 1.0h;

    InitializeBRDFDataDirect(
        half3(0.0h, 0.0h, 0.0h),
        half3(0.0h, 0.0h, 0.0h),
        specular,
        reflectivity,
        oneMinusReflectivity,
        smoothness,
        alpha,
        brdfData);
}

void InitializeWaterEnvironmentBRDFData(
    BRDFData surfaceBRDF,
    float perceptualRoughness,
    out BRDFData environmentBRDF)
{
    InitializeWaterBRDFDataFromPerceptualRoughness(perceptualRoughness, environmentBRDF);
    environmentBRDF.specular = surfaceBRDF.specular;
}

float EvaluateDeferredWaterTipThickness(
    float3 V, float3 geometryNormalWS, float normalizedHeight, float3 upDirection)
{
    float tipHeight = saturate(normalizedHeight * 2.0 - 1.0);
    float3 refractedRay = refract(-V, geometryNormalWS, WATER_INV_IOR);
    float verticalRefraction = dot(refractedRay, SafeNormalize(upDirection));
    float H = max(0.01, tipHeight);
    return 1.0 - saturate(1.0 + verticalRefraction - 0.2) * (H * H) / 0.4;
}

float3 EvaluateDeferredWaterScattering(
    float scatteringTerms, float surfaceFoam,
    float3 absorptionTint, WaterSurfaceProfile profile, bool frontFace)
{
    float3 scattering = 0.0;
    if (frontFace)
    {
        scattering = profile.scatteringAlbedo
            * PI
            * saturate(scatteringTerms)
            * (1.0 - absorptionTint)
            * (1.0 - saturate(surfaceFoam));
    }
    return scattering;
}

float3 EvaluateWaterSurfaceColorTarget(float opticalDepth, WaterSurfaceProfile profile)
{
    float depthFactor = saturate(
        opticalDepth / max(profile.surfaceColorParameters.y, 1e-4));
    float depthBlend = pow(depthFactor, max(profile.surfaceColorParameters.z, 1e-4));
    return saturate(lerp(
        profile.shallowSurfaceColor.rgb,
        profile.deepSurfaceColor.rgb,
        depthBlend));
}

float3 ApplyWaterBodyColorStylization(
    float3 bodyColor, float opticalDepth, WaterSurfaceProfile profile)
{
    float strength = saturate(profile.surfaceColorParameters.x);
    float3 stylizedColor = bodyColor;
    if (strength > 1e-4)
    {
        float3 targetColor = EvaluateWaterSurfaceColorTarget(opticalDepth, profile);
        float bodyLuminance = Luminance(max(bodyColor, 0.0));
        float targetLuminance = max(Luminance(targetColor), 1e-4);
        float3 luminanceMatchedTarget = targetColor * (bodyLuminance / targetLuminance);
        stylizedColor = lerp(bodyColor, luminanceMatchedTarget, strength);
    }
    return stylizedColor;
}

float3 SampleWaterSkyReflection(float3 reflectVector, float perceptualRoughness)
{
#if !defined(_ENVIRONMENTREFLECTIONS_OFF)
    half mip = PerceptualRoughnessToMipmapLevel(half(perceptualRoughness));
    half4 encoded = half4(SAMPLE_TEXTURECUBE_LOD(
        _GlossyEnvironmentCubeMap, sampler_TrilinearClamp, reflectVector, mip));
    return DecodeHDREnvironment(encoded, _GlossyEnvironmentCubeMap_HDR);
#else
    return _GlossyEnvironmentColor.rgb;
#endif
}

float3 GetDeferredWaterUpDirection(WaterSurfaceProfile profile)
{
    return SafeNormalize(profile.upDirection);
}

float3 EvaluateWaterProbeLighting(
    float3 absolutePositionWS,
    float3 normalWS,
    float3 viewDirectionWS,
    float2 positionSS,
    WaterSurfaceProfile profile)
{
    float3 ambientLighting = max(profile.ambientLighting, 0.0);
    float3 result = ambientLighting;

#if defined(PROBE_VOLUMES_L1) || defined(PROBE_VOLUMES_L2) || defined(PROBE_VOLUMES_DYNAMIC_BRANCH)
    if (_EnableProbeVolumes != 0u)
    {
        float3 probeLighting = SampleProbeGI(
            absolutePositionWS,
            normalWS,
            viewDirectionWS,
            positionSS,
            0xFFFFFFFFu);

        float fadeStart = profile.probeLightingFadeParameters.z;
        float fadeEnd = profile.probeLightingFadeParameters.w;
        if (fadeEnd <= fadeStart)
        {
            result = probeLighting;
        }
        else
        {
            float distanceFromSurfaceCenter = distance(
                absolutePositionWS.xz,
                profile.probeLightingFadeParameters.xy);
            float probeWeight = 1.0 - smoothstep(fadeStart, fadeEnd, distanceFromSurfaceCenter);
            result = lerp(ambientLighting, probeLighting, probeWeight);
        }
    }
#endif

    return result;
}

float3 EvaluateWaterEnvironmentSpecular(
    float3 positionWS,
    float3 N,
    float3 geometryN,
    float3 V,
    BRDFData brdfData,
    float2 positionNDC,
    float minimumPerceptualRoughness,
    bool aboveWater,
    float interfaceReflectance,
    float3 underwaterReflectionColor,
    bool screenSpaceReflectionsEnabled,
    uint featureFlags)
{
    float perceptualRoughness = max(
        float(brdfData.perceptualRoughness), saturate(minimumPerceptualRoughness));
    BRDFData environmentBRDFData;
    InitializeWaterEnvironmentBRDFData(brdfData, perceptualRoughness, environmentBRDFData);

    float NoV = saturate(dot(N, V));
    half fresnelTerm = Pow4(1.0h - half(NoV));
    float3 environmentBRDF = float3(EnvironmentBRDFSpecular(environmentBRDFData, fresnelTerm));
    if (!aboveWater)
        environmentBRDF = interfaceReflectance.xxx;

    float environmentVisibility = aboveWater
        ? GetHorizonOcclusion(V, N, geometryN, 1.0)
        : 1.0;

    float3 R = SafeNormalize(reflect(-V, N));
    R = SafeNormalize(GetSpecularDominantDir(N, R, perceptualRoughness, NoV));

    float3 environment = aboveWater
        ? SampleWaterSkyReflection(R, perceptualRoughness)
        : max(underwaterReflectionColor, 0.0);
#if CLUSTER_HAS_REFLECTION_PROBES
    if (aboveWater && (featureFlags & WATER_LIGHTING_FEATURE_ENVIRONMENT) != 0u)
    {
        environment = GlossyEnvironmentReflectionFallback(
            R, positionWS, half(perceptualRoughness), 1.0h, positionNDC);
    }
#endif

    if (aboveWater)
        environment *= max(_WaterEnvironmentReflectionIntensity, 0.0);

    if (aboveWater && screenSpaceReflectionsEnabled && _WaterHasScreenSpaceReflection != 0u)
    {
        half4 screenSpaceReflection = SampleScreenSpaceReflection(
            positionNDC, positionWS, perceptualRoughness) * _ScreenSpaceReflectionParam.x;
        environment = lerp(environment, screenSpaceReflection.rgb, screenSpaceReflection.a);
    }

    return environment * environmentBRDF * environmentVisibility;
}

float3 EvaluateWaterDirectLight(
    Light light,
    BRDFData waterBRDF,
    float3 N,
    float3 geometryN,
    float3 V,
    float3 scatteringColor,
    float tipThickness,
    float surfaceFoam,
    WaterSurfaceProfile profile,
    bool aboveWater)
{
    float attenuation = light.distanceAttenuation * light.shadowAttenuation;
    float3 lighting = 0.0;
    if (attenuation > 0.0)
    {
        float3 L = SafeNormalize(light.direction);
        float NdotL = saturate(dot(N, L));
        float lowFrequencyNdotL = dot(geometryN, L);

        half waterSpecularTerm = DirectBRDFSpecular(waterBRDF, half3(N), half3(L), half3(V));
        float3 H = SafeNormalize(L + V);
        float LdotH = saturate(dot(L, H));
        float3 fresnel = F_Schlick(float3(waterBRDF.specular), LdotH);
        float specularSelfOcclusion = saturate(lowFrequencyNdotL * 5.0);
        float3 specular = float(waterSpecularTerm) * fresnel * NdotL * specularSelfOcclusion;

        float waterIrradiance = EvaluateWaterScatteringIrradiance(lowFrequencyNdotL);
        float scatteringIrradiance = lerp(waterIrradiance, 1.0, surfaceFoam);

        float directionalScatteringGain = aboveWater
            ? EvaluateWaterDirectionalScatteringGain(
                L, V, geometryN, profile.upDirection, tipThickness,
                profile.directLightTipScattering,
                profile.directLightBodyScattering)
            : 1.0;

        float3 foamScattering = profile.foamColor * surfaceFoam;
        float3 bodyScattering = scatteringColor * directionalScatteringGain;
        float3 scatteredLighting = (bodyScattering + foamScattering) * scatteringIrradiance;
        lighting = (specular + scatteredLighting) * light.color * attenuation;
    }
    return lighting;
}

Light GetWaterMainLight(
    float4 shadowCoord,
    float3 positionWS,
    float3 normalWS,
    float2 screenUV,
    half4 shadowMask)
{
    Light light = GetMainLight();
    half4 shadowParams = GetMainLightShadowParams();
    half realtimeShadow;
    UNITY_BRANCH
    if (_WaterHasScreenSpaceShadows != 0u)
    {
        realtimeShadow = LerpWhiteTo(
            LoadWaterScreenSpaceShadows(screenUV).y, shadowParams.x);
    }
    else
    {
        realtimeShadow = MainLightRealtimeShadow(
            shadowCoord,
            positionWS,
            normalWS,
            shadowParams,
            GetMainLightShadowSamplingData(screenUV));
    }

#ifdef CALCULATE_BAKED_SHADOWS
    half bakedShadow = BakedShadow(shadowMask, _MainLightOcclusionProbes, shadowParams);
#else
    half bakedShadow = half(1.0);
#endif

    half shadow = ResolveMainLightShadow(realtimeShadow, bakedShadow, positionWS);

#if defined(_LIGHT_COOKIES)
    light.color *= SampleWaterMainLightCookie(positionWS);
#endif

    light.color = ApplyMainLightShadowTint(light.color, shadow);
    light.shadowAttenuation = 1.0h;
    return light;
}

float3 EvaluateWaterLightLoop(
    float3 positionWS,
    float2 positionNDC,
    float3 N,
    float3 geometryN,
    float3 V,
    BRDFData waterBRDF,
    float3 scatteringColor,
    float tipThickness,
    float surfaceFoam,
    WaterSurfaceProfile profile,
    bool aboveWater,
    uint featureFlags)
{
    InputData inputData = (InputData)0;
    inputData.positionWS = positionWS;
    inputData.normalWS = N;
    inputData.viewDirectionWS = V;
    inputData.normalizedScreenSpaceUV = positionNDC;

    inputData.shadowCoord = float4(0.0, 0.0, 0.0, 0.0);

    half4 shadowMask = half4(1.0h, 1.0h, 1.0h, 1.0h);
    float3 lighting = 0.0;

    Light mainLight = GetWaterMainLight(
        inputData.shadowCoord,
        inputData.positionWS,
        inputData.normalWS,
        inputData.normalizedScreenSpaceUV,
        shadowMask);

    lighting += EvaluateWaterDirectLight(
        mainLight, waterBRDF, N, geometryN, V,
        scatteringColor, tipThickness, surfaceFoam, profile, aboveWater);

#if USE_CLUSTER_LIGHT_LOOP
    UNITY_LOOP for (uint lightIndex = 0; lightIndex < min(URP_FP_DIRECTIONAL_LIGHTS_COUNT, MAX_VISIBLE_LIGHTS); lightIndex++)
    {
        CLUSTER_LIGHT_LOOP_SUBTRACTIVE_LIGHT_CHECK
        Light additionalLight = GetAdditionalLight(lightIndex, inputData.positionWS, shadowMask);
        lighting += EvaluateWaterDirectLight(
            additionalLight, waterBRDF, N, geometryN, V,
            scatteringColor, tipThickness, surfaceFoam, profile, aboveWater);
    }
#endif

    if ((featureFlags & WATER_LIGHTING_FEATURE_PUNCTUAL) != 0u)
    {
        uint pixelLightCount = GetAdditionalLightsCount();
        LIGHT_LOOP_BEGIN(pixelLightCount)
            Light additionalLight = GetAdditionalLight(lightIndex, inputData.positionWS, shadowMask);
            lighting += EvaluateWaterDirectLight(
                additionalLight, waterBRDF, N, geometryN, V,
                scatteringColor, tipThickness, surfaceFoam, profile, aboveWater);
        LIGHT_LOOP_END
    }

    return lighting;
}



half EvaluateWaterCausticsShadow(float3 positionWS, float2 positionNDC)
{
    half shadow = 1.0h;

    UNITY_BRANCH
    if (_WaterHasScreenSpaceShadows != 0u)
    {
        shadow = LerpWhiteTo(LoadWaterScreenSpaceShadows(positionNDC).x, GetMainLightShadowParams().x);
    }
#if defined(_MAIN_LIGHT_SHADOWS) || defined(_MAIN_LIGHT_SHADOWS_CASCADE)
    else
    {
        shadow = MainLightRealtimeShadow(TransformWorldToShadowCoord(positionWS));
    }
#endif

    return shadow;
}

half4 ShadeDeferredWaterSurface(
    float2 positionNDC,
    uint2 positionSS,
    uint featureFlags,
    float4 metadata,
    float rawWaterDepth,
    float4 geometryData,
    uint packedMetadata,
    WaterSurfaceProfile profile,
    float4 materialData)
{
    float4 surfaceData = LoadWaterSurfaceGBuffer(positionSS);
    float3 positionWS = ComputeWorldSpacePosition(positionNDC, rawWaterDepth, UNITY_MATRIX_I_VP);
    float3 V = SafeNormalize(GetWorldSpaceNormalizeViewDir(positionWS));
    float3 upDirection = GetDeferredWaterUpDirection(profile);

    float3 N = DecodeWaterNormal(surfaceData.rgb);
    float3 geometryN = DecodeWaterNormal(geometryData.rgb);
    bool frontFace = (packedMetadata & 1u) != 0u;
    if (dot(N, V) < 0.0) N = -N;
    if (dot(geometryN, V) < 0.0) geometryN = -geometryN;

    float surfaceFoam = saturate(geometryData.w * 0.5);
    float deepFoam = saturate(materialData.a * profile.deepFoamWeight);
    float bubbleFoam = saturate(surfaceData.a * profile.bubbleFoamWeight);

    float3 refractedPositionWS;
    float2 distortedNDC;
    float3 absorptionTint;
    float interfaceReflectance;
    ComputeWaterRefractionParameters(
        positionWS,
        positionNDC,
        V,
        N,
        frontFace,
        upDirection,
        profile.maxRefractionDistance,
        profile.extinction,
        refractedPositionWS,
        distortedNDC,
        absorptionTint,
        interfaceReflectance);

    float waterZ = LinearEyeDepth(rawWaterDepth, _ZBufferParams);
    float sceneRawDepth = WaterSampleCameraDepth(positionNDC);
    float sceneZ = LinearEyeDepth(sceneRawDepth, _ZBufferParams);
    float opticalDepth = max(sceneZ - waterZ, 0.0);

    float perceptualRoughness = 1.0 - saturate(metadata.b);

    BRDFData waterBRDF;
    InitializeWaterBRDFDataFromPerceptualRoughness(perceptualRoughness, waterBRDF);
    float3 cameraColor = 0.0;
    if (interfaceReflectance < 1.0)
        cameraColor = SampleWaterSourceColor(distortedNDC);
    if (frontFace)
    {
        if ((SampleWaterSurfaceStencil(distortedNDC) & 128u) == 0u)
            cameraColor *= absorptionTint * absorptionTint;

        if (profile.causticsAppearanceParameters.x > 0.0)
        {
            Light causticsLight = GetMainLight();
            half causticsShadow = EvaluateWaterCausticsShadow(refractedPositionWS, distortedNDC);
            cameraColor *= EvaluateWaterCaustics(
                refractedPositionWS,
                positionWS.y,
                causticsLight.direction,
                causticsShadow,
                profile.causticsDirectionalShadowDimmer,
                distance(GetCameraPositionWS(), refractedPositionWS),
                profile.causticsAppearanceParameters,
                profile.causticsProjectionParameters,
                false);
        }

        cameraColor *= EvaluateWaterRefractionColor(absorptionTint);
    }

    float3 transmittedColor = cameraColor
        * (1.0 - surfaceFoam)
        * (1.0 - interfaceReflectance);

    if (frontFace)
        transmittedColor = ApplyWaterBodyColorStylization(
            transmittedColor, opticalDepth, profile);

    float3 deepFoamTint = saturate(profile.deepFoamColor);
    transmittedColor *= lerp(1.0.xxx, deepFoamTint, deepFoam * 0.78);
    transmittedColor *= 1.0 - bubbleFoam * 0.32;

    float scatteringTerms = DecodeWaterUnorm16(materialData.rg);
    float normalizedHeight = materialData.b;
    float3 scatteringColor = EvaluateDeferredWaterScattering(
        scatteringTerms, surfaceFoam, absorptionTint, profile, frontFace);
    if (frontFace)
        scatteringColor = ApplyWaterBodyColorStylization(
            scatteringColor, opticalDepth, profile);

    float tipThickness = EvaluateDeferredWaterTipThickness(
        V, geometryN, normalizedHeight, upDirection);
    float3 directLighting = EvaluateWaterLightLoop(
        positionWS,
        positionNDC,
        N,
        geometryN,
        V,
        waterBRDF,
        scatteringColor,
        tipThickness,
        surfaceFoam,
        profile,
        frontFace,
        featureFlags);

    float3 bakedGI = EvaluateWaterProbeLighting(
        GetAbsolutePositionWS(positionWS),
        N,
        V,
        float2(positionSS),
        profile);
    bakedGI = max(bakedGI, 0.0) * _WaterAmbientProbeDimmer;
    float3 foamColor = profile.foamColor * surfaceFoam;
    float3 foamEnvironment = max(
        bakedGI,
        max(SampleWaterSkyReflection(upDirection, 1.0), 0.0)
            * _WaterAmbientProbeDimmer);
    float bubbleVisibility = frontFace ? 0.75 : 1.0;
    float3 bubbleLighting = foamEnvironment * profile.bubbleFoamColor
        * bubbleFoam * bubbleVisibility;
    float3 ambientLighting = bakedGI * scatteringColor
        + foamEnvironment * foamColor
        + bubbleLighting;

    float3 underwaterReflectionColor = max(
        bakedGI,
        max(profile.ambientLighting, 0.0)) * profile.scatteringAlbedo;

    float3 reflectedLighting = EvaluateWaterEnvironmentSpecular(
        positionWS, N, geometryN, V, waterBRDF, positionNDC,
        profile.environmentPerceptualRoughness, frontFace, interfaceReflectance,
        underwaterReflectionColor,
        profile.reflectionParameters.x > 0.5, featureFlags);

    float3 color = directLighting
        + ambientLighting
        + reflectedLighting
        + transmittedColor;

    SurfaceMediumComposite surfaceMedium = EvaluateSurfaceMedium(
        positionNDC,
        rawWaterDepth,
        _WaterSurfaceMediumAtmosphereEnabled,
        _WaterSurfaceMediumFogEnabled,
        _WaterSurfaceMediumFogSkyEnabled,
        _WaterSurfaceMediumVolumetricFogEnabled);
    color = surfaceMedium.additiveRadiance + color * surfaceMedium.transmittance;

    return half4(color, 1.0);
}

half4 ShadeDeferredWater(
    float2 positionNDC,
    uint2 positionSS,
    uint featureFlags,
    float4 metadata,
    float rawWaterDepth)
{
    float4 geometryData = LoadWaterGeometryGBuffer(positionSS);
    uint packedMetadata = DecodeWaterPackedMetadata(metadata.a);
    int surfaceIndex = DecodeWaterSurfaceIndex(packedMetadata);
    half4 color = 0.0h;
    if (surfaceIndex >= 0)
    {
        WaterSurfaceProfile profile = _WaterSurfaceProfiles[surfaceIndex];
        float4 materialData = LoadWaterMaterialGBuffer(positionSS);
        if (profile.renderMode == WATERRENDERMODE_DEBUG)
        {
            color = half4(materialData.rgb, 1.0);
        }
        else
        {
            color = ShadeDeferredWaterSurface(
                positionNDC,
                positionSS,
                featureFlags,
                metadata,
                rawWaterDepth,
                geometryData,
                packedMetadata,
                profile,
                materialData);
        }
    }
    return color;
}

#endif
