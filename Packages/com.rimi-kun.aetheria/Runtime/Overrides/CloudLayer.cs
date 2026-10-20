using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Aetheria.Runtime.Overrides
{
    [Serializable]
    public enum CloudLayerState
    {
        Disabled,
        Enabled
    }

    [Serializable]
    public enum CloudMapMode
    {
        Single,
        Double
    }

    [Serializable]
    public enum CloudDistortionMode
    {
        None,
        [InspectorName("Horizontal")]
        Procedural
    }

    [Serializable]
    public sealed class CloudLayerStateParameter : VolumeParameter<CloudLayerState>
    {
        public CloudLayerStateParameter(CloudLayerState value, bool overrideState = false)
            : base(value, overrideState) {}
    }

    [Serializable]
    public sealed class CloudMapModeParameter : VolumeParameter<CloudMapMode>
    {
        public CloudMapModeParameter(CloudMapMode value, bool overrideState = false)
            : base(value, overrideState) {}
    }

    [Serializable]
    public sealed class CloudDistortionModeParameter : VolumeParameter<CloudDistortionMode>
    {
        public CloudDistortionModeParameter(CloudDistortionMode value, bool overrideState = false)
            : base(value, overrideState) {}
    }

    [Serializable, VolumeComponentMenu("Aetheria/Cloud Layer"), SupportedOnRenderPipeline(typeof(UniversalRenderPipelineAsset))]
    public sealed class CloudLayer : VolumeComponent, IPostProcessComponent
    {
        public enum ResolutionOption
        {
            Resolution256 = 256,
            Resolution512 = 512,
            Resolution1024 = 1024,
            Resolution2048 = 2048,
            Resolution4096 = 4096,
            Resolution8192 = 8192
        }

        [Serializable]
        public sealed class ResolutionParameter : VolumeParameter<ResolutionOption>
        {
            public ResolutionParameter(ResolutionOption value, bool overrideState = false)
                : base(value, overrideState) {}
        }

        [Serializable]
        public sealed class Map
        {
            [Tooltip("LatLong cloud opacity texture. The red, green, blue and alpha channels can be mixed independently.")]
            public TextureParameter Texture = new(null);

            [Tooltip("Opacity multiplier for the red channel.")]
            public ClampedFloatParameter OpacityR = new(1.0f, 0.0f, 1.0f);

            [Tooltip("Opacity multiplier for the green channel.")]
            public ClampedFloatParameter OpacityG = new(0.0f, 0.0f, 1.0f);

            [Tooltip("Opacity multiplier for the blue channel.")]
            public ClampedFloatParameter OpacityB = new(0.0f, 0.0f, 1.0f);

            [Tooltip("Opacity multiplier for the alpha channel.")]
            public ClampedFloatParameter OpacityA = new(0.0f, 0.0f, 1.0f);

            [Tooltip("Altitude of the bottom of the cloud layer above the planet surface, in meters.")]
            public MinFloatParameter Altitude = new(2000.0f, 0.0f);

            [Tooltip("Longitude rotation of the cloud map, in degrees.")]
            public ClampedFloatParameter Rotation = new(0.0f, 0.0f, 360.0f);

            [Tooltip("Tint applied to both direct and ambient lighting of the cloud layer.")]
            public ColorParameter Tint = new(Color.white, false, false, true);

            [InspectorName("Exposure Compensation"), Tooltip("Relative exposure in EV.")]
            public FloatParameter Exposure = new(0.0f);

            [Header("Wind")]
            [InspectorName("Wind"), Tooltip("Distortion mode used to simulate cloud movement.")]
            public CloudDistortionModeParameter DistortionMode = new(CloudDistortionMode.None);

            [InspectorName("Orientation"), Tooltip("Controls the orientation of the wind relative to the X world vector. This value can be relative to the Global Wind Orientation defined in the Visual Environment.")]
            public WindOrientationParameter scrollOrientation = new();

            [InspectorName("Speed"), Tooltip("Sets the wind speed in kilometers per hour. This value can be relative to the Global Wind Speed defined in the Visual Environment.")]
            public WindSpeedParameter scrollSpeed = new();

            [Header("Lighting")]
            [InspectorName("Raymarching"), Tooltip("Simulates cloud self-shadowing using raymarching.")]
            public BoolParameter SelfShadow = new(true);

            [Tooltip("Number of raymarching steps used while baking self-shadowing.")]
            public ClampedIntParameter LightSteps = new(6, 2, 32);

            [InspectorName("Density"), Tooltip("Density of the cloud layer.")]
            public ClampedFloatParameter Density = new(0.5f, 0.0f, 1.0f);

            [Tooltip("Controls the influence of the atmosphere ambient probe on the cloud layer.")]
            public ClampedFloatParameter AmbientProbeDimmer = new(1.0f, 0.0f, 1.0f);

            [NonSerialized] internal float ScrollFactor;

            public int BakedLightSteps => SelfShadow.value ? LightSteps.value : 0;

            public Vector4 Opacities => new(OpacityR.value, OpacityG.value, OpacityB.value, OpacityA.value);

            public Color EvaluatedTint => Tint.value.linear * ColorUtils.ConvertEV100ToExposure(-Exposure.value);

            public Vector4 GetBakeParameters()
            {
                return new Vector4(
                    Rotation.value / 360.0f,
                    BakedLightSteps,
                    Mathf.Lerp(0.005f, 0.1f, Density.value),
                    Altitude.value);
            }

            public Vector4 GetAnimationParameters(VisualEnvironment environment)
            {
                float angle = scrollOrientation.GetValue(environment) * Mathf.Deg2Rad;
                return new Vector4(
                    -Mathf.Cos(angle),
                    -Mathf.Sin(angle),
                    ScrollFactor,
                    (float)DistortionMode.value);
            }

            public void UpdateScroll(float deltaTime, VisualEnvironment environment)
            {
                if (DistortionMode.value == CloudDistortionMode.None)
                    return;

                ScrollFactor += scrollSpeed.GetValue(environment) * Mathf.Max(0.0f, deltaTime) * 0.277778f;
            }

            public void Override(Map state, float interpFactor)
            {
                if (state == null)
                    return;

                OverrideTextureParameter(Texture, state.Texture, interpFactor);
                OverrideFloatParameter(OpacityR, state.OpacityR, interpFactor);
                OverrideFloatParameter(OpacityG, state.OpacityG, interpFactor);
                OverrideFloatParameter(OpacityB, state.OpacityB, interpFactor);
                OverrideFloatParameter(OpacityA, state.OpacityA, interpFactor);
                OverrideFloatParameter(Altitude, state.Altitude, interpFactor);
                OverrideFloatParameter(Rotation, state.Rotation, interpFactor);
                OverrideColorParameter(Tint, state.Tint, interpFactor);
                OverrideFloatParameter(Exposure, state.Exposure, interpFactor);
                OverrideEnumParameter(DistortionMode, state.DistortionMode, interpFactor);
                OverrideWindParameter(scrollOrientation, state.scrollOrientation, interpFactor);
                OverrideWindParameter(scrollSpeed, state.scrollSpeed, interpFactor);
                OverrideBoolParameter(SelfShadow, state.SelfShadow, interpFactor);
                OverrideIntParameter(LightSteps, state.LightSteps, interpFactor);
                OverrideFloatParameter(Density, state.Density, interpFactor);
                OverrideFloatParameter(AmbientProbeDimmer, state.AmbientProbeDimmer, interpFactor);
            }

            public int GetBakeHashCode(Texture resolvedTexture)
            {
                var hash = new HashCode();
                hash.Add(resolvedTexture ? resolvedTexture.GetInstanceID() : 0);
                hash.Add(OpacityR.value);
                hash.Add(OpacityG.value);
                hash.Add(OpacityB.value);
                hash.Add(OpacityA.value);
                hash.Add(Rotation.value);
                hash.Add(SelfShadow.value);

                if (SelfShadow.value)
                {
                    hash.Add(LightSteps.value);
                    hash.Add(Altitude.value);
                    hash.Add(Density.value);
                }

                return hash.ToHashCode();
            }

            public override int GetHashCode()
            {
                var hash = new HashCode();
                hash.Add(Texture.value ? Texture.value.GetInstanceID() : 0);
                hash.Add(OpacityR.value);
                hash.Add(OpacityG.value);
                hash.Add(OpacityB.value);
                hash.Add(OpacityA.value);
                hash.Add(Altitude.value);
                hash.Add(Rotation.value);
                hash.Add(Tint.value);
                hash.Add(Exposure.value);
                hash.Add(DistortionMode.value);
                hash.Add(scrollOrientation.GetHashCode());
                hash.Add(scrollSpeed.GetHashCode());
                hash.Add(SelfShadow.value);
                hash.Add(LightSteps.value);
                hash.Add(Density.value);
                hash.Add(AmbientProbeDimmer.value);
                return hash.ToHashCode();
            }

            private static void OverrideTextureParameter(TextureParameter source, TextureParameter state, float interpFactor)
            {
                if (!source.overrideState)
                    return;

                state.overrideState = true;
                if (interpFactor > 0.0f)
                    state.value = source.value;
            }

            private static void OverrideFloatParameter(FloatParameter source, FloatParameter state, float interpFactor)
            {
                if (!source.overrideState)
                    return;

                state.overrideState = true;
                state.value = Mathf.LerpUnclamped(state.value, source.value, interpFactor);
            }

            private static void OverrideWindParameter(WindParameter source, WindParameter state, float interpFactor)
            {
                if (!source.overrideState)
                    return;

                state.overrideState = true;
                state.Interp(state.value, source.value, interpFactor);
            }

            private static void OverrideIntParameter(IntParameter source, IntParameter state, float interpFactor)
            {
                if (!source.overrideState)
                    return;

                state.overrideState = true;
                state.value = Mathf.RoundToInt(Mathf.LerpUnclamped(state.value, source.value, interpFactor));
            }

            private static void OverrideBoolParameter(BoolParameter source, BoolParameter state, float interpFactor)
            {
                if (!source.overrideState)
                    return;

                state.overrideState = true;
                if (interpFactor > 0.0f)
                    state.value = source.value;
            }

            private static void OverrideColorParameter(ColorParameter source, ColorParameter state, float interpFactor)
            {
                if (!source.overrideState)
                    return;

                state.overrideState = true;
                state.value = Color.LerpUnclamped(state.value, source.value, interpFactor);
            }

            private static void OverrideEnumParameter<T>(VolumeParameter<T> source, VolumeParameter<T> state, float interpFactor)
            {
                if (!source.overrideState)
                    return;

                state.overrideState = true;
                if (interpFactor > 0.0f)
                    state.value = source.value;
            }
        }

        [Header("General"), Tooltip("Enable or disable the cloud layer.")]
        public CloudLayerStateParameter State = new(CloudLayerState.Disabled, overrideState: true);

        [Tooltip("Global opacity of the cloud layer.")]
        public ClampedFloatParameter Opacity = new(1.0f, 0.0f, 1.0f);

        [Tooltip("Render the layer only above the local horizon of the current planet position.")]
        public BoolParameter UpperHemisphereOnly = new(true);

        [Tooltip("Number of cloud map layers.")]
        public CloudMapModeParameter Layers = new(CloudMapMode.Single);

        [AdditionalProperty, Tooltip("Resolution of the baked cloud-layer texture.")]
        public ResolutionParameter Resolution = new(ResolutionOption.Resolution1024);

        [Header("Cloud Map A")]
        public Map LayerA = new();

        [Header("Cloud Map B")]
        public Map LayerB = new();

        public int LayerCount => Layers.value == CloudMapMode.Double ? 2 : 1;

        public bool IsActive() =>
            active && State.value == CloudLayerState.Enabled && Opacity.value > 1.0e-5f;

        public bool IsTileCompatible() => false;

        public override void Override(VolumeComponent state, float interpFactor)
        {
            base.Override(state, interpFactor);

            if (state is not CloudLayer cloudLayer)
                return;

            LayerA.Override(cloudLayer.LayerA, interpFactor);
            LayerB.Override(cloudLayer.LayerB, interpFactor);
        }

        public int GetBakeHashCode(
            Texture resolvedTextureA,
            Texture resolvedTextureB,
            Vector3 lightDirectionWs,
            float planetRadiusMeters)
        {
            var hash = new HashCode();
            hash.Add(UpperHemisphereOnly.value);
            hash.Add(Layers.value);
            hash.Add(Resolution.value);
            hash.Add(LayerA.GetBakeHashCode(resolvedTextureA));

            bool lighting = LayerA.SelfShadow.value;
            if (LayerCount == 2)
            {
                hash.Add(LayerB.GetBakeHashCode(resolvedTextureB));
                lighting |= LayerB.SelfShadow.value;
            }

            hash.Add(planetRadiusMeters);

            if (lighting)
            {
                var direction = lightDirectionWs.sqrMagnitude > 0.0f
                    ? lightDirectionWs.normalized
                    : Vector3.zero;
                int angularResolution = (int)Resolution.value;
                hash.Add(new Vector2Int(
                    Mathf.RoundToInt(
                        Mathf.Atan2(direction.x, direction.z) * angularResolution / (2.0f * Mathf.PI)),
                    Mathf.RoundToInt(
                        Mathf.Asin(Mathf.Clamp(direction.y, -1.0f, 1.0f)) * angularResolution / Mathf.PI)));
            }

            return hash.ToHashCode();
        }

        public override int GetHashCode()
        {
            var hash = new HashCode();
            hash.Add(State.value);
            hash.Add(Opacity.value);
            hash.Add(UpperHemisphereOnly.value);
            hash.Add(Layers.value);
            hash.Add(Resolution.value);
            hash.Add(LayerA.GetHashCode());
            if (LayerCount == 2)
                hash.Add(LayerB.GetHashCode());
            return hash.ToHashCode();
        }
    }
}
