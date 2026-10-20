#ifndef URP_VOLUMETRIC_CLOUDS_BLUE_NOISE_HLSL
#define URP_VOLUMETRIC_CLOUDS_BLUE_NOISE_HLSL

#define CLOUD_INTEGRATION_NOISE_TILE_SIZE 128u
#define CLOUD_INTEGRATION_NOISE_FRAME_COUNT 16u

Texture2D<float> _CloudIntegrationNoise;

float EvaluateCloudIntegrationNoise(uint2 pixelCoord, uint frameIndex)
{
    uint2 tileCoord = pixelCoord & (CLOUD_INTEGRATION_NOISE_TILE_SIZE - 1u);
    frameIndex &= CLOUD_INTEGRATION_NOISE_FRAME_COUNT - 1u;
    uint2 atlasCoord = uint2(
        tileCoord.x + frameIndex * CLOUD_INTEGRATION_NOISE_TILE_SIZE,
        tileCoord.y);
    return _CloudIntegrationNoise.Load(int3(atlasCoord, 0));
}

uint2 ComputeCheckerBoardOffset(uint2 traceCoord, uint subPixelIndex)
{
    uint checker = (traceCoord.x & 1u) ^ (traceCoord.y & 1u);
    subPixelIndex = (subPixelIndex + checker) & 0x3u;
    return uint2(((subPixelIndex >> 1u) ^ subPixelIndex) & 1u, subPixelIndex >> 1u);
}

#endif