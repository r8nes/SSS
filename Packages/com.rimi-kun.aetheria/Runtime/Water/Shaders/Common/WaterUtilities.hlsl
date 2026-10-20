#ifndef WATER_UTILITIES_INCLUDED
#define WATER_UTILITIES_INCLUDED

#define WATER_IOR 1.3333
#define WATER_INV_IOR (1.0 / WATER_IOR)
#define WATER_FRESNEL_ZERO ((WATER_IOR - 1.0) * (WATER_IOR - 1.0) / ((WATER_IOR + 1.0) * (WATER_IOR + 1.0)))
#define WATER_TIP_ANISOTROPY 0.92
#define WATER_BODY_ANISOTROPY 0.5
#define WATER_AMBIENT_SCATTERING_INTENSITY 0.25
#define WATER_HEIGHT_SCATTERING_INTENSITY 0.25
#define WATER_DISPLACEMENT_SCATTERING_INTENSITY 0.25
#define WATER_BACKGROUND_ABSORPTION_DISTANCE 1000.0
#define WATER_UNDERWATER_REFRACTION_DISTANCE 100.0
#define WATER_SCATTERING_IRRADIANCE_SPREAD 1.2

#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/CommonLighting.hlsl"
#include "../../../../ShaderLibrary/Common/PhaseFunctions.hlsl"
#include "WaterCommon.hlsl"
#include "WaterDepthSampling.hlsl"
#include "WaterCascadeUtilities.hlsl"

float2 EncodeWaterUnorm16(float value)
{
    uint packed = (uint)round(saturate(value) * 65535.0);
    return float2(packed & 255u, packed >> 8u) / 255.0;
}

float DecodeWaterUnorm16(float2 encoded)
{
    uint2 bytes = (uint2)round(saturate(encoded) * 255.0);
    return (float)(bytes.x | (bytes.y << 8u)) / 65535.0;
}

float RemapWaterGeometryHeight(float geometryHeight)
{
    geometryHeight /= max(_WaterScatteringWaveHeight, 1e-4);
    return saturate(geometryHeight * 0.5 + 0.5);
}

float EvaluateWaterHeightBasedScattering(float normalizedGeometryHeight, float distanceToCamera)
{
    normalizedGeometryHeight = lerp(
        0.5,
        normalizedGeometryHeight,
        EvaluateWaterCascadeDistanceFade(distanceToCamera * 2.0, 0));

    float heightScattering = lerp(0.0, WATER_HEIGHT_SCATTERING_INTENSITY, normalizedGeometryHeight);
    return lerp(0.0, heightScattering, _WaterHeightScattering);
}

float EvaluateWaterDisplacementScattering(float displacement)
{
    float displacementScattering = lerp(
        0.0,
        WATER_DISPLACEMENT_SCATTERING_INTENSITY,
        displacement / max(_WaterScatteringWaveHeight, 1e-4));
    return lerp(0.0, displacementScattering, _WaterDisplacementScattering);
}
float EvaluateWaterScatteringIrradiance(float lowFrequencyNdotL)
{
    return ComputeWrappedDiffuseLighting(
        lowFrequencyNdotL,
        WATER_SCATTERING_IRRADIANCE_SPREAD);
}

float EvaluateWaterDirectionalScatteringGain(
    float3 lightDirectionWS,
    float3 viewDirectionWS,
    float3 lowFrequencyNormalWS,
    float3 upDirectionWS,
    float tipThickness,
    float tipScattering,
    float bodyScattering)
{
    float3 upDirection = SafeNormalize(upDirectionWS);
    float3 biasedLightDirection = SafeNormalize(
        SafeNormalize(lightDirectionWS)
        - upDirection * ((1.0 - WATER_INV_IOR) * 2.0));
    float3 singleScatteringRay = refract(
        -SafeNormalize(viewDirectionWS),
        SafeNormalize(lowFrequencyNormalWS),
        WATER_INV_IOR);
    float cosTheta = clamp(dot(singleScatteringRay, biasedLightDirection), -1.0, 1.0);
    float anisotropy = lerp(WATER_TIP_ANISOTROPY, WATER_BODY_ANISOTROPY, tipThickness);
    float phaseGain = 4.0 * PI * CornetteShanksPhaseFunction(anisotropy, cosTheta);
    float anisotropyWeight = lerp(tipScattering, bodyScattering, tipThickness);
    return lerp(1.0, phaseGain, saturate(anisotropyWeight));
}


