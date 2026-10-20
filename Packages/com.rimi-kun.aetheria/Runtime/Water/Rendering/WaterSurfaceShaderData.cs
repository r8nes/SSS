using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;

namespace Aetheria.Runtime.Water
{
    internal sealed class WaterSurfaceShaderData
    {
        private WaterSimulationResolution m_Resolution;
        private readonly WaterSurfaceProfile[] m_Profiles = new WaterSurfaceProfile[WaterLimits.MaxSurfaceCount];
        private readonly WaterSimulationShaderVariables[] m_SimulationVariables = new WaterSimulationShaderVariables[WaterLimits.MaxSurfaceCount];
        private readonly WaterSpectrumShaderVariables[] m_SpectrumVariables = new WaterSpectrumShaderVariables[WaterLimits.MaxSurfaceCount];
        private readonly WaterSurfaceShaderVariables[] m_SurfaceVariables = new WaterSurfaceShaderVariables[WaterLimits.MaxSurfaceCount];
        private GraphicsBuffer m_ProfileBuffer;

        internal GraphicsBuffer profileBuffer => m_ProfileBuffer;

        internal bool Initialize(WaterSimulationResolution resolution)
        {
            m_Resolution = resolution;
            m_ProfileBuffer = new GraphicsBuffer(
                GraphicsBuffer.Target.Structured,
                WaterLimits.MaxSurfaceCount,
                UnsafeUtility.SizeOf<WaterSurfaceProfile>());
            return true;
        }

        internal void Cleanup()
        {
            CoreUtils.SafeRelease(m_ProfileBuffer);
            m_ProfileBuffer = null;
        }

        internal void UploadProfiles(int surfaceCount)
        {
            if (surfaceCount > 0)
                m_ProfileBuffer.SetData(m_Profiles, 0, 0, surfaceCount);
        }

