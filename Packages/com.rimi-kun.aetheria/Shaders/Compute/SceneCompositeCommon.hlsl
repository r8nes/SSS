#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#if defined(COMPOSITE_VOLUMETRIC_CLOUDS)
#include "../../ShaderLibrary/Clouds/VolumetricCloudsBilateralUpscale.hlsl"
#include "../../ShaderLibrary/Clouds/VolumetricCloudsTransmittance.hlsl"
#include "../../ShaderLibrary/Common/VolumetricDepth.hlsl"
#endif
#if defined(COMPOSITE_CLOUD_LAYER)
#define CLOUD_LAYER_AMBIENT_PROBE_BUFFER 0
#include "../../ShaderLibrary/CloudLayer/CloudLayerCommon.hlsl"
#endif
#if defined(COMPOSITE_ATMOSPHERE)
#define SURFACE_MEDIUM_ATMOSPHERE 1
#endif
#if defined(COMPOSITE_FOG)
#define SURFACE_MEDIUM_FOG 1
#define SURFACE_MEDIUM_RUNTIME_FOG 1
#endif
#include "../../ShaderLibrary/Common/SurfaceMedium.hlsl"

#define COMPOSITE_THREAD_GROUP_SIZE 8
#define LDS_UPSCALE_WIDTH 6

Texture2D<float4> _SceneColorTexture;
Texture2D<float> _CameraDepthTexture;
RWTexture2D<float4> _CompositeOutputTexture;
#if defined(COMPOSITE_REFRACTION_SPLIT)
RWTexture2D<float4> _BeforeRefractionTexture;
#endif
float4 _CompositeOutputSize;
float _ImprovedTransmittanceBlend;
int _CompositePerceptualEnabled;
uint _CompositeFeatureFlags;

#define COMPOSITE_FEATURE_ATMOSPHERE 1u
#define COMPOSITE_FEATURE_CLOUD_LAYER 2u
#define COMPOSITE_FEATURE_CLOUDS 4u
#define COMPOSITE_FEATURE_FOG 8u
#define COMPOSITE_FEATURE_FOG_SKY 16u
#define COMPOSITE_FEATURE_VOLUMETRIC_FOG 32u
#define COMPOSITE_FEATURE_LIGHT_SHAFTS 64u

bool CompositeFeatureEnabled(uint feature)
{
    return (_CompositeFeatureFlags & feature) != 0u;
}

#if defined(COMPOSITE_LIGHT_SHAFTS)
Texture2D<float> _LightShaftsMask;
float3 _LightShaftsLightDirectionWS;
float3 _LightShaftsLightColor;
float4 _LightShaftsTint;
float _LightShaftsIntensity;
float _LightShaftsAnisotropy;
float _LightShaftsOcclusionMaskDarkness;
float _LightShaftsOcclusionDepthRange;
float _LightShaftsSunViewportPadding;

static const float LIGHT_SHAFTS_OPTICAL_DEPTH = 3.8508176698;

bool LightShaftsIsFarDepth(float rawDepth)
{
#if UNITY_REVERSED_Z
    return rawDepth <= 1.0e-6;
#else
    return rawDepth >= 1.0 - 1.0e-6;
#endif
}

float LightShaftsNormalizeRawDepth(float rawDepth, float depthRange)
{
    float safeDepthRange = max(depthRange, 1.0e-4);
    float sceneDepth = LightShaftsIsFarDepth(rawDepth)
        ? safeDepthRange
        : LinearEyeDepth(rawDepth, _ZBufferParams);
    return saturate(sceneDepth / safeDepthRange);
}

float2 LightShaftsProjectLightUv(float3 directionWS, out float visibility)
{
    float3 directionVS = mul((float3x3)UNITY_MATRIX_V, directionWS);
    float4 positionCS = mul(UNITY_MATRIX_P, float4(directionVS, 1.0));

    float2 uv = float2(0.5, 0.5);
    visibility = 0.0;
    if (positionCS.w > 1.0e-6)
    {
        float2 ndc = positionCS.xy / positionCS.w;
        uv = ndc * float2(0.5, 0.5 * _ProjectionParams.x) + 0.5;

        float offscreenDistance = max(
            max(-uv.x, uv.x - 1.0),
            max(-uv.y, uv.y - 1.0));

        if (offscreenDistance < _LightShaftsSunViewportPadding)
        {
            float fade = saturate(max(offscreenDistance, 0.0) / _LightShaftsSunViewportPadding);
            visibility = 1.0 - smoothstep(0.0, 1.0, fade);
        }
    }

    return uv;
}

