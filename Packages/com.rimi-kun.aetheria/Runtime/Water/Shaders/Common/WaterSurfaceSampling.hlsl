#ifndef WATER_SURFACE_SAMPLING_INCLUDED
#define WATER_SURFACE_SAMPLING_INCLUDED

#ifndef WATER_CASCADE_LOOP
    #if defined(SHADER_STAGE_VERTEX)

        #define WATER_CASCADE_LOOP UNITY_UNROLL
    #else

        #define WATER_CASCADE_LOOP
    #endif
#endif

#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/GlobalSamplers.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Filtering.hlsl"
#include "../../Core/WaterDefinitions.cs.hlsl"
#if !defined(WATER_EVALUATION_NO_CURRENT)
#include "WaterCurrentUtilities.hlsl"
#endif
#include "WaterCascadeUtilities.hlsl"
#include "../Rendering/WaterSurfaceTransform.hlsl"

#if !defined(WATER_EVALUATION_DISPLACEMENT_ONLY)
#include "WaterFoamCascades.hlsl"
Texture2DArray<float4> _WaterFoamField;
Texture2D<float4> _WaterPhysicalFoamTexture;
#endif

#if !defined(WATER_EVALUATION_NO_MASK)
TEXTURE2D(_WaterSimulationMask);
SAMPLER(sampler_WaterSimulationMask);

float3 SampleWaterSimulationMaskUV(float2 uv)
{
#if defined(SHADER_STAGE_FRAGMENT)
    float3 mask = SAMPLE_TEXTURE2D(_WaterSimulationMask, sampler_WaterSimulationMask, uv).xyz;
#else
    float3 mask = SAMPLE_TEXTURE2D_LOD(_WaterSimulationMask, sampler_WaterSimulationMask, uv, 0).xyz;
#endif
    return saturate(_WaterSimulationMaskRemap.xxx + mask * _WaterSimulationMaskRemap.yyy);
}

float3 EvaluateWaterSimulationMask(float3 positionAWS)
{
    float3 baseMask;
    if (_WaterSimulationMaskEnabled == 0u)
        baseMask = saturate((_WaterSimulationMaskRemap.x + _WaterSimulationMaskRemap.y).xxx);
    else
    {
        float2 uv = TransformToWaterAxes(positionAWS.xz - _WaterSimulationMaskOffset) * _WaterSimulationMaskScale + 0.5;
        baseMask = SampleWaterSimulationMaskUV(uv);
    }
    return baseMask;
}

void EvaluateWaterSimulationMaskWithGradient(float3 positionAWS, out float3 mask, out float3 gradientX, out float3 gradientZ)
{
    if (_WaterSimulationMaskEnabled == 0u)
    {
        mask = saturate((_WaterSimulationMaskRemap.x + _WaterSimulationMaskRemap.y).xxx);
        gradientX = 0.0;
        gradientZ = 0.0;
    }
    else
    {
        float2 uv = TransformToWaterAxes(positionAWS.xz - _WaterSimulationMaskOffset) * _WaterSimulationMaskScale + 0.5;
        mask = SampleWaterSimulationMaskUV(uv);

        uint width;
        uint height;
        _WaterSimulationMask.GetDimensions(width, height);
        float2 texel = rcp(max(float2(width, height), 1.0));
        float3 maskXP = SampleWaterSimulationMaskUV(uv + float2(texel.x, 0.0));
        float3 maskXM = SampleWaterSimulationMaskUV(uv - float2(texel.x, 0.0));
        float3 maskZP = SampleWaterSimulationMaskUV(uv + float2(0.0, texel.y));
        float3 maskZM = SampleWaterSimulationMaskUV(uv - float2(0.0, texel.y));
        float3 gradientU = (maskXP - maskXM) / max(2.0 * texel.x, 1e-6);
        float3 gradientV = (maskZP - maskZM) / max(2.0 * texel.y, 1e-6);

        float2 axisU = float2(_WaterSurfaceTransform[0].x, _WaterSurfaceTransform[2].x);
        float2 axisV = float2(-axisU.y, axisU.x);
        gradientX = gradientU * (axisU.x * _WaterSimulationMaskScale.x)
            + gradientV * (axisV.x * _WaterSimulationMaskScale.y);
        gradientZ = gradientU * (axisU.y * _WaterSimulationMaskScale.x)
            + gradientV * (axisV.y * _WaterSimulationMaskScale.y);
    }
}
#else
float3 EvaluateWaterSimulationMask(float3 positionAWS)
{
    return saturate((_WaterSimulationMaskRemap.x + _WaterSimulationMaskRemap.y).xxx);
}

