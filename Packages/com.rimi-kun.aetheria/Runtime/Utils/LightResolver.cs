using System.Collections.Generic;
using Aetheria.Runtime.Components;
using Aetheria.Runtime.Data;
using Aetheria.Runtime.Data.CelestialLight;
using Aetheria.Runtime.Overrides;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Aetheria.Runtime.Utils
{
    public static class LightResolver
    {
        public const float SkyIlluminanceScale = Mathf.PI;

        internal readonly struct FrameResolution
        {
            public readonly CelestialLight PrimaryLight;
            public readonly CelestialLight BrightestLight;
            public readonly CelestialLight SecondBrightestLight;

            public FrameResolution(
                in CelestialLight primaryLight,
                in CelestialLight brightestLight,
                in CelestialLight secondBrightestLight)
            {
                PrimaryLight = primaryLight;
                BrightestLight = brightestLight;
                SecondBrightestLight = secondBrightestLight;
            }
        }

        internal static FrameResolution ResolveFrame(
            UniversalLightData lightData,
            IReadOnlyList<CelestialBody> registeredBodies,
            List<CelestialLight> resolvedBodies)
        {
            resolvedBodies.Clear();

            CelestialLight urpMainLight = default;
            CelestialLight brightest = default;
            CelestialLight secondBrightest = default;
            float brightestLuminance = float.MinValue;
            float secondBrightestLuminance = float.MinValue;

            for (int i = 0; i < registeredBodies.Count; i++)
            {
                if (!TryResolveBody(lightData, registeredBodies[i], out var resolved))
                    continue;

                resolvedBodies.Add(resolved);

                if (!resolved.Body.InteractsWithSky || resolved.Luminance <= 0f)
                    continue;

                if (resolved.VisibleLightIndex == lightData.mainLightIndex)
                    urpMainLight = resolved;

                if (resolved.Luminance > brightestLuminance)
                {
                    secondBrightest = brightest;
                    secondBrightestLuminance = brightestLuminance;
                    brightest = resolved;
                    brightestLuminance = resolved.Luminance;
                }
                else if (resolved.Luminance > secondBrightestLuminance)
                {
                    secondBrightest = resolved;
                    secondBrightestLuminance = resolved.Luminance;
                }
            }

            var primary = urpMainLight.IsValid ? urpMainLight : brightest;
            return new FrameResolution(primary, brightest, secondBrightest);
        }

        internal static bool TryResolveContext(
            in CelestialLight source,
            PhysicallyBasedSky volume,
            VisualEnvironment environment,
            Camera camera,
            out LightContext context)
        {
            context = default;
            return camera && TryResolveContext(source, volume, environment, camera.transform.position, out context);
        }

        internal static bool TryResolveContext(
            in CelestialLight source,
            PhysicallyBasedSky volume,
            VisualEnvironment environment,
            Vector3 samplePosition,
            out LightContext context)
        {
            context = default;

            if (!source.IsValid || !source.Light || !volume || !volume.IsActive() || !environment)
                return false;

            var surface = EvaluateSurfaceLightColor(
                source.VisibleLight.finalColor, source.DirectionToLightWS, samplePosition, volume, environment);
            var cloud = AtmosphereTools.SanitizeColor(source.RawColor);

            context = new LightContext(source, source.RawColor, surface, cloud);
            return true;
        }

        internal static bool TryResolveContext(in CelestialLight source, out LightContext context)
        {
            context = default;

            if (!source.IsValid)
                return false;

            var surface = source.VisibleLightIndex >= 0
                ? AtmosphereTools.SanitizeColor(source.VisibleLight.finalColor)
                : source.RawColor;
            var raw = AtmosphereTools.SanitizeColor(source.RawColor);
            context = new LightContext(source, raw, surface, raw);
            return true;
        }

        internal static bool IsUrpMainLight(UniversalLightData lightData, in CelestialLight source) =>
            source is { IsValid: true, VisibleLightIndex: >= 0 } &&
            source.VisibleLightIndex == lightData.mainLightIndex;

        internal static void ApplySurfaceLightToUrp(
            UniversalLightData lightData,
            in CelestialLight source,
            in LightContext context)
        {
            if (!source.IsValid || !context.IsValid || !context.Light)
                return;

            Vector4 surfaceLight = context.SurfaceLightColor;
            surfaceLight.w = ResolveSubtractiveFlag(context.Light, surfaceLight.w);

            SetVisibleLightFinalColor(lightData, source.VisibleLightIndex, surfaceLight);
        }

        internal static bool TryResolveByLight(
            UniversalLightData lightData,
            Light light,
            out CelestialLight resolved)
        {
            resolved = default;

            if (!light)
                return false;

            for (int i = 0; i < lightData.visibleLights.Length; i++)
            {
                var visibleLight = lightData.visibleLights[i];

                if (visibleLight.light != light)
                    continue;

                return TryResolveVisibleLight(visibleLight, i, out resolved);
            }

            return false;
        }

        private static bool TryResolveBody(
            UniversalLightData lightData,
            CelestialBody celestial,
            out CelestialLight resolved)
        {
            resolved = default;

            if (!celestial || !celestial.isActiveAndEnabled)
                return false;

            if (celestial.InteractsWithSky)
            {
                var light = celestial.GetComponent<Light>();
                
                if (!light || !light.enabled || light.type != LightType.Directional)
                    return false;

                return TryResolveByLight(lightData, light, out resolved);
            }

            if (!ShouldRenderVisualBody(celestial))
                return false;

            resolved = new CelestialLight(celestial, ResolveVisualBodyColor(celestial));
            return resolved.IsValid;
        }

        private static bool TryResolveVisibleLight(
            in VisibleLight visibleLight,
            int visibleLightIndex,
            out CelestialLight resolved)
        {
            resolved = default;

            if (!TryGetCelestialLight(visibleLight, out var celestial))
                return false;

            resolved = new CelestialLight(
                visibleLightIndex,
                visibleLight,
                celestial,
                RawSkyColor(visibleLight));
            
            return true;
        }

        private static bool TryGetCelestialLight(in VisibleLight visibleLight, out CelestialBody celestial)
        {
            celestial = null;

            var light = visibleLight.light;
            
            if (!light || !light.enabled || visibleLight.lightType != LightType.Directional)
                return false;
            if (!light.TryGetComponent(out celestial))
                return false;

            return CelestialBody.IsRegistrable(celestial) && celestial.InteractsWithSky;
        }

        private static bool ShouldRenderVisualBody(CelestialBody celestial)
        {
            if (!celestial || !celestial.isActiveAndEnabled || celestial.InteractsWithSky)
                return false;
            if (celestial.ShadingSource == CelestialBodyShadingSource.Manual)
                return true;

            var light = celestial.GetComponent<Light>();
            return light && light.enabled;
        }

        private static Color ResolveVisualBodyColor(CelestialBody celestial)
        {
            if (!celestial)
                return Color.black;

            if (celestial.ShadingSource == CelestialBodyShadingSource.Manual)
                return AtmosphereTools.SanitizeColor(
                    celestial.SunColor * Mathf.Max(0f, celestial.SunIntensity));

            var light = celestial.GetComponent<Light>();
            
            if (!light || !light.enabled)
                return Color.black;

            return AtmosphereTools.SanitizeColor(
                light.color * Mathf.Max(0f, light.intensity) * SkyIlluminanceScale);
        }

        private static void SetVisibleLightFinalColor(
            UniversalLightData lightData,
            int lightIndex,
            Vector4 surfaceLight)
        {
            if (lightIndex < 0 ||
                !lightData.visibleLights.IsCreated ||
                lightIndex >= lightData.visibleLights.Length)
                return;

            var visibleLight = lightData.visibleLights[lightIndex];
            
            visibleLight.finalColor = new Color(
                Mathf.Max(0f, surfaceLight.x),
                Mathf.Max(0f, surfaceLight.y),
                Mathf.Max(0f, surfaceLight.z),
                surfaceLight.w);
            
            lightData.visibleLights[lightIndex] = visibleLight;
        }

        private static float ResolveSubtractiveFlag(Light light, float fallback)
        {
            if (!light)
                return fallback;

            var baking = light.bakingOutput;
            return baking is
            {
                isBaked: true,
                lightmapBakeType: LightmapBakeType.Mixed,
                mixedLightingMode: MixedLightingMode.Subtractive
            }
                ? 0f
                : 1f;
        }


        internal static Color EvaluateSurfaceLightColor(
            Color color,
            Vector3 directionToLightWs,
            Vector3 samplePosition,
            PhysicallyBasedSky volume,
            VisualEnvironment environment)
        {
            var transmittance = AtmosphereData.EvaluateTransmittance(
                volume, environment, samplePosition, directionToLightWs);
            return ApplyArtisticEnvironmentLight(
                ApplyAtmosphericTransmittance(AtmosphereTools.SanitizeColor(color), transmittance),
                directionToLightWs,
                AtmospherePlanet.GetUpVectorWs(environment, samplePosition, samplePosition),
                volume);
        }

        private static Color ApplyArtisticEnvironmentLight(
            Color color,
            Vector3 directionToLightWs,
            Vector3 upWs,
            PhysicallyBasedSky volume)
        {
            if (!volume.ArtisticSkyEnabled.value || !volume.AffectEnvironment.value)
                return color;

            float mix = Mathf.Clamp01(volume.AffectEnvironmentMix.value);
            if (mix <= 0f)
                return color;

            float sunElevation = Vector3.Dot(directionToLightWs.normalized, upWs.normalized);
            Vector3 phase = ComputeArtisticPhase(sunElevation);
            Vector3 phaseWeights = ResolveArtisticPhaseWeights(
                volume.ArtisticSkyPhaseMode.value,
                volume.ArtisticSkyPhaseWeights.value);
            float phaseWeight = Mathf.Clamp01(Vector3.Dot(phase, Clamp01(phaseWeights)));

            Color artistic = color;
            float luma = Luminance(artistic);
            float saturation = Mathf.Lerp(1f, Mathf.Max(volume.SkySaturation.value, 0f), phaseWeight);
            artistic.r = Mathf.Lerp(luma, artistic.r, saturation);
            artistic.g = Mathf.Lerp(luma, artistic.g, saturation);
            artistic.b = Mathf.Lerp(luma, artistic.b, saturation);

            float contrast = Mathf.Lerp(1f, Mathf.Max(volume.SkyContrast.value, 0f), phaseWeight);
            artistic.r = Mathf.Max((artistic.r - 0.18f) * contrast + 0.18f, 0f);
            artistic.g = Mathf.Max((artistic.g - 0.18f) * contrast + 0.18f, 0f);
            artistic.b = Mathf.Max((artistic.b - 0.18f) * contrast + 0.18f, 0f);

            artistic = ApplyLuminancePreservingHue(
                artistic,
                volume.SkyHueColor.value.linear,
                volume.SkyHueIntensity.value * phaseWeight);

            float horizonMask = Mathf.Pow(
                Mathf.Clamp01(1f - Mathf.Abs(Vector3.Dot(directionToLightWs.normalized, upWs.normalized))),
                2f);
            artistic = ApplyLuminancePreservingHue(
                artistic,
                volume.HorizonHueColor.value.linear,
                volume.HorizonHueIntensity.value * phaseWeight * horizonMask);

            if (volume.ArtisticSunsetBoostEnabled.value)
            {
                Vector3 sunsetWeights = ResolveArtisticPhaseWeights(
                    volume.ArtisticSunsetPhaseMode.value,
                    volume.ArtisticSunsetPhaseWeights.value);
                float sunsetPhase = Mathf.Clamp01(Vector3.Dot(phase, Clamp01(sunsetWeights)));
                float sunsetBoost = volume.SunsetIntensity.value * sunsetPhase * horizonMask;
                artistic.r *= 1f + sunsetBoost * 1.75f;
                artistic.g *= 1f + sunsetBoost * 1.75f;
                artistic.b *= 1f + sunsetBoost * 1.75f;
                float boostedLuma = Luminance(artistic);
                float boostedSaturation = 1f + sunsetBoost * 0.55f;
                artistic.r = Mathf.Max(Mathf.Lerp(boostedLuma, artistic.r, boostedSaturation), 0f);
                artistic.g = Mathf.Max(Mathf.Lerp(boostedLuma, artistic.g, boostedSaturation), 0f);
                artistic.b = Mathf.Max(Mathf.Lerp(boostedLuma, artistic.b, boostedSaturation), 0f);
            }

            color.r = Mathf.Lerp(color.r, artistic.r, mix);
            color.g = Mathf.Lerp(color.g, artistic.g, mix);
            color.b = Mathf.Lerp(color.b, artistic.b, mix);
            return AtmosphereTools.SanitizeColor(color);
        }

        private static Vector3 ComputeArtisticPhase(float sunElevation)
        {
            float zenith = SmoothStep(0.18f, 0.65f, sunElevation);
            float sunset = 1f - SmoothStep(0.03f, 0.35f, Mathf.Abs(sunElevation));
            float night = 1f - SmoothStep(-0.22f, 0.02f, sunElevation);
            Vector3 phase = new(zenith, sunset, night);
            float sum = Mathf.Max(phase.x + phase.y + phase.z, 1e-4f);
            return phase / sum;
        }

        private static Vector3 ResolveArtisticPhaseWeights(
            AtmosphereArtisticPhaseMode mode,
            Vector3 custom) =>
            mode switch
            {
                AtmosphereArtisticPhaseMode.ZenithWeighted => new Vector3(1f, 0.35f, 0.1f),
                AtmosphereArtisticPhaseMode.SunsetWeighted => new Vector3(0.35f, 1f, 0.35f),
                AtmosphereArtisticPhaseMode.NightWeighted => new Vector3(0.1f, 0.35f, 1f),
                AtmosphereArtisticPhaseMode.Custom => Vector3.Max(custom, Vector3.zero),
                _ => Vector3.one
            };

        private static Color ApplyLuminancePreservingHue(Color color, Color hue, float amount)
        {
            float luminance = Luminance(color);
            hue.r = Mathf.Max(hue.r, 0f);
            hue.g = Mathf.Max(hue.g, 0f);
            hue.b = Mathf.Max(hue.b, 0f);
            float hueLuminance = Mathf.Max(Luminance(hue), 1e-5f);
            float scale = luminance / hueLuminance;
            float t = Mathf.Clamp01(amount);
            color.r = Mathf.Lerp(color.r, hue.r * scale, t);
            color.g = Mathf.Lerp(color.g, hue.g * scale, t);
            color.b = Mathf.Lerp(color.b, hue.b * scale, t);
            return color;
        }

        private static float Luminance(Color color) =>
            color.r * 0.2126f + color.g * 0.7152f + color.b * 0.0722f;

        private static Vector3 Clamp01(Vector3 value) =>
            new(Mathf.Clamp01(value.x), Mathf.Clamp01(value.y), Mathf.Clamp01(value.z));

        private static float SmoothStep(float min, float max, float value)
        {
            float t = Mathf.Clamp01((value - min) / (max - min));
            return t * t * (3f - 2f * t);
        }

        private static Color ApplyAtmosphericTransmittance(Color inputColor, Color transmittance)
        {
            inputColor.r *= Mathf.Max(0f, transmittance.r);
            inputColor.g *= Mathf.Max(0f, transmittance.g);
            inputColor.b *= Mathf.Max(0f, transmittance.b);
            return AtmosphereTools.SanitizeColor(inputColor);
        }

        private static Color RawSkyColor(in VisibleLight visibleLight) =>
            AtmosphereTools.SanitizeColor(visibleLight.finalColor * SkyIlluminanceScale);
    }
}
