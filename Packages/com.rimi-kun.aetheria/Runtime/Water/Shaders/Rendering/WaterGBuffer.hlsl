#ifndef WATER_GBUFFER_INCLUDED
#define WATER_GBUFFER_INCLUDED

#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Packing.hlsl"
#include "../Common/WaterUtilities.hlsl"
#include "../Common/WaterNormalMaps.hlsl"
#include "../Common/WaterPhysicalFoamUtilities.hlsl"
#if defined(WATER_DEBUG_PASS)
#include "../Common/WaterDebug.hlsl"
#endif

struct WaterGBufferOutputs
{
    half4 materialData : SV_Target0;
    half4 surfaceData : SV_Target1;
    half4 geometryData : SV_Target2;
    half4 metadata : SV_Target3;
};

float3 EncodeWaterNormal(float3 normalWS)
{
    float2 octNormal = PackNormalOctQuadEncode(SafeNormalize(normalWS));
    return PackFloat2To888(saturate(octNormal * 0.5 + 0.5));
}

float2 EncodeWaterNormalOct2(float3 normalWS)
{
    return saturate(PackNormalOctQuadEncode(SafeNormalize(normalWS)) * 0.5 + 0.5);
}

float EncodeWaterPackedMetadata(bool isFrontFace)
{
    uint packed = (((uint)_WaterSurfaceIndex + 1u) << 1) | (isFrontFace ? 1u : 0u);
    return (float)packed / 255.0;
}

WaterGBufferOutputs BuildWaterGBufferOutputs(
    float scatteringTerms,
    float normalizedHeight,
    float3 normalWS,
    float reflectionSmoothness,
    float surfaceFoam,
    float deepFoam,
    float bubbleFoam,
    float3 geometryNormalWS,
    bool isFrontFace)
{
    WaterGBufferOutputs output;
    float2 encodedScatteringTerms = EncodeWaterUnorm16(scatteringTerms);
    output.materialData = half4(encodedScatteringTerms, saturate(normalizedHeight), saturate(deepFoam));
    output.surfaceData = half4(EncodeWaterNormal(normalWS), saturate(bubbleFoam));
    output.geometryData = half4(EncodeWaterNormal(geometryNormalWS), saturate(surfaceFoam * 2.0));
    output.metadata = half4(
        EncodeWaterNormalOct2(normalWS),
        saturate(reflectionSmoothness),
        EncodeWaterPackedMetadata(isFrontFace));
    return output;
}

WaterGBufferOutputs BuildWaterDebugGBufferOutputs(
    half4 debugColor,
    float3 normalWS,
    float reflectionSmoothness,
    bool isFrontFace)
{
    WaterGBufferOutputs output;
    output.materialData = half4(debugColor.rgb, 0.0);
    output.surfaceData = 0.0h;
    output.geometryData = 0.0h;
    output.metadata = half4(
        EncodeWaterNormalOct2(normalWS),
        saturate(reflectionSmoothness),
        EncodeWaterPackedMetadata(isFrontFace));
    return output;
}

WaterGBufferOutputs EvaluateWaterGBuffer(
    WaterSurfaceVaryings input,
    float3 normalWS,
    float3 geometryNormalWS,
    float reflectionSmoothness,
    float surfaceFoam,
    float deepFoam,
    float bubbleFoam,
    bool isFrontFace)
{
    normalWS = SafeNormalize(normalWS);
    geometryNormalWS = SafeNormalize(geometryNormalWS);

    float scatteringTerms = EvaluateWaterScatteringTerms(
        input.positionWS, input.displacement.w, input.displacement.xyz);
    float normalizedHeight = RemapWaterGeometryHeight(input.displacement.w);

    return BuildWaterGBufferOutputs(
        scatteringTerms,
        normalizedHeight,
        normalWS,
        reflectionSmoothness,
        surfaceFoam,
        deepFoam,
        bubbleFoam,
        geometryNormalWS,
        isFrontFace);
}