void EvaluateWaterSimulationMaskWithGradient(float3 positionAWS, out float3 mask, out float3 gradientX, out float3 gradientZ)
{
    mask = EvaluateWaterSimulationMask(positionAWS);
    gradientX = 0.0;
    gradientZ = 0.0;
}
#endif

void EvaluateWaterCascadeDistanceFadeWithGradient(float3 positionRWS, int cascadeIndex, out float fade, out float2 gradient)
{
#ifndef IGNORE_WATER_FADE
    float3 cameraDelta = positionRWS - GetCameraPositionWS();
    float distanceToCamera = length(cameraDelta);
    float2 fadeScaleBias = GetWaterCascadeScalesFade(cascadeIndex).zw;
    float rawFade = distanceToCamera * fadeScaleBias.x + fadeScaleBias.y;
    float saturatedFade = saturate(rawFade);
    float squaredFade = saturatedFade * saturatedFade;
    fade = Smoothstep01(squaredFade);
    gradient = 0.0;
    if (rawFade > 0.0 && rawFade < 1.0 && distanceToCamera > 1e-5)
    {
        float fadeDerivative = 12.0 * fadeScaleBias.x * saturatedFade * squaredFade * (1.0 - squaredFade);
        float3 distanceDirection = cameraDelta / distanceToCamera;
        float3 axisX = TransformWaterDirectionToWorld(float3(1.0, 0.0, 0.0));
        float3 axisZ = TransformWaterDirectionToWorld(float3(0.0, 0.0, 1.0));
        gradient = fadeDerivative * float2(dot(distanceDirection, axisX), dot(distanceDirection, axisZ));
    }
#else
    fade = 1.0;
    gradient = 0.0;
#endif
}

struct WaterCascadeSamplingData
{

    float2 uv;
    float blend;
    float2 blendGradient;
    float4 swizzle;
};

struct WaterSimulationCoordinates
{
    WaterCascadeSamplingData data[WATER_CASCADE_COUNT];
};

WaterCascadeSamplingData BuildWaterCascadeSamplingData(
    float2 uv,
    int cascadeIndex,
    float blend,
    float2 blendGradient,
    float4 swizzle)
{
    float4 scaleOffset = GetWaterCascadeScaleOffset(cascadeIndex);
    WaterCascadeSamplingData data;
    data.uv = uv * scaleOffset.xy - scaleOffset.zw;
    data.blend = blend;
    data.blendGradient = blendGradient;
    data.swizzle = swizzle;
    return data;
}

void InitializeWaterSimulationCoordinates(float2 uv, out WaterSimulationCoordinates coordinates)
{
    const float4 identitySwizzle = float4(1.0, 0.0, 0.0, 1.0);
    coordinates.data[0] = BuildWaterCascadeSamplingData(uv, 0, 1.0, 0.0, identitySwizzle);
    coordinates.data[1] = BuildWaterCascadeSamplingData(uv, 1, 1.0, 0.0, identitySwizzle);
    coordinates.data[2] = BuildWaterCascadeSamplingData(uv, 2, 1.0, 0.0, identitySwizzle);
}

