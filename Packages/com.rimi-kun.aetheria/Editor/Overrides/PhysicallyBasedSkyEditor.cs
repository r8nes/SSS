using Aetheria.Runtime.Overrides;
using UnityEditor;
using UnityEditor.Rendering;

namespace Aetheria.Editor
{
    [CanEditMultipleObjects]
    [CustomEditor(typeof(PhysicallyBasedSky))]
    internal sealed class PhysicallyBasedSkyEditor : SkySettingsEditor
    {
        private SerializedDataParameter _artisticSkyEnabled;
        private SerializedDataParameter _skyHueColor;
        private SerializedDataParameter _skyHueIntensity;
        private SerializedDataParameter _horizonHueColor;
        private SerializedDataParameter _horizonHueIntensity;
        private SerializedDataParameter _skySaturation;
        private SerializedDataParameter _skyContrast;
        private SerializedDataParameter _artisticSkyPhaseMode;
        private SerializedDataParameter _artisticSkyPhaseWeights;
        private SerializedDataParameter _artisticPatternEnabled;
        private SerializedDataParameter _skyPatternIntensity;
        private SerializedDataParameter _skyPatternScale;
        private SerializedDataParameter _skyPatternHorizontalSize;
        private SerializedDataParameter _skyPatternVerticalSize;
        private SerializedDataParameter _skyPatternSaturation;
        private SerializedDataParameter _artisticSunsetBoostEnabled;
        private SerializedDataParameter _sunsetIntensity;
        private SerializedDataParameter _artisticSunsetPhaseMode;
        private SerializedDataParameter _artisticSunsetPhaseWeights;
        private SerializedDataParameter _affectEnvironment;
        private SerializedDataParameter _affectEnvironmentMix;
        private SerializedDataParameter _enabled;
        private SerializedDataParameter _cubemap;
        private SerializedDataParameter _tint;
        private SerializedDataParameter _luminance;
        private SerializedDataParameter _rotation;
        private SerializedDataParameter _suppressionStart;
        private SerializedDataParameter _suppressionEnd;
        private SerializedDataParameter _suppressionPower;
        private SerializedDataParameter _rayleighScaleHeight;
        private SerializedDataParameter _rayleighScatteringColor;
        private SerializedDataParameter _rayleighScatteringScale;
        private SerializedDataParameter _mieScaleHeight;
        private SerializedDataParameter _mieScatteringColor;
        private SerializedDataParameter _mieScatteringScale;
        private SerializedDataParameter _mieAbsorptionColor;
        private SerializedDataParameter _mieAbsorptionScale;
        private SerializedDataParameter _miePhaseG;
        private SerializedDataParameter _ozoneAbsorptionExtinctionColor;
        private SerializedDataParameter _ozoneAbsorptionExtinctionScale;
        private SerializedDataParameter _ozoneScale;
        private SerializedDataParameter _groundAlbedo;
        private SerializedDataParameter _topRadius;

