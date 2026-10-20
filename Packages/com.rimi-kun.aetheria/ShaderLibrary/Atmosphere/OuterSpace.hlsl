#ifndef OUTER_SPACE_INCLUDED
#define OUTER_SPACE_INCLUDED

float3 RotateSpaceAroundWorldUp(float3 v, float angleRad)
{
    float s = 0.0f;
    float c = 1.0f;
    sincos(angleRad, s, c);
    return float3(v.x * c + v.z * s, v.y, -v.x * s + v.z * c);
}

bool IsSpaceEnabled()
{
    return _SpaceParams1.x >= 0.5f && _SpaceParams0.x > 0.0f;
}

float3 SampleSpaceOuterRadiance(float3 viewDirWS)
{
    float3 radiance = 0.0f;
    if (IsSpaceEnabled())
    {
        float3 sampleDir = RotateSpaceAroundWorldUp(viewDirWS, _SpaceParams0.y);
        float3 cubemapRadiance = SAMPLE_TEXTURECUBE_LOD(
            ATMOSPHERE_SPACE_CUBEMAP,
            ATMOSPHERE_SPACE_CUBEMAP_SAMPLER,
            sampleDir,
            0.0f).rgb;
        radiance = cubemapRadiance * _SpaceTint * _SpaceParams0.x;
    }
    return radiance;
}

float ComputeSpaceVisibilityFromInscatter(float3 inScatter)
{
    float suppressionStart = _SpaceParams1.y;
    float suppressionEnd = _SpaceParams1.z;
    float suppressionPower = max(_SpaceParams1.w, 0.1f);

    float skyLuminance = Luminance(inScatter);
    float t = saturate((skyLuminance - suppressionStart) / max(suppressionEnd - suppressionStart, 1e-5f));
    return pow(1.0f - t, suppressionPower);
}

float ComputeSpaceContrastVisibility(float3 inScatter, float3 attenuatedSpaceOuter)
{
    float backgroundLuminance = max(Luminance(inScatter), 1e-6f);
    float targetLuminance = max(Luminance(attenuatedSpaceOuter), 0.0f);
    float visibility = 0.0f;

    if (targetLuminance > 1e-8f)
    {
        float tMeso = smoothstep(1e-3f, 1.0f, backgroundLuminance);
        float tPhoto = smoothstep(1.0f, 50.0f, backgroundLuminance);

        float contrastThreshold = lerp(0.002f, 0.08f, tMeso);
        contrastThreshold = lerp(contrastThreshold, 0.35f, tPhoto);

        float contrast = targetLuminance / backgroundLuminance;
        visibility = smoothstep(contrastThreshold, contrastThreshold * 2.0f, contrast);
    }

    return visibility;
}

float ComputeSpaceVisibility(float3 inScatter, float3 attenuatedSpaceOuter)
{
    return ComputeSpaceVisibilityFromInscatter(inScatter) *
           ComputeSpaceContrastVisibility(inScatter, attenuatedSpaceOuter);
}

#endif
