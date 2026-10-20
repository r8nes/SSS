#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace Akila.FPSFramework.Internal
{

    [CustomEditor(typeof(CopyTransform), true)]
    internal class CopyTransformEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            CopyTransform copyTransform = (CopyTransform)target;

            Undo.RecordObject(copyTransform, $"Modified {copyTransform}");

            copyTransform.updateMode = (UpdateMode)EditorGUILayout.EnumPopup("Update Mode", copyTransform.updateMode);
            copyTransform.target = (Transform)EditorGUILayout.ObjectField("Target", copyTransform.target, typeof(Transform), true);

            EditorGUILayout.Space();
            //EditorGUILayout.LabelField("Toggles", EditorStyles.boldLabel);

            copyTransform.executeInEditMode = EditorGUILayout.ToggleLeft("Execute In Edit Mode", copyTransform.executeInEditMode);
            EditorGUILayout.BeginHorizontal();
            copyTransform.position = GUILayout.Toggle(copyTransform.position, "Position", "Button");
            copyTransform.rotation = GUILayout.Toggle(copyTransform.rotation, "Rotation", "Button");
            copyTransform.scale = GUILayout.Toggle(copyTransform.scale, "Scale", "Button");

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space();
            copyTransform.positionOffset = EditorGUILayout.Vector3Field("Position Offset", copyTransform.positionOffset);
            copyTransform.rotationOffset = EditorGUILayout.Vector3Field("Rotation Offset", copyTransform.rotationOffset);

            if (EditorGUI.EndChangeCheck()) EditorUtility.SetDirty(copyTransform);
        }
    }
}
#endif
