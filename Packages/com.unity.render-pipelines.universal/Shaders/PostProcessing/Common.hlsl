#ifndef UNIVERSAL_POSTPROCESSING_COMMON_INCLUDED
#define UNIVERSAL_POSTPROCESSING_COMMON_INCLUDED

#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
#include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

#if _FXAA
// Notes on FXAA:
// * We now rely on the official FXAA implementation (authored by Timothy Lottes while at NVIDIA)
//   with minimal changes made by Unity to integrate with URP.
// * The following 'Tweakable' defines are used by the FXAA implementation and can be changed if desired:
//   * FXAA_PC set to 1 is the highest quality implementation ("PC" here is a misnomer, it will run on all platforms).
//   * FXAA_PC set to 0 is the cheaper 'FXAA_PC_CONSOLE' variant
//     (it's equivalent to URP's old implementation but less noisy and should run faster than before)
//   * FXAA_GREEN_AS_LUMA can be set to 0 for an extra performance increase but will only antialias edges that have
//     some green in them (will be visually equivalent on the vast majority of scenes).
//   * FXAA_QUALITY__PRESET is used when FXAA_PC is set ot 1. We chose preset 12 as it runs almost as fast on Switch as
//     our old noisy implementation did.
//     On all other platforms we could basically get away with preset 15 which has slightly better edge quality.

// Tweakable params (can be changed to get different performance and quality tradeoffs)
#if (SHADER_API_PS5 || SHADER_API_SWITCH2) && defined(HDR_INPUT)
// The console implementation does not generate artefacts when the input pixels are in nits (monitor HDR range).
#define FXAA_PC 0
#else
#define FXAA_PC 1
#endif
#define FXAA_GREEN_AS_LUMA 0
#define FXAA_QUALITY__PRESET 12

// Fixed params (should not be changed)
#define FXAA_HLSL_5 1
#define FXAA_GATHER4_ALPHA 0
#define FXAA_PC_CONSOLE !FXAA_PC

#include "Packages/com.unity.render-pipelines.universal/Shaders/PostProcessing/FXAA3_11.hlsl"
#endif

// ----------------------------------------------------------------------------------
// Utility functions

half GetLuminance(half3 colorLinear)
{
#if _TONEMAP_ACES
    return AcesLuminance(colorLinear);
#else
    return Luminance(colorLinear);
#endif
}

real3 GetSRGBToLinear(real3 c)
{
#if _USE_FAST_SRGB_LINEAR_CONVERSION
    return FastSRGBToLinear(c);
#else
    return SRGBToLinear(c);
#endif
}

real4 GetSRGBToLinear(real4 c)
{
#if _USE_FAST_SRGB_LINEAR_CONVERSION
    return FastSRGBToLinear(c);
#else
    return SRGBToLinear(c);
#endif
}

real3 GetLinearToSRGB(real3 c)
{
#if _USE_FAST_SRGB_LINEAR_CONVERSION
    return FastLinearToSRGB(c);
#else
    return LinearToSRGB(c);
#endif
}

real4 GetLinearToSRGB(real4 c)
{
#if _USE_FAST_SRGB_LINEAR_CONVERSION
    return FastLinearToSRGB(c);
#else
    return LinearToSRGB(c);
#endif
}

// ----------------------------------------------------------------------------------
// Shared functions for uber & fast path (on-tile)
// These should only process an input color, don't sample in neighbor pixels!

half3 ApplyVignette(half3 input, float2 uv, float2 center, float intensity, float roundness, float smoothness, half3 color)
{
    float2 dist = abs(uv - center) * intensity;

    dist.x *= roundness;
    float vfactor = pow(saturate(1.0 - dot(dist, dist)), smoothness);
    return input * lerp(color, (1.0).xxx, vfactor);
}

// ----------------------------------------------------------------------------------
// ACES Filmic Tone Mapping
//
// Unreal Engine FilmToneMap path. Core curve follows Engine/Shaders/Private/TonemapCommon.ush
// and the needed ACES helpers/matrices follow Engine/Shaders/Private/ACES.ush.

static const float3x3 ACES_FILMIC_AP0_2_AP1_MAT = float3x3(
     1.4514393161, -0.2365107469, -0.2149285693,
    -0.0765537734,  1.1762296998, -0.0996759264,
     0.0083161484, -0.0060324498,  0.9977163014);

