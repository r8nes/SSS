using System;

namespace UnityEngine.Rendering.Universal
{
    [Serializable]
    [VolumeComponentMenu("Post-processing/AutoExposure")]
    public sealed class AutoExposure : VolumeComponent, IPostProcessComponent
    {
        const float k_Grey125Stops = -3.0f;
        const float k_Grey14Stops = -2.8365013f;
        const float k_Grey18Stops = -2.4739312f;

        [Header("Enable")]
        public BoolParameter enabled = new(false);

        [Header("Metering")]
        [Tooltip("Specifies the metering method used to weight the scene before histogram evaluation.")]
        public AutoExposureMeteringModeParameter meteringMode = new(AutoExposureMeteringMode.CenterWeighted);

        [Tooltip("2D texture mask used when Metering Mode is set to Mask Weighted. White keeps full influence, black removes influence.")]
        public TextureParameter weightTextureMask = new(null);

        [Tooltip("Center of the procedural metering mask in normalized screen coordinates.")]
        public Vector2Parameter proceduralCenter = new(new Vector2(0.5f, 0.5f));

        [Tooltip("Procedural metering radii as fractions of the screen width and height.")]
        public Vector2Parameter proceduralRadii = new(new Vector2(0.3f, 0.3f));

        [Tooltip("Softness of the procedural metering mask. Higher values produce a sharper center bias.")]
        public MinFloatParameter proceduralSoftness = new(0.5f, 0.001f);

        [Tooltip("Minimum scene exposure stop accepted by the procedural mask. Lower values admit brighter pixels.")]
        public FloatParameter maskMinIntensity = new(-30f);

        [Tooltip("Maximum scene exposure stop accepted by the procedural mask. Higher values admit darker pixels.")]
        public FloatParameter maskMaxIntensity = new(30f);

        [Header("Histogram")]
        [Tooltip("Lower histogram percentile kept for metering. Lower values admit more shadow content.")]
        public ClampedFloatParameter lowPercentile = new(40f, 0f, 99f);

        [Tooltip("Upper histogram percentile kept for metering. Higher values admit more highlight content.")]
        public ClampedFloatParameter highPercentile = new(90f, 1f, 100f);

        [Header("Exposure")]
        [Tooltip("Desired middle grey used when curve remapping is disabled.")]
        public AutoExposureTargetMidGrayParameter targetMidGray = new(AutoExposureTargetMidGray.Grey18);

        [Tooltip("Minimum allowed exposure in logarithmic stops when curve remapping is disabled.")]
        public FloatParameter limitMin = new(-5f);

        [Tooltip("Maximum allowed exposure in logarithmic stops when curve remapping is disabled.")]
        public FloatParameter limitMax = new(4f);

        [Tooltip("Adds an exposure bias after metering or curve remapping. Positive values brighten the image.")]
        public FloatParameter compensation = new(0f);

        [Tooltip("When enabled, remaps the metered scene exposure through custom curves before adaptation.")]
        public BoolParameter histogramUseCurveRemapping = new(false);

        [Tooltip("Remaps the metered scene exposure on the X axis to the final exposure on the Y axis. Units are logarithmic exposure stops.")]
        public AnimationCurveParameter curveMap = new(new AnimationCurve(new Keyframe(-10f, -10f + k_Grey18Stops), new Keyframe(20f, 20f + k_Grey18Stops)));

        [Tooltip("For each metered scene exposure on the X axis, defines the minimum allowed final exposure on the Y axis.")]
        public AnimationCurveParameter limitMinCurveMap = new(new AnimationCurve(new Keyframe(-10f, -12f + k_Grey18Stops), new Keyframe(20f, 18f + k_Grey18Stops)));

        [Tooltip("For each metered scene exposure on the X axis, defines the maximum allowed final exposure on the Y axis.")]
        public AnimationCurveParameter limitMaxCurveMap = new(new AnimationCurve(new Keyframe(-10f, -8f + k_Grey18Stops), new Keyframe(20f, 22f + k_Grey18Stops)));

        [Header("Adaptation")]
        [Tooltip("Specifies whether exposure changes happen instantly or adapt over time.")]
        public AutoExposureAdaptationModeParameter adaptationMode = new(AutoExposureAdaptationMode.Progressive);

        [Tooltip("Adaptation speed when exposure needs to increase and brighten the image.")]
        public MinFloatParameter adaptationSpeedDarkToLight = new(3f, 0.001f);

        [Tooltip("Adaptation speed when exposure needs to decrease and darken the image.")]
        public MinFloatParameter adaptationSpeedLightToDark = new(1f, 0.001f);

        [Tooltip("Exposure used after resets before the first valid metering sample is resolved.")]
        public FloatParameter initialExposureStops = new(0f);

        internal static float GetTargetMidGrayStops(AutoExposureTargetMidGray value)
        {
            switch (value)
            {
                case AutoExposureTargetMidGray.Grey125:
                    return k_Grey125Stops;
                case AutoExposureTargetMidGray.Grey14:
                    return k_Grey14Stops;
                default:
                    return k_Grey18Stops;
            }
        }

        public bool IsActive() => enabled.value;
        public bool IsTileCompatible() => false;
    }

    public enum AutoExposureMeteringMode
    {
        Average,
        Spot,
        CenterWeighted,
        MaskWeighted,
        ProceduralMask
    }

    public enum AutoExposureTargetMidGray
    {
        Grey125,
        Grey14,
        Grey18
    }

    public enum AutoExposureAdaptationMode
    {
        Fixed,
        Progressive
    }

    [Serializable]
    public sealed class AutoExposureMeteringModeParameter : VolumeParameter<AutoExposureMeteringMode>
    {
        public AutoExposureMeteringModeParameter(AutoExposureMeteringMode value, bool overrideState = false)
            : base(value, overrideState)
        {
        }
    }

    [Serializable]
    public sealed class AutoExposureTargetMidGrayParameter : VolumeParameter<AutoExposureTargetMidGray>
    {
        public AutoExposureTargetMidGrayParameter(AutoExposureTargetMidGray value, bool overrideState = false)
            : base(value, overrideState)
        {
        }
    }

    [Serializable]
    public sealed class AutoExposureAdaptationModeParameter : VolumeParameter<AutoExposureAdaptationMode>
    {
        public AutoExposureAdaptationModeParameter(AutoExposureAdaptationMode value, bool overrideState = false)
            : base(value, overrideState)
        {
        }
    }
}