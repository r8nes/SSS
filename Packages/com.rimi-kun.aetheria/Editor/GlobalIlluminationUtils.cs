using Aetheria.Runtime.Components;
using Aetheria.Runtime.Rendering;
using Aetheria.Runtime.Sky;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Experimental.GlobalIllumination;
using UnityEngine.Rendering.Universal;
using GILightmapping = UnityEngine.Experimental.GlobalIllumination.Lightmapping;

namespace Aetheria.Editor
{
    internal static class GlobalIlluminationUtils
    {
        private static readonly GILightmapping.RequestLightsDelegate LightsDelegate = ExtractLights;

        internal static void Install() => GILightmapping.SetDelegate(LightsDelegate);

        private static void ExtractLights(Light[] requests, NativeArray<LightDataGI> lightsOutput)
        {
            var lightData = new LightDataGI();

            for (int i = 0; i < requests.Length; i++)
            {
                var light = requests[i];
                var additionalLightData = light.GetUniversalAdditionalLightData();

                LightmapperUtils.Extract(light, out Cookie cookie);

                switch (light.type)
                {
                    case UnityEngine.LightType.Directional:
                    {
                        var directionalLight = new DirectionalLight();
                        LightmapperUtils.Extract(light, ref directionalLight);

                        if (light.cookie != null)
                        {
                            cookie.sizes = additionalLightData.lightCookieSize;

                            if (additionalLightData.lightCookieOffset != Vector2.zero)
                            {
                                var right = light.transform.right * additionalLightData.lightCookieOffset.x;
                                var up = light.transform.up * additionalLightData.lightCookieOffset.y;
                                directionalLight.position += right + up;
                            }
                        }

                        lightData.Init(ref directionalLight, ref cookie);
                        break;
                    }
                    case UnityEngine.LightType.Point:
                    {
                        var pointLight = new PointLight();
                        LightmapperUtils.Extract(light, ref pointLight);
                        lightData.Init(ref pointLight, ref cookie);
                        break;
                    }
                    case UnityEngine.LightType.Spot:
                    {
                        var spotLight = new SpotLight();
                        LightmapperUtils.Extract(light, ref spotLight);
                        spotLight.innerConeAngle = light.innerSpotAngle * Mathf.Deg2Rad;
                        spotLight.angularFalloff = AngularFalloffType.AnalyticAndInnerAngle;
                        lightData.Init(ref spotLight, ref cookie);
                        break;
                    }
                    case UnityEngine.LightType.Rectangle:
                    {
                        var rectangleLight = new RectangleLight();
                        LightmapperUtils.Extract(light, ref rectangleLight);
                        rectangleLight.mode = LightMode.Baked;
                        lightData.Init(ref rectangleLight);
                        break;
                    }
                    case UnityEngine.LightType.Disc:
                    {
                        var discLight = new DiscLight();
                        LightmapperUtils.Extract(light, ref discLight);
                        discLight.mode = LightMode.Baked;
                        lightData.Init(ref discLight);
                        break;
                    }
                    default:
                        lightData.InitNoBake(light.GetEntityId());
                        break;
                }

                lightData.falloff = FalloffType.InverseSquared;
                ApplySurfaceLighting(light, ref lightData);
                lightsOutput[i] = lightData;
            }
        }

        private static void ApplySurfaceLighting(Light light, ref LightDataGI lightData)
        {
            if (!light ||
                light.type != UnityEngine.LightType.Directional ||
                !light.TryGetComponent<CelestialBody>(out var body) ||
                !body.InteractsWithSky)
                return;

            var stack = EnvironmentSettings.StaticLightingVolumeStack;
            var sky = SkyManager.GetSkySetting(stack);
            if (sky == null)
                return;

            lightData.color = ResolveSurfaceColor(lightData.color, light, sky);
            lightData.indirectColor = ResolveSurfaceColor(lightData.indirectColor, light, sky);
        }

        private static LinearColor ResolveSurfaceColor(LinearColor source, Light light, SkySettings sky)
        {
            if (source.intensity == 0f)
                return LinearColor.Black();

            var color = new Color(source.red, source.green, source.blue) * source.intensity;
            color = sky.EvaluateSurfaceLightColor(
                color, -light.transform.forward, EnvironmentSettings.StaticLightingObserverPositionWs);
            float intensity = color.maxColorComponent;
            if (intensity <= 0f)
                return LinearColor.Black();

            return new LinearColor
            {
                red = color.r / intensity,
                green = color.g / intensity,
                blue = color.b / intensity,
                intensity = intensity
            };
        }
    }
}
