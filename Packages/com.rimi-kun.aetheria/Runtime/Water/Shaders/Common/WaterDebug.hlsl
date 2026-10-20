#ifndef WATER_DEBUG_INCLUDED
#define WATER_DEBUG_INCLUDED

#include "../../Core/WaterDefinitions.cs.hlsl"

float3 EvaluateWaterDebugSimulationMask(float3 positionAWS)
{
    return EvaluateWaterSimulationMask(positionAWS);
}

float SelectWaterDebugSimulationMaskChannel(float3 mask)
{
    if (_WaterSimulationMaskDebugMode == WATERSIMULATIONMASKDEBUGMODE_GREEN_CHANNEL)
        return mask.y;
    if (_WaterSimulationMaskDebugMode == WATERSIMULATIONMASKDEBUGMODE_BLUE_CHANNEL)
        return mask.z;
    return mask.x;
}

float3 WaterDebugSignedScalar(float value)
{
    float magnitude = abs(value) / (1.0 + abs(value));
    return value < 0.0 ? float3(magnitude, 0.0, 0.0) : float3(0.0, magnitude, 0.0);
}

float3 WaterDebugNormal(float3 normalWS)
{
    return SafeNormalize(normalWS) * 0.5 + 0.5;
}

float3 WaterDebugCascadeContribution(float3 positionOS)
{
    float2 horizontalDisplacement;
    float3 verticalDisplacements;
    EvaluateWaterSimulationDisplacement(positionOS, horizontalDisplacement, verticalDisplacements);
    float3 magnitude = abs(verticalDisplacements);
    float normalization = max(max(magnitude.x, magnitude.y), max(magnitude.z, 1e-5));
    return saturate(magnitude / normalization);
}

float WaterDebugBreakingSample(WaterCascadeSamplingData cascade, int cascadeIndex)
{
    return SampleWaterWaveFieldLevel(
        cascade.uv,
        cascadeIndex,
        WATER_WAVE_DISPLACEMENT_SLICE,
        0.0).w * cascade.blend;
}

float3 WaterDebugBreaking(float2 positionOS)
{
    float3 breaking = 0.0;
#if defined(WATER_LOCAL_CURRENT)
    WaterCurrentData currentData;
    EvaluateWaterCurrentData(positionOS, currentData);
    WaterSimulationCoordinates primaryCoordinates;
    WaterSimulationCoordinates secondaryCoordinates;
    BuildWaterCurrentSimulationCoordinates(positionOS, currentData, false, primaryCoordinates);
    BuildWaterCurrentSimulationCoordinates(positionOS, currentData, true, secondaryCoordinates);
#else
    WaterSimulationCoordinates coordinates;
    InitializeWaterSimulationCoordinates(positionOS, coordinates);
#endif

    if (_WaterActiveCascadeCount > 0u)
    {
#if defined(WATER_LOCAL_CURRENT)
        breaking.x = WaterDebugBreakingSample(primaryCoordinates.data[0], 0)
            + WaterDebugBreakingSample(secondaryCoordinates.data[0], 0);
#else
        breaking.x = WaterDebugBreakingSample(coordinates.data[0], 0);
#endif
    }
    if (_WaterActiveCascadeCount > 1u)
    {
#if defined(WATER_LOCAL_CURRENT)
        breaking.y = WaterDebugBreakingSample(primaryCoordinates.data[1], 1)
            + WaterDebugBreakingSample(secondaryCoordinates.data[1], 1);
#else
        breaking.y = WaterDebugBreakingSample(coordinates.data[1], 1);
#endif
    }
    if (_WaterActiveCascadeCount > 2u)
    {
#if defined(WATER_LOCAL_CURRENT)
        breaking.z = WaterDebugBreakingSample(primaryCoordinates.data[2], 2)
            + WaterDebugBreakingSample(secondaryCoordinates.data[2], 2);
#else
        breaking.z = WaterDebugBreakingSample(coordinates.data[2], 2);
#endif
    }
    return saturate(breaking);
}

bool EvaluateWaterDebugColor(WaterSurfaceVaryings input, out half4 debugColor)
{
    debugColor = half4(0.0h, 0.0h, 0.0h, 1.0h);
    if (_WaterDebugMode == WATERDEBUGMODE_NONE)
        return false;

    float2 positionOS = input.waterUV0.xy;
    float3 positionOS3 = float3(positionOS.x, input.waterUV0.z, positionOS.y);

    if (_WaterDebugMode == WATERDEBUGMODE_SIMULATION_MASK)
    {
        float simulationMask = SelectWaterDebugSimulationMaskChannel(EvaluateWaterDebugSimulationMask(input.positionWS));
        debugColor = half4(simulationMask.xxx, 1.0);
        return true;
    }

    if (_WaterDebugMode == WATERDEBUGMODE_CURRENT)
    {
        float sineOrientation;
        float cosineOrientation;
        sincos(_WaterReferenceOrientation.x, sineOrientation, cosineOrientation);
        float2 direction = float2(cosineOrientation, sineOrientation);
#if defined(WATER_LOCAL_CURRENT)
        float2 localDirection = SampleWaterCurrentDirection(positionOS);
        direction = float2(
            cosineOrientation * localDirection.x - sineOrientation * localDirection.y,
            sineOrientation * localDirection.x + cosineOrientation * localDirection.y);
#endif
        float arrowTileSize = WATER_CURRENT_ARROW_TILE_SIZE / max(_WaterCurrentDebugArrowDensity, 0.1);
        float2 tileSize = float2(arrowTileSize, arrowTileSize);
        float arrow = EvaluateWaterCurrentDebugArrow(positionOS, direction, tileSize);
        debugColor = half4((direction * 0.5 + 0.5) * (1.0 - arrow), 0.0, 1.0);
        return true;
    }

    if (_WaterDebugMode == WATERDEBUGMODE_DISPLACEMENT)
    {
        debugColor = half4(WaterDebugSignedScalar(input.displacement.y), 1.0);
        return true;
    }

    if (_WaterDebugMode == WATERDEBUGMODE_CASCADE_CONTRIBUTION)
    {
        debugColor = half4(WaterDebugCascadeContribution(positionOS3), 1.0);
        return true;
    }

    if (_WaterDebugMode == WATERDEBUGMODE_BREAKING)
    {
        debugColor = half4(WaterDebugBreaking(positionOS), 1.0);
        return true;
    }

    WaterAdditionalData data;
    ZERO_INITIALIZE(WaterAdditionalData, data);
    EvaluateWaterAdditionalData(positionOS3, input.positionWS, data);

    if (_WaterDebugMode == WATERDEBUGMODE_SHADING_NORMAL)
    {
        debugColor = half4(WaterDebugNormal(data.normalWS), 1.0);
        return true;
    }

    if (_WaterDebugMode == WATERDEBUGMODE_GEOMETRY_NORMAL)
    {
        debugColor = half4(WaterDebugNormal(data.geometryNormalWS), 1.0);
        return true;
    }

    if (_WaterDebugMode == WATERDEBUGMODE_FOAM)
    {
        WaterFoamVisuals foam = EvaluateWaterFoamVisuals(
            positionOS,
            input.positionWS,
            data.normalWS,
            data.surfaceFoam);
        debugColor = half4(saturate(float3(foam.surface, foam.deep, foam.bubbles)), 1.0);
        return true;
    }

    debugColor = half4(1.0h, 0.0h, 1.0h, 1.0h);
    return true;
}

#endif