static const float3x3 ACES_FILMIC_AP1_2_AP0_MAT = float3x3(
     0.6954522414,  0.1406786965,  0.1638690622,
     0.0447945634,  0.8596711185,  0.0955343182,
    -0.0055258826,  0.0040252103,  1.0015006723);

static const float3 ACES_FILMIC_AP1_RGB2Y = float3(0.2722287168, 0.6740817658, 0.0536895174);

static const float3x3 ACES_FILMIC_XYZ_2_AP1_MAT = float3x3(
     1.6410233797, -0.3248032942, -0.2364246952,
    -0.6636628587,  1.6153315917,  0.0167563477,
     0.0117218943, -0.0082844420,  0.9883948585);

static const float3x3 ACES_FILMIC_AP1_2_XYZ_MAT = float3x3(
     0.6624541811, 0.1340042065, 0.1561876870,
     0.2722287168, 0.6740817658, 0.0536895174,
    -0.0055746495, 0.0040607335, 1.0103391003);

static const float3x3 ACES_FILMIC_XYZ_2_SRGB_MAT = float3x3(
     3.2409699419, -1.5373831776, -0.4986107603,
    -0.9692436363,  1.8759675015,  0.0415550574,
     0.0556300797, -0.2039769589,  1.0569715142);

static const float3x3 ACES_FILMIC_SRGB_2_XYZ_MAT = float3x3(
    0.4124564, 0.3575761, 0.1804375,
    0.2126729, 0.7151522, 0.0721750,
    0.0193339, 0.1191920, 0.9503041);

static const float3x3 ACES_FILMIC_XYZ_2_REC2020_MAT = float3x3(
     1.7166084, -0.3556621, -0.2533601,
    -0.6666829,  1.6164776,  0.0157685,
     0.0176422, -0.0427763,  0.94222867);

static const float3x3 ACES_FILMIC_REC2020_2_XYZ_MAT = float3x3(
    0.6369736, 0.1446172, 0.1688585,
    0.2627066, 0.6779996, 0.0592938,
    0.0000000, 0.0280728, 1.0608437);

static const float3x3 ACES_FILMIC_D65_2_D60_CAT = float3x3(
     1.01303,    0.00610531, -0.014971,
     0.00769823, 0.998165,   -0.00503203,
    -0.00284131, 0.00468516,  0.924507);

static const float3x3 ACES_FILMIC_D60_2_D65_CAT = float3x3(
     0.987224,   -0.00611327, 0.0159533,
    -0.00759836,  1.00186,    0.00533002,
     0.00307257, -0.00509595, 1.08168);

static const float ACES_FILMIC_SLOPE = 0.91;
static const float ACES_FILMIC_TOE = 0.53;
static const float ACES_FILMIC_SHOULDER = 0.23;
static const float ACES_FILMIC_BLACK_CLIP = 0.0;
static const float ACES_FILMIC_WHITE_CLIP = 0.035;
static const float ACES_FILMIC_PI = 3.14159265358979323846;

float ACESFilmicRgbToSaturation(float3 rgb)
{
    float minrgb = min(min(rgb.r, rgb.g), rgb.b);
    float maxrgb = max(max(rgb.r, rgb.g), rgb.b);
    return (max(maxrgb, 1e-10) - max(minrgb, 1e-10)) / max(maxrgb, 1e-2);
}

float ACESFilmicGlowFwd(float ycIn, float glowGainIn, float glowMid)
{
    float glowGainOut;

    if (ycIn <= 2.0 / 3.0 * glowMid)
        glowGainOut = glowGainIn;
    else if (ycIn >= 2.0 * glowMid)
        glowGainOut = 0.0;
    else
        glowGainOut = glowGainIn * (glowMid / ycIn - 0.5);

    return glowGainOut;
}

float ACESFilmicSigmoidShaper(float x)
{
    float t = max(1.0 - abs(0.5 * x), 0.0);
    float y = 1.0 + sign(x) * (1.0 - t * t);
    return 0.5 * y;
}

