using Aetheria.Runtime.Components;
using UnityEngine;
using UnityEngine.Rendering;

namespace Aetheria.Runtime.Data.CelestialLight
{
    internal static class CelestialLightMath
    {
        internal static Vector3 NormalizeOr(Vector3 value, Vector3 defaultValue) =>
            value.sqrMagnitude > 1.0e-6f ? value.normalized : defaultValue;
    }

    public readonly struct CelestialLight
    {
        private readonly struct Source
        {
            public readonly Light Light;
            public readonly Vector3 Forward;

            public Source(Light light, Vector3 forward)
            {
                Light = light;
                Forward = forward;
            }
        }

        public readonly bool IsValid;
        internal readonly int VisibleLightIndex;
        internal readonly VisibleLight VisibleLight;
        public readonly Light Light;
        public readonly CelestialBody Body;
        public readonly Vector3 ForwardWS;
        public readonly Vector3 DirectionToLightWS;
        public readonly Color RawColor;
        public readonly Color Color;
        public readonly float Luminance;

        public CelestialLight(
            int visibleLightIndex,
            in VisibleLight visibleLight,
            CelestialBody celestialBody,
            Color rawColor)
            : this(true, visibleLightIndex, visibleLight, celestialBody, ResolveSource(visibleLight), rawColor)
        {
        }

        public CelestialLight(CelestialBody celestialBody, Color rawColor)
            : this(celestialBody, -1, default, celestialBody, ResolveSource(celestialBody), rawColor)
        {
        }

        private CelestialLight(
            bool isValid,
            int visibleLightIndex,
            VisibleLight visibleLight,
            CelestialBody celestialBody,
            Source source,
            Color rawColor)
        {
            IsValid = isValid;
            VisibleLightIndex = visibleLightIndex;
            VisibleLight = visibleLight;
            Light = source.Light;
            Body = celestialBody;
            ForwardWS = CelestialLightMath.NormalizeOr(source.Forward, Vector3.forward);
            DirectionToLightWS = -ForwardWS;
            RawColor = rawColor;
            Color = rawColor;
            Luminance = Mathf.Max(rawColor.r, Mathf.Max(rawColor.g, rawColor.b));
        }

        private static Source ResolveSource(in VisibleLight visibleLight)
        {
            var light = visibleLight.light;
            
            var forward = light
                ? light.transform.forward
                : (Vector3)visibleLight.localToWorldMatrix.GetColumn(2);
            
            return new Source(light, forward);
        }

        private static Source ResolveSource(CelestialBody celestialBody)
        {
            var light = celestialBody ? celestialBody.GetComponent<Light>() : null;
            var transform = light ? light.transform : celestialBody ? celestialBody.transform : null;
            return new Source(light, transform ? transform.forward : Vector3.forward);
        }
    }
}