#ifndef UNIVERSAL_MAIN_LIGHT_SHADOW_CASCADES_INCLUDED
#define UNIVERSAL_MAIN_LIGHT_SHADOW_CASCADES_INCLUDED

// HDRP-style directional cascade evaluation on top of URP's existing constant layout.
// _CascadeShadowSplitSpheresN.w stores squared-range transition border; the actual
// radius squared remains in _CascadeShadowSplitSphereRadii.
struct MainLightCascadeSelection
{
    int current;
    int next;
    int cascadeCount;
    float alpha;
};

float4 GetMainLightCascadeSphere(int cascadeIndex)
{
    float4 sphere = _CascadeShadowSplitSpheres3;
    if (cascadeIndex == 0)
        sphere = _CascadeShadowSplitSpheres0;
    else if (cascadeIndex == 1)
        sphere = _CascadeShadowSplitSpheres1;
    else if (cascadeIndex == 2)
        sphere = _CascadeShadowSplitSpheres2;
    return sphere;
}

float GetMainLightCascadeRadiusSquared(int cascadeIndex)
{
    float radiusSquared = _CascadeShadowSplitSphereRadii.w;
    if (cascadeIndex == 0)
        radiusSquared = _CascadeShadowSplitSphereRadii.x;
    else if (cascadeIndex == 1)
        radiusSquared = _CascadeShadowSplitSphereRadii.y;
    else if (cascadeIndex == 2)
        radiusSquared = _CascadeShadowSplitSphereRadii.z;
    return radiusSquared;
}

int GetMainLightCascadeCount()
{
    int count = 0;
    UNITY_UNROLL
    for (int cascadeIndex = 0; cascadeIndex < MAX_SHADOW_CASCADES; ++cascadeIndex)
        count += GetMainLightCascadeRadiusSquared(cascadeIndex) > 0.0 ? 1 : 0;
    return count;
}

MainLightCascadeSelection SelectMainLightCascade(float3 positionWS)
{
    MainLightCascadeSelection result;
    result.current = -1;
    result.next = -1;
    result.cascadeCount = GetMainLightCascadeCount();
    result.alpha = 0.0;

    float relativeDistance = 0.0;
    float3 directionFromSphereCenter = 0.0;
    float border = 0.0;

#if defined(SHADER_API_METAL)
    [unroll]
#endif
    for (int cascadeIndex = 0; cascadeIndex < MAX_SHADOW_CASCADES; ++cascadeIndex)
    {
        if (cascadeIndex >= result.cascadeCount)
            break;

        float4 sphere = GetMainLightCascadeSphere(cascadeIndex);
        float radiusSquared = GetMainLightCascadeRadiusSquared(cascadeIndex);
        float3 sphereToPosition = positionWS - sphere.xyz;
        float distanceSquared = dot(sphereToPosition, sphereToPosition);
        relativeDistance = distanceSquared / max(radiusSquared, 1e-8);

        if (relativeDistance > 0.0 && relativeDistance <= 1.0)
        {
            directionFromSphereCenter = sphereToPosition * rsqrt(max(distanceSquared, 1e-8));
            border = sphere.w;
            result.current = cascadeIndex;
            break;
        }
    }

    if (result.current >= 0)
    {
        result.next = min(result.current + 1, result.cascadeCount - 1);
        result.alpha = border <= 0.0
            ? 0.0
            : saturate((relativeDistance - (1.0 - border)) / border);

        // The wrong-side suppression cannot change a zero transition weight.
        UNITY_BRANCH
        if (result.alpha > 0.0)
        {
            float3 viewDirection = GetWorldSpaceViewDir(positionWS);
            float cascadeDot = dot(viewDirection, directionFromSphereCenter);
            float lerpSharpness = 8.0 + 512.0 * smoothstep(1.0, 0.7, border);
            result.alpha = lerp(result.alpha, 0.0, saturate(cascadeDot * lerpSharpness));
        }
    }
    return result;
}

float3 GetMainLightReceiverNormal(float3 normalWS)
{
    float normalLengthSquared = dot(normalWS, normalWS);
    return normalLengthSquared > 1e-8
        ? normalWS * rsqrt(normalLengthSquared)
        : float3(0.0, 0.0, 0.0);
}

float3 ApplyMainLightReceiverNormalBias(float3 positionWS, float3 receiverNormalWS, int cascadeIndex)
{
    // Projection.w is populated by MainLightShadowCasterPass with
    // effectiveShadowNormalBias * per-cascade HDRP worldTexelSize.
    return positionWS + receiverNormalWS * _DirLightPcssProjs[cascadeIndex].w;
}