float ACESFilmicRgbToHue(float3 rgb)
{
    float hue;

    if (rgb.r == rgb.g && rgb.g == rgb.b)
        hue = 0.0;
    else
        hue = (180.0 / ACES_FILMIC_PI) * atan2(sqrt(3.0) * (rgb.g - rgb.b), 2.0 * rgb.r - rgb.g - rgb.b);

    if (hue < 0.0)
        hue += 360.0;

    return clamp(hue, 0.0, 360.0);
}

float ACESFilmicCenterHue(float hue, float centerH)
{
    float hueCentered = hue - centerH;

    if (hueCentered < -180.0)
        hueCentered += 360.0;
    else if (hueCentered > 180.0)
        hueCentered -= 360.0;

    return hueCentered;
}

float ACESFilmicRgbToYc(float3 rgb)
{
    float r = rgb.r;
    float g = rgb.g;
    float b = rgb.b;
    float ycRadiusWeight = 1.75;
    float chroma = sqrt(b * (b - g) + g * (g - r) + r * (r - b));
    return (b + g + r + ycRadiusWeight * chroma) / 3.0;
}

float3 ACESFilmicTonemapAP1(float3 linearColor)
{
    float3 colorAP1 = linearColor;
    float3 colorAP0 = mul(ACES_FILMIC_AP1_2_AP0_MAT, colorAP1);

    const float rrtGlowGain = 0.05;
    const float rrtGlowMid = 0.08;
    float saturation = ACESFilmicRgbToSaturation(colorAP0);
    float ycIn = ACESFilmicRgbToYc(colorAP0);
    float s = ACESFilmicSigmoidShaper((saturation - 0.4) / 0.2);
    float addedGlow = 1.0 + ACESFilmicGlowFwd(ycIn, rrtGlowGain * s, rrtGlowMid);
    colorAP0 *= addedGlow;

    const float rrtRedScale = 0.82;
    const float rrtRedPivot = 0.03;
    const float rrtRedHue = 0.0;
    const float rrtRedWidth = 135.0;
    float hue = ACESFilmicRgbToHue(colorAP0);
    float centeredHue = ACESFilmicCenterHue(hue, rrtRedHue);
    float hueWeightBase = smoothstep(0.0, 1.0, 1.0 - abs(2.0 * centeredHue / rrtRedWidth));
    float hueWeight = hueWeightBase * hueWeightBase;
    colorAP0.r += hueWeight * saturation * (rrtRedPivot - colorAP0.r) * (1.0 - rrtRedScale);

    float3 workingColor = mul(ACES_FILMIC_AP0_2_AP1_MAT, colorAP0);
    workingColor = max(0.0, workingColor);
    workingColor = lerp(dot(workingColor, ACES_FILMIC_AP1_RGB2Y).xxx, workingColor, 0.96);

    const float toeScale = 1.0 + ACES_FILMIC_BLACK_CLIP - ACES_FILMIC_TOE;
    const float shoulderScale = 1.0 + ACES_FILMIC_WHITE_CLIP - ACES_FILMIC_SHOULDER;
    const float inMatch = 0.18;
    const float outMatch = 0.18;

    float toeMatch;
    if (ACES_FILMIC_TOE > 0.8)
    {
        toeMatch = (1.0 - ACES_FILMIC_TOE - outMatch) / ACES_FILMIC_SLOPE + log10(inMatch);
    }
    else
    {
        const float bt = (outMatch + ACES_FILMIC_BLACK_CLIP) / toeScale - 1.0;
        toeMatch = log10(inMatch) - 0.5 * log((1.0 + bt) / (1.0 - bt)) * (toeScale / ACES_FILMIC_SLOPE);
    }

    float straightMatch = (1.0 - ACES_FILMIC_TOE) / ACES_FILMIC_SLOPE - toeMatch;
    float shoulderMatch = ACES_FILMIC_SHOULDER / ACES_FILMIC_SLOPE - straightMatch;

    float3 logColor = log10(workingColor);
    float3 straightColor = ACES_FILMIC_SLOPE * (logColor + straightMatch);
    float3 toeColor = (-ACES_FILMIC_BLACK_CLIP) + (2.0 * toeScale) / (1.0 + exp((-2.0 * ACES_FILMIC_SLOPE / toeScale) * (logColor - toeMatch)));
    float3 shoulderColor = (1.0 + ACES_FILMIC_WHITE_CLIP) - (2.0 * shoulderScale) / (1.0 + exp((2.0 * ACES_FILMIC_SLOPE / shoulderScale) * (logColor - shoulderMatch)));

    toeColor = float3(
        logColor.r < toeMatch ? toeColor.r : straightColor.r,
        logColor.g < toeMatch ? toeColor.g : straightColor.g,
        logColor.b < toeMatch ? toeColor.b : straightColor.b);
    shoulderColor = float3(
        logColor.r > shoulderMatch ? shoulderColor.r : straightColor.r,
        logColor.g > shoulderMatch ? shoulderColor.g : straightColor.g,
        logColor.b > shoulderMatch ? shoulderColor.b : straightColor.b);

    float3 t = saturate((logColor - toeMatch) / (shoulderMatch - toeMatch));
    t = shoulderMatch < toeMatch ? 1.0 - t : t;
    t = (3.0 - 2.0 * t) * t * t;
    float3 toneColor = lerp(toeColor, shoulderColor, t);

    toneColor = lerp(dot(toneColor, ACES_FILMIC_AP1_RGB2Y).xxx, toneColor, 0.93);
    return max(0.0, toneColor);
}

