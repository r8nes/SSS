using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using static UnityEngine.Rendering.Universal.ScreenSpaceReflectionVolumeSettings;

namespace UnityEditor.Rendering.Universal
{
    [CustomEditor(typeof(ScreenSpaceReflectionVolumeSettings))]
    class ScreenSpaceReflectionEditor : VolumeComponentEditor
    {
        // Serialized properties, performance settings
        SerializedDataParameter m_Resolution;
        SerializedDataParameter m_UpscalingMethod;
        SerializedDataParameter m_MaxRayLength;
        SerializedDataParameter m_RayLengthFade;
        SerializedDataParameter m_MaxRaySteps;
        SerializedDataParameter m_ObjectThickness;

        // Serialized properties, authoring settings
        SerializedDataParameter m_Mode;
        SerializedDataParameter m_ReflectionStrength;
        SerializedDataParameter m_ClampReflectedColor;
        SerializedDataParameter m_MaxColorValue;
        SerializedDataParameter m_RoughnessFilter;
        SerializedDataParameter m_RoughnessScale;
        SerializedDataParameter m_MinimumSmoothness;
        SerializedDataParameter m_SmoothnessFadeStart;
        SerializedDataParameter m_NormalFade;
        SerializedDataParameter m_ScreenEdgeFade;
        SerializedDataParameter m_ReflectSky;

        PerformancePreset m_CurrentPreset = PerformancePreset.Custom;
        bool m_IgnorePresetChange = false;
        bool m_PresetDirty = false;

        public override void OnEnable()
        {
            var o = new PropertyFetcher<ScreenSpaceReflectionVolumeSettings>(serializedObject);

            m_Mode = Unpack(o.Find(x => x.mode));
            m_ReflectionStrength = Unpack(o.Find(x => x.reflectionStrength));
            m_ClampReflectedColor = Unpack(o.Find(x => x.clampReflectedColor));
            m_MaxColorValue = Unpack(o.Find(x => x.maxColorValue));
            m_Resolution = Unpack(o.Find(x => x.resolution));
            m_UpscalingMethod = Unpack(o.Find(x => x.upscalingMethod));
            m_RoughnessFilter = Unpack(o.Find(x => x.roughnessFilter));
            m_RoughnessScale = Unpack(o.Find(x => x.roughnessScale));
            m_MinimumSmoothness = Unpack(o.Find(x => x.minimumSmoothness));
            m_SmoothnessFadeStart =  Unpack(o.Find(x => x.smoothnessFadeStart));
            m_NormalFade = Unpack(o.Find(x => x.normalFade));
            m_ScreenEdgeFade = Unpack(o.Find(x => x.screenEdgeFadeDistance));
            m_ReflectSky = Unpack(o.Find(x => x.reflectSky));
            m_MaxRayLength = Unpack(o.Find(x => x.maxRayLength));
            m_RayLengthFade = Unpack(o.Find(x => x.rayLengthFade));
            m_MaxRaySteps = Unpack(o.Find(x => x.maxRaySteps));
            m_ObjectThickness = Unpack(o.Find(x => x.objectThickness));

            // Determine current preset
            DetectCurrentPreset();

            // Re-detect preset when property changed
            ((ScreenSpaceReflectionVolumeSettings)target).propertyChanged += MarkPresetDirty;
        }

        public override void OnDisable()
        {
            ((ScreenSpaceReflectionVolumeSettings)target).propertyChanged -= MarkPresetDirty;
        }

        private void MarkPresetDirty()
        {
            if (!m_IgnorePresetChange)
                m_PresetDirty = true;
        }

