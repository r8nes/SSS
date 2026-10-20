#ifndef SCREEN_SPACE_REFLECTION_COMMON_INCLUDED
#define SCREEN_SPACE_REFLECTION_COMMON_INCLUDED
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/ImageBasedLighting.hlsl"

float4 _ScreenSpaceReflectionParam2;

float GetSSRBlurConeHalfAngle(float perceptualRoughness)
{
    float roughness = PerceptualRoughnessToRoughness(perceptualRoughness);
    float shininess = 1.0 - roughness;
    float shininess2 = shininess * shininess;
    return min(1.5, 0.2094 * (roughness + 5.6667 * (1.0 - shininess2 * shininess2)));
}

float GetSSRResultMip(float perceptualRoughness)
{
    float mip = 0.0;
    if (_ScreenSpaceReflectionParam2.z > 0.0)
    {
        float radius = _ScreenSpaceReflectionParam2.x * tan(GetSSRBlurConeHalfAngle(perceptualRoughness));
        mip = clamp(log2(max(1.0, radius)), 0.0, _ScreenSpaceReflectionParam2.y);
        if (_ScreenSpaceReflectionParam2.z == 1.0)
            mip = floor(mip);
    }
    return mip;
}

float4 DecodeSSRResult(float4 reflection)
{
    float4 result = 0.0;
    if (reflection.a > 1e-4)
    {
        float3 color = reflection.rgb / reflection.a;
        float peak = Max3(color.r, color.g, color.b);
        color *= min(1.0, _ScreenSpaceReflectionParam2.w / max(peak, 1e-6));
        result = float4(color, saturate(reflection.a));
    }
    return result;
}
#endif