float3 ACESFilmicSRGBToAP1(float3 color)
{
    return mul(ACES_FILMIC_XYZ_2_AP1_MAT, mul(ACES_FILMIC_D65_2_D60_CAT, mul(ACES_FILMIC_SRGB_2_XYZ_MAT, color)));
}

float3 ACESFilmicAP1ToSRGB(float3 color)
{
    return mul(ACES_FILMIC_XYZ_2_SRGB_MAT, mul(ACES_FILMIC_D60_2_D65_CAT, mul(ACES_FILMIC_AP1_2_XYZ_MAT, color)));
}

float3 ACESFilmicRec2020ToAP1(float3 color)
{
    return mul(ACES_FILMIC_XYZ_2_AP1_MAT, mul(ACES_FILMIC_D65_2_D60_CAT, mul(ACES_FILMIC_REC2020_2_XYZ_MAT, color)));
}

float3 ACESFilmicAP1ToRec2020(float3 color)
{
    return mul(ACES_FILMIC_XYZ_2_REC2020_MAT, mul(ACES_FILMIC_D60_2_D65_CAT, mul(ACES_FILMIC_AP1_2_XYZ_MAT, color)));
}

float3 ACESFilmicTonemapSRGB(float3 color)
{
    float3 colorAP1 = ACESFilmicSRGBToAP1(color);
    colorAP1 = ACESFilmicTonemapAP1(colorAP1);
    return max(0.0, ACESFilmicAP1ToSRGB(colorAP1));
}

float3 ACESFilmicTonemapRec2020(float3 color)
{
    float3 colorAP1 = ACESFilmicRec2020ToAP1(color);
    colorAP1 = ACESFilmicTonemapAP1(colorAP1);
    return max(0.0, ACESFilmicAP1ToRec2020(colorAP1));
}

// ----------------------------------------------------------------------------------
// GT Sport Tone Mapping
//
// Legacy Gran Turismo / Uchimura-style curve commonly used in stylized pipelines.

static const float GTSPORT_P = 1.0;
static const float GTSPORT_A = 1.0;
static const float GTSPORT_M = 0.22;
static const float GTSPORT_L = 0.4;
static const float GTSPORT_C = 1.33;
static const float GTSPORT_B = 0.0;

float GTSportTonemapCurve(float x)
{
    float l0 = (GTSPORT_P - GTSPORT_M) * GTSPORT_L / GTSPORT_A;

    float linearValue = GTSPORT_M + GTSPORT_A * (x - GTSPORT_M);
    float toe = GTSPORT_M * pow(x / GTSPORT_M, GTSPORT_C) + GTSPORT_B;

    float shoulderStartX = GTSPORT_M + l0;
    float shoulderStartY = GTSPORT_M + GTSPORT_A * l0;
    float shoulderCoeff = GTSPORT_A * GTSPORT_P / (GTSPORT_P - shoulderStartY);
    float shoulder = GTSPORT_P - (GTSPORT_P - shoulderStartY) * exp(-(shoulderCoeff * (x - shoulderStartX) / GTSPORT_P));

    float w0 = 1.0 - smoothstep(0.0, GTSPORT_M, x);
    float w2 = step(shoulderStartX, x);
    float w1 = 1.0 - w0 - w2;

    return toe * w0 + linearValue * w1 + shoulder * w2;
}

