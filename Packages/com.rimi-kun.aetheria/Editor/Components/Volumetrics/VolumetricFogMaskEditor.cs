using Aetheria.Runtime.Components.Volumetrics;
using UnityEditor;

namespace Aetheria.Editor
{
    [CanEditMultipleObjects]
    [CustomEditor(typeof(VolumetricFogMask))]
    internal sealed class VolumetricFogMaskEditor : UnityEditor.Editor
    {
        private SerializedProperty _colliderSource,
            _includeInactiveChildren,
            _explicitColliders,
            _mode,
            _edgeFade,
            _mask,
            _positiveFade,
            _negativeFade,
            _distanceFadeStart,
            _distanceFadeEnd,
            _falloffMode;

        private void OnEnable()
        {
            _colliderSource = serializedObject.FindProperty("colliderSource");
            _includeInactiveChildren = serializedObject.FindProperty("includeInactiveChildren");
            _explicitColliders = serializedObject.FindProperty("explicitColliders");
            _mode = serializedObject.FindProperty("mode");
            _edgeFade = serializedObject.FindProperty("edgeFade");
            _mask = serializedObject.FindProperty("mask");
            _positiveFade = serializedObject.FindProperty("positiveFade");
            _negativeFade = serializedObject.FindProperty("negativeFade");
            _distanceFadeStart = serializedObject.FindProperty("distanceFadeStart");
            _distanceFadeEnd = serializedObject.FindProperty("distanceFadeEnd");
            _falloffMode = serializedObject.FindProperty("falloffMode");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            EditorGUI.BeginChangeCheck();

            Section("Colliders");

            EditorGUILayout.PropertyField(_colliderSource);

            var source = (VolumetricFogMaskColliderSource)_colliderSource.enumValueIndex;

            switch (source)
            {
                case VolumetricFogMaskColliderSource.SelfAndChildren:
                    EditorGUILayout.PropertyField(_includeInactiveChildren);
                    break;
                case VolumetricFogMaskColliderSource.Explicit:
                    EditorGUILayout.PropertyField(_explicitColliders, true);
                    break;
            }

            Section("Mask");

            EditorGUILayout.PropertyField(_mode);
            EditorGUILayout.PropertyField(_mask, true);

            bool advanced = !_mode.hasMultipleDifferentValues &&
                            _mode.enumValueIndex == (int)VolumeShapingMode.Advanced;

            Section("Shape Fade");

            if (advanced)
            {
                EditorGUILayout.PropertyField(_positiveFade);
                EditorGUILayout.PropertyField(_negativeFade);
                EditorGUILayout.PropertyField(_falloffMode);
            }
            else
            {
                EditorGUILayout.PropertyField(_edgeFade);
            }

            if (advanced)
            {
                Section("Distance Fade");
                EditorGUILayout.PropertyField(_distanceFadeStart);
                EditorGUILayout.PropertyField(_distanceFadeEnd);
            }

            bool changed = EditorGUI.EndChangeCheck();
            serializedObject.ApplyModifiedProperties();

            if (changed) ConstrainTargets();
        }

        private static void Section(string label)
        {
            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField(label, EditorStyles.boldLabel);
        }

        private void ConstrainTargets()
        {
            for (int i = 0; i < targets.Length; i++)
            {
                var obj = targets[i];

                if (obj is not VolumetricFogMask mask) continue;
                mask.ConstrainSettings();
                EditorUtility.SetDirty(mask);
            }
        }
    }
}