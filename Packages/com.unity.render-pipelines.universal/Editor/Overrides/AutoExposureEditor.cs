using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace UnityEditor.Rendering.Universal
{
    [CustomEditor(typeof(AutoExposure))]
    sealed class AutoExposureEditor : VolumeComponentEditor
    {
        SerializedDataParameter m_Enabled;
        SerializedDataParameter m_MeteringMode;
        SerializedDataParameter m_WeightTextureMask;
        SerializedDataParameter m_ProceduralCenter;
        SerializedDataParameter m_ProceduralRadii;
        SerializedDataParameter m_ProceduralSoftness;
        SerializedDataParameter m_ProceduralMinIntensity;
        SerializedDataParameter m_ProceduralMaxIntensity;
        SerializedDataParameter m_LowPercentile;
        SerializedDataParameter m_HighPercentile;
        SerializedDataParameter m_TargetMidGray;
        SerializedDataParameter m_LimitMin;
        SerializedDataParameter m_LimitMax;
        SerializedDataParameter m_Compensation;
        SerializedDataParameter m_HistogramUseCurveRemapping;
        SerializedDataParameter m_CurveMap;
        SerializedDataParameter m_LimitMinCurveMap;
        SerializedDataParameter m_LimitMaxCurveMap;
        SerializedDataParameter m_AdaptationMode;
        SerializedDataParameter m_AdaptationSpeedDarkToLight;
        SerializedDataParameter m_AdaptationSpeedLightToDark;
        SerializedDataParameter m_InitialExposureStops;

        public override void OnEnable()
        {
            var o = new PropertyFetcher<AutoExposure>(serializedObject);

            m_Enabled = Unpack(o.Find(x => x.enabled));
            m_MeteringMode = Unpack(o.Find(x => x.meteringMode));
            m_WeightTextureMask = Unpack(o.Find(x => x.weightTextureMask));
            m_ProceduralCenter = Unpack(o.Find(x => x.proceduralCenter));
            m_ProceduralRadii = Unpack(o.Find(x => x.proceduralRadii));
            m_ProceduralSoftness = Unpack(o.Find(x => x.proceduralSoftness));
            m_ProceduralMinIntensity = Unpack(o.Find(x => x.maskMinIntensity));
            m_ProceduralMaxIntensity = Unpack(o.Find(x => x.maskMaxIntensity));
            m_LowPercentile = Unpack(o.Find(x => x.lowPercentile));
            m_HighPercentile = Unpack(o.Find(x => x.highPercentile));
            m_TargetMidGray = Unpack(o.Find(x => x.targetMidGray));
            m_LimitMin = Unpack(o.Find(x => x.limitMin));
            m_LimitMax = Unpack(o.Find(x => x.limitMax));
            m_Compensation = Unpack(o.Find(x => x.compensation));
            m_HistogramUseCurveRemapping = Unpack(o.Find(x => x.histogramUseCurveRemapping));
            m_CurveMap = Unpack(o.Find(x => x.curveMap));
            m_LimitMinCurveMap = Unpack(o.Find(x => x.limitMinCurveMap));
            m_LimitMaxCurveMap = Unpack(o.Find(x => x.limitMaxCurveMap));
            m_AdaptationMode = Unpack(o.Find(x => x.adaptationMode));
            m_AdaptationSpeedDarkToLight = Unpack(o.Find(x => x.adaptationSpeedDarkToLight));
            m_AdaptationSpeedLightToDark = Unpack(o.Find(x => x.adaptationSpeedLightToDark));
            m_InitialExposureStops = Unpack(o.Find(x => x.initialExposureStops));
        }

        public override void OnInspectorGUI()
        {
            PropertyField(m_Enabled);
            PropertyField(m_MeteringMode);

            switch ((AutoExposureMeteringMode)m_MeteringMode.value.intValue)
            {
                case AutoExposureMeteringMode.MaskWeighted:
                    PropertyField(m_WeightTextureMask);
                    break;
                case AutoExposureMeteringMode.ProceduralMask:
                    ClampProceduralInputs();
                    PropertyField(m_ProceduralCenter, EditorGUIUtility.TrTextContent("Center"));
                    PropertyField(m_ProceduralRadii, EditorGUIUtility.TrTextContent("Radius"));
                    PropertyField(m_ProceduralSoftness, EditorGUIUtility.TrTextContent("Softness"));
                    PropertyField(m_ProceduralMinIntensity, EditorGUIUtility.TrTextContent("Min Intensity"));
                    PropertyField(m_ProceduralMaxIntensity, EditorGUIUtility.TrTextContent("Max Intensity"));
                    break;
            }

            EditorGUILayout.Space();
            PropertyField(m_LowPercentile);
            PropertyField(m_HighPercentile);
            PropertyField(m_HistogramUseCurveRemapping, EditorGUIUtility.TrTextContent("Use Curve Remapping"));

            if (m_HistogramUseCurveRemapping.value.boolValue)
            {
                PropertyField(m_CurveMap);
                PropertyField(m_LimitMinCurveMap, EditorGUIUtility.TrTextContent("Limit Min"));
                PropertyField(m_LimitMaxCurveMap, EditorGUIUtility.TrTextContent("Limit Max"));
            }
            else
            {
                PropertyField(m_TargetMidGray, EditorGUIUtility.TrTextContent("Target Mid Grey"));
                PropertyField(m_LimitMin);
                PropertyField(m_LimitMax);
            }

            PropertyField(m_Compensation);
            PropertyField(m_AdaptationMode, EditorGUIUtility.TrTextContent("Mode"));

            if ((AutoExposureAdaptationMode)m_AdaptationMode.value.intValue == AutoExposureAdaptationMode.Progressive)
            {
                PropertyField(m_AdaptationSpeedDarkToLight, EditorGUIUtility.TrTextContent("Speed Dark to Light"));
                PropertyField(m_AdaptationSpeedLightToDark, EditorGUIUtility.TrTextContent("Speed Light to Dark"));
            }

            PropertyField(m_InitialExposureStops);
        }

        void ClampProceduralInputs()
        {
            var center = m_ProceduralCenter.value.vector2Value;
            m_ProceduralCenter.value.vector2Value = new Vector2(Mathf.Clamp01(center.x), Mathf.Clamp01(center.y));

            var radii = m_ProceduralRadii.value.vector2Value;
            m_ProceduralRadii.value.vector2Value = new Vector2(Mathf.Clamp01(radii.x), Mathf.Clamp01(radii.y));
        }
    }
}
