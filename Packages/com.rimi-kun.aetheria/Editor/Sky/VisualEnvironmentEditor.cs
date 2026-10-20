using System;
using System.Collections.Generic;
using System.Linq;
using Aetheria.Runtime.Overrides;
using Aetheria.Runtime.Sky;
using UnityEditor;
using UnityEditor.Rendering;
using UnityEngine;

namespace Aetheria.Editor
{
    [CanEditMultipleObjects]
    [CustomEditor(typeof(VisualEnvironment))]
    internal sealed class VisualEnvironmentEditor : VolumeComponentEditor
    {
        private SerializedDataParameter _skyType;
        private SerializedDataParameter _lightingSkyResolution;
        private SerializedDataParameter _planetRadius;
        private SerializedDataParameter _renderingSpace;
        private SerializedDataParameter _centerMode;
        private SerializedDataParameter _planetCenter;
        private SerializedDataParameter _windOrientation;
        private SerializedDataParameter _windSpeed;

        private static GUIContent[] _skyNames;
        private static int[] _skyIds;

        public override void OnEnable()
        {
            var o = new PropertyFetcher<VisualEnvironment>(serializedObject);
            _skyType = Unpack(o.Find(x => x.skyType));
            _lightingSkyResolution = Unpack(o.Find(x => x.lightingSkyResolution));
            _planetRadius = Unpack(o.Find(x => x.planetRadius));
            _renderingSpace = Unpack(o.Find(x => x.renderingSpace));
            _centerMode = Unpack(o.Find(x => x.centerMode));
            _planetCenter = Unpack(o.Find(x => x.planetCenter));
            _windOrientation = Unpack(o.Find(x => x.windOrientation));
            _windSpeed = Unpack(o.Find(x => x.windSpeed));
            RebuildSkyPopup();
            base.OnEnable();
        }

        private static void RebuildSkyPopup()
        {
            var entries = new List<KeyValuePair<int, Type>>
            {
                new(0, null)
            };
            entries.AddRange(SkyManager.SkyTypes.OrderBy(pair => pair.Key));

            _skyNames = entries
                .Select(pair => new GUIContent(
                    pair.Key == 0 ? "None" : ObjectNames.NicifyVariableName(pair.Value.Name)))
                .ToArray();
            _skyIds = entries.Select(pair => pair.Key).ToArray();
        }

        public override void OnInspectorGUI()
        {
            using (var scope = new OverridablePropertyScope(
                       _skyType,
                       EditorGUIUtility.TrTextContent("Sky Type", "Specifies the sky type selected by this Volume."),
                       this))
            {
                if (scope.displayed)
                    EditorGUILayout.IntPopup(_skyType.value, _skyNames, _skyIds, scope.label);
            }
            PropertyField(_lightingSkyResolution, EditorGUIUtility.TrTextContent("Lighting Sky Resolution"));
            PropertyField(_planetRadius, EditorGUIUtility.TrTextContent(
                "Radius", "Planet radius in kilometers, from the center to sea level."));
            PropertyField(_renderingSpace);

            if (_renderingSpace.value.intValue == (int)RenderingSpace.World)
            {
                PropertyField(_centerMode, EditorGUIUtility.TrTextContent(
                    "Center", "Automatic keeps sea level at world Y=0. Manual uses an explicit world-space center."));
                if (_centerMode.value.intValue == (int)VisualEnvironment.PlanetMode.Manual)
                {
                    using (new IndentLevelScope())
                        PropertyField(_planetCenter, EditorGUIUtility.TrTextContent("Position"));
                }
            }

            PropertyField(_windOrientation, EditorGUIUtility.TrTextContent(
                "Global Orientation",
                "Controls the orientation of the wind relative to the X world vector."));
            PropertyField(_windSpeed, EditorGUIUtility.TrTextContent(
                "Global Speed",
                "Controls the global wind speed in kilometers per hour."));

            if (_windSpeed.overrideState.boolValue &&
                _windSpeed.value.floatValue != 0.0f &&
                SceneView.lastActiveSceneView &&
                !SceneView.lastActiveSceneView.sceneViewState.alwaysRefreshEnabled)
            {
                EditorGUILayout.HelpBox(
                    "Wind animations in the scene view are only supported when \"Always Refresh\" is enabled.",
                    MessageType.Info);
            }
        }
    }