#if !defined(WATER_EVALUATION_NO_CURRENT)
void BuildWaterCurrentSimulationCoordinates(float2 uv, WaterCurrentData currentData, bool useSecondaryDirection, out WaterSimulationCoordinates coordinates)
{
    float4 projectedCoordinates;
    BuildWaterCurrentSamplingCoordinates(uv, currentData.sectorIndex, projectedCoordinates);

    float2 samplingUv = useSecondaryDirection ? projectedCoordinates.zw : projectedCoordinates.xy;
    float blend = useSecondaryDirection ? currentData.sectorBlend : 1.0 - currentData.sectorBlend;
    float2 blendGradient = useSecondaryDirection
        ? currentData.sectorBlendGradient : -currentData.sectorBlendGradient;
    float4 direction = _WaterCurrentSectorSwizzles[currentData.sectorIndex + 8];
    float4 swizzle = useSecondaryDirection
        ? float4(direction.zw, -direction.w, direction.z)
        : float4(direction.xy, -direction.y, direction.x);

    coordinates.data[0] = BuildWaterCascadeSamplingData(samplingUv, 0, blend, blendGradient, swizzle);
    coordinates.data[1] = BuildWaterCascadeSamplingData(samplingUv, 1, blend, blendGradient, swizzle);
    coordinates.data[2] = BuildWaterCascadeSamplingData(samplingUv, 2, blend, blendGradient, swizzle);
}
#endif

#include "WaterWaveField.hlsl"
#if !defined(WATER_EVALUATION_DISPLACEMENT_ONLY)
#include "WaterFoamUtilities.hlsl"
#endif

float4 SampleWaterCascadeDisplacementLevel(float2 uv, int cascadeIndex, float mipLevel)
{
    return SampleWaterWaveFieldLevel(uv, cascadeIndex, WATER_WAVE_DISPLACEMENT_SLICE, mipLevel);
}

float4 SampleWaterCascadeAdditionalDataGrad(float2 uv, int cascadeIndex, float2 uvDx, float2 uvDy)
{
    return SampleWaterWaveFieldGrad(uv, cascadeIndex, WATER_WAVE_ADDITIONAL_DATA_SLICE, uvDx, uvDy);
}

float4 SampleWaterCascadeDisplacementLodLevel(float2 uv, int cascadeIndex, float mipLevel)
{
    return SampleWaterCascadeDisplacementLevel(uv, cascadeIndex, max(mipLevel, 0.0));
}

float4 SampleWaterCascadeVertexDisplacement(WaterCascadeSamplingData cascade, int cascadeIndex)
{
    return SampleWaterCascadeDisplacementLodLevel(cascade.uv, cascadeIndex, 0.0);
}

void SetWaterCascadeVerticalDisplacement(inout float3 values, int cascadeIndex, float value)
{

    if (cascadeIndex == 0) values.x = value;
    else if (cascadeIndex == 1) values.y = value;
    else values.z = value;
}

void SampleWaterSimulationVertexDisplacement(WaterSimulationCoordinates coordinates, float3 simulationMask, float distanceToCamera,
    out float2 horizontalDisplacement, out float3 verticalDisplacements)
{
    horizontalDisplacement = 0.0;
    verticalDisplacements = 0.0;
    WATER_CASCADE_LOOP for (int cascadeIndex = 0; cascadeIndex < WATER_CASCADE_COUNT; ++cascadeIndex)
    {
        if ((uint)cascadeIndex >= _WaterActiveCascadeCount)
            continue;

        WaterCascadeSamplingData cascade = coordinates.data[cascadeIndex];
        float weight = cascade.blend * EvaluateWaterCascadeDistanceFade(distanceToCamera, cascadeIndex)
            * simulationMask[cascadeIndex] * GetWaterCascadeDisplacementScale(cascadeIndex);
        if (weight == 0.0)
            continue;

        float3 rawDisplacement = SampleWaterCascadeVertexDisplacement(cascade, cascadeIndex).xyz * weight;
        float2 rotatedHorizontal = float2(
            dot(rawDisplacement.yz, cascade.swizzle.xy),
            dot(rawDisplacement.yz, cascade.swizzle.zw));
        horizontalDisplacement += rotatedHorizontal;
        SetWaterCascadeVerticalDisplacement(verticalDisplacements, cascadeIndex, rawDisplacement.x);
    }
}

