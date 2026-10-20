#ifndef WATER_PHYSICAL_FOAM_UTILITIES_INCLUDED
#define WATER_PHYSICAL_FOAM_UTILITIES_INCLUDED

struct WaterFoamVisuals
{
    float surface;
    float deep;
    float bubbles;
    float3 normalWS;
};

float SampleWaterPhysicalSurfaceFoamTextureLevel(float2 uv, float mipLevel)
{
    return _WaterPhysicalFoamTexture.SampleLevel(sampler_LinearRepeat, uv, mipLevel).r;
}

float4 SampleWaterPhysicalVolumeFoamTextureLevel(float2 uv, float mipLevel)
{
    return _WaterFoamTexture.SampleLevel(sampler_LinearRepeat, uv, mipLevel);
}

float2 EvaluateWaterPhysicalFoamDetailUV(float2 positionOS, float2 flowDirection, float phaseOffset)
{
    float phase = _WaterSimulationTime * 0.22 + phaseOffset;
    return (positionOS - flowDirection * phase) * max(_WaterFoamAppearanceParameters.w, 1e-4);
}

float SampleWaterPhysicalSurfaceFoamMultiscale(
    float2 positionOS,
    float2 flowDirection,
    float2 worldOffset,
    float lodAlpha,
    float lodScale)
{
    float phase = _WaterSimulationTime * 0.22;
    float2 worldPosition = positionOS - flowDirection * phase + worldOffset;
    float authoredScale = rcp(max(_WaterFoamAppearanceParameters.w, 1e-4));
    float scale = max(authoredScale * lodScale / 10.0, 1e-4);
    float currentLodDetail = SampleWaterPhysicalSurfaceFoamTextureLevel(worldPosition / scale, 0.0);
    float nextLodDetail = SampleWaterPhysicalSurfaceFoamTextureLevel(worldPosition / (2.0 * scale), 0.0);
    return lerp(currentLodDetail, nextLodDetail, lodAlpha);
}

float EvaluateWaterSurfaceFoamCoverage(
    float2 positionOS,
    float foamAmount,
    float2 flowDirection,
    out float lodAlpha,
    out float lodScale)
{
    lodAlpha = 0.0;
    lodScale = 1.0;
    if (foamAmount <= 1e-5)
        return 0.0;

    uint slice;
    WaterFoamSelectSurfaceLod(positionOS, slice, lodAlpha, lodScale);
    float detail = SampleWaterPhysicalSurfaceFoamMultiscale(
        positionOS, flowDirection, 0.0, lodAlpha, lodScale);
    float feather = max(_WaterFoamAppearanceParameters.y, 1e-3);
    float blackPoint = saturate(1.0 - max(foamAmount, 0.0));
    return smoothstep(blackPoint, blackPoint + feather, detail);
}

float EvaluateWaterDeepFoamCoverage(float2 positionOS, float deepAmount, float2 flowDirection)
{
    if (deepAmount <= 1e-5)
        return 0.0;

    float2 uv = EvaluateWaterPhysicalFoamDetailUV(positionOS, flowDirection, 3.7) * 0.72;
    float4 detail = SampleWaterPhysicalVolumeFoamTextureLevel(uv, 2.0);
    float structure = saturate(0.65 * detail.a + 0.35 * detail.g);
    return deepAmount * smoothstep(0.12, 0.78, structure + deepAmount * 0.35);
}

