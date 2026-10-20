using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
namespace Aetheria.Runtime.Water
{
    internal sealed class WaterFoamState
    {
        internal const int k_CascadeSetStride = WaterLimits.MaxLodCount;
        private const float k_TeleportThreshold = 100.0f;

        private float m_PendingSimulationTime;
        private bool m_CascadesValid;
        private bool m_ViewpointValid;
        private Vector3 m_PreviousViewpointWorld;

        internal int resolution;
        internal int lodCount;
        internal int simulationFrequency = 30;
        internal bool simulationResourcesEnabled;
        internal readonly Vector4[] cascadeParameters = new Vector4[k_CascadeSetStride * 2];
        internal Vector2 lodCenter;
        internal float baseScale = 1.0f;
        internal float viewerAltitudeLevelAlpha;
        internal bool scaleCouldIncrease;
        internal float lodAlphaBlackPointFade;
        internal float lodAlphaBlackPointWhitePointFade = 1.0f;
        internal int lodScaleShift;
        internal bool historyValid;
        internal bool needsPrewarmingThisStep = true;
        internal bool simulationStepRequired;
        internal int substepCount = 1;
        internal float stepDeltaTime;

        internal void Configure(int simulationResolution, int simulationLodCount, bool enableResources)
        {
            int newLodCount = Mathf.Clamp(simulationLodCount, 2, WaterLimits.MaxLodCount);
            bool configurationChanged = resolution != simulationResolution || lodCount != newLodCount;
            bool enabling = enableResources && !simulationResourcesEnabled;
            bool disabling = !enableResources && simulationResourcesEnabled;

            resolution = simulationResolution;
            lodCount = newLodCount;
            simulationResourcesEnabled = enableResources;

            if (!configurationChanged && !enabling && !disabling)
                return;

            lodCenter = Vector2.zero;
            baseScale = 1.0f;
            viewerAltitudeLevelAlpha = 0.0f;
            scaleCouldIncrease = true;
            lodAlphaBlackPointFade = 0.0f;
            lodAlphaBlackPointWhitePointFade = 1.0f;
            lodScaleShift = 0;
            historyValid = false;
            needsPrewarmingThisStep = enableResources;
            simulationStepRequired = enableResources;
            substepCount = 1;
            stepDeltaTime = 0.0f;
            m_PendingSimulationTime = 0.0f;
            m_CascadesValid = false;
            m_ViewpointValid = false;
            System.Array.Clear(cascadeParameters, 0, cascadeParameters.Length);
        }

        internal void SetSimulationFrequency(int frequency)
        {
            simulationFrequency = Mathf.Clamp(frequency, 15, 200);
        }

        internal void Pause()
        {
            historyValid = false;
            needsPrewarmingThisStep = true;
            simulationStepRequired = false;
            substepCount = 1;
            stepDeltaTime = 0.0f;
            m_PendingSimulationTime = 0.0f;
        }

