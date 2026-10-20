using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using Aetheria.Runtime.Rendering;

namespace Aetheria.Runtime.Sky
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
    public sealed class SkyUniqueID : Attribute
    {
        internal readonly int UniqueID;

        public SkyUniqueID(int uniqueID) => UniqueID = uniqueID;
    }

    public enum SkyType
    {
        None = 0,
        HDRI = 1,
        PhysicallyBased = 4
    }

    public enum SkyIntensityMode
    {
        Exposure,
        Multiplier
    }

    [Serializable]
    public sealed class SkyIntensityParameter : VolumeParameter<SkyIntensityMode>
    {
        public SkyIntensityParameter(SkyIntensityMode value, bool overrideState = false)
            : base(value, overrideState) { }
    }

    [Serializable]
    public sealed class EnvUpdateParameter : VolumeParameter<EnvironmentUpdateMode>
    {
        public EnvUpdateParameter(EnvironmentUpdateMode value, bool overrideState = false)
            : base(value, overrideState) { }
    }

    public abstract class SkySettings : VolumeComponent
    {
        public ClampedFloatParameter rotation = new(0.0f, 0.0f, 360.0f);
        public SkyIntensityParameter skyIntensityMode = new(SkyIntensityMode.Exposure);
        public FloatParameter exposure = new(0.0f);
        public MinFloatParameter multiplier = new(1.0f, 0.0f);
        public EnvUpdateParameter updateMode = new(EnvironmentUpdateMode.OnChanged);
        public MinFloatParameter updatePeriod = new(0.0f, 0.0f);
        public BoolParameter includeSunInBaking = new(false);

        private static readonly Dictionary<Type, int> UniqueIDs = new();

        public virtual bool IsActive() => active;

        public virtual int GetHashCode(Camera camera) => GetHashCode();

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = 13;
                hash = hash * 23 + rotation.GetHashCode();
                hash = hash * 23 + exposure.GetHashCode();
                hash = hash * 23 + multiplier.GetHashCode();
                hash = hash * 23 + skyIntensityMode.GetHashCode();
                hash = hash * 23 + includeSunInBaking.GetHashCode();
                return hash;
            }
        }

        public static int GetUniqueID<T>() => GetUniqueID(typeof(T));

        public static int GetUniqueID(Type type)
        {
            if (UniqueIDs.TryGetValue(type, out int uniqueID))
                return uniqueID;

            var attributes = type.GetCustomAttributes(typeof(SkyUniqueID), false);
            uniqueID = attributes.Length == 0 ? -1 : ((SkyUniqueID)attributes[0]).UniqueID;
            UniqueIDs[type] = uniqueID;
            return uniqueID;
        }

        public float GetIntensityFromSettings()
        {
            return skyIntensityMode.value switch
            {
                SkyIntensityMode.Exposure => Mathf.Pow(2.0f, exposure.value),
                SkyIntensityMode.Multiplier => multiplier.value,
                _ => 1.0f
            };
        }

        public virtual bool SignificantlyDivergesFrom(SkySettings otherSettings)
        {
            if (otherSettings == null || otherSettings.GetSkyRendererType() != GetSkyRendererType())
                return true;

            float thisIntensity = Mathf.Max(GetIntensityFromSettings(), 1e-6f);
            float otherIntensity = Mathf.Max(otherSettings.GetIntensityFromSettings(), 1e-6f);
            float ratio = Mathf.Max(thisIntensity / otherIntensity, otherIntensity / thisIntensity);
            return ratio > 3.0f;
        }

        internal virtual Color EvaluateSurfaceLightColor(Color color, Vector3 lightDirection, Vector3 samplePosition) =>
            color;

        public abstract Type GetSkyRendererType();
    }
}
