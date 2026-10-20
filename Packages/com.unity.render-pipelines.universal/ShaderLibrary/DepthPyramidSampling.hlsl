#ifndef URP_DEPTH_PYRAMID_SAMPLING_INCLUDED
#define URP_DEPTH_PYRAMID_SAMPLING_INCLUDED

TEXTURE2D_FLOAT(_DepthPyramid);
TEXTURE2D_FLOAT(_DepthPyramidFarthest);

StructuredBuffer<int2> _DepthPyramidMipLevelOffsets;
StructuredBuffer<int2> _DepthPyramidMipLevelOffsetsCheckerboard;

int _DepthPyramidMipCount;
int _DepthPyramidCheckerboardMipCount;

float4 _DepthPyramidMip0Size;
float4 _DepthPyramidAtlasSize;
float4 _DepthPyramidChannelMode;

int2 DepthPyramidGetMipSize(int mip)
{
    mip = clamp(mip, 0, max(0, _DepthPyramidMipCount - 1));

    uint shift = (uint)mip;
    uint2 mip0 = (uint2)_DepthPyramidMip0Size.xy;
    uint2 roundUp = (1u << shift) - 1u;

    uint2 mipSize = max((mip0 + roundUp) >> shift, 1u.xx);
    return (int2)mipSize;
}

int2 DepthPyramidGetMipOffset(int mip, bool checkerboard)
{
    int safeMip = clamp(mip, 0, max(0, _DepthPyramidMipCount - 1));

    // Initialize on the unconditional path. Some Vulkan compilers diagnose the
    // branch-return form as potentially uninitialized when checkerboard data is
    // inactive for the current kernel.
    int2 mipOffset = _DepthPyramidMipLevelOffsets[safeMip];
    if (checkerboard && safeMip < _DepthPyramidCheckerboardMipCount)
        mipOffset = _DepthPyramidMipLevelOffsetsCheckerboard[safeMip];

    return mipOffset;
}

int2 DepthPyramidUvToMipPixel(float2 uv, int mip)
{
    int2 mipSize = DepthPyramidGetMipSize(mip);
    int2 pixel = int2(uv * mipSize);
    return clamp(pixel, int2(0, 0), mipSize - 1);
}

float DepthPyramidLoadNearest(int2 mipPixel, int mip)
{
    int2 mipSize = DepthPyramidGetMipSize(mip);
    mipPixel = clamp(mipPixel, int2(0, 0), mipSize - 1);
    int2 atlasCoord = DepthPyramidGetMipOffset(mip, false) + mipPixel;
    return LOAD_TEXTURE2D(_DepthPyramid, atlasCoord).r;
}

float DepthPyramidSampleNearest(float2 uv, int mip)
{
    return DepthPyramidLoadNearest(DepthPyramidUvToMipPixel(uv, mip), mip);
}

float DepthPyramidLoadFarthest(int2 mipPixel, int mip);

float2 DepthPyramidLoadMinMax(int2 mipPixel, int mip)
{
    float nearDepth = DepthPyramidLoadNearest(mipPixel, mip);
    float farDepth = DepthPyramidLoadFarthest(mipPixel, mip);
    return float2(nearDepth, farDepth);
}

float2 DepthPyramidSampleMinMax(float2 uv, int mip)
{
    return DepthPyramidLoadMinMax(DepthPyramidUvToMipPixel(uv, mip), mip);
}

float DepthPyramidLoadFarthest(int2 mipPixel, int mip)
{
    int2 mipSize = DepthPyramidGetMipSize(mip);
    mipPixel = clamp(mipPixel, int2(0, 0), mipSize - 1);
    int2 atlasCoord = DepthPyramidGetMipOffset(mip, false) + mipPixel;
    return LOAD_TEXTURE2D(_DepthPyramidFarthest, atlasCoord).r;
}

float DepthPyramidSampleFarthest(float2 uv, int mip)
{
    return DepthPyramidLoadFarthest(DepthPyramidUvToMipPixel(uv, mip), mip);
}

float DepthPyramidLoadCheckerboard(int2 mipPixel, int mip)
{
    int2 mipSize = DepthPyramidGetMipSize(mip);
    mipPixel = clamp(mipPixel, int2(0, 0), mipSize - 1);
    int2 atlasCoord = DepthPyramidGetMipOffset(mip, true) + mipPixel;
    return LOAD_TEXTURE2D(_DepthPyramid, atlasCoord).r;
}

float DepthPyramidSampleCheckerboard(float2 uv, int mip)
{
    return DepthPyramidLoadCheckerboard(DepthPyramidUvToMipPixel(uv, mip), mip);
}

#endif