struct MainLightCascadeCoordinates
{
    float4 atlasCoord;
    float3 positionNDC;
};

MainLightCascadeCoordinates GetMainLightCascadeCoordinates(float3 positionWS, float3 receiverNormalWS, int cascadeIndex)
{
    MainLightCascadeCoordinates result;

    float3 biasedPositionWS = ApplyMainLightReceiverNormalBias(positionWS, receiverNormalWS, cascadeIndex);
    float4 bakedAtlasCoord = float4(
        mul(_MainLightWorldToShadow[cascadeIndex], float4(biasedPositionWS, 1.0)).xyz,
        cascadeIndex);

    // URP bakes atlas scale/offset into _MainLightWorldToShadow. Recover the
    // cascade-local coordinates so evaluation follows HDRP's order:
    // world -> local NDC -> saturated local UV -> atlas UV. Sampling the baked
    // coordinates directly lets a selected neighbouring split escape its tile.
    float4 offsetScale = _CascadeOffsetScales[cascadeIndex];
    float2 safeScale = max(offsetScale.zw, float2(1e-5, 1e-5));
    float2 localUV = (bakedAtlasCoord.xy - offsetScale.xy) / safeScale;

    result.positionNDC = float3(localUV * 2.0 - 1.0, bakedAtlasCoord.z);
    result.atlasCoord = float4(saturate(localUV) * safeScale + offsetScale.xy,
        bakedAtlasCoord.z, cascadeIndex);
    return result;
}

float4 GetMainLightCascadeShadowCoord(float3 positionWS, float3 receiverNormalWS, int cascadeIndex)
{
    return GetMainLightCascadeCoordinates(positionWS, receiverNormalWS, cascadeIndex).atlasCoord;
}

bool IsMainLightCascadeProjectionValid(MainLightCascadeCoordinates coordinates, int cascadeIndex)
{
    float4 offsetScale = _CascadeOffsetScales[cascadeIndex];
    float2 inverseCascadeResolution = _MainLightShadowmapSize.xy /
        max(offsetScale.zw, float2(1e-5, 1e-5));
    return all(abs(coordinates.positionNDC.xy) <= (1.0 - inverseCascadeResolution * 0.5));
}

real SampleMainLightDirectionalFilterRaw(float4 shadowCoord, half4 shadowParams, ShadowSamplingData samplingData)
{
    real shadow = 1.0;

    if ((uint)shadowCoord.w >= MAX_SHADOW_CASCADES)
        return shadow;

    UNITY_BRANCH
    if (_PCSSShadowsEnabled > 0.5)
    {
        shadow = SampleShadowmapPCSSMainLight(
            TEXTURE2D_SHADOW_ARGS(_MainLightShadowmapTexture, sampler_LinearClampCompare),
            shadowCoord,
            samplingData);
        return shadow;
    }

    shadowCoord.z += MAIN_LIGHT_FIXED_UNIFORM_BIAS;
    if (BEYOND_SHADOW_FAR(shadowCoord))
        return shadow;

#if defined(_SHADOWS_SOFT_LOW)
    shadow = SampleShadowmapFilteredMediumQuality(
        TEXTURE2D_SHADOW_ARGS(_MainLightShadowmapTexture, sampler_LinearClampCompare),
        shadowCoord,
        samplingData);
#elif defined(_SHADOWS_SOFT_MEDIUM) || defined(_SHADOWS_SOFT_HIGH)
    shadow = SampleShadowmapFilteredHighQuality(
        TEXTURE2D_SHADOW_ARGS(_MainLightShadowmapTexture, sampler_LinearClampCompare),
        shadowCoord,
        samplingData);
#elif defined(_SHADOWS_SOFT)
    if (shadowParams.y == SOFT_SHADOW_QUALITY_LOW)
    {
        shadow = SampleShadowmapFilteredMediumQuality(
            TEXTURE2D_SHADOW_ARGS(_MainLightShadowmapTexture, sampler_LinearClampCompare),
            shadowCoord,
            samplingData);
    }
    else if (shadowParams.y >= SOFT_SHADOW_QUALITY_MEDIUM)
    {
        shadow = SampleShadowmapFilteredHighQuality(
            TEXTURE2D_SHADOW_ARGS(_MainLightShadowmapTexture, sampler_LinearClampCompare),
            shadowCoord,
            samplingData);
    }
    else
    {
        shadow = SAMPLE_TEXTURE2D_SHADOW(
            _MainLightShadowmapTexture,
            sampler_LinearClampCompare,
            shadowCoord.xyz);
    }
#else
    shadow = SAMPLE_TEXTURE2D_SHADOW(
        _MainLightShadowmapTexture,
        sampler_LinearClampCompare,
        shadowCoord.xyz);
#endif

    return shadow;
}

