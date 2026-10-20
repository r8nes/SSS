using System.Linq;
using Aetheria.Runtime;
using Aetheria.Runtime.Overrides;
using UnityEditor;
using UnityEditor.Rendering;
using UnityEngine;
using UnityEngine.Rendering;

namespace Aetheria.Editor
{
    [CanEditMultipleObjects]
    [CustomEditor(typeof(VolumetricClouds))]
    internal sealed class VolumetricCloudsEditor : VolumeComponentEditor
    {
        private SerializedDataParameter _enable;
        private SerializedDataParameter _cloudSimpleMode;
        private SerializedDataParameter _cloudPreset;
        private SerializedDataParameter _densityCurve;
        private SerializedDataParameter _erosionCurve;
        private SerializedDataParameter _ambientOcclusionCurve;
        private SerializedDataParameter _bottomAltitude;
        private SerializedDataParameter _altitudeRange;
        private SerializedDataParameter _fadeInMode;
        private SerializedDataParameter _fadeInStart;
        private SerializedDataParameter _fadeInDistance;
        private SerializedDataParameter _densityMultiplier;
        private SerializedDataParameter _shapeFactor;
        private SerializedDataParameter _shapeScale;
        private SerializedDataParameter _shapeOffset;
        private SerializedDataParameter _erosionFactor;
        private SerializedDataParameter _erosionScale;
        private SerializedDataParameter _microErosionFactor;
        private SerializedDataParameter _microErosionScale;
        private SerializedDataParameter _scatteringTint;
        private SerializedDataParameter _powderEffectIntensity;
        private SerializedDataParameter _ambientLightProbeDimmer;
        private SerializedDataParameter _sunLightDimmer;
        private SerializedDataParameter _erosionOcclusion;
        private SerializedDataParameter _multiScattering;
        private SerializedDataParameter _globalWindSpeed;
        private SerializedDataParameter _orientation;
        private SerializedDataParameter _shapeSpeedMultiplier;
        private SerializedDataParameter _erosionSpeedMultiplier;
        private SerializedDataParameter _verticalShapeWindSpeed;
        private SerializedDataParameter _verticalErosionWindSpeed;
        private SerializedDataParameter _altitudeDistortion;
        private SerializedDataParameter _perceptualBlending;
        private SerializedDataParameter _fullResolution;
        private SerializedDataParameter _temporalAccumulationFactor;
        private SerializedDataParameter _ghostingReduction;
        private SerializedDataParameter _numPrimarySteps;
        private SerializedDataParameter _numLightSteps;

        private static readonly GUIContent PerceptualBlending = EditorGUIUtility.TrTextContent(
            "Perceptual Blending",
            "Applies HDRP-style luminance-dependent cloud transmittance.");
        private static readonly GUIContent TemporalAccumulation = EditorGUIUtility.TrTextContent(
            "Temporal Accumulation",
            "Higher values reduce cloud noise but may increase ghosting.");
        private static readonly GUIContent GhostReduction = EditorGUIUtility.TrTextContent(
            "Ghost Reduction",
            "Clips reprojected cloud history to the current tracing neighborhood.");

        private const string NoRendererFeatureMessage = "Aetheria renderer feature asset was not found in the project.";
        private const string RendererFeatureOffMessage = "Aetheria renderer feature asset is disabled.";
        private const string RenderingDebuggerMessage = "\"Volumetric Clouds\" is disabled to avoid affecting rendering debugging.";

