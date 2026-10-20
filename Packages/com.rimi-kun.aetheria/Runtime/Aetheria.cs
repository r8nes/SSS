using System;
using Aetheria.Runtime.Components;
using Aetheria.Runtime.Data;
using Aetheria.Runtime.Overrides;
using Aetheria.Runtime.Sky;
using Aetheria.Runtime.Utils;
using UnityEngine;
using UnityEngine.Rendering;

namespace Aetheria.Runtime
{
    public static class Aetheria
    {
        private static Action _requestEnvironmentUpdate;

        internal static void RegisterEnvironmentUpdateRequest(Action request) =>
            _requestEnvironmentUpdate = request;

        public static void RequestEnvironmentUpdate() => _requestEnvironmentUpdate?.Invoke();

        public static SkySettings GetActiveSky(Camera camera = null) =>
            SkyManager.GetSkySetting(AtmosphereTools.GetVolumeStack(camera));

        public static bool TryGetPhysicallyBasedSky(
            Camera camera,
            out PhysicallyBasedSky sky,
            out VisualEnvironment visualEnvironment)
        {
            var stack = AtmosphereTools.GetVolumeStack(camera);
            visualEnvironment = stack?.GetComponent<VisualEnvironment>();
            sky = SkyManager.GetSkySetting(stack) as PhysicallyBasedSky;
            return sky && visualEnvironment;
        }

        public static Color EvaluateTransmittance(
            PhysicallyBasedSky sky,
            VisualEnvironment visualEnvironment,
            Vector3 worldPosition,
            Vector3 directionToLightWs) =>
            AtmosphereData.EvaluateTransmittance(
                sky,
                visualEnvironment,
                worldPosition,
                directionToLightWs);

        public static CelestialBodySettings GetSettings(CelestialBody body) =>
            body ? body.Settings : default;

        public static void Configure(CelestialBody body, in CelestialBodySettings settings)
        {
            if (!body) return;
            body.Apply(settings);
        }
    }
}