void EvaluateWaterSimulationDisplacementNoCurrent(float3 positionOS, float3 simulationMask, float distanceToCamera,
    out float2 horizontalDisplacement, out float3 verticalDisplacements)
{
    WaterSimulationCoordinates coordinates;
    InitializeWaterSimulationCoordinates(positionOS.xz, coordinates);
    SampleWaterSimulationVertexDisplacement(coordinates, simulationMask, distanceToCamera,
        horizontalDisplacement, verticalDisplacements);
}

#if !defined(WATER_EVALUATION_NO_CURRENT)
void EvaluateWaterSimulationDisplacementWithCurrent(float3 positionOS, float3 simulationMask, float distanceToCamera,
    out float2 horizontalDisplacement, out float3 verticalDisplacements)
{
    WaterCurrentData currentData;
    EvaluateWaterCurrentData(positionOS.xz, currentData);
    WaterSimulationCoordinates primaryCoordinates;
    WaterSimulationCoordinates secondaryCoordinates;
    BuildWaterCurrentSimulationCoordinates(positionOS.xz, currentData, false, primaryCoordinates);
    BuildWaterCurrentSimulationCoordinates(positionOS.xz, currentData, true, secondaryCoordinates);
    float2 firstHorizontal;
    float2 secondHorizontal;
    float3 firstVertical;
    float3 secondVertical;
    SampleWaterSimulationVertexDisplacement(primaryCoordinates, simulationMask, distanceToCamera, firstHorizontal, firstVertical);
    SampleWaterSimulationVertexDisplacement(secondaryCoordinates, simulationMask, distanceToCamera, secondHorizontal, secondVertical);
    horizontalDisplacement = firstHorizontal + secondHorizontal;
    verticalDisplacements = firstVertical + secondVertical;
}
#endif

void EvaluateWaterSimulationDisplacement(float3 positionOS, out float2 horizontalDisplacement, out float3 verticalDisplacements)
{
    float3 positionWS = TransformWaterPositionToWorld(positionOS);
    float distanceToCamera = distance(positionWS, GetCameraPositionWS());
    float3 simulationMask = EvaluateWaterSimulationMask(positionWS);

#if defined(WATER_EVALUATION_NO_CURRENT)
    EvaluateWaterSimulationDisplacementNoCurrent(positionOS, simulationMask, distanceToCamera,
        horizontalDisplacement, verticalDisplacements);
#elif defined(WATER_RASTER_PATH)
    #if defined(WATER_LOCAL_CURRENT)
        EvaluateWaterSimulationDisplacementWithCurrent(positionOS, simulationMask, distanceToCamera,
            horizontalDisplacement, verticalDisplacements);
    #else
        EvaluateWaterSimulationDisplacementNoCurrent(positionOS, simulationMask, distanceToCamera,
            horizontalDisplacement, verticalDisplacements);
    #endif
#else
    if (!HasWaterLocalCurrent())
        EvaluateWaterSimulationDisplacementNoCurrent(positionOS, simulationMask, distanceToCamera,
            horizontalDisplacement, verticalDisplacements);
    else
        EvaluateWaterSimulationDisplacementWithCurrent(positionOS, simulationMask, distanceToCamera,
            horizontalDisplacement, verticalDisplacements);
#endif

}

struct WaterDisplacementData
{
    float3 displacement;
    float geometryHeight;
};

void EvaluateWaterDisplacement(float3 positionOS, out WaterDisplacementData displacementData)
{
    float2 horizontalDisplacement;
    float3 verticalDisplacements;
    EvaluateWaterSimulationDisplacement(positionOS, horizontalDisplacement, verticalDisplacements);
    float verticalDisplacement = verticalDisplacements.x + verticalDisplacements.y + verticalDisplacements.z;
    displacementData.displacement = float3(horizontalDisplacement.x, verticalDisplacement, horizontalDisplacement.y);
    displacementData.geometryHeight = verticalDisplacements.x + verticalDisplacements.y + verticalDisplacements.z;
}

#if !defined(WATER_EVALUATION_DISPLACEMENT_ONLY)
struct WaterAdditionalData
{
    float3 normalWS;
    float3 geometryNormalWS;
    float surfaceFoam;
    float deepFoam;
    float bubbleFoam;
};

