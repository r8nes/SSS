using Aetheria.Runtime.Components.Volumetrics;
using UnityEditor;
using UnityEngine;

namespace Aetheria.Editor
{
    [CanEditMultipleObjects]
    [CustomEditor(typeof(VolumetricFogZone))]
    internal sealed class VolumetricFogZoneEditor : UnityEditor.Editor
    {
        private SerializedProperty _parameters,
            _mode,
            _albedo,
            _meanFreePath,
            _blendingMode,
            _priority,
            _scaleMode,
            _size,
            _edgeFade,
            _mask,
            _positiveFade,
            _negativeFade,
            _invertFade,
            _distanceFadeStart,
            _distanceFadeEnd,
            _falloffMode;

        private static readonly string[] SimpleBlendNames = { "Additive", "Overwrite" };

        private static readonly int[] SimpleBlendValues =
            { (int)LocalMediumBlendMode.Additive, (int)LocalMediumBlendMode.Overwrite };

        private void OnEnable()
        {
            _parameters = serializedObject.FindProperty("parameters");
            _mode = _parameters.FindPropertyRelative("mode");
            _albedo = _parameters.FindPropertyRelative("albedo");
            _meanFreePath = _parameters.FindPropertyRelative("meanFreePath");
            _blendingMode = _parameters.FindPropertyRelative("blendingMode");
            _priority = _parameters.FindPropertyRelative("priority");
            _scaleMode = _parameters.FindPropertyRelative("scaleMode");
            _size = _parameters.FindPropertyRelative("size");
            _edgeFade = _parameters.FindPropertyRelative("edgeFade");
            _mask = _parameters.FindPropertyRelative("mask");
            _positiveFade = _parameters.FindPropertyRelative("positiveFade");
            _negativeFade = _parameters.FindPropertyRelative("negativeFade");
            _invertFade = _parameters.FindPropertyRelative("invertFade");
            _distanceFadeStart = _parameters.FindPropertyRelative("distanceFadeStart");
            _distanceFadeEnd = _parameters.FindPropertyRelative("distanceFadeEnd");
            _falloffMode = _parameters.FindPropertyRelative("falloffMode");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            EditorGUI.BeginChangeCheck();
            EditorGUILayout.PropertyField(_mode);
            bool advanced = !_mode.hasMultipleDifferentValues &&
                            _mode.enumValueIndex == (int)VolumeShapingMode.Advanced;

            Section("Volume");
            EditorGUILayout.PropertyField(_scaleMode);
            EditorGUILayout.PropertyField(_size);
            EditorGUILayout.PropertyField(_priority);

            Section("Medium");
            EditorGUILayout.PropertyField(_albedo);
            EditorGUILayout.PropertyField(_meanFreePath);
            if (advanced) EditorGUILayout.PropertyField(_blendingMode);
            else DrawSimpleBlendMode();

            Section("Shape Fade");
            if (advanced)
            {
                EditorGUILayout.PropertyField(_positiveFade);
                EditorGUILayout.PropertyField(_negativeFade);
                EditorGUILayout.PropertyField(_invertFade);
                EditorGUILayout.PropertyField(_falloffMode);
            }
            else
            {
                EditorGUILayout.PropertyField(_edgeFade);
            }

            Section("Mask");
            EditorGUILayout.PropertyField(_mask, true);

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

        private void DrawSimpleBlendMode()
        {
            int current = _blendingMode.intValue == (int)LocalMediumBlendMode.Overwrite
                ? (int)LocalMediumBlendMode.Overwrite
                : (int)LocalMediumBlendMode.Additive;

            int selected = EditorGUILayout.IntPopup("Blending Mode", current, SimpleBlendNames, SimpleBlendValues);

            _blendingMode.intValue = selected;
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

                if (obj is not VolumetricFogZone zone) continue;
                zone.Parameters.Constrain();
                EditorUtility.SetDirty(zone);
            }
        }
    }
}