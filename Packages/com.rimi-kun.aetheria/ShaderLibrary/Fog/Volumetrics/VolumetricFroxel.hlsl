#ifndef VOLUMETRIC_FROXEL_INCLUDED
#define VOLUMETRIC_FROXEL_INCLUDED
#include "../../../Runtime/Components/Volumetrics/VolumeMediaParameters.cs.hlsl"

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/GeometricTools.hlsl"
#include "../../Common/PhaseFunctions.hlsl"
#include "VolumetricTextureAtlas.hlsl"
#include "VolumetricContextGlobals.hlsl"

#ifndef FROXEL_TRANSMITTANCE_EPSILON
#define FROXEL_TRANSMITTANCE_EPSILON 1.0e-3
#endif

#if !defined(LOCAL_MEDIA_SHAPE)
Texture3D _VolumetricVolumeMaskAtlas;
Texture3D _VolumetricVolumeMaskAtlas1;
Texture3D _VolumetricVolumeMaskAtlas2;
Texture3D _VolumetricVolumeMaskAtlas3;
Texture3D _VolumetricFogMaskAtlas;
Texture3D _VolumetricFogMaskAtlas1;
Texture3D _VolumetricFogMaskAtlas2;
Texture3D _VolumetricFogMaskAtlas3;
#endif

struct VolumetricVolume
{
    float4 worldToLocal0;
    float4 worldToLocal1;
    float4 worldToLocal2;
    float4 scatteringExtinction;
    float4 textureTilingInvertFade;
    float4 textureScrollRcpDistFade;
    float4 rcpPosFaceFadeEnd;
    float4 rcpNegFaceFadeBlend;
    float4 sizeFalloff;
    float4 maskShaping;
    float4 maskNearClampSampling;
};

StructuredBuffer<VolumetricVolume> _VolumetricVolumeBuffer;

struct VolumetricFogMaskShape
{
    float4 worldToLocal0;
    float4 worldToLocal1;
    float4 worldToLocal2;
    float4 positiveFadeStrength;
    float4 negativeFadeFalloff;
    float4 maskShaping;
    float4 distanceNearClamp;
    float4 halfSizeSampling;
    float4 shapeParams;
};

StructuredBuffer<VolumetricFogMaskShape> _VolumetricFogMaskBuffer;
float2 GetFroxelUv(uint2 xy, float2 offset)
{
    float2 gridXY = max((float2)_VolumetricGridParams.xy, float2(1.0, 1.0));
    return (float2(xy) + 0.5 + offset) / gridXY;
}

float3 GetWorldRayDirectionFromFroxel(uint2 xy)
{
    float3 ray =
        _VolumetricViewDirOriginWS.xyz
        + _VolumetricViewDirStepXWS.xyz * xy.x
        + _VolumetricViewDirStepYWS.xyz * xy.y;
    return ray * rsqrt(max(dot(ray, ray), 1.0e-20f));
}

float3 TransformWorldToVolumeLocal(float3 worldPos, VolumetricVolume volume)
{
    return float3(
        dot(volume.worldToLocal0, float4(worldPos, 1.0)),
        dot(volume.worldToLocal1, float4(worldPos, 1.0)),
        dot(volume.worldToLocal2, float4(worldPos, 1.0)));
}

float3 TransformWorldVectorToVolumeLocal(float3 worldDir, VolumetricVolume volume)
{
    return float3(
        dot(volume.worldToLocal0.xyz, worldDir),
        dot(volume.worldToLocal1.xyz, worldDir),
        dot(volume.worldToLocal2.xyz, worldDir));
}

float3 TransformWorldToFogMaskLocal(float3 worldPos, VolumetricFogMaskShape mask)
{
    return float3(
        dot(mask.worldToLocal0, float4(worldPos, 1.0)),
        dot(mask.worldToLocal1, float4(worldPos, 1.0)),
        dot(mask.worldToLocal2, float4(worldPos, 1.0)));
}

float3 TransformWorldVectorToFogMaskLocal(float3 worldDir, VolumetricFogMaskShape mask)
{
    return float3(
        dot(mask.worldToLocal0.xyz, worldDir),
        dot(mask.worldToLocal1.xyz, worldDir),
        dot(mask.worldToLocal2.xyz, worldDir));
}


