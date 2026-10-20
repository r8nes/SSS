#ifndef WATER_CURRENT_UTILITIES_INCLUDED
#define WATER_CURRENT_UTILITIES_INCLUDED

#include "../../Core/WaterShaderVariables.cs.hlsl"

StructuredBuffer<float4> _WaterCurrentSectorSwizzles;
TEXTURE2D(_WaterCurrentMap);
SAMPLER(sampler_WaterCurrentMap);

bool HasWaterLocalCurrent()
{
    return _WaterCurrentMapInfluence > 1e-4;
}

#define WATER_CURRENT_SECTOR_COUNT 8u
#define WATER_CURRENT_SECTOR_ANGLE ((2.0 * PI) / WATER_CURRENT_SECTOR_COUNT)
#define WATER_CURRENT_SECTOR_DIRECTION_OFFSET 0

#ifdef SHADER_STAGE_FRAGMENT
#define SAMPLE_WATER_CURRENT_TEXTURE(textureName, samplerName, coord2) SAMPLE_TEXTURE2D(textureName, samplerName, coord2)
#else
#define SAMPLE_WATER_CURRENT_TEXTURE(textureName, samplerName, coord2) SAMPLE_TEXTURE2D_LOD(textureName, samplerName, coord2, 0)
#endif

float WrapWaterCurrentAngle(float angle)
{
    angle = angle % (2.0 * PI);
    return angle > PI ? angle - 2.0 * PI : (angle < -PI ? angle + 2.0 * PI : angle);
}

float DecodeWaterCurrentAngle(float3 encodedDirection, float orientation, float influence, float2 flipDirection)
{
    float3 direction = float3((encodedDirection.xy * 2.0 - 1.0) * flipDirection, encodedDirection.z);
    float angle = WrapWaterCurrentAngle(atan2(direction.y, direction.x) - orientation);
    if (direction.x == 0.0 && direction.y == 0.0)
        angle = 0.0;
    return angle * influence * direction.z;
}

struct WaterCurrentData
{
    uint sectorIndex;
    float sectorBlend;
    float angle;
    float2 sectorBlendGradient;
};

void DecodeWaterCurrentData(float3 encodedDirection, float orientation, float influence, float2 flipDirection, out WaterCurrentData currentData)
{
    float angle = DecodeWaterCurrentAngle(encodedDirection, orientation, influence, flipDirection);
    angle = angle < 0.0 ? angle + 2.0 * PI : angle;
    float sector = angle / WATER_CURRENT_SECTOR_ANGLE;
    float relativeAngle = frac(sector);
    uint sectorIndex = (uint)sector;
    currentData.angle = angle;
    currentData.sectorIndex = sectorIndex >= WATER_CURRENT_SECTOR_COUNT ? sectorIndex - WATER_CURRENT_SECTOR_COUNT : sectorIndex;
    currentData.sectorBlend = PositivePow((currentData.sectorIndex % 2u == 0u) ? relativeAngle : 1.0 - relativeAngle, 0.75);
    currentData.sectorBlendGradient = 0.0;
}

float2 TransformWaterCurrentUV(float2 currentUV)
{
    return float2(currentUV.x - _WaterCurrentRegionScaleOffset.z, currentUV.y + _WaterCurrentRegionScaleOffset.w)
        * _WaterCurrentRegionScaleOffset.xy + 0.5;
}

void EvaluateWaterCurrentData(float2 currentUV, out WaterCurrentData currentData)
{
    float2 uv = TransformWaterCurrentUV(currentUV);
    float2 flipDirection = sign(_WaterCurrentRegionScaleOffset.xy);
    float3 encodedDirection = SAMPLE_WATER_CURRENT_TEXTURE(_WaterCurrentMap, sampler_WaterCurrentMap, uv).xyz;
    DecodeWaterCurrentData(encodedDirection, _WaterReferenceOrientation.x, _WaterCurrentMapInfluence, flipDirection, currentData);
}

