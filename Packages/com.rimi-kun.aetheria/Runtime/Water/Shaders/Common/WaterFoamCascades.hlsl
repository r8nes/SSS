#ifndef WATER_FOAM_CASCADES_INCLUDED
#define WATER_FOAM_CASCADES_INCLUDED

#include "../../Core/WaterDefinitions.cs.hlsl"

#define WATER_FOAM_MAX_LOD_COUNT WATERSHADERLIMITS_MAX_LOD_COUNT
#define WATER_FOAM_SOURCE_CASCADE_OFFSET WATER_FOAM_MAX_LOD_COUNT

StructuredBuffer<float4> _WaterFoamCascades;

uint WaterFoamLodCount()
{
    return (uint)clamp(round(_WaterFoamLodParameters.x), 1.0, (float)WATER_FOAM_MAX_LOD_COUNT);
}

float WaterFoamResolution()
{
    return max(_WaterFoamLodParameters.y, 1.0);
}

float4 WaterFoamCascadeParameters(uint slice, bool source)
{
    uint index = min(slice, WATER_FOAM_MAX_LOD_COUNT - 1u);
    return _WaterFoamCascades[index + (source ? WATER_FOAM_SOURCE_CASCADE_OFFSET : 0u)];
}

float WaterFoamCascadeTexel(uint slice, bool source)
{
    return max(WaterFoamCascadeParameters(slice, source).z, 1e-6);
}

float3 WaterFoamCascadeIDToUV(uint slice, uint2 id)
{
    return float3((float2(id) + 0.5) / WaterFoamResolution(), slice);
}

float2 WaterFoamCascadeUVToWorld(uint slice, float3 uv, bool source)
{
    float4 cascade = WaterFoamCascadeParameters(slice, source);
    return cascade.z * WaterFoamResolution() * (uv.xy - 0.5) + cascade.xy;
}

float2 WaterFoamCascadeIDToWorld(uint slice, uint2 id, bool source)
{
    return WaterFoamCascadeUVToWorld(slice, WaterFoamCascadeIDToUV(slice, id), source);
}

float3 WaterFoamCascadeWorldToUV(uint slice, float2 position, bool source)
{
    float4 cascade = WaterFoamCascadeParameters(slice, source);
    return float3(
        (position - cascade.xy) / (max(cascade.z, 1e-6) * WaterFoamResolution()) + 0.5,
        slice);
}

bool WaterFoamCascadeContains(float2 uv, float border)
{
    float radius = length(uv - 0.5);
    float radiusMax = 0.5 - _WaterFoamLodParameters.z * border;
    return radius <= radiusMax;
}

float WaterFoamLodScale(uint slice)
{
    return max(_WaterFoamLodParameters.w, 1e-6) * exp2((float)slice);
}

void WaterFoamSelectSurfaceLod(float2 position, out uint slice, out float lodAlpha, out float lodScale)
{
    uint count = WaterFoamLodCount();
    float radius = length(position - _WaterFoamLodCenter.xy);

    slice = 0u;
    lodScale = WaterFoamLodScale(0u);
    [loop]
    while (slice + 1u < count && radius > 2.0 * lodScale)
    {
        ++slice;
        lodScale *= 2.0;
    }

    lodAlpha = radius / max(lodScale, 1e-6) - 1.0;
    lodAlpha = max((lodAlpha - _WaterFoamLodCenter.z) / max(_WaterFoamLodCenter.w, 1e-6), 0.0);
    if (slice == 0u && _WaterFoamLodScale.y > 0.5)
        lodAlpha = min(lodAlpha + _WaterFoamLodScale.x, 1.0);
    lodAlpha = saturate(lodAlpha);
}

float WaterFoamOuterCoverage(float2 position)
{
    uint count = WaterFoamLodCount();
    float outerScale = WaterFoamLodScale(count - 1u);
    float radius = length(position - _WaterFoamLodCenter.xy);
    return 1.0 - smoothstep(1.72 * outerScale, 1.96 * outerScale, radius);
}

#endif