#if !defined(WATER_SIMULATION)
struct WaterSpectralDifferentialData
{
    float height;
    float2 slope;
};

void SampleWaterCascadeDifferentials(WaterCascadeSamplingData cascade, int cascadeIndex, float3 positionRWS,
    float mask, float2 maskGradient, out WaterSpectralDifferentialData shading,
    out WaterSpectralDifferentialData geometry)
{
    ZERO_INITIALIZE(WaterSpectralDifferentialData, shading);
    ZERO_INITIALIZE(WaterSpectralDifferentialData, geometry);
    float fade;
    float2 fadeGradient;
    EvaluateWaterCascadeDistanceFadeWithGradient(positionRWS, cascadeIndex, fade, fadeGradient);
    float baseWeight = cascade.blend * fade * mask;
    float2 baseWeightGradient = cascade.blend * (mask * fadeGradient + fade * maskGradient)
        + fade * mask * cascade.blendGradient;
    float displacementScale = GetWaterCascadeDisplacementScale(cascadeIndex);
    float normalScale = GetWaterCascadeNormalScale(cascadeIndex);
    float2 uvDx = 2.0 * ddx(cascade.uv);
    float2 uvDy = 2.0 * ddy(cascade.uv);
    if (baseWeight == 0.0 && all(baseWeightGradient == 0.0))
        return;

    WaterWaveField field = SampleWaterWaveFieldGrad(cascade.uv, cascadeIndex, uvDx, uvDy);
    field = RotateWaterWaveField(field, cascade.swizzle);
    geometry.height = field.height * baseWeight * displacementScale;
    geometry.slope = field.geometrySlope * baseWeight
        + field.height * baseWeightGradient * displacementScale;
    shading.height = geometry.height;
    shading.slope = field.shadingSlope * baseWeight * normalScale
        + field.height * baseWeightGradient * displacementScale;
}

void AccumulateWaterDifferentials(WaterSimulationCoordinates coordinates, float3 positionRWS, float3 simulationMask,
    float3 maskGradientX, float3 maskGradientZ,
    out WaterSpectralDifferentialData full, out WaterSpectralDifferentialData geometryDifferential)
{
    ZERO_INITIALIZE(WaterSpectralDifferentialData, full);
    ZERO_INITIALIZE(WaterSpectralDifferentialData, geometryDifferential);
    if (_WaterActiveCascadeCount > 0u)
    {
        WaterSpectralDifferentialData shading;
        WaterSpectralDifferentialData geometry;
        SampleWaterCascadeDifferentials(
            coordinates.data[0], 0, positionRWS, simulationMask.x,
            float2(maskGradientX.x, maskGradientZ.x), shading, geometry);
        full.height += shading.height;
        full.slope += shading.slope;
        geometryDifferential.height += geometry.height;
        geometryDifferential.slope += geometry.slope;
    }
    if (_WaterActiveCascadeCount > 1u)
    {
        WaterSpectralDifferentialData shading;
        WaterSpectralDifferentialData geometry;
        SampleWaterCascadeDifferentials(
            coordinates.data[1], 1, positionRWS, simulationMask.y,
            float2(maskGradientX.y, maskGradientZ.y), shading, geometry);
        full.height += shading.height;
        full.slope += shading.slope;
        geometryDifferential.height += geometry.height;
        geometryDifferential.slope += geometry.slope;
    }
    if (_WaterActiveCascadeCount > 2u)
    {
        WaterSpectralDifferentialData shading;
        WaterSpectralDifferentialData geometry;
        SampleWaterCascadeDifferentials(
            coordinates.data[2], 2, positionRWS, simulationMask.z,
            float2(maskGradientX.z, maskGradientZ.z), shading, geometry);
        full.height += shading.height;
        full.slope += shading.slope;
        geometryDifferential.height += geometry.height;
        geometryDifferential.slope += geometry.slope;
    }
}

float3 ResolveWaterSpectralNormal(WaterSpectralDifferentialData data)
{
    return normalize(float3(-data.slope.x, 1.0, -data.slope.y));
}

