using System;
using Aetheria.Runtime.Rendering.Sky;
using Aetheria.Runtime.Sky;
using UnityEngine;
using UnityEngine.Rendering;

namespace Aetheria.Runtime.Overrides
{
    [Serializable]
    [VolumeComponentMenu("Aetheria/Sky/HDRI Sky")]
    [SkyUniqueID((int)SkyType.HDRI)]
    public sealed class HDRISky : SkySettings
    {
        public enum DistortionMode
        {
            None,
            Procedural,
            Flowmap
        }

        [Serializable]
        public sealed class DistortionModeParameter : VolumeParameter<DistortionMode>
        {
            public DistortionModeParameter(DistortionMode value, bool overrideState = false)
                : base(value, overrideState) { }
        }

        public CubemapParameter hdriSky = new(null);
        public DistortionModeParameter distortionMode = new(DistortionMode.None);
        public Texture2DParameter flowmap = new(null);
        public BoolParameter upperHemisphereOnly = new(true);
        public WindOrientationParameter scrollOrientation = new();
        public WindSpeedParameter scrollSpeed = new();
        public FloatParameter sunInitialRotation = new(float.NegativeInfinity);
        public BoolParameter lockSun = new(false);

        public override bool IsActive() => active && hdriSky.value;

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = base.GetHashCode();
                hash = hash * 23 + hdriSky.GetHashCode();
                hash = hash * 23 + flowmap.GetHashCode();
                hash = hash * 23 + distortionMode.GetHashCode();
                hash = hash * 23 + upperHemisphereOnly.GetHashCode();
                hash = hash * 23 + scrollOrientation.GetHashCode();
                hash = hash * 23 + scrollSpeed.GetHashCode();
                return hash;
            }
        }

        public override bool SignificantlyDivergesFrom(SkySettings otherSettings) =>
            base.SignificantlyDivergesFrom(otherSettings) ||
            otherSettings is not HDRISky other ||
            hdriSky.value != other.hdriSky.value;

        public override Type GetSkyRendererType() => typeof(HDRISkyRenderer);
    }
}
