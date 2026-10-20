#ifndef HDRI_SKY_COMMON_INCLUDED
#define HDRI_SKY_COMMON_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

TEXTURECUBE(_Cubemap);
SAMPLER(sampler_Cubemap);
TEXTURE2D(_Flowmap);
SAMPLER(sampler_Flowmap);

float4 _SkyParam;
float4 _FlowmapParam;
int _HDRIDistortionMode;

#define HDRI_INTENSITY _SkyParam.x
#define HDRI_COS_PHI _SkyParam.z
#define HDRI_SIN_PHI _SkyParam.w
#define HDRI_UPPER_HEMISPHERE _FlowmapParam.x
#define HDRI_SCROLL_FACTOR _FlowmapParam.y
#define HDRI_SCROLL_DIRECTION _FlowmapParam.zw

float3 RotateHDRIDirection(float3 direction, float2 cosSin)
{
    return float3(
        cosSin.x * direction.x - cosSin.y * direction.z,
        direction.y,
        cosSin.y * direction.x + cosSin.x * direction.z);
}

float2 HDRILatLong(float3 direction, bool upperHemisphereOnly)
{
    float2 uv = DirectionToLatLongCoordinate(normalize(direction));
    if (upperHemisphereOnly)
        uv.y = saturate((uv.y - 0.5) * 2.0);
    return uv;
}

float3 SampleHDRIRaw(float3 direction)
{
    return SAMPLE_TEXTURECUBE_LOD(_Cubemap, sampler_Cubemap, normalize(direction), 0).rgb;
}

float3 SampleHDRIUnrotated(float3 direction)
{
    float3 result = SampleHDRIRaw(direction);
    bool upperHemisphereOnly = HDRI_UPPER_HEMISPHERE > 0.5;

    if (_HDRIDistortionMode != 0 && (direction.y >= 0.0 || !upperHemisphereOnly))
    {
        float2 alpha = frac(float2(HDRI_SCROLL_FACTOR, HDRI_SCROLL_FACTOR + 0.5)) - 0.5;
        float3 dd = float3(0.0, 0.0, 0.0);

        if (_HDRIDistortionMode == 2)
        {
            float3 tangent = normalize(cross(direction, float3(0.0, 1.0, 0.0)) + float3(1e-6, 0.0, 0.0));
            float3 bitangent = cross(tangent, direction);
            float3 windDir = RotateHDRIDirection(direction, HDRI_SCROLL_DIRECTION);
            float2 flow = SAMPLE_TEXTURE2D_LOD(
                _Flowmap,
                sampler_Flowmap,
                HDRILatLong(windDir, upperHemisphereOnly),
                0).rg * 2.0 - 1.0;
            dd = flow.x * tangent + flow.y * bitangent;
        }
        else
        {
            float3 windDir = float3(HDRI_SCROLL_DIRECTION.x, 0.0, HDRI_SCROLL_DIRECTION.y);
            dd = windDir * sin(direction.y * PI * 0.5);
        }

        float3 color1 = SampleHDRIRaw(direction + alpha.x * dd);
        float3 color2 = SampleHDRIRaw(direction + alpha.y * dd);
        result = lerp(color1, color2, abs(2.0 * alpha.x));
    }

    return result;
}

float3 SampleHDRISky(float3 direction)
{
    direction = RotateHDRIDirection(normalize(direction), float2(HDRI_COS_PHI, HDRI_SIN_PHI));
    return max(SampleHDRIUnrotated(direction) * HDRI_INTENSITY, 0.0);
}

#endif