float EvaluateWaterScatteringTerms(float3 positionWS, float geometryHeight, float3 displacement)
{
    float distanceToCamera = length(positionWS - displacement - GetCameraPositionWS());
    float heightScattering = EvaluateWaterHeightBasedScattering(
        RemapWaterGeometryHeight(geometryHeight), distanceToCamera);
    float displacementScattering = EvaluateWaterDisplacementScattering(length(displacement.xz));
    float ambientScattering = WATER_AMBIENT_SCATTERING_INTENSITY * _WaterAmbientScattering;
    return saturate(ambientScattering + heightScattering + displacementScattering);
}
float3 EvaluateWaterRefractionColor(float3 absorptionTint)
{
    return absorptionTint * absorptionTint;
}

float EvaluateWaterReflectionSmoothness(
    float3 positionWS,
    float surfaceFoam,
    float opticalDepth,
    float nearSmoothness,
    float foamSmoothness,
    float farSmoothness,
    float smoothnessFadeStart,
    float smoothnessFadeDistance,
    float3 shorelineSmoothnessParameters)
{
    float smoothness = lerp(nearSmoothness, foamSmoothness, saturate(surfaceFoam));
    float perceptualRoughness = 1.0 - saturate(smoothness);
    float farRoughness = 1.0 - saturate(farSmoothness);

    if (perceptualRoughness < farRoughness)
    {
        float blend = saturate(
            (distance(positionWS, GetCameraPositionWS()) - smoothnessFadeStart)
            / max(smoothnessFadeDistance, 1e-4));
        perceptualRoughness = lerp(perceptualRoughness, farRoughness, blend);
    }

    float shorelineWeight = (1.0 - saturate(
        opticalDepth / max(shorelineSmoothnessParameters.z, 1e-4)))
        * saturate(shorelineSmoothnessParameters.y)
        * (1.0 - saturate(surfaceFoam));

    return saturate(lerp(
        1.0 - saturate(perceptualRoughness),
        saturate(shorelineSmoothnessParameters.x),
        shorelineWeight));
}

float EvaluateWaterEdgeBlend(float2 screenPosition, float distanceToWaterSurface)
{
    float2 screenPosNDC = screenPosition * 2.0 - 1.0;
    float distanceToEdge = 1.0 - min(1.0 - abs(screenPosNDC.x), 1.0 - abs(screenPosNDC.y));
    float distanceAttenuation = 1.0 - saturate((distanceToWaterSurface - 0.75) / 0.25);
    return lerp(
        saturate((distanceToEdge - 0.8) / 0.2),
        saturate(distanceToEdge + 0.25),
        distanceAttenuation);
}

float EvaluateWaterInterfaceReflectance(float3 V, float3 N, bool aboveWater)
{
    float reflectance = 1.0;
    float cosIncident = saturate(dot(N, V));
    float etaIncident = aboveWater ? 1.0 : WATER_IOR;
    float etaTransmitted = aboveWater ? WATER_IOR : 1.0;
    float eta = etaIncident / etaTransmitted;
    float sinTransmittedSq = eta * eta * max(1.0 - cosIncident * cosIncident, 0.0);
    if (sinTransmittedSq < 1.0)
    {
        float cosTransmitted = sqrt(max(1.0 - sinTransmittedSq, 0.0));
        float rsNumerator = etaIncident * cosIncident - etaTransmitted * cosTransmitted;
        float rsDenominator = etaIncident * cosIncident + etaTransmitted * cosTransmitted;
        float rpNumerator = etaTransmitted * cosIncident - etaIncident * cosTransmitted;
        float rpDenominator = etaTransmitted * cosIncident + etaIncident * cosTransmitted;
        float rs = rsNumerator / max(abs(rsDenominator), 1e-6);
        float rp = rpNumerator / max(abs(rpDenominator), 1e-6);
        reflectance = saturate(0.5 * (rs * rs + rp * rp));
    }
    return reflectance;
}