float3 GTSportTonemap(float3 color)
{
    color = max(color, 0.0);
    return float3(
        GTSportTonemapCurve(color.r),
        GTSportTonemapCurve(color.g),
        GTSportTonemapCurve(color.b));
}

// ----------------------------------------------------------------------------------
// GT7 Tone Mapping
//
// HLSL port of Polyphony Digital's reference C++ sample:
// https://blog.selfshadow.com/publications/s2025-shading-course/pdi/supplemental/gt7_tone_mapping.cpp
// Default UCS mode: ICtCp.
//
// MIT License
//
// Copyright (c) 2025 Polyphony Digital Inc.
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.

static const float GT7_REFERENCE_LUMINANCE = 100.0;
static const float GT7_SDR_PAPER_WHITE = 250.0;
static const float GT7_ALPHA = 0.25;
static const float GT7_GRAY_POINT = 0.538;
static const float GT7_LINEAR_SECTION = 0.444;
static const float GT7_TOE_STRENGTH = 1.280;
static const float GT7_BLEND_RATIO = 0.6;
static const float GT7_FADE_START = 0.98;
static const float GT7_FADE_END = 1.16;

float GT7FramebufferToPhysical(float fbValue)
{
    return fbValue * GT7_REFERENCE_LUMINANCE;
}

float GT7PhysicalToFramebuffer(float physicalNits)
{
    return physicalNits / GT7_REFERENCE_LUMINANCE;
}

float GT7SmoothStep(float x, float edge0, float edge1)
{
    float edgeRange = max(edge1 - edge0, 1e-6);
    float t = saturate((x - edge0) / edgeRange);
    return t * t * (3.0 - 2.0 * t);
}

float GT7ChromaCurve(float x, float a, float b)
{
    return 1.0 - GT7SmoothStep(x, a, b);
}

float GT7EvaluateCurve(float x, float peakIntensity)
{
    float result = 0.0;

    if (x >= 0.0)
    {
        float safePeakIntensity = max(peakIntensity, 1e-6);
        float k = (GT7_LINEAR_SECTION - 1.0) / (GT7_ALPHA - 1.0);
        float kA = safePeakIntensity * GT7_LINEAR_SECTION + safePeakIntensity * k;
        float kB = -safePeakIntensity * k * exp(GT7_LINEAR_SECTION / k);
        float kC = -1.0 / (k * safePeakIntensity);

        float weightLinear = GT7SmoothStep(x, 0.0, GT7_GRAY_POINT);
        float weightToe = 1.0 - weightLinear;
        float shoulder = kA + kB * exp(x * kC);

        if (x < GT7_LINEAR_SECTION * safePeakIntensity)
        {
            float toeBase = max(x / GT7_GRAY_POINT, 0.0);
            float toeMapped = GT7_GRAY_POINT * pow(max(toeBase, 0.0), GT7_TOE_STRENGTH);
            result = weightToe * toeMapped + weightLinear * x;
        }
        else
        {
            result = shoulder;
        }
    }

    return result;
}

float GT7EotfSt2084(float n, float exponentScaleFactor)
{
    n = saturate(n);

    const float m1 = 0.1593017578125;
    float m2 = 78.84375 * exponentScaleFactor;
    const float c1 = 0.8359375;
    const float c2 = 18.8515625;
    const float c3 = 18.6875;
    const float pqC = 10000.0;

    float np = pow(max(n, 0.0), 1.0 / m2);
    float l = np - c1;

    if (l < 0.0)
        l = 0.0;

    l = l / (c2 - c3 * np);
    l = pow(max(l, 0.0), 1.0 / m1);

    // Convert absolute luminance (cd/m^2) into the frame-buffer linear scale.
    return GT7PhysicalToFramebuffer(l * pqC);
}

