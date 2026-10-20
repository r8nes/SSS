using System;
using Aetheria.Runtime.Sky;
using Aetheria.Runtime.Rendering;
using UnityEngine;
using UnityEngine.Rendering;

namespace Aetheria.Runtime.Overrides
{
    public enum RenderingSpace
    {
        Camera,
        World
    }

    [Serializable]
    [VolumeComponentMenu("Aetheria/Visual Environment")]
    public sealed class VisualEnvironment : VolumeComponent
    {
        public enum PlanetMode
        {
            Automatic,
            Manual
        }

        public const float DefaultPlanetRadiusKm = 6360.0f;

        [Header("Sky")]
        public NoInterpIntParameter skyType = new((int)SkyType.None);
        public EnumParameter<LightingSkyResolution> lightingSkyResolution =
            new(LightingSkyResolution.Resolution512);

        [Header("Planet")]
        public MinFloatParameter planetRadius = new(DefaultPlanetRadiusKm, 0.0f);
        public EnumParameter<RenderingSpace> renderingSpace = new(RenderingSpace.World);
        public EnumParameter<PlanetMode> centerMode = new(PlanetMode.Automatic);
        public Vector3Parameter planetCenter = new(new Vector3(0.0f, -DefaultPlanetRadiusKm * 1000.0f, 0.0f));

        [Header("Wind")]
        public ClampedFloatParameter windOrientation = new(0.0f, 0.0f, 360.0f);
        public FloatParameter windSpeed = new(0.0f);

        public float PlanetRadiusMeters => planetRadius.value * 1000.0f;

        public Vector3 GetPlanetCenterWS(Vector3 cameraPositionWs)
        {
            if (renderingSpace.value == RenderingSpace.Camera)
                return new Vector3(cameraPositionWs.x, cameraPositionWs.y - PlanetRadiusMeters, cameraPositionWs.z);

            return centerMode.value == PlanetMode.Manual
                ? planetCenter.value
                : new Vector3(0.0f, -PlanetRadiusMeters, 0.0f);
        }

        public override int GetHashCode()
        {
            var hash = new HashCode();
            hash.Add(skyType.value);
            hash.Add(lightingSkyResolution.value);
            hash.Add(planetRadius.value);
            hash.Add(renderingSpace.value);
            hash.Add(centerMode.value);
            if (centerMode.value == PlanetMode.Manual)
                hash.Add(planetCenter.value);
            hash.Add(windOrientation.value);
            hash.Add(windSpeed.value);
            return hash.ToHashCode();
        }
    }

    [Serializable]
    public abstract class WindParameter : VolumeParameter<WindParameter.WindParamaterValue>
    {
        public enum WindOverrideMode
        {
            Custom,
            Global,
            Additive,
            Multiply
        }

        [Serializable]
        public struct WindParamaterValue
        {
            public WindOverrideMode mode;
            public float customValue;
            public float additiveValue;
            public float multiplyValue;

            public override string ToString()
            {
                if (mode == WindOverrideMode.Global)
                    return mode.ToString();

                string valueString = mode switch
                {
                    WindOverrideMode.Custom => customValue.ToString(),
                    WindOverrideMode.Additive => additiveValue.ToString(),
                    WindOverrideMode.Multiply => multiplyValue.ToString(),
                    _ => string.Empty
                };
                return valueString + " (" + mode + ")";
            }
        }

        protected WindParameter(
            float value = 0.0f,
            WindOverrideMode mode = WindOverrideMode.Global,
            bool overrideState = false)
            : base(default, overrideState)
        {
            this.value = new WindParamaterValue
            {
                mode = mode,
                customValue = mode <= WindOverrideMode.Global ? value : 0.0f,
                additiveValue = mode == WindOverrideMode.Additive ? value : 0.0f,
                multiplyValue = mode == WindOverrideMode.Multiply ? value : 1.0f
            };
        }

        public override void Interp(WindParamaterValue from, WindParamaterValue to, float t)
        {
            m_Value.mode = t > 0.0f ? to.mode : from.mode;
            m_Value.customValue = from.customValue + (to.customValue - from.customValue) * t;
            m_Value.additiveValue = from.additiveValue + (to.additiveValue - from.additiveValue) * t;
            m_Value.multiplyValue = from.multiplyValue + (to.multiplyValue - from.multiplyValue) * t;
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = 17;
                hash = hash * 23 + overrideState.GetHashCode();
                hash = hash * 23 + value.mode.GetHashCode();
                hash = hash * 23 + value.customValue.GetHashCode();
                hash = hash * 23 + value.additiveValue.GetHashCode();
                hash = hash * 23 + value.multiplyValue.GetHashCode();
                return hash;
            }
        }

        public virtual float GetValue(VisualEnvironment environment)
        {
            if (value.mode == WindOverrideMode.Custom)
                return value.customValue;

            float globalValue = GetGlobalValue(environment);
            if (value.mode == WindOverrideMode.Additive)
                return globalValue + value.additiveValue;
            if (value.mode == WindOverrideMode.Multiply)
                return globalValue * value.multiplyValue;
            return globalValue;
        }

        protected abstract float GetGlobalValue(VisualEnvironment environment);
    }

    [Serializable]
    public sealed class WindOrientationParameter : WindParameter
    {
        public WindOrientationParameter(
            float value = 0.0f,
            WindOverrideMode mode = WindOverrideMode.Global,
            bool overrideState = false)
            : base(value, mode, overrideState) { }

        protected override float GetGlobalValue(VisualEnvironment environment) =>
            environment ? environment.windOrientation.value : 0.0f;

        public override void Interp(WindParamaterValue from, WindParamaterValue to, float t)
        {
            m_Value.multiplyValue = 0.0f;
            m_Value.mode = t > 0.0f ? to.mode : from.mode;
            m_Value.additiveValue = from.additiveValue + (to.additiveValue - from.additiveValue) * t;
            m_Value.customValue = LerpDegrees(from.customValue, to.customValue, t);
        }

        public override float GetValue(VisualEnvironment environment)
        {
            if (value.mode == WindOverrideMode.Multiply)
                throw new NotSupportedException("Wind orientation does not support Multiply mode.");
            return base.GetValue(environment);
        }

        private static float LerpDegrees(float from, float to, float t)
        {
            float delta = Mathf.DeltaAngle(from, to);
            return Mathf.Repeat(from + delta * t, 360.0f);
        }
    }

    [Serializable]
    public sealed class WindSpeedParameter : WindParameter
    {
        public WindSpeedParameter(
            float value = 100.0f,
            WindOverrideMode mode = WindOverrideMode.Global,
            bool overrideState = false)
            : base(value, mode, overrideState) { }

        protected override float GetGlobalValue(VisualEnvironment environment) =>
            environment ? environment.windSpeed.value : 0.0f;
    }
}
