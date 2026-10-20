using Aetheria.Runtime.Overrides;
using Aetheria.Runtime.Sky;
using UnityEditor;
using UnityEditor.Rendering;
using UnityEngine;

namespace Aetheria.Editor
{
    [CanEditMultipleObjects]
    [CustomEditor(typeof(HDRISky))]
    internal sealed class HDRISkyEditor : SkySettingsEditor
    {
        private SerializedDataParameter _hdriSky;
        private SerializedDataParameter _distortionMode;
        private SerializedDataParameter _flowmap;
        private SerializedDataParameter _upperHemisphereOnly;
        private SerializedDataParameter _scrollOrientation;
        private SerializedDataParameter _scrollSpeed;
        private SerializedDataParameter _rotation;
        private SerializedDataParameter _sunInitialRotation;
        private SerializedDataParameter _lockSun;

        public override void OnEnable()
        {
            base.OnEnable();
            CommonUI &= ~(CommonSkyUI.Rotation | CommonSkyUI.IncludeSunInBaking);

            var o = new PropertyFetcher<HDRISky>(serializedObject);
            _hdriSky = Unpack(o.Find(x => x.hdriSky));
            _distortionMode = Unpack(o.Find(x => x.distortionMode));
            _flowmap = Unpack(o.Find(x => x.flowmap));
            _upperHemisphereOnly = Unpack(o.Find(x => x.upperHemisphereOnly));
            _scrollOrientation = Unpack(o.Find(x => x.scrollOrientation));
            _scrollSpeed = Unpack(o.Find(x => x.scrollSpeed));
            _rotation = Unpack(o.Find(x => x.rotation));
            _sunInitialRotation = Unpack(o.Find(x => x.sunInitialRotation));
            _lockSun = Unpack(o.Find(x => x.lockSun));
        }

        public override void OnInspectorGUI()
        {
            PropertyField(_hdriSky);

            PropertyField(_distortionMode);
            if (_distortionMode.value.intValue != (int)HDRISky.DistortionMode.None)
            {
                using (new IndentLevelScope())
                {
                    PropertyField(_scrollOrientation, EditorGUIUtility.TrTextContent("Orientation"));
                    PropertyField(_scrollSpeed, EditorGUIUtility.TrTextContent("Speed"));
                    if (_distortionMode.value.intValue == (int)HDRISky.DistortionMode.Flowmap)
                    {
                        PropertyField(_flowmap);
                        PropertyField(_upperHemisphereOnly);
                    }
                }
            }

            bool rotationChanged;
            EditorGUI.BeginChangeCheck();
            PropertyField(_rotation);
            rotationChanged = EditorGUI.EndChangeCheck();
            DrawSunLock(rotationChanged);

            DrawCommonSkySettings();
        }

        private void DrawSunLock(bool rotationChanged)
        {
            if (_lockSun.value.boolValue && rotationChanged && _lockSun.overrideState.boolValue)
            {
                var sun = FindSun();
                if (sun)
                {
                    Undo.RecordObject(sun.transform, "Rotate Sun With HDRI Sky");
                    var euler = sun.transform.rotation.eulerAngles;
                    euler.y = _sunInitialRotation.value.floatValue + _rotation.value.floatValue;
                    sun.transform.rotation = Quaternion.Euler(euler);
                    EditorUtility.SetDirty(sun);
                }
            }

            if (!_rotation.overrideState.boolValue)
                return;

            using (new IndentLevelScope())
            {
                EditorGUI.BeginChangeCheck();
                PropertyField(_lockSun, EditorGUIUtility.TrTextContent("Lock Sun"));
                if (!EditorGUI.EndChangeCheck())
                    return;

                if (_lockSun.value.boolValue)
                {
                    var sun = FindSun();
                    _sunInitialRotation.value.floatValue = sun
                        ? sun.transform.eulerAngles.y - _rotation.value.floatValue
                        : float.NegativeInfinity;
                }
                else
                {
                    _sunInitialRotation.value.floatValue = float.NegativeInfinity;
                }
            }
        }

        private static Light FindSun()
        {
            if (RenderSettings.sun && RenderSettings.sun.type == LightType.Directional)
                return RenderSettings.sun;

            Light best = null;
            float score = float.NegativeInfinity;
            foreach (var light in Object.FindObjectsByType<Light>(
                         FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                if (!light || light.type != LightType.Directional)
                    continue;
                float candidate = light.intensity + (light.shadows != LightShadows.None ? 100000.0f : 0.0f);
                if (candidate <= score)
                    continue;
                score = candidate;
                best = light;
            }
            return best;
        }
    }
}