bool WaterUVInsideScreen(float2 uv)
{
    return all(uv >= 0.0) && all(uv < 1.0);
}

void ComputeWaterRefractionParameters(float3 waterPositionWS, float2 positionNDC, float3 V,
    float3 waterNormalWS, bool aboveWater, float3 upDirection,
    float maxRefractionDistance, float3 extinction,
    out float3 refractedWaterPositionWS, out float2 distortedWaterNDC,
    out float3 absorptionTint, out float interfaceReflectance)
{
    absorptionTint = 0.0;
    interfaceReflectance = EvaluateWaterInterfaceReflectance(V, waterNormalWS, aboveWater);

    float directWaterDepth = WaterSampleCameraDepth(positionNDC);
    float3 directWaterPositionWS = directWaterDepth == UNITY_RAW_FAR_CLIP_VALUE
        ? waterPositionWS
        : ComputeWorldSpacePosition(positionNDC, directWaterDepth, UNITY_MATRIX_I_VP);
    float underWaterDistance = directWaterDepth == UNITY_RAW_FAR_CLIP_VALUE
        ? WATER_BACKGROUND_ABSORPTION_DISTANCE
        : length(directWaterPositionWS - waterPositionWS);

    float3 distortedWaterWS;
    if (aboveWater)
    {
        float edgeBlend = EvaluateWaterEdgeBlend(
            positionNDC,
            distance(waterPositionWS, GetCameraPositionWS()));
        float3 refractedView = lerp(waterNormalWS, upDirection, edgeBlend);
        refractedView -= upDirection * dot(refractedView, upDirection);
        distortedWaterWS = waterPositionWS
            + refractedView * min(underWaterDistance, maxRefractionDistance);
    }
    else
    {
        float3 refractedView = refract(-V, waterNormalWS, WATER_IOR);
        if (any(refractedView != 0.0))
            absorptionTint = 1.0;
        distortedWaterWS = waterPositionWS
            + refractedView * WATER_UNDERWATER_REFRACTION_DISTANCE;
    }

    distortedWaterNDC = saturate(WaterComputeNormalizedDeviceCoordinates(distortedWaterWS));
    distortedWaterNDC = min(
        distortedWaterNDC,
        1.0 - rcp(max(_ScaledScreenParams.xy, float2(1.0, 1.0))));

    float refractedWaterDepth = WaterSampleCameraDepth(distortedWaterNDC);
    refractedWaterPositionWS = refractedWaterDepth == UNITY_RAW_FAR_CLIP_VALUE
        ? directWaterPositionWS
        : ComputeWorldSpacePosition(distortedWaterNDC, refractedWaterDepth, UNITY_MATRIX_I_VP);
    float refractedWaterDistance = refractedWaterDepth == UNITY_RAW_FAR_CLIP_VALUE
        ? WATER_BACKGROUND_ABSORPTION_DISTANCE
        : length(refractedWaterPositionWS - waterPositionWS);

    if (refractedWaterDepth != UNITY_RAW_FAR_CLIP_VALUE
        && dot(refractedWaterPositionWS - waterPositionWS, V) > 0.0)
    {
        refractedWaterDistance = underWaterDistance;
        refractedWaterPositionWS = directWaterPositionWS;
        distortedWaterNDC = positionNDC;
    }

    if (aboveWater)
        absorptionTint = exp(-refractedWaterDistance * extinction);
}

#endif