        public override void OnEnable()
        {
            var o = new PropertyFetcher<VolumetricClouds>(serializedObject);
            _enable = Unpack(o.Find(x => x.enable));
            _cloudSimpleMode = Unpack(o.Find(x => x.cloudSimpleMode));
            _cloudPreset = Unpack(serializedObject.FindProperty("m_CloudPreset"));
            _densityCurve = Unpack(o.Find(x => x.densityCurve));
            _erosionCurve = Unpack(o.Find(x => x.erosionCurve));
            _ambientOcclusionCurve = Unpack(o.Find(x => x.ambientOcclusionCurve));
            _bottomAltitude = Unpack(o.Find(x => x.bottomAltitude));
            _altitudeRange = Unpack(o.Find(x => x.altitudeRange));
            _fadeInMode = Unpack(o.Find(x => x.fadeInMode));
            _fadeInStart = Unpack(o.Find(x => x.fadeInStart));
            _fadeInDistance = Unpack(o.Find(x => x.fadeInDistance));
            _densityMultiplier = Unpack(o.Find(x => x.densityMultiplier));
            _shapeFactor = Unpack(o.Find(x => x.shapeFactor));
            _shapeScale = Unpack(o.Find(x => x.shapeScale));
            _shapeOffset = Unpack(o.Find(x => x.shapeOffset));
            _erosionFactor = Unpack(o.Find(x => x.erosionFactor));
            _erosionScale = Unpack(o.Find(x => x.erosionScale));
            _microErosionFactor = Unpack(o.Find(x => x.microErosionFactor));
            _microErosionScale = Unpack(o.Find(x => x.microErosionScale));
            _scatteringTint = Unpack(o.Find(x => x.scatteringTint));
            _powderEffectIntensity = Unpack(o.Find(x => x.powderEffectIntensity));
            _ambientLightProbeDimmer = Unpack(o.Find(x => x.ambientLightProbeDimmer));
            _sunLightDimmer = Unpack(o.Find(x => x.sunLightDimmer));
            _erosionOcclusion = Unpack(o.Find(x => x.erosionOcclusion));
            _multiScattering = Unpack(o.Find(x => x.multiScattering));
            _orientation = Unpack(o.Find(x => x.orientation));
            _globalWindSpeed = Unpack(o.Find(x => x.globalWindSpeed));
            _shapeSpeedMultiplier = Unpack(o.Find(x => x.shapeSpeedMultiplier));
            _erosionSpeedMultiplier = Unpack(o.Find(x => x.erosionSpeedMultiplier));
            _altitudeDistortion = Unpack(o.Find(x => x.altitudeDistortion));
            _verticalShapeWindSpeed = Unpack(o.Find(x => x.verticalShapeWindSpeed));
            _verticalErosionWindSpeed = Unpack(o.Find(x => x.verticalErosionWindSpeed));
            _perceptualBlending = Unpack(o.Find(x => x.perceptualBlending));
            _fullResolution = Unpack(o.Find(x => x.fullResolution));
            _temporalAccumulationFactor = Unpack(o.Find(x => x.temporalAccumulationFactor));
            _ghostingReduction = Unpack(o.Find(x => x.ghostingReduction));
            _numPrimarySteps = Unpack(o.Find(x => x.numPrimarySteps));
            _numLightSteps = Unpack(o.Find(x => x.numLightSteps));
            base.OnEnable();
        }

        public override void OnInspectorGUI()
        {
            var feature = FindAetheriaFeature();
            if (!feature)
            {
                EditorGUILayout.Space();
                EditorGUILayout.HelpBox(NoRendererFeatureMessage, MessageType.Error, wide: true);
                return;
            }

            if (!feature.isActive)
            {
                EditorGUILayout.Space();
                CoreEditorUtils.DrawFixMeBox(RendererFeatureOffMessage, MessageType.Warning, "Fix", () =>
                {
                    feature.SetActive(true);
                    GUIUtility.ExitGUI();
                });
                EditorGUILayout.Space();
            }

            bool enabled = _enable.value.boolValue && _enable.overrideState.boolValue;
            bool debuggerBlocked = DebugManager.instance.isAnyDebugUIActive && !feature.RenderingDebugger;
            if (feature.isActive && enabled && debuggerBlocked)
            {
                EditorGUILayout.Space();
                CoreEditorUtils.DrawFixMeBox(RenderingDebuggerMessage, MessageType.Warning, "Enable", () =>
                {
                    feature.RenderingDebugger = true;
                    GUIUtility.ExitGUI();
                });
                EditorGUILayout.Space();
            }

            PropertyField(_enable);

            DrawShapeUI();

            PropertyField(_globalWindSpeed, EditorGUIUtility.TrTextContent(
                "Global Horizontal Wind Speed",
                "Sets the global horizontal wind speed in kilometers per hour. This value can be relative to the Global Wind Speed defined in the Visual Environment."));
            if (showAdditionalProperties)
            {
                using (new IndentLevelScope())
                {
                    PropertyField(_shapeSpeedMultiplier);
                    PropertyField(_erosionSpeedMultiplier);
                }
            }
            PropertyField(_orientation);
            using (new IndentLevelScope())
                PropertyField(_altitudeDistortion);
            PropertyField(_verticalShapeWindSpeed);
            PropertyField(_verticalErosionWindSpeed);

            PropertyField(_ambientLightProbeDimmer);
            PropertyField(_sunLightDimmer);
            PropertyField(_erosionOcclusion);
            PropertyField(_multiScattering);
            PropertyField(_scatteringTint);
            PropertyField(_powderEffectIntensity);

            PropertyField(_fullResolution);
            using (new OverridablePropertyScope(_perceptualBlending, PerceptualBlending, this))
            {
                _perceptualBlending.value.floatValue =
                    EditorGUILayout.Toggle(PerceptualBlending, _perceptualBlending.value.floatValue > 0.0f) ? 1.0f : 0.0f;
            }
            PropertyField(_temporalAccumulationFactor, TemporalAccumulation);
            PropertyField(_ghostingReduction, GhostReduction);
            PropertyField(_numPrimarySteps);
            PropertyField(_numLightSteps);
            PropertyField(_fadeInMode);
            if ((VolumetricClouds.CloudFadeInMode)_fadeInMode.value.enumValueIndex == VolumetricClouds.CloudFadeInMode.Manual)
            {
                using (new IndentLevelScope())
                {
                    PropertyField(_fadeInStart);
                    PropertyField(_fadeInDistance);
                }
            }
        }

