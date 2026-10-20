using UnityEditor;
using UnityEditor.Rendering;
using UnityEditor.Rendering.Universal;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Aetheria.Runtime.Water;

namespace Aetheria.Editor.Water
{
    [CanEditMultipleObjects]
    [CustomEditor(typeof(WaterRenderingSettings))]
    sealed class WaterRenderingSettingsEditor : VolumeComponentEditor
    {
        SerializedDataParameter m_Enable;
        SerializedDataParameter m_SimulationResolution;
        SerializedDataParameter m_AmbientProbeDimmer;
        SerializedDataParameter m_EnvironmentReflectionIntensity;

        public override void OnEnable()
        {
            var fetcher = new PropertyFetcher<WaterRenderingSettings>(serializedObject);
            m_Enable = Unpack(fetcher.Find(x => x.enabled));
            m_SimulationResolution = Unpack(fetcher.Find(x => x.simulationResolution));
            m_AmbientProbeDimmer = Unpack(fetcher.Find(x => x.ambientProbeDimmer));
            m_EnvironmentReflectionIntensity = Unpack(fetcher.Find(x => x.environmentReflectionIntensity));
        }

        public override void OnInspectorGUI()
        {
            EditorGUILayout.LabelField("General", EditorStyles.miniLabel);
            PropertyField(m_Enable, EditorGUIUtility.TrTextContent("State"));
            PropertyField(m_SimulationResolution);

            EditorGUILayout.LabelField("Lighting", EditorStyles.miniLabel);
            PropertyField(m_AmbientProbeDimmer);
            PropertyField(m_EnvironmentReflectionIntensity, EditorGUIUtility.TrTextContent("Environment Reflection Intensity"));
        }
    }
}