        public override void OnEnable()
        {
            var o = new PropertyFetcher<PhysicallyBasedSky>(serializedObject);
            _artisticSkyEnabled = Unpack(o.Find(x => x.ArtisticSkyEnabled));
            _skyHueColor = Unpack(o.Find(x => x.SkyHueColor));
            _skyHueIntensity = Unpack(o.Find(x => x.SkyHueIntensity));
            _horizonHueColor = Unpack(o.Find(x => x.HorizonHueColor));
            _horizonHueIntensity = Unpack(o.Find(x => x.HorizonHueIntensity));
            _skySaturation = Unpack(o.Find(x => x.SkySaturation));
            _skyContrast = Unpack(o.Find(x => x.SkyContrast));
            _artisticSkyPhaseMode = Unpack(o.Find(x => x.ArtisticSkyPhaseMode));
            _artisticSkyPhaseWeights = Unpack(o.Find(x => x.ArtisticSkyPhaseWeights));
            _artisticPatternEnabled = Unpack(o.Find(x => x.ArtisticPatternEnabled));
            _skyPatternIntensity = Unpack(o.Find(x => x.SkyPatternIntensity));
            _skyPatternScale = Unpack(o.Find(x => x.SkyPatternScale));
            _skyPatternHorizontalSize = Unpack(o.Find(x => x.SkyPatternHorizontalSize));
            _skyPatternVerticalSize = Unpack(o.Find(x => x.SkyPatternVerticalSize));
            _skyPatternSaturation = Unpack(o.Find(x => x.SkyPatternSaturation));
            _artisticSunsetBoostEnabled = Unpack(o.Find(x => x.ArtisticSunsetBoostEnabled));
            _sunsetIntensity = Unpack(o.Find(x => x.SunsetIntensity));
            _artisticSunsetPhaseMode = Unpack(o.Find(x => x.ArtisticSunsetPhaseMode));
            _artisticSunsetPhaseWeights = Unpack(o.Find(x => x.ArtisticSunsetPhaseWeights));
            _affectEnvironment = Unpack(o.Find(x => x.AffectEnvironment));
            _affectEnvironmentMix = Unpack(o.Find(x => x.AffectEnvironmentMix));

            var space = serializedObject.FindProperty(nameof(PhysicallyBasedSky.Space));
            _enabled = Unpack(space.FindPropertyRelative(nameof(AtmosphereSpaceSettings.Enabled)));
            _cubemap = Unpack(space.FindPropertyRelative(nameof(AtmosphereSpaceSettings.Cubemap)));
            _tint = Unpack(space.FindPropertyRelative(nameof(AtmosphereSpaceSettings.Tint)));
            _luminance = Unpack(space.FindPropertyRelative(nameof(AtmosphereSpaceSettings.Luminance)));
            _rotation = Unpack(space.FindPropertyRelative(nameof(AtmosphereSpaceSettings.Rotation)));
            _suppressionStart = Unpack(space.FindPropertyRelative(nameof(AtmosphereSpaceSettings.SuppressionStart)));
            _suppressionEnd = Unpack(space.FindPropertyRelative(nameof(AtmosphereSpaceSettings.SuppressionEnd)));
            _suppressionPower = Unpack(space.FindPropertyRelative(nameof(AtmosphereSpaceSettings.SuppressionPower)));

            _rayleighScaleHeight = Unpack(o.Find(x => x.RayleighScaleHeight));
            _rayleighScatteringColor = Unpack(o.Find(x => x.RayleighScatteringColor));
            _rayleighScatteringScale = Unpack(o.Find(x => x.RayleighScatteringScale));
            _mieScaleHeight = Unpack(o.Find(x => x.MieScaleHeight));
            _mieScatteringColor = Unpack(o.Find(x => x.MieScatteringColor));
            _mieScatteringScale = Unpack(o.Find(x => x.MieScatteringScale));
            _mieAbsorptionColor = Unpack(o.Find(x => x.MieAbsorptionColor));
            _mieAbsorptionScale = Unpack(o.Find(x => x.MieAbsorptionScale));
            _miePhaseG = Unpack(o.Find(x => x.MiePhaseG));
            _ozoneAbsorptionExtinctionColor = Unpack(o.Find(x => x.OzoneAbsorptionExtinctionColor));
            _ozoneAbsorptionExtinctionScale = Unpack(o.Find(x => x.OzoneAbsorptionExtinctionScale));
            _ozoneScale = Unpack(o.Find(x => x.OzoneScale));
            _groundAlbedo = Unpack(o.Find(x => x.GroundAlbedo));
            _topRadius = Unpack(o.Find(x => x.TopRadius));
            base.OnEnable();
        }