float SampleWaterPersistentFoam(float2 positionOS, float distanceToCamera)
{
    if (_WaterFoamEnabled == 0u)
        return 0.0;

    uint primarySlice;
    float lodAlpha;
    float lodScale;
    WaterFoamSelectSurfaceLod(positionOS, primarySlice, lodAlpha, lodScale);

    uint lodCount = WaterFoamLodCount();
    float primaryWeight = 1.0 - lodAlpha;
    float3 foam = 0.0;
    if (primaryWeight > 0.0)
    {
        float3 primaryUv = WaterFoamCascadeWorldToUV(primarySlice, positionOS, false);
        foam += _WaterFoamField.SampleLevel(sampler_LinearClamp, primaryUv, 0.0).xyz * primaryWeight;
    }

    if (primarySlice + 1u < lodCount)
    {
        float secondaryWeight = lodAlpha;
        if (secondaryWeight > 0.0)
        {
            float3 secondaryUv = WaterFoamCascadeWorldToUV(primarySlice + 1u, positionOS, false);
            foam += _WaterFoamField.SampleLevel(sampler_LinearClamp, secondaryUv, 0.0).xyz * secondaryWeight;
        }
    }

    float3 distanceFade = float3(
        EvaluateWaterCascadeDistanceFade(distanceToCamera, 0),
        EvaluateWaterCascadeDistanceFade(distanceToCamera, 1),
        EvaluateWaterCascadeDistanceFade(distanceToCamera, 2));
    foam *= distanceFade * WaterFoamOuterCoverage(positionOS);
    return max(dot(foam, 1.0.xxx), 0.0);
}

float4 SampleWaterRasterSlopes(
    WaterCascadeSamplingData cascade, int cascadeIndex, float2 uvDx, float2 uvDy)
{
    float4 slopes = SampleWaterCascadeAdditionalDataGrad(cascade.uv, cascadeIndex, uvDx, uvDy);
    return float4(
        RotateWaterWaveVector(slopes.xy, cascade.swizzle),
        RotateWaterWaveVector(slopes.zw, cascade.swizzle));
}

void AccumulateWaterRasterAdditionalData(
    WaterSimulationCoordinates coordinates,
    float distanceToCamera,
    float3 simulationMask,
    out float2 fullSlope,
    out float2 geometrySlope)
{
    fullSlope = 0.0;
    geometrySlope = 0.0;

    if (_WaterActiveCascadeCount > 0u)
    {
        WaterCascadeSamplingData cascade = coordinates.data[0];
        float weight = cascade.blend
            * EvaluateWaterCascadeDistanceFade(distanceToCamera, 0)
            * simulationMask.x;
        float2 uvDx = 2.0 * ddx(cascade.uv);
        float2 uvDy = 2.0 * ddy(cascade.uv);
        if (weight != 0.0)
        {
            float4 slopes = SampleWaterRasterSlopes(cascade, 0, uvDx, uvDy);
            fullSlope += slopes.xy * (GetWaterCascadeNormalScale(0) * weight);
            geometrySlope += slopes.zw * weight;
        }
    }

    if (_WaterActiveCascadeCount > 1u)
    {
        WaterCascadeSamplingData cascade = coordinates.data[1];
        float weight = cascade.blend
            * EvaluateWaterCascadeDistanceFade(distanceToCamera, 1)
            * simulationMask.y;
        float2 uvDx = 2.0 * ddx(cascade.uv);
        float2 uvDy = 2.0 * ddy(cascade.uv);
        if (weight != 0.0)
        {
            float4 slopes = SampleWaterRasterSlopes(cascade, 1, uvDx, uvDy);
            fullSlope += slopes.xy * (GetWaterCascadeNormalScale(1) * weight);
            geometrySlope += slopes.zw * weight;
        }
    }

    if (_WaterActiveCascadeCount > 2u)
    {
        WaterCascadeSamplingData cascade = coordinates.data[2];
        float weight = cascade.blend
            * EvaluateWaterCascadeDistanceFade(distanceToCamera, 2)
            * simulationMask.z;
        float2 uvDx = 2.0 * ddx(cascade.uv);
        float2 uvDy = 2.0 * ddy(cascade.uv);
        if (weight != 0.0)
        {
            float4 slopes = SampleWaterRasterSlopes(cascade, 2, uvDx, uvDy);
            fullSlope += slopes.xy * (GetWaterCascadeNormalScale(2) * weight);
            geometrySlope += slopes.zw * weight;
        }
    }
}