float GT7InverseEotfSt2084(float v, float exponentScaleFactor)
{
    const float m1 = 0.1593017578125;
    float m2 = 78.84375 * exponentScaleFactor;
    const float c1 = 0.8359375;
    const float c2 = 18.8515625;
    const float c3 = 18.6875;
    const float pqC = 10000.0;

    // Convert the frame-buffer linear scale into absolute luminance (cd/m^2).
    float physical = GT7FramebufferToPhysical(v);
    float y = max(physical / pqC, 0.0); // Normalize for the ST-2084 curve

    float ym = pow(max(y, 0.0), m1);
    return exp2(m2 * (log2(c1 + c2 * ym) - log2(1.0 + c3 * ym)));
}

float3 GT7RgbToICtCp(float3 rgb)
{
    float l = (rgb.r * 1688.0 + rgb.g * 2146.0 + rgb.b * 262.0) / 4096.0;
    float m = (rgb.r * 683.0 + rgb.g * 2951.0 + rgb.b * 462.0) / 4096.0;
    float s = (rgb.r * 99.0 + rgb.g * 309.0 + rgb.b * 3688.0) / 4096.0;

    float lPQ = GT7InverseEotfSt2084(l, 1.0);
    float mPQ = GT7InverseEotfSt2084(m, 1.0);
    float sPQ = GT7InverseEotfSt2084(s, 1.0);

    return float3(
        (2048.0 * lPQ + 2048.0 * mPQ) / 4096.0,
        (6610.0 * lPQ - 13613.0 * mPQ + 7003.0 * sPQ) / 4096.0,
        (17933.0 * lPQ - 17390.0 * mPQ - 543.0 * sPQ) / 4096.0);
}

float3 GT7ICtCpToRgb(float3 ictCp)
{
    float l = ictCp.x + 0.00860904 * ictCp.y + 0.11103 * ictCp.z;
    float m = ictCp.x - 0.00860904 * ictCp.y - 0.11103 * ictCp.z;
    float s = ictCp.x + 0.560031 * ictCp.y - 0.320627 * ictCp.z;

    float lLin = GT7EotfSt2084(l, 1.0);
    float mLin = GT7EotfSt2084(m, 1.0);
    float sLin = GT7EotfSt2084(s, 1.0);

    return float3(
        max(3.43661 * lLin - 2.50645 * mLin + 0.0698454 * sLin, 0.0),
        max(-0.79133 * lLin + 1.9836 * mLin - 0.192271 * sLin, 0.0),
        max(-0.0259499 * lLin - 0.0989137 * mLin + 1.12486 * sLin, 0.0));
}

float3 GT7RgbToUcs(float3 rgb)
{
    return GT7RgbToICtCp(rgb);
}

float3 GT7UcsToRgb(float3 ucs)
{
    return GT7ICtCpToRgb(ucs);
}

float3 GT7Rec709ToRec2020(float3 c)
{
    return mul(float3x3(
        0.6274040, 0.3292820, 0.0433136,
        0.0690970, 0.9195400, 0.0113612,
        0.0163916, 0.0880132, 0.8955950), c);
}

float3 GT7Rec2020ToRec709(float3 c)
{
    return mul(float3x3(
        1.6604910, -0.5876411, -0.0728499,
        -0.1245505, 1.1328999, -0.0083494,
        -0.0181508, -0.1005789, 1.1187297), c);
}

// Canonical GT7 operator core.
// Input:  linear Rec.2020 RGB in GT frame-buffer values.
// Output: tone-mapped Rec.2020 RGB in GT frame-buffer values.
float3 GT7TonemapRec2020(float3 rgb, float physicalTargetLuminance, float sdrCorrectionFactor)
{
    float framebufferLuminanceTarget = GT7PhysicalToFramebuffer(physicalTargetLuminance);

    float3 ucs = GT7RgbToUcs(rgb);

    // Per-channel tone mapping ("skewed" color).
    float3 skewedRgb = float3(
        GT7EvaluateCurve(rgb.r, framebufferLuminanceTarget),
        GT7EvaluateCurve(rgb.g, framebufferLuminanceTarget),
        GT7EvaluateCurve(rgb.b, framebufferLuminanceTarget));

    float3 skewedUcs = GT7RgbToUcs(skewedRgb);

    float framebufferLuminanceTargetUcs = GT7RgbToUcs(framebufferLuminanceTarget.xxx).x;
    float chromaScale = GT7ChromaCurve(ucs.x / framebufferLuminanceTargetUcs, GT7_FADE_START, GT7_FADE_END);

    float3 scaledUcs = float3(
        skewedUcs.x,          // Luminance from skewed color
        ucs.y * chromaScale,  // Scaled chroma components
        ucs.z * chromaScale);

    // Convert back to RGB.
    float3 scaledRgb = GT7UcsToRgb(scaledUcs);

    // Final blend between per-channel and UCS-scaled results.
    float3 blended = (1.0 - GT7_BLEND_RATIO) * skewedRgb + GT7_BLEND_RATIO * scaledRgb;
    return sdrCorrectionFactor * min(blended, framebufferLuminanceTarget.xxx);
}

