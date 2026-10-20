using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Aetheria.Runtime.Overrides
{
    [Serializable]
    [VolumeComponentMenu("Aetheria/Volumetric Clouds")]
    [SupportedOnRenderPipeline(typeof(UniversalRenderPipelineAsset))]
    public sealed class VolumetricClouds : VolumeComponent, IPostProcessComponent
    {
        public enum CloudSimpleMode
        {
            Performance,
            Quality
        }

        public enum CloudPresets
        {
            Sparse,
            Cloudy,
            Overcast,
            Stormy,
            Custom
        }

        public enum CloudFadeInMode
        {
            Automatic,
            Manual
        }

        [Header("General")]
        [Tooltip("Enable/Disable the volumetric clouds effect.")]
        public BoolParameter enable = new(false, BoolParameter.DisplayType.EnumPopup, overrideState: true);

        [Header("Shape")]
        [Tooltip("Specifies the quality mode used by the HDRP Simple cloud model.")]
        public EnumParameter<CloudSimpleMode> cloudSimpleMode = new(CloudSimpleMode.Performance);

        public CloudPresets cloudPreset
        {
            get => m_CloudPreset.value;
            set
            {
                m_CloudPreset.value = value;
                ApplyCurrentCloudPreset();
            }
        }

        [SerializeField]
        private EnumParameter<CloudPresets> m_CloudPreset = new(CloudPresets.Cloudy);

        public ClampedFloatParameter densityMultiplier = new(0.4f, 0.0f, 1.0f);
        public AnimationCurveParameter densityCurve = new(
            new AnimationCurve(new Keyframe(0f, 0f), new Keyframe(0.15f, 1.0f), new Keyframe(1.0f, 0.1f)));
        public ClampedFloatParameter shapeFactor = new(0.9f, 0.0f, 1.0f);
        public MinFloatParameter shapeScale = new(5.0f, 0.1f);
        public ClampedFloatParameter erosionFactor = new(0.8f, 0.0f, 1.0f);
        public MinFloatParameter erosionScale = new(107.0f, 1.0f);

        public AnimationCurveParameter erosionCurve = new(
            new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(0.1f, 0.9f), new Keyframe(1.0f, 1.0f)));
        public AnimationCurveParameter ambientOcclusionCurve = new(
            new AnimationCurve(new Keyframe(0f, 0f), new Keyframe(0.25f, 0.4f), new Keyframe(1.0f, 0.0f)));
        public ClampedFloatParameter microErosionFactor = new(0.5f, 0.0f, 1.0f);
        public MinFloatParameter microErosionScale = new(200.0f, 0.1f);
        public MinFloatParameter bottomAltitude = new(1200.0f, 0.01f);
        public MinFloatParameter altitudeRange = new(2000.0f, 100.0f);
        public Vector3Parameter shapeOffset = new(Vector3.zero);

        [Header("Wind")]
        [Tooltip("Sets the global horizontal wind speed in kilometers per hour. This value can be relative to the Global Wind Speed defined in the Visual Environment.")]
        public WindSpeedParameter globalWindSpeed = new();

        [Tooltip("Controls the orientation of the wind relative to the X world vector. This value can be relative to the Global Wind Orientation defined in the Visual Environment.")]
        public WindOrientationParameter orientation = new();

        [AdditionalProperty]
        public ClampedFloatParameter shapeSpeedMultiplier = new(1.0f, 0.0f, 1.0f);

        [AdditionalProperty]
        public ClampedFloatParameter erosionSpeedMultiplier = new(0.25f, 0.0f, 1.0f);

        [AdditionalProperty]
        public ClampedFloatParameter altitudeDistortion = new(0.25f, -1.0f, 1.0f);

        [AdditionalProperty]
        public FloatParameter verticalShapeWindSpeed = new(0.0f);

        [AdditionalProperty]
        public FloatParameter verticalErosionWindSpeed = new(0.0f);

        [Header("Lighting")]
        public ClampedFloatParameter ambientLightProbeDimmer = new(1.0f, 0.0f, 1.0f);
        public ClampedFloatParameter sunLightDimmer = new(1.0f, 0.0f, 1.0f);

        [AdditionalProperty]
        public ClampedFloatParameter erosionOcclusion = new(0.1f, 0.0f, 1.0f);

        public ColorParameter scatteringTint = new(new Color(0.0f, 0.0f, 0.0f, 1.0f));

        [AdditionalProperty]
        public ClampedFloatParameter multiScattering = new(0.5f, 0.0f, 1.0f);

        [AdditionalProperty]
        public ClampedFloatParameter powderEffectIntensity = new(0.25f, 0.0f, 1.0f);

        [Header("Rendering")]
        [Tooltip("Renders cloud tracing at half resolution and temporal accumulation at full resolution.")]
        public BoolParameter fullResolution = new(false);

        public ClampedFloatParameter perceptualBlending = new(1.0f, 0.0f, 1.0f);
        public ClampedFloatParameter temporalAccumulationFactor = new(0.95f, 0.0f, 1.0f);
        public BoolParameter ghostingReduction = new(true);
        public ClampedIntParameter numPrimarySteps = new(64, 32, 1024);

        [AdditionalProperty]
        public ClampedIntParameter numLightSteps = new(6, 1, 32);

        public EnumParameter<CloudFadeInMode> fadeInMode = new(CloudFadeInMode.Automatic);
        public MinFloatParameter fadeInStart = new(0.0f, 0.0f);
        public MinFloatParameter fadeInDistance = new(0.0f, 0.0f);

        public bool IsActive() => active && enable.value;
        public bool IsTileCompatible() => false;

        public void ApplyCurrentCloudPreset()
        {
            bool microDetails = cloudSimpleMode.value == CloudSimpleMode.Quality;
            switch (cloudPreset)
            {
                case CloudPresets.Sparse:
                    ApplyPreset(
                        density: 0.4f,
                        performanceShape: 0.95f,
                        qualityShape: 0.925f,
                        performanceErosion: 0.8f,
                        qualityErosion: 0.85f,
                        microErosion: 0.65f,
                        densityKeys: new[] { new Keyframe(0f, 0f), new Keyframe(0.05f, 1f), new Keyframe(0.75f, 1f), new Keyframe(1f, 0f) },
                        erosionKeys: new[] { new Keyframe(0f, 1f), new Keyframe(0.1f, 0.9f), new Keyframe(1f, 1f) },
                        ambientOcclusionKeys: new[] { new Keyframe(0f, 0f), new Keyframe(0.25f, 0.5f), new Keyframe(1f, 0f) },
                        bottom: 3000f,
                        range: 1000f,
                        microDetails: microDetails);
                    break;
                case CloudPresets.Cloudy:
                    ApplyPreset(
                        density: 0.4f,
                        performanceShape: 0.9f,
                        qualityShape: 0.875f,
                        performanceErosion: 0.8f,
                        qualityErosion: 0.9f,
                        microErosion: 0.65f,
                        densityKeys: new[] { new Keyframe(0f, 0f), new Keyframe(0.15f, 1f), new Keyframe(1f, 0.1f) },
                        erosionKeys: new[] { new Keyframe(0f, 1f), new Keyframe(0.1f, 0.9f), new Keyframe(1f, 1f) },
                        ambientOcclusionKeys: new[] { new Keyframe(0f, 0f), new Keyframe(0.25f, 0.4f), new Keyframe(1f, 0f) },
                        bottom: 1200f,
                        range: 2000f,
                        microDetails: microDetails);
                    break;
                case CloudPresets.Overcast:
                    ApplyPreset(
                        density: 0.3f,
                        performanceShape: 0.5f,
                        qualityShape: 0.45f,
                        performanceErosion: 0.5f,
                        qualityErosion: 0.7f,
                        microErosion: 0.5f,
                        densityKeys: new[] { new Keyframe(0f, 0f), new Keyframe(0.05f, 1f), new Keyframe(0.9f, 0f), new Keyframe(1f, 0f) },
                        erosionKeys: new[] { new Keyframe(0f, 1f), new Keyframe(0.1f, 0.9f), new Keyframe(1f, 1f) },
                        ambientOcclusionKeys: new[] { new Keyframe(0f, 0f), new Keyframe(1f, 0f) },
                        bottom: 1500f,
                        range: 2500f,
                        microDetails: microDetails);
                    break;
                case CloudPresets.Stormy:
                    ApplyPreset(
                        density: 0.35f,
                        performanceShape: 0.85f,
                        qualityShape: 0.825f,
                        performanceErosion: 0.75f,
                        qualityErosion: 0.9f,
                        microErosion: 0.6f,
                        densityKeys: new[] { new Keyframe(0f, 0f), new Keyframe(0.037f, 1f), new Keyframe(0.6f, 1f), new Keyframe(1f, 0f) },
                        erosionKeys: new[] { new Keyframe(0f, 1f), new Keyframe(0.05f, 0.8f), new Keyframe(0.2438f, 0.9498f), new Keyframe(0.5f, 1f), new Keyframe(0.93f, 0.9268f), new Keyframe(1f, 1f) },
                        ambientOcclusionKeys: new[] { new Keyframe(0f, 0f), new Keyframe(0.1f, 0.4f), new Keyframe(1f, 0f) },
                        bottom: 1000f,
                        range: 5000f,
                        microDetails: microDetails);
                    break;
                case CloudPresets.Custom:
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        private void ApplyPreset(
            float density,
            float performanceShape,
            float qualityShape,
            float performanceErosion,
            float qualityErosion,
            float microErosion,
            Keyframe[] densityKeys,
            Keyframe[] erosionKeys,
            Keyframe[] ambientOcclusionKeys,
            float bottom,
            float range,
            bool microDetails)
        {
            densityMultiplier.value = density;
            shapeFactor.value = microDetails ? qualityShape : performanceShape;
            shapeScale.value = 5.0f;
            erosionFactor.value = microDetails ? qualityErosion : performanceErosion;
            erosionScale.value = microDetails ? 75.0f : 107.0f;
            // HDRP changes the micro-detail values only in Quality mode.
            if (microDetails)
            {
                microErosionFactor.value = microErosion;
                microErosionScale.value = 300.0f;
            }

            densityCurve.value = new AnimationCurve(densityKeys);
            erosionCurve.value = new AnimationCurve(erosionKeys);
            ambientOcclusionCurve.value = new AnimationCurve(ambientOcclusionKeys);
            bottomAltitude.value = bottom;
            altitudeRange.value = range;
        }
    }
}