float3 ResolveWaterRasterNormal(float2 slope)
{
    return normalize(float3(-slope.x, 1.0, -slope.y));
}

void EvaluateWaterRasterDataNoCurrent(float3 positionOS, float distanceToCamera, float3 simulationMask,
    out float2 fullSlope, out float2 geometrySlope)
{
    WaterSimulationCoordinates coordinates;
    InitializeWaterSimulationCoordinates(positionOS.xz, coordinates);
    AccumulateWaterRasterAdditionalData(coordinates, distanceToCamera, simulationMask,
        fullSlope, geometrySlope);
}

void EvaluateWaterRasterDataWithCurrent(float3 positionOS, float distanceToCamera, float3 simulationMask,
    out float2 fullSlope, out float2 geometrySlope)
{
    WaterCurrentData currentData;
    EvaluateWaterCurrentData(positionOS.xz, currentData);

    WaterSimulationCoordinates primaryCoordinates;
    WaterSimulationCoordinates secondaryCoordinates;
    BuildWaterCurrentSimulationCoordinates(positionOS.xz, currentData, false, primaryCoordinates);
    BuildWaterCurrentSimulationCoordinates(positionOS.xz, currentData, true, secondaryCoordinates);

    float2 primaryFullSlope, primaryGeometrySlope;
    float2 secondaryFullSlope, secondaryGeometrySlope;
    AccumulateWaterRasterAdditionalData(primaryCoordinates, distanceToCamera, simulationMask,
        primaryFullSlope, primaryGeometrySlope);
    AccumulateWaterRasterAdditionalData(secondaryCoordinates, distanceToCamera, simulationMask,
        secondaryFullSlope, secondaryGeometrySlope);

    fullSlope = primaryFullSlope + secondaryFullSlope;
    geometrySlope = primaryGeometrySlope + secondaryGeometrySlope;
}

void EvaluateWaterAdditionalDataLighting(float3 positionOS, float3 positionRWS,
    out WaterAdditionalData additionalData)
{
    ZERO_INITIALIZE(WaterAdditionalData, additionalData);
    float distanceToCamera = distance(positionRWS, GetCameraPositionWS());
    float3 simulationMask = EvaluateWaterSimulationMask(positionRWS);

    float2 fullSlope;
    float2 geometrySlope;

#if defined(WATER_RASTER_PATH)
    #if defined(WATER_LOCAL_CURRENT)
        EvaluateWaterRasterDataWithCurrent(positionOS, distanceToCamera, simulationMask,
            fullSlope, geometrySlope);
    #else
        EvaluateWaterRasterDataNoCurrent(positionOS, distanceToCamera, simulationMask,
            fullSlope, geometrySlope);
    #endif
#else
    if (!HasWaterLocalCurrent())
        EvaluateWaterRasterDataNoCurrent(positionOS, distanceToCamera, simulationMask,
            fullSlope, geometrySlope);
    else
        EvaluateWaterRasterDataWithCurrent(positionOS, distanceToCamera, simulationMask,
            fullSlope, geometrySlope);
#endif
    additionalData.normalWS = TransformWaterDirectionToWorld(ResolveWaterRasterNormal(fullSlope));
    additionalData.geometryNormalWS = TransformWaterDirectionToWorld(ResolveWaterRasterNormal(geometrySlope));
    if (_WaterFoamEnabled == 0u)
        return;

    float simulationMaskVisibility = saturate(max(simulationMask.x, max(simulationMask.y, simulationMask.z)));
    float foamState = SampleWaterPersistentFoam(positionOS.xz, distanceToCamera) * simulationMaskVisibility;
    additionalData.surfaceFoam = max(foamState, 0.0);
    additionalData.deepFoam = max(foamState, 0.0);
    additionalData.bubbleFoam = max(foamState, 0.0);
}