        public override void OnInspectorGUI()
        {
#if UNITY_WEBGL
            GraphicsDeviceType[] graphicsApis = PlayerSettings.GetGraphicsAPIs(BuildTarget.WebGL);
            if (Array.FindIndex(graphicsApis, x => x == GraphicsDeviceType.WebGPU) == -1)
                EditorGUILayout.HelpBox("WebGL is not supported for Screen Space Reflection.", MessageType.Warning);
#endif
            if (m_PresetDirty)
            {
                DetectCurrentPreset();
                m_PresetDirty = false;
            }

            // Quality Preset Selection
            EditorGUI.BeginChangeCheck();
            var qualityTextContent = EditorGUIUtility.TrTextContent("Performance Preset", "Select the quality vs. performance preset or use Custom for manual settings");
            var newPreset = (PerformancePreset)EditorGUILayout.EnumPopup(qualityTextContent, m_CurrentPreset);
            if (EditorGUI.EndChangeCheck() && newPreset != m_CurrentPreset)
            {
                ApplyQualityPreset(newPreset);
                m_CurrentPreset = newPreset;
            }

            // Performance Settings
            DrawHeader("Performance");
            PropertyField(m_Resolution);
            PropertyField(m_UpscalingMethod);
            PropertyField(m_MaxRaySteps);
            PropertyField(m_ObjectThickness);
            PropertyField(m_MaxRayLength);

            // Authoring related settings (not part of preset).
            DrawHeader("Visual Quality");
            PropertyField(m_Mode);
            PropertyField(m_ReflectionStrength);
            PropertyField(m_ClampReflectedColor);
            if (m_ClampReflectedColor.value.boolValue)
            {
                using (new EditorGUI.DisabledScope(!m_ClampReflectedColor.overrideState.boolValue))
                {
                    using (new IndentLevelScope())
                        PropertyField(m_MaxColorValue);
                }
            }
            PropertyField(m_RoughnessFilter);
            if (m_RoughnessFilter.value.enumValueIndex != (int)RoughReflectionsQuality.Disabled)
            {
                using (new EditorGUI.DisabledScope(!m_RoughnessFilter.overrideState.boolValue))
                {
                    using (new IndentLevelScope())
                        PropertyField(m_RoughnessScale);
                }
            }

            PropertyField(m_MinimumSmoothness);
            PropertyField(m_SmoothnessFadeStart);
            m_SmoothnessFadeStart.value.floatValue = Mathf.Max(m_MinimumSmoothness.value.floatValue, m_SmoothnessFadeStart.value.floatValue);
            PropertyField(m_ScreenEdgeFade);
            PropertyField(m_NormalFade);
            PropertyField(m_RayLengthFade);
            m_RayLengthFade.value.floatValue = Mathf.Min(m_MaxRayLength.value.floatValue, m_RayLengthFade.value.floatValue);
            PropertyField(m_ReflectSky);

        }

        void DetectCurrentPreset()
        {
            for (int i = 0; i < k_PerformancePresets.Length; i++)
            {
                if (MatchesPreset(in k_PerformancePresets[i]))
                {
                    m_CurrentPreset = (PerformancePreset)i;
                    return;
                }
            }
            m_CurrentPreset = PerformancePreset.Custom;
        }

        // Ignores authoring and debugging settings.
        bool MatchesPreset(in PerformancePresetValues preset)
        {
            return m_Resolution.value.enumValueFlag == (int)preset.resolution &&
                   m_UpscalingMethod.value.enumValueFlag == (int)preset.upscalingMethod &&
                   Mathf.Approximately(m_MaxRayLength.value.floatValue, preset.maxRayLength) &&
                   m_MaxRaySteps.value.intValue == preset.maxRaySteps &&
                   Mathf.Approximately(m_ObjectThickness.value.floatValue, preset.objectThickness);
        }

        void ApplyQualityPreset(PerformancePreset preset)
        {
            if (preset == PerformancePreset.Custom)
                return;

            m_IgnorePresetChange = true;

            ref readonly var settings = ref k_PerformancePresets[(int)preset];

            m_Resolution.overrideState.boolValue = true;
            m_Resolution.value.intValue = (int)settings.resolution;
            m_UpscalingMethod.value.enumValueIndex = (int)settings.upscalingMethod;
            m_UpscalingMethod.overrideState.boolValue = true;
            m_MaxRayLength.value.floatValue = settings.maxRayLength;
            m_MaxRayLength.overrideState.boolValue = true;
            m_MaxRaySteps.value.intValue = settings.maxRaySteps;
            m_MaxRaySteps.overrideState.boolValue = true;
            m_ObjectThickness.value.floatValue = settings.objectThickness;
            m_ObjectThickness.overrideState.boolValue = true;

            m_IgnorePresetChange = false;
        }
    }
}