float2 LightShaftsAspectCorrection()
{
    float2 screenSize = max(GetScaledScreenParams().xy, float2(1.0, 1.0));
    return float2(1.0, screenSize.y / screenSize.x);
}

float3 LightShaftsViewRayWS(float2 uv)
{
    float3 positionWS = ComputeWorldSpacePosition(uv, 0.5, UNITY_MATRIX_I_VP);
    return SafeNormalize(positionWS - GetCameraPositionWS());
}

float LightShaftsHenyeyGreenstein(float cosTheta, float anisotropy)
{
    float g = clamp(anisotropy, 0.0, 0.98);
    float oneMinusG = 1.0 - g;
    float denominator = max(1.0 + g * g - 2.0 * g * clamp(cosTheta, -1.0, 1.0), 1.0e-5);
    float ratio = saturate((oneMinusG * oneMinusG) / denominator);
    return ratio * sqrt(ratio);
}

float LightShaftsDepthScatteringResponse(float normalizedDepth)
{
    return 1.0 - exp(-LIGHT_SHAFTS_OPTICAL_DEPTH * saturate(normalizedDepth));
}

float3 EvaluateScreenSpaceLightShafts(float2 uv, float rawDepth)
{
    float blurredOcclusion = _LightShaftsMask.SampleLevel(sampler_LinearClamp, uv, 0.0).r;

    float finalOcclusion = lerp(
        saturate(_LightShaftsOcclusionMaskDarkness),
        1.0,
        blurredOcclusion * blurredOcclusion);

    float visibility;
    float2 sunUv = LightShaftsProjectLightUv(_LightShaftsLightDirectionWS, visibility);
    float2 aspectCorrection = LightShaftsAspectCorrection();
    float blurOriginDistanceMask = saturate(length((sunUv - uv) * aspectCorrection) * 0.2);
    finalOcclusion = lerp(finalOcclusion, 1.0, blurOriginDistanceMask);

    float3 viewRayWS = LightShaftsViewRayWS(uv);
    float cosTheta = dot(viewRayWS, _LightShaftsLightDirectionWS);
    float phase = LightShaftsHenyeyGreenstein(cosTheta, _LightShaftsAnisotropy);

    float normalizedDepth = LightShaftsNormalizeRawDepth(rawDepth, _LightShaftsOcclusionDepthRange);
    float depthResponse = LightShaftsDepthScatteringResponse(normalizedDepth);
    float scattering = phase * max(_LightShaftsIntensity, 0.0) * visibility * finalOcclusion * depthResponse;

    return _LightShaftsLightColor * _LightShaftsTint.rgb * scattering;
}
#endif

bool CompositeIsFarRawDepth(float rawDepth)
{
#if UNITY_REVERSED_Z
    return rawDepth <= 1.0e-6f;
#else
    return rawDepth >= 1.0f - 1.0e-6f;
#endif
}

#if defined(COMPOSITE_WATER_BOUNDARY)
bool CompositeHasWaterBoundary(uint2 pixelCoord)
{
    return (GetStencilValue(_WaterStencilTexture.Load(int3(pixelCoord, 0))) & 128u) != 0u;
}
#endif

#if defined(COMPOSITE_VOLUMETRIC_CLOUDS)
Texture2D<float3> _SourceLightingTexture;
Texture2D<float4> _SourceDataTexture;
Texture2D<float> _CloudGuideDepthTexture;
float4 _SourceSize;
float _NearPlane;
int _OutputToFullResolutionScale;

groupshared float4 gs_upscaleLighting[36];
groupshared float4 gs_upscaleData[36];

uint UpscaleLdsAddress(uint2 groupThreadId, int2 offset)
{
    uint2 tapAddress = (uint2)((int2)(groupThreadId / 2u + 1u) + offset);
    return clamp(
        (uint)(tapAddress.x) % LDS_UPSCALE_WIDTH + tapAddress.y * LDS_UPSCALE_WIDTH,
        0u,
        35u);
}

void PrefetchHistoryNeighborhoodLds(uint groupIndex, uint2 outputTileOrigin)
{
    uint2 ldsCoord = uint2(groupIndex % LDS_UPSCALE_WIDTH, groupIndex / LDS_UPSCALE_WIDTH);
    int2 sourceCoord = int2(outputTileOrigin / 2u) + int2(ldsCoord) - int2(1, 1);
    sourceCoord = clamp(sourceCoord, int2(0, 0), max(int2(_SourceSize.xy) - 1, 0));

    float4 sourceData = _SourceDataTexture[uint2(sourceCoord)];
    float3 sourceLighting = _SourceLightingTexture[uint2(sourceCoord)];
    float rawSceneDepth = sourceData.y;
    gs_upscaleLighting[groupIndex] = float4(sourceLighting, saturate(sourceData.w));
    gs_upscaleData[groupIndex] = float4(
        saturate(sourceData.x),
        Linear01Depth(rawSceneDepth, _ZBufferParams),
        sourceData.z,
        IsFarDepth(rawSceneDepth) ? 1.0f : 0.0f);
}

