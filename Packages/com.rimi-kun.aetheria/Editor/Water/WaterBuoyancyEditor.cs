using UnityEditor.Rendering;
using UnityEditor.Rendering.Universal;
using UnityEngine.Rendering;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using Aetheria.Runtime.Water;

namespace Aetheria.Editor.Water
{
    [CustomEditor(typeof(WaterBuoyancy))]
    sealed class WaterBuoyancyEditor : UnityEditor.Editor
    {
        SerializedProperty m_Probes;

        void OnEnable()
        {
            m_Probes = serializedObject.FindProperty("probes");
        }


        void OnSceneGUI()
        {
            WaterBuoyancy buoyancy = (WaterBuoyancy)target;
            Transform transform = buoyancy.transform;

            serializedObject.Update();
            for (int i = 0; i < m_Probes.arraySize; ++i)
            {
                SerializedProperty probe = m_Probes.GetArrayElementAtIndex(i);
                Vector3 localPosition = probe.vector3Value;
                Vector3 worldPosition = transform.TransformPoint(localPosition);

                EditorGUI.BeginChangeCheck();
                Vector3 movedPosition = Handles.PositionHandle(worldPosition, transform.rotation);
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(buoyancy, "Move Buoyancy Probe");
                    probe.vector3Value = transform.InverseTransformPoint(movedPosition);
                }
            }
            serializedObject.ApplyModifiedProperties();
        }
    }
}