        private void DrawShapeUI()
        {
            var previousMode = (VolumetricClouds.CloudSimpleMode)_cloudSimpleMode.value.enumValueIndex;
            var previousPreset = (VolumetricClouds.CloudPresets)_cloudPreset.value.enumValueIndex;

            EditorGUI.BeginChangeCheck();
            PropertyField(_cloudSimpleMode);
            PropertyField(_cloudPreset);
            var mode = (VolumetricClouds.CloudSimpleMode)_cloudSimpleMode.value.enumValueIndex;
            var preset = (VolumetricClouds.CloudPresets)_cloudPreset.value.enumValueIndex;
            bool presetChanged = EditorGUI.EndChangeCheck() || previousMode != mode || previousPreset != preset;

            if (presetChanged && preset != VolumetricClouds.CloudPresets.Custom)
                ApplyPreset(preset, mode == VolumetricClouds.CloudSimpleMode.Quality);

            if (preset != VolumetricClouds.CloudPresets.Custom)
                SyncPresetOverrideStates();

            EditorGUI.BeginChangeCheck();
            using (new EditorGUI.DisabledScope(!_cloudPreset.overrideState.boolValue))
            using (new IndentLevelScope())
            {
                PropertyField(_densityMultiplier);
                PropertyField(_densityCurve);
                PropertyField(_shapeFactor);
                PropertyField(_shapeScale);
                PropertyField(_erosionFactor);
                PropertyField(_erosionScale);
                PropertyField(_erosionCurve);
                if (mode == VolumetricClouds.CloudSimpleMode.Quality)
                {
                    PropertyField(_microErosionFactor);
                    PropertyField(_microErosionScale);
                }
                PropertyField(_ambientOcclusionCurve);
                PropertyField(_bottomAltitude);
                PropertyField(_altitudeRange);
            }

            if (EditorGUI.EndChangeCheck() && preset != VolumetricClouds.CloudPresets.Custom)
                _cloudPreset.value.enumValueIndex = (int)VolumetricClouds.CloudPresets.Custom;

            PropertyField(_shapeOffset);
        }