#if !defined(LOCAL_MEDIA_SHAPE)
VOLUMETRIC_DEFINE_ATLAS_PARAMS_SELECTOR(
    GetVolumeMaskAtlasParams,
    _VolumetricVolumeMaskAtlasParams,
    _VolumetricVolumeMaskAtlasParams1,
    _VolumetricVolumeMaskAtlasParams2,
    _VolumetricVolumeMaskAtlasParams3)

VOLUMETRIC_DEFINE_ATLAS_PARAMS_SELECTOR(
    GetFogMaskAtlasParams,
    _VolumetricFogMaskAtlasParams,
    _VolumetricFogMaskAtlasParams1,
    _VolumetricFogMaskAtlasParams2,
    _VolumetricFogMaskAtlasParams3)

VOLUMETRIC_DEFINE_TEXTURE3D_ATLAS_SAMPLER(
    SampleVolumeMaskAtlasByGroup,
    _VolumetricVolumeMaskAtlas,
    _VolumetricVolumeMaskAtlas1,
    _VolumetricVolumeMaskAtlas2,
    _VolumetricVolumeMaskAtlas3,
    sampler_LinearRepeat)

VOLUMETRIC_DEFINE_TEXTURE3D_ATLAS_SAMPLER(
    SampleFogMaskAtlasByGroup,
    _VolumetricFogMaskAtlas,
    _VolumetricFogMaskAtlas1,
    _VolumetricFogMaskAtlas2,
    _VolumetricFogMaskAtlas3,
    sampler_LinearClamp)

#endif

float SampleFogZoneVolumeMask(float3 localPos, VolumetricVolume volume)
{
    float result = 1.0;
#if !defined(LOCAL_MEDIA_SHAPE)
    if (volume.maskNearClampSampling.x >= -0.5)
    {
        float3 uvw = localPos * 0.5 + 0.5;
        uvw = uvw * max(
            volume.textureTilingInvertFade.xyz,
            float3(1.0e-5, 1.0e-5, 1.0e-5)) +
            volume.textureScrollRcpDistFade.xyz;
        uvw = frac(uvw);

        int atlasGroup = 0;
        float atlasSlice = 0.0;
        DecodeVolumetricPackedAtlasIndex(
            volume.maskNearClampSampling.x, atlasGroup, atlasSlice);
        float atlasZ = ComputeVolumetricTexture3DAtlasZ(
            frac(uvw.z),
            atlasSlice,
            GetVolumeMaskAtlasParams(atlasGroup));
        float rawMask = SampleVolumeMaskAtlasByGroup(
            atlasGroup, float3(uvw.xy, atlasZ));
        result = ShapeVolumetricTextureMask(
            rawMask,
            volume.maskShaping.x,
            volume.maskShaping.y,
            volume.maskShaping.z,
            volume.maskShaping.w);
    }
#endif
    return result;
}

float EvaluateLocalFogNearClampAtDistance(
    float distanceToCamera,
    VolumetricVolume volume)
{
    float result = 1.0;
    if (volume.maskNearClampSampling.z > 0.0)
    {
        result = saturate(
            (distanceToCamera - max(volume.maskNearClampSampling.y, 0.0)) *
            max(volume.maskNearClampSampling.z, 1.0e-5));
    }
    return result;
}

float VolumeMaskLocal(
    float3 localPos,
    float distanceToCamera,
    VolumetricVolume volume)
{
    float mask = 0.0;
    float3 absLocal = abs(localPos);
    if (!any(absLocal > 1.0))
    {
        float3 halfSize = max(
            volume.sizeFalloff.xyz,
            float3(0.0005, 0.0005, 0.0005));
        int falloffMode = (int)round(volume.sizeFalloff.w);

        mask = EvaluateVolumetricBoxFaceFade(
            localPos,
            halfSize,
            volume.rcpPosFaceFadeEnd.xyz,
            volume.rcpNegFaceFadeBlend.xyz,
            falloffMode);

        if (volume.textureTilingInvertFade.w > 0.5)
            mask = 1.0 - mask;

        mask *= SampleFogZoneVolumeMask(localPos, volume);
        mask *= EvaluateLocalFogNearClampAtDistance(distanceToCamera, volume);
        float distanceFade = saturate(
            volume.rcpPosFaceFadeEnd.w -
            distanceToCamera * volume.textureScrollRcpDistFade.w);
        mask *= distanceFade;
    }
    return saturate(mask);
}