// Unity integration wrapper: URP LDR tonemapping reaches this point in linear Rec.709.
float3 GT7TonemapSDR(float3 inputRec709)
{
    float3 rec2020 = GT7Rec709ToRec2020(inputRec709);
    float3 mappedRec2020 = GT7TonemapRec2020(
        rec2020,
        GT7_SDR_PAPER_WHITE,
        1.0 / GT7PhysicalToFramebuffer(GT7_SDR_PAPER_WHITE));

    return max(GT7Rec2020ToRec709(mappedRec2020), 0.0);
}

half3 ApplyTonemap(half3 input)
{
#if _TONEMAP_ACES
    float3 aces = unity_to_ACES(input);
    input = AcesTonemap(aces);
#elif _TONEMAP_ACES_FILMIC
    input = ACESFilmicTonemapSRGB(input);
#elif _TONEMAP_NEUTRAL
    input = NeutralTonemap(input);
#elif _TONEMAP_GTSPORT
    input = GTSportTonemap(input);
#elif _TONEMAP_GT7
    input = GT7TonemapSDR(input);
#endif

    return saturate(input);
}

half3 ApplyColorGrading(half3 input, TEXTURE3D_PARAM(lutTex, lutSampler), float2 lutParams,
    TEXTURE2D_PARAM(userLutTex, userLutSampler), float3 userLutParams, float userLutContrib,
    float paperWhite, float oneOverPaperWhite)
{
#if _HDR_GRADING
    {
        float3 inputLutSpace = saturate(LinearToLogC(input));
        input = ApplyLut3D(TEXTURE3D_ARGS(lutTex, lutSampler), inputLutSpace, lutParams);

        UNITY_BRANCH
        if (userLutContrib > 0.0)
        {
        #ifdef HDR_INPUT
            input = RotateOutputSpaceToRec709(input * oneOverPaperWhite);
        #endif

            input = saturate(input);
            input.rgb = GetLinearToSRGB(input.rgb);
            half3 outLut = ApplyLut2D(TEXTURE2D_ARGS(userLutTex, userLutSampler), input, userLutParams);
            input = lerp(input, outLut, userLutContrib);
            input.rgb = GetSRGBToLinear(input.rgb);

        #ifdef HDR_INPUT
            input = RotateRec709ToOutputSpace(input) * paperWhite;
        #endif
        }
    }
#else
    {
        input = ApplyTonemap(input);

        UNITY_BRANCH
        if (userLutContrib > 0.0)
        {
            input.rgb = GetLinearToSRGB(input.rgb);
            half3 outLut = ApplyLut2D(TEXTURE2D_ARGS(userLutTex, userLutSampler), input, userLutParams);
            input = lerp(input, outLut, userLutContrib);
            input.rgb = GetSRGBToLinear(input.rgb);
        }

        input = ApplyLut3D(TEXTURE3D_ARGS(lutTex, lutSampler), input, lutParams);
    }
#endif

    return input;
}

half3 ApplyGrain(half3 input, float2 uv, TEXTURE2D_PARAM(GrainTexture, GrainSampler), float intensity, float response, float2 scale, float2 offset, float oneOverPaperWhite)
{
    // Grain in range [0;1] with neutral at 0.5
    half grain = SAMPLE_TEXTURE2D(GrainTexture, GrainSampler, uv * scale + offset).w;

    // Remap [-1;1]
    grain = (grain - 0.5) * 2.0;

    // Noisiness response curve based on scene luminance
    float lum = Luminance(input);
    #ifdef HDR_INPUT
    lum *= oneOverPaperWhite;
    #endif
    lum = 1.0 - sqrt(lum);
    lum = lerp(1.0, lum, response);

    return input + input * grain * intensity * lum;
}