float WaterFoamHash21(float2 p)
{
    p = frac(p * float2(123.34, 456.21));
    p += dot(p, p + 45.32);
    return frac(p.x * p.y);
}

float WaterFoamValueNoise(float2 p)
{
    float2 cell = floor(p);
    float2 local = frac(p);
    float2 smoothLocal = local * local * (3.0 - 2.0 * local);
    float lowerLeft = WaterFoamHash21(cell);
    float lowerRight = WaterFoamHash21(cell + float2(1.0, 0.0));
    float upperLeft = WaterFoamHash21(cell + float2(0.0, 1.0));
    float upperRight = WaterFoamHash21(cell + float2(1.0, 1.0));
    return lerp(lerp(lowerLeft, lowerRight, smoothLocal.x), lerp(upperLeft, upperRight, smoothLocal.x), smoothLocal.y);
}

float WaterFoamNoise(float2 p)
{
    float primaryNoise = WaterFoamValueNoise(p);
    float2 rotated = float2(
        0.7313537 * p.x - 0.6819984 * p.y,
        0.6819984 * p.x + 0.7313537 * p.y);
    float secondaryNoise = WaterFoamValueNoise(rotated * 1.913 + float2(19.17, 7.53));
    return primaryNoise * 0.68 + secondaryNoise * 0.32;
}

float WaterOpticalDepth(WaterSurfaceVaryings input)
{
    float2 positionNDC = GetNormalizedScreenSpaceUV(input.positionCS);
    float sceneRawDepth = WaterSampleCameraDepth(positionNDC);
    float sceneZ = LinearEyeDepth(sceneRawDepth, _ZBufferParams);
    float waterZ = max(-TransformWorldToView(input.positionWS).z, 0.0);
    return max(sceneZ - waterZ, 0.0);
}

float WaterShorelineFoam(float2 worldXZ, float opticalDepth)
{
    float foamDistance = max(_WaterShorelineFoamParameters.y, 1e-4);
    float noiseScale = max(_WaterShorelineFoamNoiseParameters.y, 1e-4);
    float noiseAmount = saturate(_WaterShorelineFoamNoiseParameters.x);
    float detail = lerp(0.75, 2.5, saturate(_WaterShorelineFoamParameters.z * 0.1));
    float drift = _TimeParameters.x * _WaterShorelineFoamParameters.w * 0.025;
    float2 driftVector = float2(drift, -drift * 0.71);
    float2 noiseUV = worldXZ / noiseScale;

    float noiseA = SAMPLE_TEXTURE2D(
        _WaterShorelineFoamNoiseMap,
        sampler_WaterShorelineFoamNoiseMap,
        noiseUV + driftVector).r;
    float noiseB = SAMPLE_TEXTURE2D(
        _WaterShorelineFoamNoiseMap,
        sampler_WaterShorelineFoamNoiseMap,
        noiseUV * detail + float2(13.37, 7.91) - driftVector * 0.43).r;
    float procedural = WaterFoamNoise(noiseUV * 0.82 + driftVector * 0.31);
    float breakup = saturate(noiseA * 0.46 + noiseB * 0.31 + procedural * 0.23);

    float distanceVariation = lerp(1.0, lerp(0.58, 1.42, breakup), noiseAmount);
    float shoreline = saturate(1.0 - opticalDepth / max(foamDistance * distanceVariation, 1e-4));
    float shorelineShape = smoothstep(0.0, 0.38, shoreline);
    float breakupThreshold = lerp(0.64, 0.20, saturate(shoreline * 1.25));
    float brokenFoam = smoothstep(breakupThreshold, breakupThreshold + 0.16, breakup);
    float contactFoam = smoothstep(0.68, 0.96, shoreline);
    float foam = max(contactFoam, brokenFoam * shorelineShape);

    return saturate(foam * _WaterShorelineFoamParameters.x);
}