        internal void UpdateCascades(
            WaterSurface surface,
            WaterCameraContext cameraContext,
            in WaterLodEvaluation lodEvaluation)
        {
            if (surface == null || cameraContext.camera == null)
                return;

            Vector2 center = lodEvaluation.center;
            float newScale = lodEvaluation.baseScale;
            float newViewerAltitudeLevelAlpha = lodEvaluation.viewerAltitudeLevelAlpha;
            bool newScaleCouldIncrease = lodEvaluation.scaleCouldIncrease;
            float newBlackPointFade = lodEvaluation.lodAlphaBlackPointFade;
            float newBlackPointWhitePointFade = lodEvaluation.lodAlphaBlackPointWhitePointFade;

            float previousScale = baseScale;
            bool hadCascades = m_CascadesValid;
            if (hadCascades)
            {
                for (int slice = 0; slice < k_CascadeSetStride; ++slice)
                    cascadeParameters[k_CascadeSetStride + slice] = cascadeParameters[slice];
            }

            bool cascadesMoved = !hadCascades;
            for (int slice = 0; slice < k_CascadeSetStride; ++slice)
                cascadeParameters[slice] = Vector4.zero;

            int count = Mathf.Clamp(lodCount, 2, WaterLimits.MaxLodCount);
            for (int slice = 0; slice < count; ++slice)
            {
                float lodScale = newScale * Mathf.Pow(2.0f, slice);
                float texel = 4.0f * lodScale / Mathf.Max(resolution, 1);
                Vector2 snapped = new Vector2(
                    center.x - Mathf.Repeat(center.x, texel),
                    center.y - Mathf.Repeat(center.y, texel));
                Vector4 nextCascade = new Vector4(snapped.x, snapped.y, texel, 0.0f);
                if (hadCascades)
                {
                    Vector4 previousCascade = cascadeParameters[k_CascadeSetStride + slice];
                    cascadesMoved |= (nextCascade - previousCascade).sqrMagnitude > 1e-12f;
                }
                cascadeParameters[slice] = nextCascade;
            }

            if (!hadCascades)
            {
                for (int slice = 0; slice < k_CascadeSetStride; ++slice)
                    cascadeParameters[k_CascadeSetStride + slice] = cascadeParameters[slice];
                lodScaleShift = 0;
            }
            else
            {
                float ratio = newScale / Mathf.Max(previousScale, 1e-6f);
                lodScaleShift = Mathf.RoundToInt(Mathf.Log(ratio, 2.0f));
            }

            Vector3 viewpointWorld = cameraContext.camera.transform.position;
            if (!m_ViewpointValid
                || (viewpointWorld - m_PreviousViewpointWorld).sqrMagnitude > k_TeleportThreshold * k_TeleportThreshold)
            {
                needsPrewarmingThisStep = true;
            }
            m_PreviousViewpointWorld = viewpointWorld;
            m_ViewpointValid = true;

            lodCenter = center;
            baseScale = newScale;
            viewerAltitudeLevelAlpha = newViewerAltitudeLevelAlpha;
            scaleCouldIncrease = newScaleCouldIncrease;
            lodAlphaBlackPointFade = newBlackPointFade;
            lodAlphaBlackPointWhitePointFade = newBlackPointWhitePointFade;
            m_CascadesValid = true;

            if (simulationResourcesEnabled && cascadesMoved && !simulationStepRequired)
            {
                simulationStepRequired = true;
                substepCount = 1;
                stepDeltaTime = 0.0f;
            }
        }

        internal void Advance(float simulationDeltaTime)
        {
            if (!simulationResourcesEnabled)
                return;

            m_PendingSimulationTime += Mathf.Max(simulationDeltaTime, 0.0f);
            float frequency = Mathf.Max(simulationFrequency, 1);
            int steps = Mathf.FloorToInt(m_PendingSimulationTime * frequency);
            if (steps > 0)
            {
                stepDeltaTime = 1.0f / frequency;
                substepCount = steps;
                simulationStepRequired = true;
            }
            else
            {
                stepDeltaTime = 0.0f;
                substepCount = 1;
                simulationStepRequired |= needsPrewarmingThisStep;
            }
        }

        internal void CommitStep()
        {
            if (stepDeltaTime > 0.0f)
                m_PendingSimulationTime = Mathf.Max(0.0f, m_PendingSimulationTime - substepCount * stepDeltaTime);
            historyValid = true;
            needsPrewarmingThisStep = false;
            simulationStepRequired = false;
            substepCount = 1;
            stepDeltaTime = 0.0f;
            lodScaleShift = 0;
        }

        internal void Reset()
        {
            historyValid = false;
            needsPrewarmingThisStep = true;
            simulationStepRequired = simulationResourcesEnabled;
            substepCount = 1;
            stepDeltaTime = 0.0f;
            lodScaleShift = 0;
            m_PendingSimulationTime = 0.0f;
        }
    }
}
