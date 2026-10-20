#ifndef PHASE_FUNCTIONS_INCLUDED
#define PHASE_FUNCTIONS_INCLUDED

#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/VolumeRendering.hlsl"

static const float MAX_VOLUMETRIC_ANISOTROPY = 0.96;

float DrainePhase(float alpha, float g, float cosTheta)
{
    float gg = g * g;
    float denom = max(1.0 + gg - 2.0 * g * cosTheta, 1e-6);
    float normalization = 1.0 + alpha * (1.0 + 2.0 * gg) / 3.0;
    return (1.0 - gg) * (1.0 + alpha * cosTheta * cosTheta)
        / (4.0 * PI * denom * sqrt(denom) * normalization);
}

void ApproximateMieLargeDropletParameters(
    float diameterMicrometers,
    out float hgG,
    out float draineG,
    out float draineAlpha,
    out float draineWeight)
{
    float d = clamp(diameterMicrometers, 5.0, 50.0);
    hgG = exp(-0.0990567 * pow(d, -1.67154));
    draineG = exp(-2.20679 / (d + 3.91029) - 0.428934);
    draineAlpha = exp(3.62489 - 8.29288 / (d + 5.52825));
    draineWeight = exp(-0.599085 * pow(d, -0.641583) - 0.665888);
}

float ApproximateMiePhase(float diameterMicrometers, float cosTheta)
{
    float hgG;
    float draineG;
    float draineAlpha;
    float draineWeight;
    ApproximateMieLargeDropletParameters(
        diameterMicrometers, hgG, draineG, draineAlpha, draineWeight);
    return lerp(
        HenyeyGreensteinPhaseFunction(hgG, cosTheta),
        DrainePhase(draineAlpha, draineG, cosTheta),
        draineWeight);
}

float2 DraineLegendreMomentsL1L2(float alpha, float g)
{
    float g2 = g * g;
    float g3 = g2 * g;
    float g4 = g2 * g2;
    float normalization = 1.0 + alpha * (1.0 + 2.0 * g2) / 3.0;
    float l1 = (g + alpha * (3.0 * g + 2.0 * g3) / 5.0) / normalization;
    float l2 = (g2 + alpha * (2.0 / 15.0 + 11.0 * g2 / 21.0 + 12.0 * g4 / 35.0))
        / normalization;
    return float2(l1, l2);
}

float2 ApproximateMieLegendreMomentsL1L2(float diameterMicrometers)
{
    float hgG;
    float draineG;
    float draineAlpha;
    float draineWeight;
    ApproximateMieLargeDropletParameters(
        diameterMicrometers, hgG, draineG, draineAlpha, draineWeight);
    float2 draineMoments = DraineLegendreMomentsL1L2(draineAlpha, draineG);
    return lerp(float2(hgG, hgG * hgG), draineMoments, draineWeight);
}

#endif