void StoreHistoryUpscaleTap(
    uint2 groupThreadId,
    int2 offset,
    int weightIndex,
    bool guideIsSky,
    out float4 value,
    out float pixelDepth,
    out float volumetricDepth,
    out float weight)
{
    uint address = UpscaleLdsAddress(groupThreadId, offset);
    value = gs_upscaleLighting[address];
    float4 data = gs_upscaleData[address];
    pixelDepth = data.y;
    volumetricDepth = data.z;
    bool tapIsSky = data.w > 0.5f;
    weight = data.x * kDistanceBasedWeights3x3[weightIndex]
        * (tapIsSky == guideIsSky ? 1.0f : 0.0f);
}

void FillHistoryUpscaleNeighborhoodDataLds(
    uint2 groupThreadId,
    int subRegionIdx,
    bool guideIsSky,
    out NeighborhoodUpsampleData3x3 data)
{
    int weightBase = subRegionIdx * 9;
    StoreHistoryUpscaleTap(groupThreadId, int2(-1, -1), weightBase + 0, guideIsSky, data.lowValue0, data.lowDepthA.x, data.lowDepthValueA.x, data.lowWeightA.x);
    StoreHistoryUpscaleTap(groupThreadId, int2( 0, -1), weightBase + 1, guideIsSky, data.lowValue1, data.lowDepthA.y, data.lowDepthValueA.y, data.lowWeightA.y);
    StoreHistoryUpscaleTap(groupThreadId, int2( 1, -1), weightBase + 2, guideIsSky, data.lowValue2, data.lowDepthA.z, data.lowDepthValueA.z, data.lowWeightA.z);
    StoreHistoryUpscaleTap(groupThreadId, int2(-1,  0), weightBase + 3, guideIsSky, data.lowValue3, data.lowDepthA.w, data.lowDepthValueA.w, data.lowWeightA.w);
    StoreHistoryUpscaleTap(groupThreadId, int2( 0,  0), weightBase + 4, guideIsSky, data.lowValue4, data.lowDepthB.x, data.lowDepthValueB.x, data.lowWeightB.x);
    StoreHistoryUpscaleTap(groupThreadId, int2( 1,  0), weightBase + 5, guideIsSky, data.lowValue5, data.lowDepthB.y, data.lowDepthValueB.y, data.lowWeightB.y);
    StoreHistoryUpscaleTap(groupThreadId, int2(-1,  1), weightBase + 6, guideIsSky, data.lowValue6, data.lowDepthB.z, data.lowDepthValueB.z, data.lowWeightB.z);
    StoreHistoryUpscaleTap(groupThreadId, int2( 0,  1), weightBase + 7, guideIsSky, data.lowValue7, data.lowDepthB.w, data.lowDepthValueB.w, data.lowWeightB.w);
    StoreHistoryUpscaleTap(groupThreadId, int2( 1,  1), weightBase + 8, guideIsSky, data.lowValue8, data.lowDepthC, data.lowDepthValueC, data.lowWeightC);
}

bool SceneOccludesVolumetricCloud(float sceneDepth, float cloudVolumetricDepth)
{
    bool occluded = false;
    if (!IsFarDepth(sceneDepth))
    {
        float sceneViewDepth = LinearEyeDepth(sceneDepth, _ZBufferParams);
        float cloudViewDepth = DecodeInfiniteVolumetricDepth(cloudVolumetricDepth, _NearPlane);
        occluded = cloudViewDepth >= sceneViewDepth;
    }
    return occluded;
}

void ApplyVolumetricCloudSceneOcclusion(
    float finalSceneDepth,
    float cloudVolumetricDepth,
    inout float3 cloudLighting,
    inout float cloudTransmittance)
{
    if (!SceneOccludesVolumetricCloud(finalSceneDepth, cloudVolumetricDepth))
        return;

    cloudLighting = 0.0f;
    cloudTransmittance = 1.0f;
}

