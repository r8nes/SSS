using UnityEngine;

namespace Aetheria.Runtime.Water
{
    internal sealed class WaterSimulationState
    {
        private double m_PreviousUpdateTime;
        private double m_AccumulatedSimulationTime;
        private bool m_DirectionalNormalizationValid;
        private WaterSpectrumParameters m_DirectionalNormalizationSpectrum;

        internal double accumulatedSimulationTime => m_AccumulatedSimulationTime;
        internal float simulationTime;
        internal float simulationDeltaTime;
        internal int simulationResolution;
        internal readonly WaterFoamState foam = new();
        internal int activeCascadeCount;
        internal WaterSpectrumParameters spectrum;
        internal WaterSpectrumParameters previousSpectrum;
        internal bool currentSpectrumValid;
        internal bool previousSpectrumValid;
        internal bool spectrumCollapsePending;
        internal bool spectrumTransitionReleasePending;
        internal bool spectrumBlendRequiresNormalization;
        internal bool spectrumCollapseRequiresNormalization;
        internal float spectrumCollapseBlend;
        internal float spectrumBlend = 1.0f;
        private const float k_SpectrumTransitionDuration = 1.0f;
        internal WaterSimulationRenderingParameters renderingParameters;
        internal WaterGpuSimulationResources gpuResources;
        internal bool waveFieldUpdateRequired = true;

        internal void Initialize(int resolution, int cascadeCount)
        {
            gpuResources?.Release();
            simulationResolution = resolution;
            activeCascadeCount = cascadeCount;
            simulationTime = 0.0f;
            simulationDeltaTime = 0.0f;
            m_AccumulatedSimulationTime = 0.0;
            currentSpectrumValid = false;
            ResetSpectrumTransitionState();
            m_DirectionalNormalizationValid = false;
            m_DirectionalNormalizationSpectrum = default;
            waveFieldUpdateRequired = true;
            m_PreviousUpdateTime = Application.isPlaying ? Time.timeAsDouble : Time.realtimeSinceStartupAsDouble;
            gpuResources = WaterGpuSimulationResources.Create(simulationResolution, activeCascadeCount);
        }

        internal bool RequiresDirectionalNormalizationUpdate() =>
            !m_DirectionalNormalizationValid || m_DirectionalNormalizationSpectrum != spectrum;

        internal void CommitDirectionalNormalization()
        {
            m_DirectionalNormalizationSpectrum = spectrum;
            m_DirectionalNormalizationValid = true;
        }

        internal bool MatchesConfiguration(int resolution, int cascadeCount) =>
            resolution == simulationResolution
            && cascadeCount == activeCascadeCount
            && gpuResources != null
            && gpuResources.IsValid(cascadeCount);

        internal void ConfigureFoam(int foamResolution, int foamLodCount, bool enabled)
        {
            int clampedLodCount = Mathf.Clamp(foamLodCount, 2, WaterLimits.MaxLodCount);
            bool configurationChanged = foam.resolution != foamResolution || foam.lodCount != clampedLodCount;
            bool enabledChanged = foam.simulationResourcesEnabled != enabled;
            if (configurationChanged)
                gpuResources?.ReleaseFoamResources();
            if (configurationChanged || enabledChanged)
                foam.Configure(foamResolution, clampedLodCount, enabled);
            if (enabled)
                gpuResources?.EnsureFoamResources(foam.resolution, foam.lodCount);
            else
                gpuResources?.ReleaseFoamResources();
        }

        private void ReleaseCompletedSpectrumTransition()
        {
            previousSpectrumValid = false;
            previousSpectrum = default;
            spectrumTransitionReleasePending = false;
            spectrumBlendRequiresNormalization = false;
            gpuResources?.ReleasePreviousSpectrumResources();
        }

        private void ResetSpectrumTransitionState()
        {
            previousSpectrumValid = false;
            previousSpectrum = default;
            spectrumCollapsePending = false;
            spectrumTransitionReleasePending = false;
            spectrumBlendRequiresNormalization = false;
            spectrumCollapseRequiresNormalization = false;
            spectrumCollapseBlend = 1.0f;
            spectrumBlend = 1.0f;
            gpuResources?.ReleasePreviousSpectrumResources();
        }