        internal void UpdateSimulation(WaterSurface surface)
        {
            ref WaterSimulationShaderVariables simCb = ref m_SimulationVariables[surface.surfaceIndex];
            ref WaterSpectrumShaderVariables spectrumCb = ref m_SpectrumVariables[surface.surfaceIndex];
            WaterSimulationState simulation = surface.simulation;
            WaterSpectrumParameters spectrum = simulation.spectrum;

            simCb._WaterSimulationTime = simulation.simulationTime;
            simCb._WaterSimulationResolution = (uint)m_Resolution;
            simCb._WaterActiveCascadeCount = surface.simulationEnabled ? (uint)simulation.activeCascadeCount : 0u;
            simCb._WaterFoamEnabled = surface.HasSimulationFoam() ? 1u : 0u;
            simCb._WaterFoamSimulationParameters = new Vector4(
                0.0f, 0.0f, Mathf.Clamp01(surface.foamCurrentInfluence), Mathf.Max(surface.foamTextureTiling, 0.0f));

            WaterFoamState foamState = simulation.foam;
            float foamResolution = Mathf.Max(foamState.resolution, 1);
            simCb._WaterFoamLodParameters = new Vector4(
                foamState.lodCount, foamResolution, 1.0f / foamResolution, foamState.baseScale);
            simCb._WaterFoamLodCenter = new Vector4(
                foamState.lodCenter.x, foamState.lodCenter.y,
                foamState.lodAlphaBlackPointFade, foamState.lodAlphaBlackPointWhitePointFade);
            simCb._WaterFoamLodScale = new Vector4(
                foamState.viewerAltitudeLevelAlpha,
                foamState.scaleCouldIncrease ? 1.0f : 0.0f,
                foamState.lodScaleShift,
                Mathf.Clamp(surface.waveFoamMinimumLod, 0, WaterLimits.MaxLodCount - 2));
            simCb._WaterFoamTemporalParameters = new Vector4(foamState.stepDeltaTime, 0.0f, 0.0f, 0.0f);

            WaterSimulationLayout.CascadeSamplingContext cascadeContext =
                WaterSimulationLayout.EvaluateCascadeSamplingContext(surface);
            WaterSimulationLayout.CascadeSamplingParameters cascade0 =
                WaterSimulationLayout.EvaluateCascadeSamplingParameters(surface, simulation, cascadeContext, 0);
            WaterSimulationLayout.CascadeSamplingParameters cascade1 =
                WaterSimulationLayout.EvaluateCascadeSamplingParameters(surface, simulation, cascadeContext, 1);
            WaterSimulationLayout.CascadeSamplingParameters cascade2 =
                WaterSimulationLayout.EvaluateCascadeSamplingParameters(surface, simulation, cascadeContext, 2);
            simCb._WaterCascade0ScaleOffset = cascade0.scaleOffset;
            simCb._WaterCascade1ScaleOffset = cascade1.scaleOffset;
            simCb._WaterCascade2ScaleOffset = cascade2.scaleOffset;
            simCb._WaterCascade0ScalesFade = cascade0.scalesFade;
            simCb._WaterCascade1ScalesFade = cascade1.scalesFade;
            simCb._WaterCascade2ScalesFade = cascade2.scalesFade;

            float currentSpeed = simulation.renderingParameters.cascade0.currentSpeed;
            float2 currentDirection = cascadeContext.currentDirection;
            simCb._WaterReferenceOrientation = new float2(
                WaterSimulationLayout.NormalizeAngle(surface.currentDirection) * Mathf.Deg2Rad, 0.0f);
            simCb._WaterFoamAdvectionVelocity = new Vector4(
                currentDirection.x * currentSpeed,
                currentDirection.y * currentSpeed,
                currentSpeed,
                0.0f);
            simCb._WaterScatteringWaveHeight = Mathf.Max(
                simulation.renderingParameters.maxWaveHeight * WaterConstants.k_ScatteringRange,
                WaterConstants.k_MinScatteringAmplitude);

            Vector3 localScale = surface.transform.localScale;
            Vector2 invertScale = new Vector2(localScale.x < 0.0f ? -1.0f : 1.0f, localScale.z < 0.0f ? -1.0f : 1.0f);
            simCb._WaterCurrentRegionScaleOffset.Set(
                invertScale.x / Mathf.Max(surface.currentRegionExtent.x, 0.001f),
                invertScale.y / Mathf.Max(surface.currentRegionExtent.y, 0.001f),
                surface.currentRegionOffset.x,
                -surface.currentRegionOffset.y);
            simCb._WaterCurrentMapInfluence = surface.currentMap != null ? surface.currentMapInfluence : 0.0f;
            simCb._WaterSimulationPadding0 = 0.0f;
            simCb._WaterSimulationPadding1 = 0.0f;
            simCb._WaterSimulationPadding2 = 0.0f;
            simCb._WaterSimulationPadding3 = 0.0f;

            FillSpectrumCascade(ref spectrumCb._WaterCascade0Grid, ref spectrumCb._WaterCascade0Spectrum,
                ref spectrumCb._WaterCascade0Shape, spectrum.cascade0);
            FillSpectrumCascade(ref spectrumCb._WaterCascade1Grid, ref spectrumCb._WaterCascade1Spectrum,
                ref spectrumCb._WaterCascade1Shape, spectrum.cascade1);
            FillSpectrumCascade(ref spectrumCb._WaterCascade2Grid, ref spectrumCb._WaterCascade2Spectrum,
                ref spectrumCb._WaterCascade2Shape, spectrum.cascade2);
            spectrumCb._WaterCascade0Seed = spectrum.cascade0.seed;
            spectrumCb._WaterCascade1Seed = spectrum.cascade1.seed;
            spectrumCb._WaterCascade2Seed = spectrum.cascade2.seed;
            spectrumCb._WaterSpectrumBlend = simulation.previousSpectrumValid ? simulation.spectrumBlend : 1.0f;
            spectrumCb._WaterSpectrumBlendRequiresNormalization = simulation.previousSpectrumValid
                && simulation.spectrumBlendRequiresNormalization ? 1u : 0u;
            spectrumCb._WaterSpectrumPadding0 = 0u;
            spectrumCb._WaterSpectrumPadding1 = 0u;
            spectrumCb._WaterSpectrumPadding2 = 0u;

            surface.simulationConstantBuffer.SetData(m_SimulationVariables, surface.surfaceIndex, 0, 1);
            surface.spectrumConstantBuffer.SetData(m_SpectrumVariables, surface.surfaceIndex, 0, 1);
        }

