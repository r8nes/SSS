using Aetheria.Runtime.Rendering;
using Aetheria.Runtime.Sky;
using UnityEditor;
using UnityEditor.Rendering;
using UnityEngine;

namespace Aetheria.Editor
{
    internal abstract class SkySettingsEditor : VolumeComponentEditor
    {
        [System.Flags]
        protected enum CommonSkyUI
        {
            Intensity = 1 << 0,
            Rotation = 1 << 1,
            UpdateMode = 1 << 2,
            IncludeSunInBaking = 1 << 3
        }

        private SerializedDataParameter _exposure;
        private SerializedDataParameter _multiplier;
        private SerializedDataParameter _rotation;
        private SerializedDataParameter _updateMode;
        private SerializedDataParameter _updatePeriod;
        private SerializedDataParameter _includeSunInBaking;
        private SerializedDataParameter _intensityMode;

        protected CommonSkyUI CommonUI = (CommonSkyUI)~0;

        private static readonly GUIContent IntensityModeLabel = new("Intensity Mode");
        private static readonly GUIContent ExposureLabel = new("Exposure Compensation", "Exposure compensation of the sky in EV.");
        private static readonly GUIContent[] IntensityModes =
        {
            new("Exposure"), new("Multiplier")
        };
        private static readonly int[] IntensityModeValues =
        {
            (int)SkyIntensityMode.Exposure,
            (int)SkyIntensityMode.Multiplier
        };

        public override void OnEnable()
        {
            var o = new PropertyFetcher<SkySettings>(serializedObject);
            _exposure = Unpack(o.Find(x => x.exposure));
            _multiplier = Unpack(o.Find(x => x.multiplier));
            _rotation = Unpack(o.Find(x => x.rotation));
            _updateMode = Unpack(o.Find(x => x.updateMode));
            _updatePeriod = Unpack(o.Find(x => x.updatePeriod));
            _includeSunInBaking = Unpack(o.Find(x => x.includeSunInBaking));
            _intensityMode = Unpack(o.Find(x => x.skyIntensityMode));
            base.OnEnable();
        }

        protected void DrawCommonSkySettings()
        {
            if ((CommonUI & CommonSkyUI.Intensity) != 0)
            {
                using (var scope = new OverridablePropertyScope(_intensityMode, IntensityModeLabel, this))
                {
                    if (scope.displayed)
                    {
                        var rect = EditorGUILayout.GetControlRect();
                        EditorGUI.BeginProperty(rect, IntensityModeLabel, _intensityMode.value);
                        _intensityMode.value.intValue = EditorGUI.IntPopup(
                            rect,
                            IntensityModeLabel,
                            _intensityMode.value.intValue,
                            IntensityModes,
                            IntensityModeValues);
                        EditorGUI.EndProperty();
                    }
                }

                using (new IndentLevelScope())
                {
                    var mode = (SkyIntensityMode)_intensityMode.value.intValue;
                    if (mode == SkyIntensityMode.Exposure)
                        PropertyField(_exposure, ExposureLabel);
                    else if (mode == SkyIntensityMode.Multiplier)
                        PropertyField(_multiplier);
                }
            }

            if ((CommonUI & CommonSkyUI.Rotation) != 0)
                PropertyField(_rotation);

            if ((CommonUI & CommonSkyUI.UpdateMode) != 0)
            {
                PropertyField(_updateMode);
                if (!_updateMode.value.hasMultipleDifferentValues &&
                    _updateMode.value.intValue == (int)EnvironmentUpdateMode.Realtime)
                {
                    using (new IndentLevelScope())
                        PropertyField(_updatePeriod);
                }
            }

            if ((CommonUI & CommonSkyUI.IncludeSunInBaking) != 0)
                PropertyField(_includeSunInBaking);
        }
    }
}