        internal void SetSpectrum(WaterSpectrumParameters newSpectrum)
        {
            if (spectrumTransitionReleasePending)
                ReleaseCompletedSpectrumTransition();

            WaterSpectrumParameters currentDefinition = spectrum;
            bool hasSpectrumDefinition = currentDefinition.cascade0.tileLength.x > 0.0f;
            bool sameWaveNumberGrid = !hasSpectrumDefinition || currentDefinition.UsesSameWaveNumberGrid(newSpectrum);
            if (!sameWaveNumberGrid)
            {
                ResetSpectrumTransitionState();
            }
            else if (currentSpectrumValid)
            {
                bool previousBlendContainedIndependentRealizations = previousSpectrumValid && spectrumBlendRequiresNormalization;
                bool nextBlendRequiresNormalization = !currentDefinition.UsesSameSeeds(newSpectrum)
                    || previousBlendContainedIndependentRealizations;
                if (previousSpectrumValid)
                {
                    spectrumCollapseBlend = Mathf.Clamp01(spectrumBlend);
                    spectrumCollapsePending = true;
                    spectrumCollapseRequiresNormalization = spectrumBlendRequiresNormalization;
                }
                else
                {
                    gpuResources.EnsurePreviousSpectrumResources();
                    gpuResources.SwapSpectrumHistory();
                    previousSpectrum = currentDefinition;
                    previousSpectrumValid = true;
                    spectrumCollapsePending = false;
                    spectrumCollapseRequiresNormalization = false;
                }
                spectrumBlendRequiresNormalization = nextBlendRequiresNormalization;
                spectrumBlend = 0.0f;
            }
            else if (!previousSpectrumValid)
            {
                spectrumBlend = 1.0f;
                spectrumBlendRequiresNormalization = false;
            }

            spectrum = newSpectrum;
            currentSpectrumValid = false;
            m_DirectionalNormalizationValid = false;
            waveFieldUpdateRequired = true;
        }

        internal void SetSpectrumImmediate(WaterSpectrumParameters newSpectrum)
        {
            if (spectrumTransitionReleasePending)
                ReleaseCompletedSpectrumTransition();
            ResetSpectrumTransitionState();
            spectrum = newSpectrum;
            currentSpectrumValid = false;
            m_DirectionalNormalizationValid = false;
            waveFieldUpdateRequired = true;
        }

        internal void ResetSimulationTime(float time)
        {
            simulationTime = Mathf.Repeat(time, 4096.0f);
            m_AccumulatedSimulationTime = time;
            simulationDeltaTime = 0.0f;
            currentSpectrumValid = false;
            ResetSpectrumTransitionState();
            foam.Reset();
            waveFieldUpdateRequired = true;
        }

        internal void Update(float simulationTimeScale)
        {
            if (spectrumTransitionReleasePending)
                ReleaseCompletedSpectrumTransition();

            double totalTime = Application.isPlaying ? Time.timeAsDouble : Time.realtimeSinceStartupAsDouble;
            float delta = (float)(totalTime - m_PreviousUpdateTime);
            m_PreviousUpdateTime = totalTime;
#if UNITY_EDITOR
            if (UnityEditor.EditorApplication.isPaused)
                delta = 0.0f;
#endif
            simulationDeltaTime = delta * simulationTimeScale;
            m_AccumulatedSimulationTime += simulationDeltaTime;
            simulationTime = Mathf.Repeat((float)m_AccumulatedSimulationTime, 4096.0f);
            foam.Advance(simulationDeltaTime);

            bool transitionAdvancing = previousSpectrumValid && currentSpectrumValid;
            if (transitionAdvancing)
            {
                spectrumBlend = Mathf.Min(1.0f, spectrumBlend + Mathf.Max(delta, 0.0f) / k_SpectrumTransitionDuration);
                if (spectrumBlend >= 1.0f)
                    spectrumTransitionReleasePending = true;
            }

            waveFieldUpdateRequired = !currentSpectrumValid
                || Mathf.Abs(simulationDeltaTime) > 1e-7f
                || transitionAdvancing
                || spectrumCollapsePending;
        }

        internal float EvaluateCurrentDistance(float speed) => (float)(m_AccumulatedSimulationTime * speed);

        internal void Release()
        {
            gpuResources?.Release();
            gpuResources = null;
        }
    }
}