WaterGBufferOutputs FragWaterGBuffer(WaterSurfaceVaryings input, FRONT_FACE_TYPE faceSign : FRONT_FACE_SEMANTIC)
{
    float2 positionNDC = GetNormalizedScreenSpaceUV(input.positionCS);
    float opaqueDeviceDepth = WaterSampleCameraDepth(positionNDC);
    float waterDeviceDepth = input.positionCS.z;
    if (!COMPARE_DEVICE_DEPTH_CLOSEREQUAL(waterDeviceDepth, opaqueDeviceDepth))
        discard;


    bool isFrontFace = ResolveWaterSurfaceFrontFace(input, faceSign);

#if defined(WATER_DEBUG_PASS)
    half4 debugColor;
    EvaluateWaterDebugColor(input, debugColor);

    float3 normalWS = SafeNormalize(input.normalWS);
    if (!isFrontFace)
        normalWS = -normalWS;

    float opticalDepth = WaterOpticalDepth(input);
    float reflectionSmoothness = EvaluateWaterReflectionSmoothness(
        input.positionWS,
        0.0,
        opticalDepth,
        _WaterNearSmoothness,
        _WaterFoamAppearanceParameters.x,
        _WaterFarSmoothness,
        _WaterSmoothnessFadeStart,
        _WaterSmoothnessFadeDistance,
        _WaterShorelineSmoothnessParameters.xyz);

    return BuildWaterDebugGBufferOutputs(
        debugColor, normalWS, reflectionSmoothness, isFrontFace);
#else
    float3 positionOS = input.waterUV0.xzy;

    WaterAdditionalData waterData;
    EvaluateWaterAdditionalDataLighting(positionOS, input.positionWS, waterData);

    float3 normalWS = SafeNormalize(waterData.normalWS);
    normalWS = all(abs(normalWS) < 1e-4) ? SafeNormalize(input.normalWS) : normalWS;
    float3 geometryNormalWS = SafeNormalize(waterData.geometryNormalWS);
    geometryNormalWS = all(abs(geometryNormalWS) < 1e-4) ? normalWS : geometryNormalWS;

    float3 detailNormalWS = normalWS;
    UNITY_BRANCH
    if (isFrontFace)
        detailNormalWS = EvaluateWaterDetailNormal(input.waterUV0.xy, normalWS, input.normalLod);
    WaterFoamVisuals foamVisuals = EvaluateWaterFoamVisuals(
        positionOS.xz,
        input.positionWS,
        detailNormalWS,
        waterData.surfaceFoam);

    float surfaceFoam = foamVisuals.surface;

    float opticalDepth = WaterOpticalDepth(input);
    float shorelineFoam = 0.0;
    if (_WaterShorelineFoamParameters.x > 1e-4)
        shorelineFoam = WaterShorelineFoam(input.positionWS.xz, opticalDepth);

    surfaceFoam = saturate(max(surfaceFoam, shorelineFoam));
    float foamNormalSuppression = saturate(shorelineFoam);
    normalWS = SafeNormalize(lerp(foamVisuals.normalWS, geometryNormalWS, foamNormalSuppression));

    if (!isFrontFace)
    {
        normalWS = -normalWS;
        detailNormalWS = -detailNormalWS;
        geometryNormalWS = -geometryNormalWS;
    }

    float reflectionSmoothness = EvaluateWaterReflectionSmoothness(
        input.positionWS,
        surfaceFoam,
        opticalDepth,
        _WaterNearSmoothness,
        _WaterFoamAppearanceParameters.x,
        _WaterFarSmoothness,
        _WaterSmoothnessFadeStart,
        _WaterSmoothnessFadeDistance,
        _WaterShorelineSmoothnessParameters.xyz);

    return EvaluateWaterGBuffer(
        input,
        normalWS,
        geometryNormalWS,
        reflectionSmoothness,
        surfaceFoam,
        foamVisuals.deep,
        foamVisuals.bubbles,
        isFrontFace);
#endif
}

#endif
