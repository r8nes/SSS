using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace Aetheria.Runtime.Components.Volumetrics
{
    public enum VolumeScaleMode
    {
        [InspectorName("Scale Invariant")]
        ScaleInvariant,
        [InspectorName("Inherit from Hierarchy")]
        InheritFromHierarchy,
    }

    [GenerateHLSL]
    public enum VolumeShapingMode
    {
        Simple = 0,
        Advanced = 1,
    }

    [GenerateHLSL]
    public enum VolumeFalloffMode
    {
        Linear = 0,
        Exponential = 1,
    }

    [GenerateHLSL]
    public enum VolumeSamplingMode
    {
        Smooth = 0,
        Crisp = 1,
    }

    [GenerateHLSL]
    public enum LocalMediumBlendMode
    {
        Overwrite = 0,
        Additive = 1,
        Multiply = 2,
        Min = 3,
        Max = 4,
    }

    [Serializable]
    public struct VolumeMaskParameters
    {
        public VolumeSamplingMode samplingMode;
        [Min(0f)] public float remapMin;
        [Min(0f)] public float remapMax;
        [Min(0.01f)] public float power;
        [Min(0f)] public float strength;

        public bool nearClampEnabled;
        [Min(0f)] public float nearClampStartDistance;
        [Min(0.0001f)] public float nearClampFadeDistance;

        public static VolumeMaskParameters Default() => new()
        {
            samplingMode = VolumeSamplingMode.Smooth,
            remapMin = 0f,
            remapMax = 1f,
            power = 1f,
            strength = 1f,
            nearClampEnabled = false,
            nearClampStartDistance = 0f,
            nearClampFadeDistance = 1f,
        };

        public void Constrain()
        {
            remapMin = Mathf.Clamp(remapMin, 0f, 1f - 1.0e-4f);
            remapMax = Mathf.Clamp(remapMax, remapMin + 1.0e-4f, 1f);

            power = Mathf.Max(0.01f, power);
            strength = Mathf.Max(0f, strength);
            nearClampStartDistance = Mathf.Max(0f, nearClampStartDistance);
            nearClampFadeDistance = Mathf.Max(0.0001f, nearClampFadeDistance);
        }
    }

    [Serializable]
    public struct VolumeTextureMask
    {
        public Texture3D texture;
        public Vector3 tiling;
        public Vector3 scrollingSpeed;
        public VolumeMaskParameters parameters;

        [SerializeField, HideInInspector]
        internal Vector3 offset;

        public static VolumeTextureMask Default(Vector3 tiling) => new()
        {
            texture = null,
            tiling = tiling,
            scrollingSpeed = Vector3.zero,
            parameters = VolumeMaskParameters.Default(),
            offset = Vector3.zero,
        };

        internal void Update(float time) =>
            offset = texture ? -(scrollingSpeed * time) : Vector3.zero;

        public void Constrain()
        {
            tiling = new Vector3(
                Mathf.Max(tiling.x, 1.0e-5f),
                Mathf.Max(tiling.y, 1.0e-5f),
                Mathf.Max(tiling.z, 1.0e-5f));
            parameters.Constrain();
        }
    }
}