        private static void FillSpectrumCascade(ref Vector4 grid, ref Vector4 spectrum, ref Vector4 shape,
            WaterCascadeSpectrumParameters cascade)
        {
            grid = new Vector4(cascade.tileLength.x, cascade.tileLength.y, cascade.deltaK.x, cascade.deltaK.y);
            spectrum = new Vector4(cascade.alpha, cascade.omegaPeak, cascade.windSpeed, cascade.windDirection);
            shape = new Vector4(cascade.swell, cascade.spread, cascade.detail, 0.0f);
        }

        private static float EvaluateEnvironmentWindSpeed(WaterSurface surface)
        {
            float weight0 = Mathf.Max(surface.cascade0.normalScale, 0.0f);
            float weight1 = Mathf.Max(surface.cascade1.normalScale, 0.0f);
            float weight2 = Mathf.Max(surface.cascade2.normalScale, 0.0f);
            float totalWeight = weight0 + weight1 + weight2;
            if (totalWeight <= 1e-5f)
                return 0.0f;
            return (surface.cascade0.windSpeed * weight0
                + surface.cascade1.windSpeed * weight1
                + surface.cascade2.windSpeed * weight2) / totalWeight;
        }

        internal void UpdateAppearance(WaterSurface surface)
        {
            ref var surfaceCb = ref m_SurfaceVariables[surface.surfaceIndex];
            WaterSimulationState simulation = surface.simulation;

            surfaceCb._WaterFoamAppearanceParameters = surface.EvaluateFoamAppearanceParameters();
            surfaceCb._WaterFoamVolumeAppearanceParameters = surface.EvaluateFoamVolumeAppearanceParameters();
            Color foamColor = surface.foamColor;
            Color deepFoamColor = surface.deepFoamColor;
            Color bubbleFoamColor = surface.bubbleFoamColor;
            surfaceCb._WaterFoamColor = new Vector4(foamColor.r, foamColor.g, foamColor.b, foamColor.a);
            surfaceCb._WaterDeepFoamColor = new Vector4(deepFoamColor.r, deepFoamColor.g, deepFoamColor.b, deepFoamColor.a);
            surfaceCb._WaterBubbleFoamColor = new Vector4(bubbleFoamColor.r, bubbleFoamColor.g, bubbleFoamColor.b, bubbleFoamColor.a);
            surfaceCb._WaterShorelineFoamParameters = new Vector4(
                Mathf.Clamp01(surface.shorelineFoamStrength),
                Mathf.Max(surface.shorelineFoamDistance, 0.001f),
                Mathf.Max(surface.shorelineFoamWaves, 0.0f),
                Mathf.Max(surface.shorelineFoamSpeed, 0.0f));
            surfaceCb._WaterShorelineFoamNoiseParameters = new Vector4(
                Mathf.Max(surface.shorelineFoamNoiseIntensity, 0.0f),
                Mathf.Max(surface.shorelineFoamNoiseScale, 0.001f),
                0.0f,
                0.0f);
            surfaceCb._WaterShorelineSmoothnessParameters = new Vector4(
                Mathf.Clamp01(surface.shorelineSmoothness),
                Mathf.Clamp01(surface.shorelineSmoothnessInfluence),
                Mathf.Max(surface.shorelineSmoothnessDepthRange, 0.001f),
                0.0f);

            surfaceCb._WaterNearSmoothness = surface.nearSmoothness;
            surfaceCb._WaterFarSmoothness = surface.farSmoothness;
            surfaceCb._WaterSmoothnessFadeStart = Mathf.Max(0.0f, surface.smoothnessFadeStart);
            surfaceCb._WaterSmoothnessFadeDistance = Mathf.Max(0.001f, surface.smoothnessFadeDistance);
            surfaceCb._WaterSimulationMaskEnabled = surface.simulationMask != null ? 1u : 0u;
            surfaceCb._WaterDirectLightTipScattering = surface.directLightTipScattering;
            surfaceCb._WaterDirectLightBodyScattering = surface.directLightBodyScattering;
            surfaceCb._WaterEnvironmentPerceptualRoughness = surface.surfaceType == WaterSurfaceType.Pool
                ? 0.0f
                : Mathf.Lerp(0.0f, 0.15f, Mathf.Clamp01(
                    EvaluateEnvironmentWindSpeed(surface) / WaterConstants.k_EnvironmentRoughnessWindSpeedMetersPerSecond));
            surfaceCb._WaterSurfaceIndex = (uint)surface.surfaceIndex;
            surfaceCb._WaterMaxRefractionDistance = Mathf.Min(
                Mathf.Max(surface.absorptionDistance, 0.001f),
                Mathf.Max(surface.maxRefractionDistance, 0.001f));
            surfaceCb._WaterExtinction.xyz = surface.extinction;
            surfaceCb._WaterScatteringAlbedo.Set(
                surface.scatteringColor.r,
                surface.scatteringColor.g,
                surface.scatteringColor.b,
                0.0f);
            surfaceCb._WaterUpDirection.xyz = surface.GetUpVector();
            surfaceCb._WaterAmbientScattering = surface.ambientScattering;
            surfaceCb._WaterHeightScattering = surface.heightScattering;
            surfaceCb._WaterDisplacementScattering = surface.displacementScattering;

            var localScale = surface.transform.localScale;
            Vector2 invertScale = new Vector2(
                localScale.x < 0.0f ? -1.0f : 1.0f,
                localScale.z < 0.0f ? -1.0f : 1.0f);
            surfaceCb._WaterSimulationMaskOffset = Vector2.Scale(surface.simulationMaskOffset, invertScale);
            surfaceCb._WaterSimulationMaskScale.Set(
                1.0f / Mathf.Max(surface.simulationMaskExtent.x, 0.001f),
                1.0f / Mathf.Max(surface.simulationMaskExtent.y, 0.001f));
            surfaceCb._WaterSimulationMaskRemap.Set(
                surface.simulationMaskRemap.x,
                surface.simulationMaskRemap.y - surface.simulationMaskRemap.x);
            surfaceCb._WaterSurfaceTransform = simulation.renderingParameters.waterToWorldMatrix;

            surfaceCb._WaterDebugMode = (int)surface.debugMode;
            surfaceCb._WaterSimulationMaskDebugMode = (int)surface.simulationMaskDebugMode;
            surfaceCb._WaterCurrentDebugArrowDensity = Mathf.Max(surface.currentDebugArrowDensity, 0.1f);
            surfaceCb._WaterSurfacePadding = 0.0f;

            surface.surfaceConstantBuffer.SetData(
                m_SurfaceVariables, surface.surfaceIndex, 0, 1);
        }