float EvaluateWaterBubbleFoamCoverage(
    float2 positionOS,
    float3 positionWS,
    float3 normalWS,
    float bubbleAmount,
    float2 flowDirection)
{
    if (bubbleAmount <= 1e-5)
        return 0.0;

    float3 viewWS = SafeNormalize(GetWorldSpaceNormalizeViewDir(positionWS));
    float3 axisX = SafeNormalize(TransformWaterDirectionToWorld(float3(1.0, 0.0, 0.0)));
    float3 axisZ = SafeNormalize(TransformWaterDirectionToWorld(float3(0.0, 0.0, 1.0)));
    float2 viewTangent = float2(dot(viewWS, axisX), dot(viewWS, axisZ));
    float viewNormal = max(abs(dot(viewWS, SafeNormalize(normalWS))), 0.15);
    float2 parallax = -_WaterFoamVolumeAppearanceParameters.x * viewTangent / viewNormal;

    float2 uv = EvaluateWaterPhysicalFoamDetailUV(positionOS + parallax, flowDirection, 7.3) * 0.74;
    float bubblePattern = SampleWaterPhysicalVolumeFoamTextureLevel(uv, 3.0).r;
    return bubbleAmount * smoothstep(0.10, 0.76, bubblePattern + bubbleAmount * 0.25);
}

float3 EvaluateWaterSurfaceFoamNormal(
    float2 positionOS,
    float3 baseNormalWS,
    float surfaceCoverage,
    float foamAmount,
    float2 flowDirection,
    float lodAlpha,
    float lodScale)
{
    float normalStrength = max(_WaterFoamAppearanceParameters.z, 0.0) * surfaceCoverage;
    if (normalStrength <= 1e-5)
        return baseNormalWS;

    float authoredScale = rcp(max(_WaterFoamAppearanceParameters.w, 1e-4));
    float worldDelta = max(0.0025 * authoredScale * lodScale, 1e-4);
    float detailX = SampleWaterPhysicalSurfaceFoamMultiscale(
        positionOS, flowDirection, float2(worldDelta, 0.0), lodAlpha, lodScale);
    float detailZ = SampleWaterPhysicalSurfaceFoamMultiscale(
        positionOS, flowDirection, float2(0.0, worldDelta), lodAlpha, lodScale);

    float feather = max(_WaterFoamAppearanceParameters.y, 1e-3);
    float blackPoint = saturate(1.0 - max(foamAmount, 0.0));
    float sampleX = smoothstep(blackPoint, blackPoint + feather, detailX);
    float sampleZ = smoothstep(blackPoint, blackPoint + feather, detailZ);
    float2 foamNormal = 0.01 * normalStrength
        * float2(sampleX - surfaceCoverage, sampleZ - surfaceCoverage) / worldDelta;

    float3 axisX = SafeNormalize(TransformWaterDirectionToWorld(float3(1.0, 0.0, 0.0)));
    float3 axisZ = SafeNormalize(TransformWaterDirectionToWorld(float3(0.0, 0.0, 1.0)));
    return SafeNormalize(baseNormalWS - foamNormal.x * axisX - foamNormal.y * axisZ);
}

WaterFoamVisuals EvaluateWaterFoamVisuals(
    float2 positionOS,
    float3 positionWS,
    float3 baseNormalWS,
    float foamAmount)
{
    WaterFoamVisuals output = (WaterFoamVisuals)0;

    float deepAmount = saturate(foamAmount * _WaterFoamVolumeAppearanceParameters.y);
    float bubbleAmount = saturate(foamAmount * _WaterFoamVolumeAppearanceParameters.z);
    bool surfaceActive = foamAmount > 1e-5;
    bool deepActive = deepAmount > 1e-5;
    bool bubbleActive = bubbleAmount > 1e-5;

    float2 flowDirection = 0.0;
    if (surfaceActive || deepActive || bubbleActive)
        flowDirection = EvaluateWaterFoamFlowDirection(positionOS);

    float lodAlpha;
    float lodScale;
    output.surface = EvaluateWaterSurfaceFoamCoverage(
        positionOS, foamAmount, flowDirection, lodAlpha, lodScale);
    output.deep = EvaluateWaterDeepFoamCoverage(positionOS, deepAmount, flowDirection);
    output.bubbles = EvaluateWaterBubbleFoamCoverage(
        positionOS, positionWS, baseNormalWS, bubbleAmount, flowDirection);
    output.normalWS = EvaluateWaterSurfaceFoamNormal(
        positionOS, baseNormalWS, output.surface, foamAmount,
        flowDirection, lodAlpha, lodScale);
    return output;
}

#endif
