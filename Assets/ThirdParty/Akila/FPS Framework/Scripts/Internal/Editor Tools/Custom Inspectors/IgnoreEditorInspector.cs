#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace Akila.FPSFramework.Internal
{
    [CustomEditor(typeof(Ignore))]
    public class IgnoreEditorInspector : Editor
    {
        public override void OnInspectorGUI()
        {
            Ignore ignore = (Ignore)target;
            
            Undo.RecordObject(ignore, "Changed Moving Platform");

            EditorGUI.BeginChangeCheck();

            EditorGUILayout.LabelField("Ignored Elements", EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();

            ignore.ignoreFirearmHits = GUILayout.Toggle(ignore.ignoreFirearmHits, "Firearm Hits", "Button", GUILayout.MaxHeight(24), GUILayout.MinWidth(150));
            ignore.ignoreMeleeHits = GUILayout.Toggle(ignore.ignoreMeleeHits, "Melee Hits", "Button", GUILayout.MaxHeight(24), GUILayout.MinWidth(150));

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            ignore.ignoreWallAvoidance = GUILayout.Toggle(ignore.ignoreWallAvoidance, "Wall Avoidance", "Button", GUILayout.MaxHeight(24), GUILayout.MinWidth(150));
            ignore.ignoreFallDamage = GUILayout.Toggle(ignore.ignoreFallDamage, "Fall Damage", "Button", GUILayout.MaxHeight(24), GUILayout.MinWidth(150));
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            ignore.ignoreLaserDetection = GUILayout.Toggle(ignore.ignoreLaserDetection, "Laser Detection", "Button", GUILayout.MaxHeight(24), GUILayout.MinWidth(150));
            ignore.ignoreMovingPlatform = GUILayout.Toggle(ignore.ignoreMovingPlatform, "Moving Platforms", "Button", GUILayout.MaxHeight(24), GUILayout.MinWidth(150));
            EditorGUILayout.EndHorizontal();

            if (EditorGUI.EndChangeCheck())
            {
                EditorUtility.SetDirty(ignore);
            }
        }
    }
}
#endif