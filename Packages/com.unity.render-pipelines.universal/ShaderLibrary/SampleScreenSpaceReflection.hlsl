#ifndef SAMPLE_SCREEN_SPACE_REFLECTION_INCLUDED
#define SAMPLE_SCREEN_SPACE_REFLECTION_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ScreenSpaceReflectionCommon.hlsl"
TEXTURE2D(_ScreenSpaceReflectionTexture);

half4 SampleScreenSpaceReflection(float2 normalizedScreenSpaceUV, float3 positionWS, float perceptualRoughness)
{
    half4 result = 0.0h;
    float smoothness = PerceptualRoughnessToPerceptualSmoothness(perceptualRoughness);
    float fade = smoothstep(_ScreenSpaceReflectionParam.y,
                           max(_ScreenSpaceReflectionParam.z, _ScreenSpaceReflectionParam.y + 1e-4), smoothness);
    if (!(fade <= 1e-3))
    {
        float4 reflection = SAMPLE_TEXTURE2D_LOD(_ScreenSpaceReflectionTexture, sampler_TrilinearClamp,
            normalizedScreenSpaceUV, GetSSRResultMip(perceptualRoughness));
        reflection = DecodeSSRResult(reflection);
        reflection.a *= fade;
        result = reflection;
    }
    return result;
}

half4 GetScreenSpaceReflection(float2 normalizedScreenSpaceUV, float3 positionWS, float perceptualRoughness)
{
#if _SCREEN_SPACE_REFLECTION_KEYWORD_DECLARED
    UNITY_BRANCH if (_SCREEN_SPACE_REFLECTION && _ScreenSpaceReflectionParam.x > 0.0h)
        return SampleScreenSpaceReflection(normalizedScreenSpaceUV, positionWS, perceptualRoughness) * _ScreenSpaceReflectionParam.x;
#endif
    return 0.0;
}
#endif