void ResolveUpscaledVolumetricClouds(
    uint2 outputCoord,
    uint2 groupThreadId,
    float guideSceneDepth,
    float finalSceneDepth,
    out float3 cloudLighting,
    out float cloudTransmittance,
    out float cloudVolumetricDepth)
{
    NeighborhoodUpsampleData3x3 upsampleData = (NeighborhoodUpsampleData3x3)0;
    int subRegionIdx = (int)((outputCoord.x & 1u) + ((outputCoord.y & 1u) << 1u));
    bool guideIsSky = IsFarDepth(guideSceneDepth);
    FillHistoryUpscaleNeighborhoodDataLds(
        groupThreadId,
        subRegionIdx,
        guideIsSky,
        upsampleData);

    float guideDepth = Linear01Depth(guideSceneDepth, _ZBufferParams);

    float4 finalColor = 0.0f;
    float finalVolumetricDepth = UNITY_RAW_FAR_CLIP_VALUE;
    BilateralUpscale3x3(guideDepth, upsampleData, finalColor, finalVolumetricDepth);

    cloudLighting = finalColor.rgb;
    cloudTransmittance = saturate(finalColor.a);
    cloudVolumetricDepth = finalVolumetricDepth;
    ApplyVolumetricCloudSceneOcclusion(
        finalSceneDepth,
        finalVolumetricDepth,
        cloudLighting,
        cloudTransmittance);
}

void ResolveVolumetricClouds(
    uint2 outputCoord,
    uint2 groupThreadId,
    float guideSceneDepth,
    float finalSceneDepth,
    out float3 cloudLighting,
    out float cloudTransmittance,
    out float cloudVolumetricDepth)
{
    cloudLighting = 0.0f;
    cloudTransmittance = 1.0f;
    cloudVolumetricDepth = UNITY_RAW_FAR_CLIP_VALUE;
    if (_OutputToFullResolutionScale > 1)
    {
        ResolveUpscaledVolumetricClouds(
            outputCoord,
            groupThreadId,
            guideSceneDepth,
            finalSceneDepth,
            cloudLighting,
            cloudTransmittance,
            cloudVolumetricDepth);
    }
    else
    {
        float4 cloudData = _SourceDataTexture[outputCoord];
        cloudLighting = _SourceLightingTexture[outputCoord];
        cloudTransmittance = saturate(cloudData.w);
        cloudVolumetricDepth = cloudData.z;
        ApplyVolumetricCloudSceneOcclusion(
            finalSceneDepth,
            cloudData.z,
            cloudLighting,
            cloudTransmittance);
    }
}
#endif

float LoadOpaqueSceneDepth(uint2 pixelCoord)
{
    return _CameraDepthTexture.Load(int3(pixelCoord, 0));
}