void EvaluateWaterDifferentialsWithCurrent(
    float3 positionOS,
    float3 positionRWS,
    float3 simulationMask,
    float3 maskGradientX,
    float3 maskGradientZ,
    out WaterSpectralDifferentialData full,
    out WaterSpectralDifferentialData geometryDifferential)
{
    WaterCurrentData currentData;
    EvaluateWaterCurrentDataWithGradient(positionOS.xz, currentData);
    WaterSimulationCoordinates primaryCoordinates;
    WaterSimulationCoordinates secondaryCoordinates;
    BuildWaterCurrentSimulationCoordinates(positionOS.xz, currentData, false, primaryCoordinates);
    BuildWaterCurrentSimulationCoordinates(positionOS.xz, currentData, true, secondaryCoordinates);

    WaterSpectralDifferentialData primaryFull, primaryGeometry, secondaryFull, secondaryGeometry;
    AccumulateWaterDifferentials(primaryCoordinates, positionRWS, simulationMask, maskGradientX, maskGradientZ, primaryFull, primaryGeometry);
    AccumulateWaterDifferentials(secondaryCoordinates, positionRWS, simulationMask, maskGradientX, maskGradientZ, secondaryFull, secondaryGeometry);

    full.height = primaryFull.height + secondaryFull.height;
    full.slope = primaryFull.slope + secondaryFull.slope;
    geometryDifferential.height = primaryGeometry.height + secondaryGeometry.height;
    geometryDifferential.slope = primaryGeometry.slope + secondaryGeometry.slope;
}

void EvaluateWaterAdditionalData(float3 positionOS, float3 positionRWS,
    out WaterAdditionalData additionalData)
{
    ZERO_INITIALIZE(WaterAdditionalData, additionalData);
    float3 simulationMask;
    float3 maskGradientX;
    float3 maskGradientZ;
    EvaluateWaterSimulationMaskWithGradient(positionRWS, simulationMask, maskGradientX, maskGradientZ);
    WaterSpectralDifferentialData full;
    WaterSpectralDifferentialData geometryDifferential;
#if defined(WATER_RASTER_PATH)
    #if defined(WATER_LOCAL_CURRENT)
        EvaluateWaterDifferentialsWithCurrent(
            positionOS, positionRWS, simulationMask, maskGradientX, maskGradientZ, full, geometryDifferential);
    #else
        WaterSimulationCoordinates coordinates;
        InitializeWaterSimulationCoordinates(positionOS.xz, coordinates);
        AccumulateWaterDifferentials(coordinates, positionRWS, simulationMask, maskGradientX, maskGradientZ, full, geometryDifferential);
    #endif
#else
    if (!HasWaterLocalCurrent())
    {
        WaterSimulationCoordinates coordinates;
        InitializeWaterSimulationCoordinates(positionOS.xz, coordinates);
        AccumulateWaterDifferentials(coordinates, positionRWS, simulationMask, maskGradientX, maskGradientZ, full, geometryDifferential);
    }
    else
    {
        EvaluateWaterDifferentialsWithCurrent(
            positionOS, positionRWS, simulationMask, maskGradientX, maskGradientZ, full, geometryDifferential);
    }
#endif

    additionalData.normalWS = TransformWaterDirectionToWorld(ResolveWaterSpectralNormal(full));
    additionalData.geometryNormalWS = TransformWaterDirectionToWorld(ResolveWaterSpectralNormal(geometryDifferential));

    if (_WaterFoamEnabled != 0u)
    {
        float distanceToCamera = distance(positionRWS, GetCameraPositionWS());
        float simulationMaskVisibility = saturate(max(simulationMask.x, max(simulationMask.y, simulationMask.z)));
        float foamState = SampleWaterPersistentFoam(positionOS.xz, distanceToCamera) * simulationMaskVisibility;
        additionalData.surfaceFoam = max(foamState, 0.0);
        additionalData.deepFoam = max(foamState, 0.0);
        additionalData.bubbleFoam = max(foamState, 0.0);
    }
}

#endif

#endif // WATER_EVALUATION_DISPLACEMENT_ONLY

#endif