half3 ApplyDithering(half3 input, float2 uv, TEXTURE2D_PARAM(BlueNoiseTexture, BlueNoiseSampler), float2 scale, float2 offset, float paperWhite, float oneOverPaperWhite)
{
    // Symmetric triangular distribution on [-1,1] with maximal density at 0
    float noise = SAMPLE_TEXTURE2D(BlueNoiseTexture, BlueNoiseSampler, uv * scale + offset).a * 2.0 - 1.0;
    noise = FastSign(noise) * (1.0 - sqrt(1.0 - abs(noise)));

#if UNITY_COLORSPACE_GAMMA
    input += noise / 255.0;
#elif defined(HDR_INPUT)
    input = input * oneOverPaperWhite;
    // Do not call GetSRGBToLinear/GetLinearToSRGB because the "fast" version will clamp values!
    input = SRGBToLinear(LinearToSRGB(input) + noise / 255.0);
    input = input * paperWhite;
#else
    input = GetSRGBToLinear(GetLinearToSRGB(input) + noise /255.0);
#endif

    return input;
}

#if _FXAA
static const FxaaFloat kSubpixelBlendAmount = 0.65;
static const FxaaFloat kRelativeContrastThreshold = 0.15;
static const FxaaFloat kAbsoluteContrastThreshold = 0.03;
#endif

half3 ApplyFXAA(half3 color, float2 positionNDC, int2 positionSS, float4 sourceSize, TEXTURE2D_X(inputTexture), float paperWhite, float oneOverPaperWhite)
{
#if _FXAA
    FxaaTex tex = {sampler_LinearClamp, _BlitTexture};
    FxaaFloat4 kUnusedFloat4 = FxaaFloat4(0, 0, 0, 0);

    FxaaFloat4 fxaaConsolePos = 0;
    FxaaFloat4 kFxaaConsoleRcpFrameOpt = 0;
    FxaaFloat4 kFxaaConsoleRcpFrameOpt2 = 0;
    FxaaFloat kFxaaConsoleEdgeSharpness = 0;
    FxaaFloat kFxaaConsoleEdgeThreshold = 0;
    FxaaFloat kFxaaConsoleEdgeThresholdMin = 0;
    FxaaFloat2 fxaaHDROutputPaperWhiteNits = 0;

#if FXAA_PC_CONSOLE == 1
    fxaaConsolePos = FxaaFloat4(positionNDC.xy - 0.5*sourceSize.zw, positionNDC.xy + 0.5*sourceSize.zw);
    kFxaaConsoleRcpFrameOpt = 0.5*FxaaFloat4(sourceSize.zw, -sourceSize.zw);
    kFxaaConsoleRcpFrameOpt2 = 2.0*FxaaFloat4(-sourceSize.zw, sourceSize.zw);
    kFxaaConsoleEdgeSharpness = 8.0;
    kFxaaConsoleEdgeThreshold = 0.125;
    kFxaaConsoleEdgeThresholdMin = 0.05;
#endif
    fxaaHDROutputPaperWhiteNits = FxaaFloat2(paperWhite, oneOverPaperWhite);

    return FxaaPixelShader(
        positionNDC,
        FxaaFloat4(color, 0),
        fxaaConsolePos,
        tex,
        tex,
        tex,
        sourceSize.zw,
        kFxaaConsoleRcpFrameOpt,
        kFxaaConsoleRcpFrameOpt2,
        kUnusedFloat4,
        kSubpixelBlendAmount,
        kRelativeContrastThreshold,
        kAbsoluteContrastThreshold,
        kFxaaConsoleEdgeSharpness,
        kFxaaConsoleEdgeThreshold,
        kFxaaConsoleEdgeThresholdMin,
        kUnusedFloat4,
        fxaaHDROutputPaperWhiteNits
    ).rgb;
#else
    return color;
#endif
}

#endif // UNIVERSAL_POSTPROCESSING_COMMON_INCLUDED
