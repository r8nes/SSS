#ifndef VOLUMETRIC_CLOUDS_BILATERAL_UPSCALE_INCLUDED
#define VOLUMETRIC_CLOUDS_BILATERAL_UPSCALE_INCLUDED

#define UPSAMPLE_TOLERANCE 1e-5f
#define NOISE_FILTER_STRENGTH 0.99999999f

static const float kDistanceBasedWeights3x3[36] =
{
    0.324652f, 0.535261f, 0.119433f, 0.535261f, 0.882497f, 0.196912f, 0.119433f, 0.196912f, 0.0439369f,
    0.119433f, 0.535261f, 0.324652f, 0.196912f, 0.882497f, 0.535261f, 0.0439369f, 0.196912f, 0.119433f,
    0.119433f, 0.196912f, 0.0439369f, 0.535261f, 0.882497f, 0.196912f, 0.324652f, 0.535261f, 0.119433f,
    0.0439369f, 0.196912f, 0.119433f, 0.196912f, 0.882497f, 0.535261f, 0.119433f, 0.535261f, 0.324652f
};

struct NeighborhoodUpsampleData3x3
{
    float4 lowDepthA;
    float4 lowDepthB;
    float lowDepthC;
    float4 lowDepthValueA;
    float4 lowDepthValueB;
    float lowDepthValueC;
    float4 lowValue0;
    float4 lowValue1;
    float4 lowValue2;
    float4 lowValue3;
    float4 lowValue4;
    float4 lowValue5;
    float4 lowValue6;
    float4 lowValue7;
    float4 lowValue8;
    float4 lowWeightA;
    float4 lowWeightB;
    float lowWeightC;
};

bool IsFarDepth(float depth)
{
    return depth == UNITY_RAW_FAR_CLIP_VALUE;
}

void ApplySkyNeighborhoodMask(inout NeighborhoodUpsampleData3x3 data, bool isSky)
{
    data.lowWeightA.x *= IsFarDepth(data.lowDepthA.x) == isSky ? 1.0f : 0.0f;
    data.lowWeightA.y *= IsFarDepth(data.lowDepthA.y) == isSky ? 1.0f : 0.0f;
    data.lowWeightA.z *= IsFarDepth(data.lowDepthA.z) == isSky ? 1.0f : 0.0f;
    data.lowWeightA.w *= IsFarDepth(data.lowDepthA.w) == isSky ? 1.0f : 0.0f;
    data.lowWeightB.x *= IsFarDepth(data.lowDepthB.x) == isSky ? 1.0f : 0.0f;
    data.lowWeightB.y *= IsFarDepth(data.lowDepthB.y) == isSky ? 1.0f : 0.0f;
    data.lowWeightB.z *= IsFarDepth(data.lowDepthB.z) == isSky ? 1.0f : 0.0f;
    data.lowWeightB.w *= IsFarDepth(data.lowDepthB.w) == isSky ? 1.0f : 0.0f;
    data.lowWeightC *= IsFarDepth(data.lowDepthC) == isSky ? 1.0f : 0.0f;
}

void BilateralUpscale3x3(
    float highDepth,
    in NeighborhoodUpsampleData3x3 data,
    out float4 outColor,
    out float outDepth)
{
    float4 combinedWeightsA =
        data.lowWeightA / (abs(highDepth - data.lowDepthA) + UPSAMPLE_TOLERANCE);
    float4 combinedWeightsB =
        data.lowWeightB / (abs(highDepth - data.lowDepthB) + UPSAMPLE_TOLERANCE);
    float combinedWeightC =
        data.lowWeightC / (abs(highDepth - data.lowDepthC) + UPSAMPLE_TOLERANCE);

    float totalWeight =
        combinedWeightsA.x + combinedWeightsA.y + combinedWeightsA.z + combinedWeightsA.w
        + combinedWeightsB.x + combinedWeightsB.y + combinedWeightsB.z + combinedWeightsB.w
        + combinedWeightC;

    float4 weightedSum =
        data.lowValue0 * combinedWeightsA.x
        + data.lowValue1 * combinedWeightsA.y
        + data.lowValue2 * combinedWeightsA.z
        + data.lowValue3 * combinedWeightsA.w
        + data.lowValue4 * combinedWeightsB.x
        + data.lowValue5 * combinedWeightsB.y
        + data.lowValue6 * combinedWeightsB.z
        + data.lowValue7 * combinedWeightsB.w
        + data.lowValue8 * combinedWeightC
        + float4(NOISE_FILTER_STRENGTH.xxx, 0.0f);

    float weightedDepth =
        dot(data.lowDepthValueA, combinedWeightsA)
        + dot(data.lowDepthValueB, combinedWeightsB)
        + data.lowDepthValueC * combinedWeightC
        + NOISE_FILTER_STRENGTH;

    if (totalWeight == 0.0f)
    {
        outColor = float4(0.0f, 0.0f, 0.0f, 1.0f);
        outDepth = UNITY_NEAR_CLIP_VALUE;
    }
    else
    {
        outColor = weightedSum / (totalWeight + NOISE_FILTER_STRENGTH);
        outDepth = weightedDepth / (totalWeight + NOISE_FILTER_STRENGTH);
    }
}

#endif