        private void ApplyPreset(VolumetricClouds.CloudPresets preset, bool quality)
        {
            switch (preset)
            {
                case VolumetricClouds.CloudPresets.Sparse:
                    ApplyPresetValues(0.4f, quality ? 0.925f : 0.95f, quality ? 0.85f : 0.8f, quality ? 75f : 107f,
                        0.65f,
                        new[] { new Keyframe(0f, 0f), new Keyframe(0.05f, 1f), new Keyframe(0.75f, 1f), new Keyframe(1f, 0f) },
                        new[] { new Keyframe(0f, 1f), new Keyframe(0.1f, 0.9f), new Keyframe(1f, 1f) },
                        new[] { new Keyframe(0f, 0f), new Keyframe(0.25f, 0.5f), new Keyframe(1f, 0f) }, 3000f, 1000f, quality);
                    break;
                case VolumetricClouds.CloudPresets.Cloudy:
                    ApplyPresetValues(0.4f, quality ? 0.875f : 0.9f, quality ? 0.9f : 0.8f, quality ? 75f : 107f,
                        0.65f,
                        new[] { new Keyframe(0f, 0f), new Keyframe(0.15f, 1f), new Keyframe(1f, 0.1f) },
                        new[] { new Keyframe(0f, 1f), new Keyframe(0.1f, 0.9f), new Keyframe(1f, 1f) },
                        new[] { new Keyframe(0f, 0f), new Keyframe(0.25f, 0.4f), new Keyframe(1f, 0f) }, 1200f, 2000f, quality);
                    break;
                case VolumetricClouds.CloudPresets.Overcast:
                    ApplyPresetValues(0.3f, quality ? 0.45f : 0.5f, quality ? 0.7f : 0.5f, quality ? 75f : 107f,
                        0.5f,
                        new[] { new Keyframe(0f, 0f), new Keyframe(0.05f, 1f), new Keyframe(0.9f, 0f), new Keyframe(1f, 0f) },
                        new[] { new Keyframe(0f, 1f), new Keyframe(0.1f, 0.9f), new Keyframe(1f, 1f) },
                        new[] { new Keyframe(0f, 0f), new Keyframe(1f, 0f) }, 1500f, 2500f, quality);
                    break;
                case VolumetricClouds.CloudPresets.Stormy:
                    ApplyPresetValues(0.35f, quality ? 0.825f : 0.85f, quality ? 0.9f : 0.75f, quality ? 75f : 107f,
                        0.6f,
                        new[] { new Keyframe(0f, 0f), new Keyframe(0.037f, 1f), new Keyframe(0.6f, 1f), new Keyframe(1f, 0f) },
                        new[] { new Keyframe(0f, 1f), new Keyframe(0.05f, 0.8f), new Keyframe(0.2438f, 0.9498f), new Keyframe(0.5f, 1f), new Keyframe(0.93f, 0.9268f), new Keyframe(1f, 1f) },
                        new[] { new Keyframe(0f, 0f), new Keyframe(0.1f, 0.4f), new Keyframe(1f, 0f) }, 1000f, 5000f, quality);
                    break;
            }
        }

        private void ApplyPresetValues(
            float density,
            float shape,
            float erosion,
            float erosionScale,
            float microErosion,
            Keyframe[] densityKeys,
            Keyframe[] erosionKeys,
            Keyframe[] ambientOcclusionKeys,
            float bottom,
            float range,
            bool quality)
        {
            _densityMultiplier.value.floatValue = density;
            _shapeFactor.value.floatValue = shape;
            _shapeScale.value.floatValue = 5.0f;
            _erosionFactor.value.floatValue = erosion;
            _erosionScale.value.floatValue = erosionScale;
            if (quality)
            {
                _microErosionFactor.value.floatValue = microErosion;
                _microErosionScale.value.floatValue = 300.0f;
            }
            _densityCurve.value.animationCurveValue = new AnimationCurve(densityKeys);
            _erosionCurve.value.animationCurveValue = new AnimationCurve(erosionKeys);
            _ambientOcclusionCurve.value.animationCurveValue = new AnimationCurve(ambientOcclusionKeys);
            _bottomAltitude.value.floatValue = bottom;
            _altitudeRange.value.floatValue = range;
        }

        private void SyncPresetOverrideStates()
        {
            bool enabled = _cloudPreset.overrideState.boolValue;
            _densityMultiplier.overrideState.boolValue = enabled;
            _densityCurve.overrideState.boolValue = enabled;
            _shapeFactor.overrideState.boolValue = enabled;
            _shapeScale.overrideState.boolValue = enabled;
            _erosionFactor.overrideState.boolValue = enabled;
            _erosionScale.overrideState.boolValue = enabled;
            _erosionCurve.overrideState.boolValue = enabled;
            _microErosionFactor.overrideState.boolValue = enabled;
            _microErosionScale.overrideState.boolValue = enabled;
            _ambientOcclusionCurve.overrideState.boolValue = enabled;
            _bottomAltitude.overrideState.boolValue = enabled;
            _altitudeRange.overrideState.boolValue = enabled;
        }

        private static AetheriaFeature FindAetheriaFeature() =>
            Resources.FindObjectsOfTypeAll<AetheriaFeature>().FirstOrDefault(feature => feature);
    }
}