        internal void FillProfile(
            int surfaceIndex,
            WaterRenderMode renderMode,
            WaterSurface surface,
            Color ambientLighting)
        {
            ref var surfaceCb = ref m_SurfaceVariables[surfaceIndex];

            Vector3 scatteringAlbedo = new Vector3(surfaceCb._WaterScatteringAlbedo.x, surfaceCb._WaterScatteringAlbedo.y, surfaceCb._WaterScatteringAlbedo.z);
            float causticsStrength = surface.causticsEnabled
                ? Mathf.Max(surface.causticsStrength, 0.0f)
                : 0.0f;

            WaterSurfaceProfile profile = new WaterSurfaceProfile
            {
                upDirection = new Vector3(surfaceCb._WaterUpDirection.x, surfaceCb._WaterUpDirection.y, surfaceCb._WaterUpDirection.z),
                maxRefractionDistance = surfaceCb._WaterMaxRefractionDistance,
                extinction = new Vector3(surfaceCb._WaterExtinction.x, surfaceCb._WaterExtinction.y, surfaceCb._WaterExtinction.z),
                environmentPerceptualRoughness = surfaceCb._WaterEnvironmentPerceptualRoughness,
                scatteringAlbedo = scatteringAlbedo,
                scatteringAlbedoPadding = 0.0f,
                ambientLighting = new Vector3(ambientLighting.r, ambientLighting.g, ambientLighting.b),
                causticsDirectionalShadowDimmer = Mathf.Clamp01(surface.causticsDirectionalShadowDimmer),
                probeLightingFadeParameters = new Vector4(
                    surface.transform.position.x,
                    surface.transform.position.z,
                    Mathf.Max(surface.probeLightingFadeStart, 0.0f),
                    Mathf.Max(surface.probeLightingFadeEnd, 0.0f)),
                foamColor = new Vector3(surfaceCb._WaterFoamColor.x, surfaceCb._WaterFoamColor.y, surfaceCb._WaterFoamColor.z),
                foamSmoothness = surfaceCb._WaterFoamAppearanceParameters.x,
                deepFoamColor = new Vector3(surfaceCb._WaterDeepFoamColor.x, surfaceCb._WaterDeepFoamColor.y, surfaceCb._WaterDeepFoamColor.z),
                deepFoamWeight = 1.0f,
                bubbleFoamColor = new Vector3(surfaceCb._WaterBubbleFoamColor.x, surfaceCb._WaterBubbleFoamColor.y, surfaceCb._WaterBubbleFoamColor.z),
                bubbleFoamWeight = 1.0f,
                nearSmoothness = surfaceCb._WaterNearSmoothness,
                farRoughness = 1.0f - surfaceCb._WaterFarSmoothness,
                smoothnessFadeStart = surfaceCb._WaterSmoothnessFadeStart,
                smoothnessFadeDistance = surfaceCb._WaterSmoothnessFadeDistance,
                directLightTipScattering = surfaceCb._WaterDirectLightTipScattering,
                directLightBodyScattering = surfaceCb._WaterDirectLightBodyScattering,
                renderMode = (uint)renderMode,
                surfaceLevel = surface.transform.position.y,
                surfaceColorParameters = new Vector4(
                    Mathf.Clamp01(surface.depthTintStrength),
                    Mathf.Max(surface.depthTintDistance, 0.001f),
                    Mathf.Max(surface.depthTintFalloff, 0.001f),
                    0.0f),
                shallowSurfaceColor = new Vector4(
                    Mathf.Clamp01(surface.shallowDepthTint.r),
                    Mathf.Clamp01(surface.shallowDepthTint.g),
                    Mathf.Clamp01(surface.shallowDepthTint.b),
                    0.0f),
                deepSurfaceColor = new Vector4(
                    Mathf.Clamp01(surface.deepDepthTint.r),
                    Mathf.Clamp01(surface.deepDepthTint.g),
                    Mathf.Clamp01(surface.deepDepthTint.b),
                    0.0f),
                shorelineSmoothnessParameters = new Vector4(
                    Mathf.Clamp01(surface.shorelineSmoothness),
                    Mathf.Clamp01(surface.shorelineSmoothnessInfluence),
                    Mathf.Max(surface.shorelineSmoothnessDepthRange, 0.001f),
                    0.0f),
                reflectionParameters = new Vector4(
                    surface.screenSpaceReflections ? 1.0f : 0.0f,
                    0.0f,
                    0.0f,
                    0.0f),
                causticsAppearanceParameters = new Vector4(
                    causticsStrength,
                    Mathf.Max(surface.causticsScale, 0.01f),
                    Mathf.Max(surface.causticsScrollSpeed, 0.0f),
                    Mathf.Clamp01(surface.causticsTextureAverage)),
                causticsProjectionParameters = new Vector4(
                    Mathf.Max(surface.causticsFocalDepth, 0.0f),
                    Mathf.Max(surface.causticsDepthOfField, 0.01f),
                    Mathf.Clamp(surface.causticsDistortionStrength, 0.0f, 0.25f),
                    Mathf.Max(surface.causticsDistortionScale, 0.01f)),
            };

            m_Profiles[surfaceIndex] = profile;
        }

    }
}
