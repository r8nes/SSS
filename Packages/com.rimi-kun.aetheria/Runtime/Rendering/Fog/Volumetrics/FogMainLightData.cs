using Aetheria.Runtime.Utils;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Aetheria.Runtime.Rendering.Fog.Volumetrics
{
    internal readonly struct FogMainLightData
    {
        public readonly Vector4 Direction;
        public readonly Vector4 Color;

        public FogMainLightData(AtmosphereFrameData atmosphere, UniversalLightData lightData)
        {
            if ((TryGetAtmosphereLight(atmosphere, out var directionToLightWs, out var color,
                     out float subtractiveFlag) ||
                 TryGetUrpMainLight(lightData, out directionToLightWs, out color, out subtractiveFlag)))
            {
                directionToLightWs = directionToLightWs.sqrMagnitude > 1.0e-12f
                    ? directionToLightWs.normalized
                    : Vector3.up;
                Direction = new Vector4(directionToLightWs.x, directionToLightWs.y, directionToLightWs.z, 1f);
                Color = new Vector4(
                    Mathf.Max(0f, color.r),
                    Mathf.Max(0f, color.g),
                    Mathf.Max(0f, color.b),
                    subtractiveFlag);
                return;
            }

            Direction = Vector4.zero;
            Color = Vector4.zero;
        }


        private static bool TryGetUrpMainLight(
            UniversalLightData lightData,
            out Vector3 directionToLightWs,
            out Color color,
            out float subtractiveFlag)
        {
            directionToLightWs = default;
            color = default;
            subtractiveFlag = 1f;

            int mainLightIndex = lightData.mainLightIndex;
            if (mainLightIndex < 0 ||
                !lightData.visibleLights.IsCreated ||
                mainLightIndex >= lightData.visibleLights.Length)
                return false;

            var visibleLight = lightData.visibleLights[mainLightIndex];
            var light = visibleLight.light;
            if (!light || !light.enabled || visibleLight.lightType != LightType.Directional)
                return false;

            directionToLightWs = -light.transform.forward;
            color = visibleLight.finalColor;

            var baking = light.bakingOutput;
            if (baking is
                {
                    isBaked: true, lightmapBakeType: LightmapBakeType.Mixed,
                    mixedLightingMode: MixedLightingMode.Subtractive
                })
                subtractiveFlag = 0f;

            return true;
        }

        private static bool TryGetAtmosphereLight(
            AtmosphereFrameData atmosphere,
            out Vector3 directionToLightWs,
            out Color color,
            out float subtractiveFlag)
        {
            directionToLightWs = default;
            color = default;
            subtractiveFlag = 1f;

            if (atmosphere == null || !atmosphere.IsActive)
                return false;

            if (!LightResolver.TryResolveContext(
                    atmosphere.PrimaryCelestialLight,
                    atmosphere.Volume,
                    atmosphere.VisualEnvironment,
                    atmosphere.Camera,
                    out var lightContext) ||
                !lightContext.IsValid || !lightContext.Light)
                return false;

            var light = lightContext.Light;
            
            if (!light.enabled || light.type != LightType.Directional)
                return false;

            directionToLightWs = lightContext.DirectionToLightWS;
            color = lightContext.SurfaceLightColor;
            
            var baking = light.bakingOutput;
            
            if (baking is
                {
                    isBaked: true, lightmapBakeType: LightmapBakeType.Mixed,
                    mixedLightingMode: MixedLightingMode.Subtractive
                })
                subtractiveFlag = 0f;

            return true;
        }
    }
}