    internal sealed class LocalWindParameterDrawer
    {
        private static readonly string[] ModeNames = Enum.GetNames(typeof(WindParameter.WindOverrideMode));
        private static readonly string[] ModeNamesNoMultiply =
        {
            WindParameter.WindOverrideMode.Custom.ToString(),
            WindParameter.WindOverrideMode.Global.ToString(),
            WindParameter.WindOverrideMode.Additive.ToString()
        };
        private const float PopupWidth = 70.0f;

        public static bool BeginGUI(
            out Rect rect,
            GUIContent title,
            SerializedDataParameter parameter,
            SerializedProperty mode,
            bool excludeMultiply)
        {
            rect = EditorGUILayout.GetControlRect();
            rect.xMax -= PopupWidth + 2.0f;
            EditorGUI.BeginProperty(rect, title, parameter.value);

            var popupRect = rect;
            popupRect.x = rect.xMax + 2.0f;
            popupRect.width = PopupWidth;
            mode.intValue = EditorGUI.Popup(
                popupRect,
                mode.intValue,
                excludeMultiply ? ModeNamesNoMultiply : ModeNames);

            if (mode.intValue == (int)WindParameter.WindOverrideMode.Additive)
            {
                var value = parameter.value.FindPropertyRelative("additiveValue");
                value.floatValue = EditorGUI.FloatField(rect, title, value.floatValue);
            }
            else if (mode.intValue == (int)WindParameter.WindOverrideMode.Multiply)
            {
                var value = parameter.value.FindPropertyRelative("multiplyValue");
                value.floatValue = EditorGUI.FloatField(rect, title, value.floatValue);
            }
            else
            {
                if (mode.intValue == (int)WindParameter.WindOverrideMode.Global)
                {
                    EditorGUI.BeginDisabledGroup(true);
                    EditorGUI.showMixedValue = true;
                }
                return false;
            }

            return true;
        }

        public static void EndGUI(SerializedProperty mode)
        {
            EditorGUI.EndProperty();
            if (mode.intValue == (int)WindParameter.WindOverrideMode.Global)
            {
                EditorGUI.showMixedValue = false;
                EditorGUI.EndDisabledGroup();
            }
        }
    }

    [VolumeParameterDrawer(typeof(WindOrientationParameter))]
    internal sealed class WindOrientationParameterDrawer : VolumeParameterDrawer
    {
        public override bool OnGUI(SerializedDataParameter parameter, GUIContent title)
        {
            var mode = parameter.value.FindPropertyRelative("mode");
            if (!LocalWindParameterDrawer.BeginGUI(out var rect, title, parameter, mode, true))
            {
                var value = parameter.value.FindPropertyRelative("customValue");
                value.floatValue = EditorGUI.Slider(rect, title, value.floatValue, 0.0f, 360.0f);
            }
            LocalWindParameterDrawer.EndGUI(mode);
            return true;
        }
    }

    [VolumeParameterDrawer(typeof(WindSpeedParameter))]
    internal sealed class WindSpeedParameterDrawer : VolumeParameterDrawer
    {
        public override bool OnGUI(SerializedDataParameter parameter, GUIContent title)
        {
            var mode = parameter.value.FindPropertyRelative("mode");
            if (!LocalWindParameterDrawer.BeginGUI(out var rect, title, parameter, mode, false))
            {
                var value = parameter.value.FindPropertyRelative("customValue");
                value.floatValue = EditorGUI.FloatField(rect, title, value.floatValue);
            }
            LocalWindParameterDrawer.EndGUI(mode);
            return true;
        }
    }
}