        public override void OnInspectorGUI()
        {
            DrawHeader("General");
            DrawCommonSkySettings();
            PropertyField(_artisticSkyEnabled, EditorGUIUtility.TrTextContent("Artistic Sky"));

            if (_artisticSkyEnabled.value.boolValue)
            {
                using (new IndentLevelScope())
                {
                    PropertyField(_skyHueColor, EditorGUIUtility.TrTextContent("Sky Hue Color"));
                    PropertyField(_skyHueIntensity, EditorGUIUtility.TrTextContent("Sky Hue Intensity"));
                    PropertyField(_horizonHueColor, EditorGUIUtility.TrTextContent("Horizon Hue Color"));
                    PropertyField(_horizonHueIntensity, EditorGUIUtility.TrTextContent("Horizon Hue Intensity"));
                    PropertyField(_skySaturation, EditorGUIUtility.TrTextContent("Sky Saturation"));
                    PropertyField(_skyContrast, EditorGUIUtility.TrTextContent("Sky Contrast"));
                    PropertyField(_artisticSkyPhaseMode, EditorGUIUtility.TrTextContent("Sky Phase Response"));

                    if ((AtmosphereArtisticPhaseMode)_artisticSkyPhaseMode.value.intValue ==
                        AtmosphereArtisticPhaseMode.Custom)
                        PropertyField(_artisticSkyPhaseWeights, EditorGUIUtility.TrTextContent("Sky Phase Weights"));

                    PropertyField(_artisticPatternEnabled, EditorGUIUtility.TrTextContent("Sky Pattern"));

                    if (_artisticPatternEnabled.value.boolValue)
                    {
                        using (new IndentLevelScope())
                        {
                            PropertyField(_skyPatternIntensity);
                            PropertyField(_skyPatternScale);
                            PropertyField(_skyPatternHorizontalSize);
                            PropertyField(_skyPatternVerticalSize);
                            PropertyField(_skyPatternSaturation);
                        }
                    }

                    PropertyField(_artisticSunsetBoostEnabled, EditorGUIUtility.TrTextContent("Sunset Boost"));

                    if (_artisticSunsetBoostEnabled.value.boolValue)
                    {
                        using (new IndentLevelScope())
                        {
                            PropertyField(_sunsetIntensity);
                            PropertyField(_artisticSunsetPhaseMode,
                                EditorGUIUtility.TrTextContent("Sunset Phase Response"));

                            if ((AtmosphereArtisticPhaseMode)_artisticSunsetPhaseMode.value.intValue ==
                                AtmosphereArtisticPhaseMode.Custom)
                                PropertyField(_artisticSunsetPhaseWeights,
                                    EditorGUIUtility.TrTextContent("Sunset Phase Weights"));
                        }
                    }
                }
            }

            PropertyField(_affectEnvironment, EditorGUIUtility.TrTextContent("Affect Environment"));
            if (_affectEnvironment.value.boolValue)
            {
                using (new IndentLevelScope())
                    PropertyField(_affectEnvironmentMix, EditorGUIUtility.TrTextContent("Mix"));
            }

            DrawHeader("Space");
            PropertyField(_enabled);
            if (_enabled.value.boolValue)
            {
                using (new IndentLevelScope())
                {
                    PropertyField(_cubemap);
                    PropertyField(_tint);
                    PropertyField(_luminance);
                    PropertyField(_rotation);
                    PropertyField(_suppressionStart);
                    PropertyField(_suppressionEnd);
                    PropertyField(_suppressionPower);
                }
            }

            DrawHeader("Rayleigh");
            PropertyField(_rayleighScaleHeight);
            PropertyField(_rayleighScatteringColor);
            PropertyField(_rayleighScatteringScale);

            DrawHeader("Mie");
            PropertyField(_mieScaleHeight);
            PropertyField(_mieScatteringColor);
            PropertyField(_mieScatteringScale);
            PropertyField(_mieAbsorptionColor);
            PropertyField(_mieAbsorptionScale);
            PropertyField(_miePhaseG);

            DrawHeader("Ozone");
            PropertyField(_ozoneAbsorptionExtinctionColor);
            PropertyField(_ozoneAbsorptionExtinctionScale);
            PropertyField(_ozoneScale);

            DrawHeader("Planet");
            PropertyField(_groundAlbedo);
            PropertyField(_topRadius);
        }
    }
}