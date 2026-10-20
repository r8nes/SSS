using Aetheria.Runtime.Components;
using UnityEditor;
using UnityEditor.Rendering;
using UnityEngine;

namespace Aetheria.Editor
{
    [CustomEditor(typeof(CelestialBody))]
    public class CelestialBodyControllerEditor : UnityEditor.Editor
    {
        private SerializedProperty _interactsWithSky;
        private SerializedProperty _diameterMultiplierMode;
        private SerializedProperty _diameterMultiplier;
        private SerializedProperty _diameterOverride;
        private SerializedProperty _angularDiameter;
        private SerializedProperty _shadingSource;
        private SerializedProperty _surfaceTint;
        private SerializedProperty _surfaceCookieTexture;
        private SerializedProperty _cookieRotation;
        private SerializedProperty _limbDarkening;
        private SerializedProperty _flareSize;
        private SerializedProperty _flareFalloff;
        private SerializedProperty _sunLightOverride;
        private SerializedProperty _sunColor;
        private SerializedProperty _sunIntensity;
        private SerializedProperty _moonLunarLambertWeight;
        private SerializedProperty _moonPhase;
        private SerializedProperty _moonPhaseRotation;
        private SerializedProperty _earthshine;
        private SerializedProperty _distance;

        private void OnEnable()
        {
            var o = new PropertyFetcher<CelestialBody>(serializedObject);

            _interactsWithSky = o.Find(x => x._interactsWithSky);
            _diameterMultiplierMode = o.Find(x => x._diameterMultiplierMode);
            _diameterMultiplier = o.Find(x => x._diameterMultiplier);
            _diameterOverride = o.Find(x => x._diameterOverride);
            _angularDiameter = o.Find(x => x._angularDiameter);
            _shadingSource = o.Find(x => x._shadingSource);
            _surfaceTint = o.Find(x => x._surfaceTint);
            _surfaceCookieTexture = o.Find(x => x._surfaceCookieTexture);
            _cookieRotation = o.Find(x => x._cookieRotation);
            _limbDarkening = o.Find(x => x._limbDarkening);
            _flareSize = o.Find(x => x._flareSize);
            _flareFalloff = o.Find(x => x._flareFalloff);
            _sunLightOverride = o.Find(x => x._sunLightOverride);
            _sunColor = o.Find(x => x._sunColor);
            _sunIntensity = o.Find(x => x._sunIntensity);
            _moonLunarLambertWeight = o.Find(x => x._moonLunarLambertWeight);
            _moonPhase = o.Find(x => x._moonPhase);
            _moonPhaseRotation = o.Find(x => x._moonPhaseRotation);
            _earthshine = o.Find(x => x._earthshine);
            _distance = o.Find(x => x._distance);
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            DrawSkyGroup();
            EditorGUILayout.Space(4f);

            DrawSizeGroup();
            EditorGUILayout.Space(4f);

            DrawShadingGroup();
            EditorGUILayout.Space(4f);

            DrawSurfaceGroup();
            EditorGUILayout.Space(4f);

            DrawMoonGroup();
            EditorGUILayout.Space(4f);

            DrawDistanceGroup();

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawSkyGroup()
        {
            EditorGUILayout.PropertyField(_interactsWithSky);

            var celestial = (CelestialBody)target;

            if (!_interactsWithSky.boolValue) return;

            if (!celestial.TryGetComponent<Light>(out var light))
            {
                EditorGUILayout.HelpBox(
                    "Interacts With Sky needs a Directional Light because this body participates in atmosphere and sky lighting.",
                    MessageType.Warning);

                if (!GUILayout.Button("Add Directional Light")) return;

                light = Undo.AddComponent<Light>(celestial.gameObject);
                light.type = LightType.Directional;
                EditorUtility.SetDirty(light);
            }
            else if (light.type != LightType.Directional)
            {
                EditorGUILayout.HelpBox("Interacts With Sky only resolves Directional Lights.", MessageType.Warning);
            }
        }

        private void DrawSizeGroup()
        {
            EditorGUILayout.PropertyField(_diameterMultiplierMode);

            if (_diameterMultiplierMode.boolValue)
            {
                EditorGUILayout.PropertyField(_angularDiameter);
                EditorGUILayout.PropertyField(_diameterMultiplier);
            }
            else
            {
                EditorGUILayout.PropertyField(_diameterOverride);
            }
        }

        private void DrawShadingGroup() => EditorGUILayout.PropertyField(_shadingSource);

        private void DrawSurfaceGroup()
        {
            EditorGUILayout.PropertyField(_surfaceTint);
            EditorGUILayout.PropertyField(_surfaceCookieTexture);
            EditorGUILayout.PropertyField(_cookieRotation);
            EditorGUILayout.PropertyField(_limbDarkening);

            if ((CelestialBodyShadingSource)_shadingSource.intValue != CelestialBodyShadingSource.Emission) return;

            EditorGUILayout.PropertyField(_flareSize);
            EditorGUILayout.PropertyField(_flareFalloff);
        }

        private void DrawMoonGroup()
        {
            var currentShading = (CelestialBodyShadingSource)_shadingSource.intValue;
            bool isEmission = currentShading == CelestialBodyShadingSource.Emission;

            if (isEmission) return;

            switch (currentShading)
            {
                case CelestialBodyShadingSource.ReflectSunLight:
                    EditorGUILayout.PropertyField(_sunLightOverride);
                    break;
                case CelestialBodyShadingSource.Manual:
                    EditorGUILayout.PropertyField(_sunColor);
                    EditorGUILayout.PropertyField(_sunIntensity);
                    break;
            }

            EditorGUILayout.PropertyField(_moonLunarLambertWeight);

            EditorGUILayout.PropertyField(_moonPhase);
            EditorGUILayout.PropertyField(_moonPhaseRotation);
            EditorGUILayout.PropertyField(_earthshine);
        }

        private void DrawDistanceGroup() => EditorGUILayout.PropertyField(_distance);
    }
}