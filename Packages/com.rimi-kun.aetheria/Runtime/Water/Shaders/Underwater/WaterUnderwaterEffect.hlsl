#ifndef WATER_UNDERWATER_INCLUDED
#define WATER_UNDERWATER_INCLUDED

#include "../../../../ShaderLibrary/Common/PhaseFunctions.hlsl"
#include "../../Core/WaterShaderVariables.cs.hlsl"
#include "../Common/WaterCaustics.hlsl"
#include "../Common/WaterUnderwaterMask.hlsl"

TEXTURE2D_X(_WaterUnderwaterCameraColorTexture);
TEXTURE2D_X_FLOAT(_WaterUnderwaterSceneDepthTexture);
Texture2D<float4> _BeforeRefractionTexture;
TEXTURE2D_X_FLOAT(_WaterDepthTexture);
uint _WaterHasScreenSpaceShadows;
float4 _WaterUnderwaterAbsorption;
float4 _WaterUnderwaterScattering;
float4 _WaterUnderwaterAmbientLighting;
float _WaterUnderwaterSurfaceLevel;
float4 _WaterUnderwaterSurfacePositionWS;
float4 _WaterUnderwaterUpDirectionWS;
float _WaterUnderwaterVolumeExtinctionLength;
float _WaterUnderwaterExtinctionMultiplier;
float _WaterUnderwaterSunBoost;
float _WaterUnderwaterOutScatteringFactor;
float _WaterUnderwaterOutScatteringExtinctionFactor;
float _WaterUnderwaterAnisotropy;
float _WaterUnderwaterDirectLightingMultiplier;
float _WaterUnderwaterAmbientLightingMultiplier;
float _WaterUnderwaterAmbientShadowInfluence;
float _WaterUnderwaterDitheringIntensity;
float _WaterUnderwaterDitheringEnabled;
StructuredBuffer<float> _WaterCameraHeightBuffer;

float GetWaterCameraHeight()
{
    return _WaterCameraHeightBuffer[0];
}
float4 _WaterCausticsAppearanceParameters;
float _WaterCausticsShadowDimmer;
float4 _WaterCausticsProjectionParameters;

