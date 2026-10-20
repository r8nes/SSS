#ifndef UNIVERSAL_COLOR_PYRAMID_SAMPLING_INCLUDED
#define UNIVERSAL_COLOR_PYRAMID_SAMPLING_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

// Renderer-wide snapshot generated immediately before regular transparents.
// Mip 0 is the scene snapshot; higher mips are Gaussian filtered.
TEXTURE2D(_CameraColorPyramidTexture);

float4 _CameraColorPyramidMip0Size;
float4 _CameraColorPyramidUvScaleAndLimit; // xy = viewport scale, zw = last valid texel UV
int _CameraColorPyramidMipCount;
int _CameraColorPyramidMaxMip;
float _SSRefractionInvScreenWeightDistance;
float _EnableSSRefraction;

float2 ColorPyramidSamplingUV(float2 uv, float mipLevel, float4 scaleAndLimit)
{
    float2 scale = scaleAndLimit.xy;
    float2 samplingUV = uv * scale;
    float2 diffLimit = scale - scaleAndLimit.zw;
    float2 diffLimitMipAdjusted = diffLimit * exp2(2.0 + ceil(abs(mipLevel)));
    float2 limit = max(0.0, scale - diffLimitMipAdjusted);
    return clamp(samplingUV, 0.0, limit);
}

float2 CameraColorPyramidSamplingUV(float2 positionNDC, float mipLevel)
{
    return ColorPyramidSamplingUV(
        positionNDC,
        mipLevel,
        _CameraColorPyramidUvScaleAndLimit);
}

half4 SampleCameraColorPyramid(float2 positionNDC, float mipLevel)
{
    mipLevel = clamp(mipLevel, 0.0, max(0.0, (float)_CameraColorPyramidMaxMip));
    float2 uv = CameraColorPyramidSamplingUV(positionNDC, mipLevel);
    return SAMPLE_TEXTURE2D_LOD(
        _CameraColorPyramidTexture,
        sampler_LinearClamp,
        uv,
        mipLevel);
}

#endif