float SampleWaterCurrentAngle(float2 uv, float2 flipDirection)
{
    float3 encodedDirection = SAMPLE_WATER_CURRENT_TEXTURE(_WaterCurrentMap, sampler_WaterCurrentMap, uv).xyz;
    float angle = DecodeWaterCurrentAngle(encodedDirection, _WaterReferenceOrientation.x, _WaterCurrentMapInfluence, flipDirection);
    return angle < 0.0 ? angle + 2.0 * PI : angle;
}

void EvaluateWaterCurrentDataWithGradient(float2 currentUV, out WaterCurrentData currentData)
{
    EvaluateWaterCurrentData(currentUV, currentData);

    float2 uv = TransformWaterCurrentUV(currentUV);
    float2 flipDirection = sign(_WaterCurrentRegionScaleOffset.xy);
    uint width;
    uint height;
    _WaterCurrentMap.GetDimensions(width, height);
    float2 texel = rcp(max(float2(width, height), 1.0));
    float angleXP = SampleWaterCurrentAngle(uv + float2(texel.x, 0.0), flipDirection);
    float angleXM = SampleWaterCurrentAngle(uv - float2(texel.x, 0.0), flipDirection);
    float angleZP = SampleWaterCurrentAngle(uv + float2(0.0, texel.y), flipDirection);
    float angleZM = SampleWaterCurrentAngle(uv - float2(0.0, texel.y), flipDirection);
    float2 angleGradientUV = float2(
        WrapWaterCurrentAngle(angleXP - angleXM) / max(2.0 * texel.x, 1e-6),
        WrapWaterCurrentAngle(angleZP - angleZM) / max(2.0 * texel.y, 1e-6));
    float2 angleGradient = angleGradientUV * _WaterCurrentRegionScaleOffset.xy;

    float sector = currentData.angle / WATER_CURRENT_SECTOR_ANGLE;
    float relativeAngle = frac(sector);
    bool evenSector = (currentData.sectorIndex % 2u) == 0u;
    float baseWeight = evenSector ? relativeAngle : 1.0 - relativeAngle;
    float derivative = (evenSector ? 1.0 : -1.0) * 0.75
        * pow(max(baseWeight, 1e-4), -0.25) / WATER_CURRENT_SECTOR_ANGLE;
    currentData.sectorBlendGradient = derivative * angleGradient;
}

float2 SampleWaterCurrentDirection(float2 currentUV)
{
    WaterCurrentData data;
    EvaluateWaterCurrentData(currentUV, data);
    return float2(cos(data.angle), sin(data.angle));
}

#define WATER_CURRENT_ARROW_TILE_SIZE 4.0

float EvaluateWaterCurrentDebugArrow(float2 positionAWS, float2 direction, float2 tileSize)
{
    float2 tileCoordinate = frac(positionAWS / max(tileSize, 1e-4)) * 2.0 - 1.0;
    float x = dot(direction, tileCoordinate);
    float y = dot(float2(direction.y, -direction.x), tileCoordinate);
    float mask = 0.0;
    if (y > -0.1 && y < 0.1 && x > -0.9 && x < 0.5)
        mask = 1.0;
    else if (y > -0.4 && y < 0.4 && x > 0.5 && x < 0.9 && (0.4 - (x - 0.5)) / max(abs(y), 1e-4) > 1.0)
        mask = 1.0;
    return mask;
}

void BuildWaterCurrentSamplingCoordinates(float2 coordinate, uint sectorIndex, out float4 projectedCoordinates)
{
    float4 direction = _WaterCurrentSectorSwizzles[sectorIndex + WATER_CURRENT_SECTOR_DIRECTION_OFFSET];
    projectedCoordinates.xy = float2(dot(coordinate, direction.xy), dot(coordinate, float2(-direction.y, direction.x)));
    projectedCoordinates.zw = float2(dot(coordinate, direction.zw), dot(coordinate, float2(-direction.w, direction.z)));
}

#endif
