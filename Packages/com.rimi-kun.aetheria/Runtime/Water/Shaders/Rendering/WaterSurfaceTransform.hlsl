#ifndef WATER_SURFACE_TRANSFORM_INCLUDED
#define WATER_SURFACE_TRANSFORM_INCLUDED

float3 TransformWaterPositionToWorld(float3 positionOS)
{
#if defined(WATER_BUOYANCY_QUERY)
    return mul(_WaterBuoyancyWaterToWorld, float4(positionOS, 1.0)).xyz;
#else
    return mul(_WaterSurfaceTransform, float4(positionOS, 1.0)).xyz;
#endif
}

float3 TransformWaterDirectionToWorld(float3 directionOS)
{
#if defined(WATER_BUOYANCY_QUERY)
    return mul((float3x3)_WaterBuoyancyWaterToWorld, directionOS);
#else
    return mul((float3x3)_WaterSurfaceTransform, directionOS);
#endif
}

float3 GetWaterSurfaceUp()
{
    float3 upWS = TransformWaterDirectionToWorld(float3(0.0, 1.0, 0.0));
    return dot(upWS, upWS) > 1e-8 ? SafeNormalize(upWS) : float3(0.0, 1.0, 0.0);
}

float3 TransformWaterDisplacedPositionToWorld(float3 basePositionOS, float3 displacementOS)
{
    return TransformWaterPositionToWorld(basePositionOS + displacementOS);
}

float2 TransformToWaterAxes(float2 coordinates)
{
#if defined(WATER_BUOYANCY_QUERY)
    float2 axisX = float2(_WaterBuoyancyWaterToWorld[0].x, _WaterBuoyancyWaterToWorld[2].x);
#else
    float2 axisX = float2(_WaterSurfaceTransform[0].x, _WaterSurfaceTransform[2].x);
#endif
    float2 axisZ = float2(-axisX.y, axisX.x);
    return float2(dot(coordinates, axisX), dot(coordinates, axisZ));
}

#endif