float EvaluateCapsuleRadius(float3 localPos, VolumetricFogMaskShape mask)
{
    int axis = clamp((int)round(mask.shapeParams.y), 0, 2);
    float axial = localPos.z;
    float2 radial = localPos.xy;
    if (axis == 0)
    {
        axial = localPos.x;
        radial = localPos.yz;
    }
    else if (axis == 1)
    {
        axial = localPos.y;
        radial = localPos.xz;
    }

    float capDistance = max(abs(axial) - mask.shapeParams.z, 0.0) *
        max(mask.shapeParams.w, 1.0);
    return sqrt(dot(radial, radial) + capDistance * capDistance);
}

float SampleVolumetricFogMask(
    float3 localPos,
    VolumetricFogMaskShape mask)
{
    float result = 0.0;
    if (!any(abs(localPos) > 1.0))
    {
        int shapeType = (int)round(mask.shapeParams.x);
        if (shapeType == 1)
        {
            result = 1.0;
        }
        else if (shapeType == 2)
        {
            result = length(localPos) <= 1.0 ? 1.0 : 0.0;
        }
        else if (shapeType == 3)
        {
            result = EvaluateCapsuleRadius(localPos, mask) <= 1.0 ? 1.0 : 0.0;
        }
#if !defined(LOCAL_MEDIA_SHAPE)
        else
        {
            float3 uvw = localPos * 0.5 + 0.5;
            int atlasGroup = 0;
            float atlasSlice = 0.0;
            DecodeVolumetricPackedAtlasIndex(
                mask.maskShaping.w, atlasGroup, atlasSlice);
            float atlasZ = ComputeVolumetricTexture3DAtlasZ(
                uvw.z,
                atlasSlice,
                GetFogMaskAtlasParams(atlasGroup));
            result = saturate(SampleFogMaskAtlasByGroup(
                atlasGroup, float3(uvw.xy, atlasZ)));
        }
#endif
    }
    return result;
}

float GetVolumetricFogMaskSymmetricInverseFade(VolumetricFogMaskShape mask)
{
    float positiveInv = min(
        mask.positiveFadeStrength.x,
        min(mask.positiveFadeStrength.y, mask.positiveFadeStrength.z));
    float negativeInv = min(
        mask.negativeFadeFalloff.x,
        min(mask.negativeFadeFalloff.y, mask.negativeFadeFalloff.z));
    return min(positiveInv, negativeInv);
}

float EvaluateSphereBoundaryFade(float3 localPos, VolumetricFogMaskShape mask, int falloffMode)
{
    float radius = length(localPos);
    float fade = 0.0;
    if (radius < 1.0)
    {
        // localPos is dimensionless because the collider transform already contains
        // the sphere radius and hierarchy scale. Convert the implicit sphere
        // distance back to world units before applying the user fade distance.
        float3 localNormal = radius > 1.0e-6
            ? localPos * rcp(radius)
            : float3(1.0, 0.0, 0.0);
        float3 worldGradient =
            mask.worldToLocal0.xyz * localNormal.x +
            mask.worldToLocal1.xyz * localNormal.y +
            mask.worldToLocal2.xyz * localNormal.z;
        float distanceToSurfaceWS =
            (1.0 - radius) * rcp(max(length(worldGradient), 1.0e-6));

        fade = EvaluateVolumetricFaceFade(
            distanceToSurfaceWS,
            GetVolumetricFogMaskSymmetricInverseFade(mask),
            falloffMode);
    }
    return fade;
}

