#ifndef ATMOSPHERE_ARTISTIC_INCLUDED
#define ATMOSPHERE_ARTISTIC_INCLUDED

#include "../AtmosphereCore.hlsl"

float3 ApplyLuminancePreservingHue(float3 color, float3 hue, float amount)
{
    
    float luminance = Luminance(color);
    float hueLuminance = max(Luminance(max(hue, 0.0f)), 1e-5f);
    
    return lerp(color, max(hue, 0.0f) * (luminance / hueLuminance), saturate(amount));
}

float3 ComputeArtisticSkyPhase()
{
    float sunElevation = 1.0f;
    
    if (ATMOSPHERE_CELESTIAL_LIGHT_COUNT > 0u)
    {
#if defined(BUILDING_SKY_VIEW_LUT)
        sunElevation = _LutBuildSunDirs[0].y;
#else
        sunElevation = -GetCelestialBody(0).forward.y;
#endif
    }
    
    float zenith = smoothstep(0.18f, 0.65f, sunElevation);
    float sunset = 1.0f - smoothstep(0.03f, 0.35f, abs(sunElevation));
    float night = 1.0f - smoothstep(-0.22f, 0.02f, sunElevation);
    
    float3 basis = float3(zenith, sunset, night);
    return basis / max(dot(basis, 1.0f), 1e-4f);
}

float SkyHash31(float3 p)
{
    p = frac(p * 0.1031f);
    p += dot(p, p.yzx + 33.33f);
    return frac((p.x + p.y) * p.z);
}

float SkyValueNoise(float3 p)
{
    float3 i = floor(p);
    float3 f = frac(p);
    
    f = f * f * (3.0f - 2.0f * f);
    
    float n00 = lerp(SkyHash31(i), SkyHash31(i + float3(1, 0, 0)), f.x);
    float n10 = lerp(SkyHash31(i + float3(0, 1, 0)), SkyHash31(i + float3(1, 1, 0)), f.x);
    float n01 = lerp(SkyHash31(i + float3(0, 0, 1)), SkyHash31(i + float3(1, 0, 1)), f.x);
    float n11 = lerp(SkyHash31(i + float3(0, 1, 1)), SkyHash31(i + float3(1, 1, 1)), f.x);
    
    return lerp(lerp(n00, n10, f.y), lerp(n01, n11, f.y), f.z);
}

float SkyFbm(float3 p)
{
    float value = SkyValueNoise(p) * 0.5f;
    p *= 2.03f;
    value += SkyValueNoise(p + 19.17f) * 0.25f;
    p *= 2.01f;
    value += SkyValueNoise(p + 47.31f) * 0.125f;
    return saturate(value / 0.875f);
}

float ComputeArtisticSkyPattern(float3 viewDir, float3 upVector)
{
    float3 forwardVector;
    float3 sideVector;
    
    GetCelestialLightReferenceFrame(normalize(upVector), forwardVector, sideVector);
    
    float3 dir = normalize(viewDir);
    
    float horizontal = dot(dir, sideVector) / max(_SkyPatternParams2.x, 0.05f);
    float vertical = dot(dir, normalize(upVector)) / max(_SkyPatternParams2.y, 0.05f);
    
    float depth = dot(dir, forwardVector) * 0.18f;
    
    return SkyFbm(float3(horizontal, vertical, depth) * max(_SkyPatternParams.z, 0.25f));
}

float3 ApplyArtisticSky(float3 color, float3 viewDir, float3 upVector)
{
    float3 phase = ComputeArtisticSkyPhase();
    float phaseWeight = saturate(dot(phase, saturate(_SkyArtisticPhaseParams.xyz)));
    
    
    float luma = Luminance(color);
    
    float saturation = lerp(1.0f, max(_SkyArtisticParams.z, 0.0f), phaseWeight);
    color = lerp(luma.xxx, color, saturation);
    
    float contrast = lerp(1.0f, max(_SkyArtisticParams.w, 0.0f), phaseWeight);
    color = max((color - 0.18f) * contrast + 0.18f, 0.0f);
    color = ApplyLuminancePreservingHue(color, _SkyHueColor, _SkyArtisticParams.x * phaseWeight);
    
    float horizonMask = pow(
        saturate(1.0f - abs(dot(normalize(viewDir), normalize(upVector)))),
        2.0f);
    color = ApplyLuminancePreservingHue(color, _HorizonHueColor,
        _SkyArtisticParams.y * phaseWeight * horizonMask);
    
    float patternIntensity = _SkyPatternParams.x * phaseWeight;
    
    if (patternIntensity > 0.0f)
    {
        float pattern = ComputeArtisticSkyPattern(viewDir, upVector);
        color = lerp(luma.xxx, color,
            lerp(1.0f, lerp(0.45f, 1.65f, pattern), patternIntensity * _SkyPatternParams.y));
    }
    
    float sunsetPhase = saturate(dot(phase, saturate(_SkySunsetParams.yzw)));
    float sunsetBoost = _SkySunsetParams.x * sunsetPhase * horizonMask;
    color *= 1.0f + sunsetBoost * 1.75f;
    luma = Luminance(color);
    
    return max(lerp(luma.xxx, color, 1.0f + sunsetBoost * 0.55f), 0.0f);
}

#endif