#ifndef WATER_SURFACE_GEOMETRY_INCLUDED
#define WATER_SURFACE_GEOMETRY_INCLUDED

#include "WaterSurfaceTransform.hlsl"

#include "../Common/WaterSurfaceSampling.hlsl"

#include "../Common/WaterDepthSampling.hlsl"

#ifdef UNITY_PROCEDURAL_INSTANCING_ENABLED
float ComputeWaterLodAlpha(float3 positionOS, float meshScaleAlpha, float lodScale, WaterCameraShaderVariables cameraData)
{
    float2 offsetFromCenter = abs(positionOS.xz - cameraData._LodCenter);
    float taxicabNorm = max(offsetFromCenter.x, offsetFromCenter.y);
    float lodAlpha = taxicabNorm / lodScale - 1.0;
    lodAlpha = max((lodAlpha - cameraData._LodAlphaBlackPointFade) / cameraData._LodAlphaBlackPointWhitePointFade, 0.0);
    lodAlpha = min(lodAlpha + meshScaleAlpha, 1.0);
    return lodAlpha;
}

float ApplyWaterPatchEdge(float coordinate, float mode, float normalizedGridWidth, float extentsMultiplier)
{
    float outerThreshold = 0.5 + 0.5 * normalizedGridWidth;
    float regularThreshold = 0.5 - 0.5 * normalizedGridWidth;

    if (mode < -0.5)
    {
        if (coordinate > regularThreshold)
            return 0.5 - normalizedGridWidth;
    }
    else if (mode < 0.5)
    {
        if (coordinate > outerThreshold)
            return 0.5;
    }
    else if (mode > 1.5 && coordinate > outerThreshold)
    {
        return coordinate * extentsMultiplier;
    }

    return coordinate;
}

float3 ComputeWaterSimulationPosition(float3 objectPosition, WaterCameraShaderVariables cameraData, uint instanceID, out float lodAlpha)
{
    WaterLodPatchData patch = _WaterLodPatches[instanceID];
    float2 axisX = patch.positionAxisX.zw;
    float2 axisZ = patch.axisScaleGrid.xy;
    float lodScale = patch.axisScaleGrid.z;
    float geometryGridWidth = patch.axisScaleGrid.w;
    float meshScaleAlpha = patch.morphNormalLod.x;
    float normalizedGridWidth = geometryGridWidth / max(lodScale, 1e-6);

    objectPosition.x = ApplyWaterPatchEdge(objectPosition.x, patch.edgeTopology.x, normalizedGridWidth, patch.edgeTopology.z);
    objectPosition.z = ApplyWaterPatchEdge(objectPosition.z, patch.edgeTopology.y, normalizedGridWidth, patch.edgeTopology.z);

    float2 positionXZ = patch.positionAxisX.xy
        + axisX * (objectPosition.x * lodScale)
        + axisZ * (objectPosition.z * lodScale);

    float gridSize2 = 2.0 * geometryGridWidth;
    float gridSize4 = 4.0 * geometryGridWidth;
    positionXZ -= frac(patch.positionAxisX.xy / gridSize2) * gridSize2;

    float3 simulationPos = float3(positionXZ.x, objectPosition.y, positionXZ.y);
    lodAlpha = ComputeWaterLodAlpha(simulationPos, meshScaleAlpha, lodScale, cameraData);

    float2 m = frac(simulationPos.xz / gridSize4);
    float2 offset = m - 0.5;
    const float minRadius = 0.375;
    if (abs(offset.x) < minRadius)
        simulationPos.x += offset.x * lodAlpha * gridSize4;
    if (abs(offset.y) < minRadius)
        simulationPos.z += offset.y * lodAlpha * gridSize4;

    float epsilonScale = lerp(
        1.0,
        1.01,
        max(abs(cameraData._LodCameraPosition.x), abs(cameraData._LodCameraPosition.y)) * 0.00001);
    simulationPos.xz = lerp(patch.positionAxisX.xy, simulationPos.xz, epsilonScale);
    return simulationPos;
}
#else
float3 ComputeWaterSimulationPosition(float3 objectPosition, WaterCameraShaderVariables cameraData)
{
    float3 simulationPos = objectPosition;
    simulationPos.xz = simulationPos.xz * cameraData._GridSize + cameraData._PatchOffset;
    return simulationPos;
}
#endif

WaterSurfaceVaryings BuildWaterVaryings(WaterSurfaceAttributes input, uint instanceID)
{
    WaterSurfaceVaryings output;
    UNITY_SETUP_INSTANCE_ID(input);

    WaterCameraShaderVariables cameraData = _WaterCameraVariables[_WaterSurfaceIndex];

    #ifdef UNITY_PROCEDURAL_INSTANCING_ENABLED
        float lodAlpha;
        float3 positionOS = ComputeWaterSimulationPosition(input.positionOS, cameraData, instanceID, lodAlpha);
        float3 normalOS = float3(0.0, 1.0, 0.0);
        WaterLodPatchData patch = _WaterLodPatches[instanceID];
        float4 normalLod = float4(patch.axisScaleGrid.z, lodAlpha * patch.morphNormalLod.w, patch.morphNormalLod.y, patch.morphNormalLod.z);
    #else
        float3 positionOS = ComputeWaterSimulationPosition(input.positionOS, cameraData);
        float3 normalOS = input.normalOS;
        float4 normalLod = float4(10.0, 0.0, 1.0, 1.0);
        positionOS = mul(GetObjectToWorldMatrix(), float4(positionOS, 1.0)).xyz;
        normalOS = SafeNormalize(mul(normalOS, (float3x3)GetWorldToObjectMatrix()));
    #endif

    float3 preDisplacementOS = positionOS;
    WaterDisplacementData displacementData;
    EvaluateWaterDisplacement(preDisplacementOS, displacementData);

    float3 positionWS = TransformWaterDisplacedPositionToWorld(
        preDisplacementOS, displacementData.displacement);
    float3 normalWS = SafeNormalize(TransformWaterDirectionToWorld(normalOS));

    output.positionWS = positionWS;
    output.normalWS = normalWS;
    output.positionCS = WaterTransformWorldToHClip(positionWS);
    output.waterUV0 = float3(preDisplacementOS.x, preDisplacementOS.z, displacementData.displacement.y);
    output.displacement = float4(displacementData.displacement, displacementData.geometryHeight);
    output.normalLod = normalLod;

    if (any(abs(preDisplacementOS.xz) > cameraData._RegionExtent + 0.1))
        output.positionCS.w = asfloat(0x7fc00000);

    return output;
}

WaterSurfaceVaryings VertWater(WaterSurfaceAttributes input, uint instanceID : SV_InstanceID)
{
    return BuildWaterVaryings(input, instanceID);
}

#endif