struct WaterUnderwaterAttributes
{
    uint id : SV_VertexID;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct WaterUnderwaterVaryings
{
    float4 positionCS : SV_POSITION;
    UNITY_VERTEX_OUTPUT_STEREO
};

WaterUnderwaterVaryings VertUnderwater(WaterUnderwaterAttributes input)
{
    WaterUnderwaterVaryings output = (WaterUnderwaterVaryings)0;
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
    output.positionCS = GetFullScreenTriangleVertexPosition(input.id, UNITY_RAW_FAR_CLIP_VALUE);
    return output;
}


float3 WaterUnderwaterScreenSpaceDither(float2 screenPosition)
{
    float3 dither = dot(float2(171.0, 231.0), screenPosition.xy);
    dither = frac(dither / float3(103.0, 71.0, 97.0)) - 0.5;
    return dither / 255.0;
}

float WaterUnderwaterTthgPhase(float forwardG, float cosTheta)
{
    float g1 = clamp(forwardG, 0.3062, 0.99);
    float g1Sq = g1 * g1;
    float g2 = -0.30614 + 1.0006 * g1 - 0.01826 * g1Sq + 0.03644 * g1Sq * g1;
    float weight = g2 * (1.0 + g2)
        / max((g1 + g2) * (1.0 + g2 - g1), 1e-5);
    return lerp(
        HenyeyGreensteinPhaseFunction(-g2, cosTheta),
        HenyeyGreensteinPhaseFunction(g1, cosTheta),
        saturate(weight));
}

float3 WaterUnderwaterRefractedLightDirection(float3 lightDirection)
{
    float3 upDirectionWS = SafeNormalize(_WaterUnderwaterUpDirectionWS.xyz);
    float3 transmittedDirection = refract(-SafeNormalize(lightDirection), upDirectionWS, 1.0 / 1.333);
    return -SafeNormalize(transmittedDirection);
}

float WaterUnderwaterDepthBelowSurface(float3 positionWS)
{
    float3 upDirectionWS = SafeNormalize(_WaterUnderwaterUpDirectionWS.xyz);
    return max(0.0, dot(_WaterUnderwaterSurfacePositionWS.xyz - positionWS, upDirectionWS));
}

void WaterUnderwaterVolumeLighting(
    half3 absorption,
    half3 scattering,
    half phaseG,
    half3 viewDirectionWS,
    half3 ambientLighting,
    half3 primaryLightDirection,
    half3 primaryLightIntensity,
    half ambientLightingTerm,
    half primaryLightingTerm,
    half waterRayLength,
    half sunBoost,
    out half3 volumeLight,
    out half3 volumeOpacity)
{
    half3 extinction = absorption + scattering;

    float sunPhase = WaterUnderwaterTthgPhase(phaseG, dot(-primaryLightDirection, viewDirectionWS));
    float3 inScatteredSun = (1.0 + sunBoost) * sunPhase * primaryLightIntensity * primaryLightingTerm;
    float3 inScatteredAmbient = ambientLighting * ambientLightingTerm;
    float3 inscattered = inScatteredAmbient + inScatteredSun;
    float3 scatteringAmount = saturate(scattering / max(extinction, 0.00001));
    volumeLight = inscattered * scatteringAmount;
    volumeOpacity = OpacityFromOpticalDepth(extinction * max(0.0, waterRayLength));
}

half WaterUnderwaterLoadSceneMainLightShadow(uint2 positionSS, float3 positionWS)
{
    UNITY_BRANCH
    if (_WaterHasScreenSpaceShadows != 0u)
    {
        half realtimeShadow = LOAD_TEXTURE2D_X(_ScreenSpaceShadowmapTexture, positionSS).r;
        return LerpWhiteTo(realtimeShadow, GetMainLightShadowParams().x);
    }

#if defined(_MAIN_LIGHT_SHADOWS) || defined(_MAIN_LIGHT_SHADOWS_CASCADE)
    return MainLightRealtimeShadow(TransformWorldToShadowCoord(positionWS));
#else
    return 1.0h;
#endif
}

half3 WaterUnderwaterApplyEffect(
    half3 sceneColor,
    float sceneZ,
    float waterRayLength,
    half3 view,
    uint2 positionSS,
    float3 positionWS,
    half shadow,
    bool hasCaustics)
{
    Light mainLight = GetMainLight();
    float3 lightDirection = mainLight.direction;
    float3 refractedLightDirection = WaterUnderwaterRefractedLightDirection(lightDirection);
    float3 lightColor = mainLight.color;

    half3 absorption = _WaterUnderwaterAbsorption.xyz;
    half3 scattering = _WaterUnderwaterScattering.xyz;
    half3 ambientLighting = _WaterUnderwaterAmbientLighting.xyz;

    if (hasCaustics && _WaterCausticsAppearanceParameters.x > 0.0)
    {
        sceneColor *= EvaluateWaterCaustics(
            positionWS,
            _WaterUnderwaterSurfaceLevel,
            refractedLightDirection,
            shadow,
            _WaterCausticsShadowDimmer,
            sceneZ,
            _WaterCausticsAppearanceParameters,
            _WaterCausticsProjectionParameters,
            true);
    }

    float3 cappedPositionWS = positionWS;
    float3 toSphere = -view * _WaterUnderwaterVolumeExtinctionLength * _WaterUnderwaterOutScatteringExtinctionFactor;
    float3 toScene = positionWS - _WorldSpaceCameraPos.xyz;
    cappedPositionWS = _WorldSpaceCameraPos.xyz + toSphere;
    cappedPositionWS = dot(toScene, toScene) < dot(toSphere, toSphere) ? positionWS : cappedPositionWS;

    half3 extinction = (absorption + scattering) * _WaterUnderwaterExtinctionMultiplier;
    float waterDepth = WaterUnderwaterDepthBelowSurface(cappedPositionWS);
    waterDepth *= _WaterUnderwaterOutScatteringFactor;
    float3 outScatteringTerm = TransmittanceFromOpticalDepth(extinction * waterDepth);
    sceneColor *= outScatteringTerm;
    lightColor *= outScatteringTerm;
    ambientLighting *= outScatteringTerm;

    half3 volumeLight;
    half3 volumeOpacity;
    WaterUnderwaterVolumeLighting(
        absorption * _WaterUnderwaterExtinctionMultiplier,
        scattering * _WaterUnderwaterExtinctionMultiplier,
        _WaterUnderwaterAnisotropy,
        view,
        ambientLighting,
        refractedLightDirection,
        lightColor,
        _WaterUnderwaterAmbientLightingMultiplier,
        _WaterUnderwaterDirectLightingMultiplier,
        waterRayLength,
        _WaterUnderwaterSunBoost,
        volumeLight,
        volumeOpacity);

    if (_WaterUnderwaterDitheringEnabled > 0.5)
        volumeLight += WaterUnderwaterScreenSpaceDither(positionSS) * _WaterUnderwaterDitheringIntensity;

    return lerp(sceneColor, volumeLight, volumeOpacity);
}


float WaterUnderwaterMeniscusWeight(float2 positionSS, WaterUnderwaterLineData lineData)
{
    float side = lineData.signedDistancePixels < 0.0 ? -1.0 : 1.0;
    float2 offsetDirection = -side * lineData.normalSS;
    float weight = 1.0;
    float distance1 = WaterUnderwaterEvaluateLine(positionSS + offsetDirection).signedDistancePixels;
    float distance2 = WaterUnderwaterEvaluateLine(positionSS + offsetDirection * 2.0).signedDistancePixels;
    float distance3 = WaterUnderwaterEvaluateLine(positionSS + offsetDirection * 3.0).signedDistancePixels;
    weight *= distance1 * side <= 0.0 ? 0.9 : 1.0;
    weight *= distance2 * side <= 0.0 ? 0.9 : 1.0;
    weight *= distance3 * side <= 0.0 ? 0.9 : 1.0;
    return weight;
}

half4 FragUnderwater(WaterUnderwaterVaryings input) : SV_Target
{
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

    float2 positionSS = input.positionCS.xy;
    uint2 pixel = (uint2)positionSS;
    WaterUnderwaterLineData lineData = WaterUnderwaterEvaluateLine(positionSS);
    float lineAntialias = clamp(fwidth(lineData.signedDistancePixels), 0.75, 1.5);
    float underwaterCoverage = 1.0 - smoothstep(
        -lineAntialias,
        lineAntialias,
        lineData.signedDistancePixels);
    float meniscusWeight = WaterUnderwaterMeniscusWeight(positionSS, lineData);

    half4 sourceColor = LOAD_TEXTURE2D_X(_WaterUnderwaterCameraColorTexture, pixel);
    float4 beforeRefraction = _BeforeRefractionTexture.Load(int3(pixel, 0));
    float beforeRefractionTransmittance = saturate(1.0 - beforeRefraction.a);
    if (underwaterCoverage <= 0.0 && meniscusWeight >= 1.0)
    {
        half3 compositeColor = beforeRefraction.rgb + sourceColor.rgb * beforeRefractionTransmittance;
        return half4(compositeColor, sourceColor.a);
    }

    float cameraHeight = GetWaterCameraHeight();
    float rawSceneDepth = LOAD_TEXTURE2D_X(_WaterUnderwaterSceneDepthTexture, pixel).r;
    float rawWaterDepth = LOAD_TEXTURE2D_X(_WaterDepthTexture, pixel).r;
    bool hasScene = rawSceneDepth != UNITY_RAW_FAR_CLIP_VALUE;
    bool hasWater = COMPARE_DEVICE_DEPTH_CLOSER(rawWaterDepth, rawSceneDepth);
    bool waterInFrontOfScene = hasWater && (!hasScene
        || COMPARE_DEVICE_DEPTH_CLOSEREQUAL(rawWaterDepth, rawSceneDepth));

    float2 uv = input.positionCS.xy / _ScaledScreenParams.xy;
    float sceneZ = unity_OrthoParams.w == 0.0
        ? LinearEyeDepth(rawSceneDepth, _ZBufferParams)
        : LinearDepthToEyeDepth(rawSceneDepth);
    float3 scenePositionWS = ComputeWorldSpacePosition(uv, rawSceneDepth, UNITY_MATRIX_I_VP);
    float waterRayLength = sceneZ;
    float3 effectPositionWS = scenePositionWS;
    bool hasCaustics = hasScene;

    if (waterInFrontOfScene)
    {
        float waterZ = unity_OrthoParams.w == 0.0
            ? LinearEyeDepth(rawWaterDepth, _ZBufferParams)
            : LinearDepthToEyeDepth(rawWaterDepth);
        float3 waterPositionWS = ComputeWorldSpacePosition(uv, rawWaterDepth, UNITY_MATRIX_I_VP);

        if (cameraHeight < 0.0)
        {
            sceneZ = waterZ;
            waterRayLength = waterZ;
            effectPositionWS = waterPositionWS;
            hasCaustics = false;
        }
        else if (hasScene)
        {
            waterRayLength = length(scenePositionWS - waterPositionWS);
        }
    }

    half shadow = hasScene ? WaterUnderwaterLoadSceneMainLightShadow(pixel, scenePositionWS) : 1.0h;
    half3 view = GetWorldSpaceNormalizeViewDir(effectPositionWS);
    half3 underwaterColor = WaterUnderwaterApplyEffect(
        sourceColor.rgb,
        sceneZ,
        waterRayLength,
        view,
        pixel,
        effectPositionWS,
        shadow,
        hasCaustics);

    half3 finalColor = lerp(sourceColor.rgb, underwaterColor, underwaterCoverage) * meniscusWeight;
    finalColor = beforeRefraction.rgb + finalColor * beforeRefractionTransmittance;
    return half4(finalColor, sourceColor.a);
}
#endif