float EvaluateCapsuleBoundaryFade(float3 localPos, VolumetricFogMaskShape mask, int falloffMode)
{
    float capsuleRadius = EvaluateCapsuleRadius(localPos, mask);
    float fade = 0.0;
    if (capsuleRadius < 1.0)
    {
        float3 halfSize = max(
            mask.halfSizeSampling.xyz,
            float3(0.0005, 0.0005, 0.0005));
        float radialScaleWS = min(halfSize.x, min(halfSize.y, halfSize.z));
        float distanceToSurfaceWS = (1.0 - capsuleRadius) * radialScaleWS;

        fade = EvaluateVolumetricFaceFade(
            distanceToSurfaceWS,
            GetVolumetricFogMaskSymmetricInverseFade(mask),
            falloffMode);
    }
    return fade;
}

float EvaluateVolumetricFogMaskEdgeFade(float3 localPos, VolumetricFogMaskShape mask)
{
    float3 halfSize = max(mask.halfSizeSampling.xyz, float3(0.0005, 0.0005, 0.0005));
    int falloffMode = (int)round(mask.negativeFadeFalloff.w);
    int shapeType = (int)round(mask.shapeParams.x);

    float fade;
    if (shapeType == 2)
    {
        fade = EvaluateSphereBoundaryFade(localPos, mask, falloffMode);
    }
    else if (shapeType == 3)
    {
        fade = EvaluateCapsuleBoundaryFade(localPos, mask, falloffMode);
    }
    else
    {
        fade = EvaluateVolumetricBoxFaceFade(
            localPos,
            halfSize,
            mask.positiveFadeStrength.xyz,
            mask.negativeFadeFalloff.xyz,
            falloffMode);
    }
    return fade;
}

float EvaluateVolumetricFogMaskNearClamp(
    float distanceToCamera,
    VolumetricFogMaskShape mask)
{
    float result = 1.0;
    if (mask.distanceNearClamp.w > 0.0)
    {
        result = saturate(
            (distanceToCamera - max(mask.distanceNearClamp.z, 0.0)) *
            max(mask.distanceNearClamp.w, 1.0e-5));
    }
    return result;
}

float EvaluateVolumetricFogMaskDistanceFade(float distanceToCamera, VolumetricFogMaskShape mask)
{
    return saturate(mask.distanceNearClamp.y - distanceToCamera * mask.distanceNearClamp.x);
}

float EvaluateVolumetricFogMaskAtDistance(float3 localPos, float distanceToCamera, VolumetricFogMaskShape mask)
{
    float coverage = SampleVolumetricFogMask(localPos, mask);
    coverage = ShapeVolumetricTextureMask(
        coverage,
        mask.maskShaping.x,
        mask.maskShaping.y,
        mask.maskShaping.z,
        mask.positiveFadeStrength.w);
    coverage *= EvaluateVolumetricFogMaskEdgeFade(localPos, mask);
    coverage *= EvaluateVolumetricFogMaskNearClamp(distanceToCamera, mask);
    coverage *= EvaluateVolumetricFogMaskDistanceFade(distanceToCamera, mask);
    return saturate(coverage);
}

void ApplyLocalMediumBlend(
    inout float3 scattering,
    inout float extinction,
    float3 sourceScattering,
    float sourceExtinction,
    int blendMode)
{
    if (blendMode == LOCALMEDIUMBLENDMODE_OVERWRITE)
    {
        scattering = sourceScattering;
        extinction = sourceExtinction;
    }
    else if (blendMode == LOCALMEDIUMBLENDMODE_MULTIPLY)
    {
        scattering *= sourceScattering;
        extinction *= sourceExtinction;
    }
    else if (blendMode == LOCALMEDIUMBLENDMODE_MIN)
    {
        scattering = min(scattering, sourceScattering);
        extinction = min(extinction, sourceExtinction);
    }
    else if (blendMode == LOCALMEDIUMBLENDMODE_MAX)
    {
        scattering = max(scattering, sourceScattering);
        extinction = max(extinction, sourceExtinction);
    }
    else if (blendMode == LOCALMEDIUMBLENDMODE_ADDITIVE)
    {
        scattering += sourceScattering;
        extinction += sourceExtinction;
    }
}

#endif
