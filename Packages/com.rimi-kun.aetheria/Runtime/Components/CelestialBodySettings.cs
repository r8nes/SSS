using UnityEngine;

namespace Aetheria.Runtime.Components
{
    public readonly struct CelestialBodySettings
    {
        public readonly bool InteractsWithSky;
        public readonly bool DiameterMultiplierMode;
        public readonly float DiameterMultiplier;
        public readonly float DiameterOverride;
        public readonly float AngularDiameter;
        public readonly CelestialBodyShadingSource ShadingSource;
        public readonly Color SurfaceTint;
        public readonly Texture2D SurfaceCookieTexture;
        public readonly float CookieRotation;
        public readonly float LimbDarkening;
        public readonly float FlareSize;
        public readonly float FlareFalloff;
        public readonly Light SunLightOverride;
        public readonly Color SunColor;
        public readonly float SunIntensity;
        public readonly float MoonLunarLambertWeight;
        public readonly float MoonPhase;
        public readonly float MoonPhaseRotation;
        public readonly float Earthshine;
        public readonly float Distance;

        public CelestialBodySettings(
            bool interactsWithSky,
            bool diameterMultiplierMode,
            float diameterMultiplier,
            float diameterOverride,
            float angularDiameter,
            CelestialBodyShadingSource shadingSource,
            Color surfaceTint,
            Texture2D surfaceCookieTexture,
            float cookieRotation,
            float limbDarkening,
            float flareSize,
            float flareFalloff,
            Light sunLightOverride,
            Color sunColor,
            float sunIntensity,
            float moonLunarLambertWeight,
            float moonPhase,
            float moonPhaseRotation,
            float earthshine,
            float distance)
        {
            InteractsWithSky = interactsWithSky;
            DiameterMultiplierMode = diameterMultiplierMode;
            DiameterMultiplier = Mathf.Max(0f, diameterMultiplier);
            DiameterOverride = Mathf.Max(0f, diameterOverride);
            AngularDiameter = angularDiameter;
            ShadingSource = shadingSource;
            SurfaceTint = surfaceTint;
            SurfaceCookieTexture = surfaceCookieTexture;
            CookieRotation = Mathf.Repeat(cookieRotation, 360f);
            LimbDarkening = Mathf.Clamp(limbDarkening, 0f, 2f);
            FlareSize = Mathf.Max(0f, flareSize);
            FlareFalloff = Mathf.Max(0f, flareFalloff);
            SunLightOverride = sunLightOverride;
            SunColor = sunColor;
            SunIntensity = Mathf.Max(0f, sunIntensity);
            MoonLunarLambertWeight = Mathf.Clamp01(moonLunarLambertWeight);
            MoonPhase = Mathf.Clamp01(moonPhase);
            MoonPhaseRotation = Mathf.Repeat(moonPhaseRotation, 360f);
            Earthshine = Mathf.Clamp01(earthshine);
            Distance = distance;
        }
    }
}