real EvaluateMainLightCascadeRaw(float3 positionWS, float3 receiverNormalWS, int cascadeIndex,
    half4 shadowParams, ShadowSamplingData samplingData, out float4 shadowCoord)
{
    shadowCoord = GetMainLightCascadeShadowCoord(positionWS, receiverNormalWS, cascadeIndex);
    return SampleMainLightDirectionalFilterRaw(shadowCoord, shadowParams, samplingData);
}

half EvaluateMainLightCascadeDither(float3 positionWS, float3 receiverNormalWS,
    half4 shadowParams, ShadowSamplingData samplingData, MainLightCascadeSelection selection)
{
    if (selection.current < 0)
        return 1.0h;

    int selectedCascade = selection.current;
    bool hasNextCascade = selection.next != selection.current;
    if (hasNextCascade && selection.alpha > 0.0)
    {
        float noise = InterleavedGradientNoise(
            samplingData.screenUV * _ScreenParams.xy,
            (uint)_PCSSFrameIndex);
        if (step(noise, selection.alpha) > 0.0)
            selectedCascade = selection.next;
    }

    float4 shadowCoord;
    half shadow = half(EvaluateMainLightCascadeRaw(
        positionWS, receiverNormalWS, selectedCascade, shadowParams, samplingData, shadowCoord));

    // HDRP fades the last cascade to fully lit instead of dithering to a nonexistent split.
    return hasNextCascade ? shadow : lerp(shadow, 1.0h, half(selection.alpha));
}

half EvaluateMainLightCascadeBlend(float3 positionWS, float3 receiverNormalWS,
    half4 shadowParams, ShadowSamplingData samplingData, MainLightCascadeSelection selection)
{
    if (selection.current < 0)
        return 1.0h;

    float4 currentCoord;
    half currentShadow = half(EvaluateMainLightCascadeRaw(
        positionWS, receiverNormalWS, selection.current, shadowParams, samplingData, currentCoord));

    bool hasNextCascade = selection.next != selection.current;
    if (!hasNextCascade)
        return lerp(currentShadow, 1.0h, half(selection.alpha));

    // Do not materialize the second cascade outside the transition band.
    UNITY_BRANCH
    if (selection.alpha <= 0.0)
        return currentShadow;

    MainLightCascadeCoordinates nextCoordinates =
        GetMainLightCascadeCoordinates(positionWS, receiverNormalWS, selection.next);
    half nextShadow = currentShadow;
    UNITY_BRANCH
    if (IsMainLightCascadeProjectionValid(nextCoordinates, selection.next))
        nextShadow = half(SampleMainLightDirectionalFilterRaw(
            nextCoordinates.atlasCoord, shadowParams, samplingData));

    return lerp(currentShadow, nextShadow, half(selection.alpha));
}

half EvaluateMainLightDirectionalShadowRaw(float3 positionWS, float3 normalWS,
    half4 shadowParams, ShadowSamplingData samplingData)
{
    MainLightCascadeSelection selection = SelectMainLightCascade(positionWS);
    if (selection.current < 0)
        return 1.0h;

    float3 receiverNormalWS = GetMainLightReceiverNormal(normalWS);

    // HDRP uses a true two-cascade blend for its expensive PCSS path and a
    // single filtered read with stochastic split selection for cheaper paths.
    UNITY_BRANCH
    if (_PCSSShadowsEnabled > 0.5)
        return EvaluateMainLightCascadeBlend(
            positionWS, receiverNormalWS, shadowParams, samplingData, selection);

    return EvaluateMainLightCascadeDither(
        positionWS, receiverNormalWS, shadowParams, samplingData, selection);
}

half EvaluateMainLightDirectionalShadow(float3 positionWS, float3 normalWS,
    half4 shadowParams, ShadowSamplingData samplingData)
{
    return LerpWhiteTo(
        EvaluateMainLightDirectionalShadowRaw(positionWS, normalWS, shadowParams, samplingData),
        shadowParams.x);
}

#endif