[numthreads(COMPOSITE_THREAD_GROUP_SIZE, COMPOSITE_THREAD_GROUP_SIZE, 1)]
void COMPOSITE_KERNEL(
    uint3 dispatchThreadId : SV_DispatchThreadID,
    uint3 groupId : SV_GroupID,
    uint3 groupThreadId : SV_GroupThreadID,
    uint groupIndex : SV_GroupIndex)
{
#if defined(COMPOSITE_VOLUMETRIC_CLOUDS)
    if (CompositeFeatureEnabled(COMPOSITE_FEATURE_CLOUDS) && _OutputToFullResolutionScale > 1)
    {
        if (groupIndex < 36u)
            PrefetchHistoryNeighborhoodLds(groupIndex, groupId.xy * COMPOSITE_THREAD_GROUP_SIZE);
        GroupMemoryBarrierWithGroupSync();
    }
#endif

    uint2 pixelCoord = dispatchThreadId.xy;
    if (any(pixelCoord >= (uint2)_CompositeOutputSize.xy))
        return;

    float4 sceneColorSample = _SceneColorTexture[pixelCoord];
    float3 sceneColor = max(sceneColorSample.rgb, 0.0f);

    float rawDepth = LoadOpaqueSceneDepth(pixelCoord);
    float2 screenUv = (float2(pixelCoord) + 0.5f) * _CompositeOutputSize.zw;
    float3 compositeColor = sceneColor;
    bool isSky = CompositeIsFarRawDepth(rawDepth);
    bool waterBoundary = false;
#if defined(COMPOSITE_WATER_BOUNDARY)
    waterBoundary = CompositeHasWaterBoundary(pixelCoord);
#endif

#if defined(COMPOSITE_ATMOSPHERE)
    if (CompositeFeatureEnabled(COMPOSITE_FEATURE_ATMOSPHERE) && !isSky && !waterBoundary)
    {
        SurfaceMediumComposite atmosphere = EvaluateSurfaceAtmosphereMedium(screenUv, rawDepth, 1);
        compositeColor = atmosphere.additiveRadiance + compositeColor * atmosphere.transmittance;
    }
#endif

#if defined(COMPOSITE_CLOUD_LAYER)
    if (CompositeFeatureEnabled(COMPOSITE_FEATURE_CLOUD_LAYER) && isSky)
    {
        float3 viewDirectionWS = GetCloudLayerViewDirectionWS(float2(pixelCoord) + 0.5f);
        float4 cloudLayer = RenderCloudLayer(viewDirectionWS, screenUv);
        float layerTransmittance = saturate(1.0f - cloudLayer.a);
        compositeColor = max(cloudLayer.rgb, 0.0f) + compositeColor * layerTransmittance;
    }
#endif

#if defined(COMPOSITE_VOLUMETRIC_CLOUDS)
    if (CompositeFeatureEnabled(COMPOSITE_FEATURE_CLOUDS))
    {
        float3 cloudLighting = 0.0f;
        float cloudPhysicalTransmittance = 1.0f;
        float cloudVolumetricDepth = UNITY_RAW_FAR_CLIP_VALUE;
        float cloudGuideDepth = _CloudGuideDepthTexture.Load(int3(pixelCoord, 0));
        ResolveVolumetricClouds(
            pixelCoord,
            groupThreadId.xy,
            cloudGuideDepth,
            rawDepth,
            cloudLighting,
            cloudPhysicalTransmittance,
            cloudVolumetricDepth);
        cloudLighting = max(cloudLighting, 0.0f);
        float cloudTransmittance = saturate(cloudPhysicalTransmittance);

        if (_CompositePerceptualEnabled != 0)
        {
            float perceptualTransmittance = EvaluateVolumetricCloudCompositeTransmittance(
                compositeColor,
                cloudPhysicalTransmittance,
                _ImprovedTransmittanceBlend);
            if (CompositeFeatureEnabled(COMPOSITE_FEATURE_ATMOSPHERE)
                && cloudPhysicalTransmittance < 0.99999f
                && abs(perceptualTransmittance - cloudPhysicalTransmittance) > 1.0e-5f)
            {
                float cloudViewDepth = DecodeInfiniteVolumetricDepth(cloudVolumetricDepth, _NearPlane);
                SceneCompositeCoefficients atmosphereAtCloud = SampleAtmosphereCompositeCoefficients(
                    screenUv,
                    cloudViewDepth * rcp(_DistanceUnitMeter));
                cloudLighting += atmosphereAtCloud.additiveRadiance
                    * (cloudPhysicalTransmittance - perceptualTransmittance);
                cloudLighting = max(cloudLighting, 0.0f);
            }
            cloudTransmittance = perceptualTransmittance;
        }

#if defined(COMPOSITE_REFRACTION_SPLIT)
        if (waterBoundary && cloudTransmittance < 0.99999f)
        {
            float waterRawDepth = _WaterSurfaceDepthTexture.Load(int3(pixelCoord, 0));
            float cloudViewDepth = DecodeInfiniteVolumetricDepth(cloudVolumetricDepth, _NearPlane);
            float waterViewDepth = LinearEyeDepth(waterRawDepth, _ZBufferParams);
            if (cloudViewDepth < waterViewDepth)
            {
                float4 previousColor = _BeforeRefractionTexture[pixelCoord];
                float previousTransmittance = saturate(1.0f - previousColor.a);
                float combinedTransmittance = previousTransmittance * cloudTransmittance;
                _BeforeRefractionTexture[pixelCoord] = float4(
                    cloudLighting + previousColor.rgb * cloudTransmittance,
                    1.0f - combinedTransmittance);
                cloudLighting = 0.0f;
                cloudTransmittance = 1.0f;
            }
        }
#endif

        compositeColor = cloudLighting + compositeColor * cloudTransmittance;
    }
#endif

#if defined(COMPOSITE_FOG)
    if (CompositeFeatureEnabled(COMPOSITE_FEATURE_FOG) && !waterBoundary)
    {
        SurfaceMediumComposite fog = EvaluateSurfaceFogMedium(
            screenUv,
            rawDepth,
            1,
            CompositeFeatureEnabled(COMPOSITE_FEATURE_FOG_SKY) ? 1 : 0,
            CompositeFeatureEnabled(COMPOSITE_FEATURE_VOLUMETRIC_FOG) ? 1 : 0);
        compositeColor = fog.additiveRadiance + compositeColor * fog.transmittance;
    }
#endif

#if defined(COMPOSITE_LIGHT_SHAFTS)
    if (CompositeFeatureEnabled(COMPOSITE_FEATURE_LIGHT_SHAFTS))
        compositeColor += EvaluateScreenSpaceLightShafts(screenUv, rawDepth);
#endif

    _CompositeOutputTexture[pixelCoord] = float4(max(compositeColor, 0.0f), sceneColorSample.a);
}
