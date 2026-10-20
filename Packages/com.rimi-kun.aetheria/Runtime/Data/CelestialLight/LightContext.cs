using Aetheria.Runtime.Components;
using UnityEngine;

namespace Aetheria.Runtime.Data.CelestialLight
{
    public readonly struct LightContext
    {
        public readonly bool IsValid;
        public readonly Light Light;
        public readonly CelestialBody Body;
        public readonly Vector3 ForwardWS;
        public readonly Vector3 DirectionToLightWS;
        public readonly Color RawLightColor;
        public readonly Color SurfaceLightColor;
        public readonly Color CloudSunColor;

        internal LightContext(
            in CelestialLight source,
            Color rawLightColor,
            Color surfaceLightColor,
            Color cloudSunColor)
        {
            IsValid = source.IsValid;
            Light = source.Light;
            Body = source.Body;
            ForwardWS = source.ForwardWS;
            DirectionToLightWS = source.DirectionToLightWS;
            RawLightColor = rawLightColor;
            SurfaceLightColor = surfaceLightColor;
            CloudSunColor = cloudSunColor;
        }
    }
}
