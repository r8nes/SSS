#ifndef WATER_UNDERWATER_MASK_INCLUDED
#define WATER_UNDERWATER_MASK_INCLUDED

StructuredBuffer<uint> _WaterUnderwaterLine;
float4 _WaterUnderwaterBoundsSS;
float4 _WaterUnderwaterUpDirection;
uint _WaterUnderwaterBufferStride;

struct WaterUnderwaterLineData
{
    float signedDistancePixels;
    float2 linePositionSS;
    float2 normalSS;
};

float WaterUnderwaterReadLineHeight(float rightCoordinate)
{
    uint2 boundsX = uint2(
        0xFFFFFFFFu - _WaterUnderwaterLine[0],
        _WaterUnderwaterLine[1]);
    float lowerBound = (float)min(boundsX.x, boundsX.y);
    float upperBound = (float)max(boundsX.x, boundsX.y);
    float coordinate = clamp(rightCoordinate, lowerBound, upperBound);
    uint index0 = min((uint)floor(coordinate) + 2u, _WaterUnderwaterBufferStride - 1u);
    uint index1 = min(index0 + 1u, _WaterUnderwaterBufferStride - 1u);
    float height0 = (float)(_WaterUnderwaterLine[index0] & 0xFFFFu) - 1.0;
    float height1 = (float)(_WaterUnderwaterLine[index1] & 0xFFFFu) - 1.0;
    return lerp(height0, height1, frac(coordinate));
}

WaterUnderwaterLineData WaterUnderwaterEvaluateLine(float2 coord)
{
    WaterUnderwaterLineData data;
    float2 upVector = _WaterUnderwaterUpDirection.xy;
    float2 rightVector = float2(upVector.y, -upVector.x);
    float rightCoordinate = dot(coord, rightVector) - _WaterUnderwaterBoundsSS.x;
    float lineHeight = WaterUnderwaterReadLineHeight(rightCoordinate);
    float previousHeight = WaterUnderwaterReadLineHeight(rightCoordinate - 1.0);
    float nextHeight = WaterUnderwaterReadLineHeight(rightCoordinate + 1.0);
    float slope = 0.5 * (nextHeight - previousHeight);
    data.normalSS = normalize(upVector - slope * rightVector);
    data.linePositionSS = rightVector * (_WaterUnderwaterBoundsSS.x + rightCoordinate)
        + upVector * (_WaterUnderwaterBoundsSS.z + lineHeight);
    data.signedDistancePixels = dot(coord - data.linePositionSS, data.normalSS);
    return data;
}

float WaterUnderwaterGetDistance(uint2 coord)
{
    WaterUnderwaterLineData data = WaterUnderwaterEvaluateLine((float2)coord);
    float maxHeight = max(_WaterUnderwaterBoundsSS.w - _WaterUnderwaterBoundsSS.z, 1.0);
    return data.signedDistancePixels / maxHeight;
}

bool WaterUnderwaterIsUnderWater(uint2 coord)
{
    return WaterUnderwaterEvaluateLine((float2)coord).signedDistancePixels < 0.0;
}

#endif
