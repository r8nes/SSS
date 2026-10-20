#ifndef WATER_CAUSTICS_INCLUDED
#define WATER_CAUSTICS_INCLUDED

TEXTURE2D(_WaterCausticsTexture);
TEXTURE2D(_WaterCausticsDistortionTexture);

half3 EvaluateWaterCaustics(
    const float3 scenePositionWS,
    const float surfacePositionY,
    const half3 lightDirection,
    const half lightShadowAttenuation,
    const half shadowDimmer,
    const float sceneDistance,
    const float4 causticsAppearanceParameters,
    const float4 causticsProjectionParameters,
    const bool underwater)
{
    const half strength = causticsAppearanceParameters.x;
    const float textureScale = causticsAppearanceParameters.y;
    const float scrollSpeed = causticsAppearanceParameters.z;
    const half textureAverage = causticsAppearanceParameters.w;
    const half focalDepth = causticsProjectionParameters.x;
    const half depthOfField = causticsProjectionParameters.y;
    const half distortionStrength = causticsProjectionParameters.z;
    const float distortionScale = causticsProjectionParameters.w;

    half sceneDepth = surfacePositionY - scenePositionWS.y;
    float mipLod = log2(max(sceneDistance, 1e-4))
        + abs(sceneDepth - focalDepth) / max(depthOfField, 1e-4);

    float2 lightProjection = lightDirection.xz * sceneDepth / (4.0 * lightDirection.y);

    float2 surfacePosXZ = scenePositionWS.xz + lightProjection;
    float scale = textureScale / 10.0;
    const float speed = _TimeParameters.x * scrollSpeed;

    float3 cuv1 = float3(
        surfacePosXZ / scale + float2(0.044 * speed + 17.16, -0.169 * speed),
        mipLod);
    float3 cuv2 = float3(
        1.37 * surfacePosXZ / scale + float2(0.248 * speed, 0.117 * speed),
        mipLod);

    if (underwater)
    {
        float distortionTextureScale = distortionScale / 10.0;
        half2 causticN = distortionStrength * UnpackNormal(
#if defined(SHADER_STAGE_COMPUTE)
            SAMPLE_TEXTURE2D_LOD(
                _WaterCausticsDistortionTexture,
                sampler_LinearRepeat,
                surfacePosXZ / distortionTextureScale,
                0)
#else
            SAMPLE_TEXTURE2D(
                _WaterCausticsDistortionTexture,
                sampler_LinearRepeat,
                surfacePosXZ / distortionTextureScale)
#endif
            ).xy;
        cuv1.xy += 1.30 * causticN;
        cuv2.xy += 1.77 * causticN;
    }

    half shadowFactor = lerp(saturate(shadowDimmer), 1.0h, saturate(lightShadowAttenuation));
    half3 causticsContribution = max(
        0.5 * SAMPLE_TEXTURE2D_LOD(
            _WaterCausticsTexture,
            sampler_LinearRepeat,
            cuv1.xy,
            cuv1.z).xyz
        + 0.5 * SAMPLE_TEXTURE2D_LOD(
            _WaterCausticsTexture,
            sampler_LinearRepeat,
            cuv2.xy,
            cuv2.z).xyz
        - textureAverage, 0.0h);
    return 1.0h + strength * shadowFactor * causticsContribution;
}

#endif
