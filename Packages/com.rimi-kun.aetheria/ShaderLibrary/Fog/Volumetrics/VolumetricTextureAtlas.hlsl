#ifndef VOLUMETRIC_TEXTURE_ATLAS_INCLUDED
#define VOLUMETRIC_TEXTURE_ATLAS_INCLUDED

#include "../../../Runtime/Data/ShaderConfigs/VolumetricShaderConfig.cs.hlsl"
#include "../../../Runtime/Components/Volumetrics/VolumeMediaParameters.cs.hlsl"

#define VOLUMETRIC_ATLAS_GROUP_COUNT VOLUMETRICSHADERCONFIG_ATLAS_GROUP_COUNT
#define VOLUMETRIC_ATLAS_SLICES_PER_GROUP VOLUMETRICSHADERCONFIG_ATLAS_SLICES_PER_GROUP

void DecodeVolumetricPackedAtlasIndex(float packedIndex, out int groupIndex, out float sliceIndex)
{
    uint packed = (uint)max((int)round(packedIndex), 0);
    groupIndex = (int)min(
        packed / VOLUMETRIC_ATLAS_SLICES_PER_GROUP,
        VOLUMETRIC_ATLAS_GROUP_COUNT - 1u);
    sliceIndex = (float)(packed % VOLUMETRIC_ATLAS_SLICES_PER_GROUP);
}

float ComputeVolumetricTexture3DAtlasZ(float localZ, float sliceIndex, float4 atlasParams)
{
    float atlasDepth = max(atlasParams.x, 1.0);
    float sourceDepth = max(atlasParams.y, 1.0);
    float invAtlasDepth = max(atlasParams.z, rcp(atlasDepth));
    float sliceCapacity = max(atlasParams.w, 1.0);
    float slice = clamp(sliceIndex, 0.0, sliceCapacity - 1.0);
    float zTexel = slice * sourceDepth + (saturate(localZ) * (sourceDepth - 1.0) + 0.5);
    return zTexel * invAtlasDepth;
}

float ShapeVolumetricTextureMask(
    float rawMask,
    float minimum,
    float inverseRange,
    float power,
    float strength)
{
    float shaped = saturate((saturate(rawMask) - minimum) * max(inverseRange, 1.0e-4));
    shaped = pow(max(shaped, 0.0), max(power, 0.01));
    return saturate(shaped * max(strength, 0.0));
}

float EvaluateVolumetricFaceFade(float distanceToFace, float inverseFadeDistance, int falloffMode)
{
    float fade = max(distanceToFace, 0.0) * max(inverseFadeDistance, 0.0);
    return falloffMode == VOLUMEFALLOFFMODE_EXPONENTIAL
        ? saturate(1.0 - exp(-fade))
        : saturate(fade);
}

float EvaluateVolumetricBoxFaceFade(
    float3 localPosition,
    float3 halfSize,
    float3 positiveInverseFade,
    float3 negativeInverseFade,
    int falloffMode)
{
    float3 positiveDistance = (1.0 - localPosition) * halfSize;
    float3 negativeDistance = (localPosition + 1.0) * halfSize;
    float3 positiveFade = float3(
        EvaluateVolumetricFaceFade(positiveDistance.x, positiveInverseFade.x, falloffMode),
        EvaluateVolumetricFaceFade(positiveDistance.y, positiveInverseFade.y, falloffMode),
        EvaluateVolumetricFaceFade(positiveDistance.z, positiveInverseFade.z, falloffMode));
    float3 negativeFade = float3(
        EvaluateVolumetricFaceFade(negativeDistance.x, negativeInverseFade.x, falloffMode),
        EvaluateVolumetricFaceFade(negativeDistance.y, negativeInverseFade.y, falloffMode),
        EvaluateVolumetricFaceFade(negativeDistance.z, negativeInverseFade.z, falloffMode));
    return min(
        min(min(positiveFade.x, positiveFade.y), positiveFade.z),
        min(min(negativeFade.x, negativeFade.y), negativeFade.z));
}


#define VOLUMETRIC_DEFINE_ATLAS_PARAMS_SELECTOR(FUNCTION_NAME, PARAMS_0, PARAMS_1, PARAMS_2, PARAMS_3) \
float4 FUNCTION_NAME(int groupIndex) \
{ \
    float4 result = PARAMS_0; \
    if (groupIndex == 1) result = PARAMS_1; \
    else if (groupIndex == 2) result = PARAMS_2; \
    else if (groupIndex == 3) result = PARAMS_3; \
    return result; \
}

#define VOLUMETRIC_DEFINE_TEXTURE3D_ATLAS_SAMPLER(FUNCTION_NAME, TEXTURE_0, TEXTURE_1, TEXTURE_2, TEXTURE_3, SAMPLER_NAME) \
float FUNCTION_NAME(int groupIndex, float3 uvw) \
{ \
    float result = 0.0; \
    if (groupIndex == 1) \
        result = TEXTURE_1.SampleLevel(SAMPLER_NAME, uvw, 0.0).r; \
    else if (groupIndex == 2) \
        result = TEXTURE_2.SampleLevel(SAMPLER_NAME, uvw, 0.0).r; \
    else if (groupIndex == 3) \
        result = TEXTURE_3.SampleLevel(SAMPLER_NAME, uvw, 0.0).r; \
    else \
        result = TEXTURE_0.SampleLevel(SAMPLER_NAME, uvw, 0.0).r; \
    return result; \
}


#endif
