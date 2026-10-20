using Aetheria.Runtime.Data;
using Aetheria.Runtime.Overrides;
using UnityEngine;

namespace Aetheria.Runtime.Utils
{
    public static class AtmospherePlanet
    {
        public const float DistanceUnitMeters = (float)AtmosphereShaderConfig.DistanceUnitMeters;
        private const float MinUpVectorSqrMagnitude = 1e-8f;

        public static float GetRadiusMeters(VisualEnvironment environment) =>
            environment ? environment.PlanetRadiusMeters : 1f;

        public static Vector3 GetCenterWs(VisualEnvironment environment, Vector3 cameraPositionWs) =>
            environment ? environment.GetPlanetCenterWS(cameraPositionWs) : Vector3.zero;

        public static Vector3 GetUpVectorWs(
            VisualEnvironment environment,
            Vector3 cameraPositionWs,
            Vector3 worldPosition) =>
            GetUpVectorWs(GetCenterWs(environment, cameraPositionWs), worldPosition);

        public static Vector3 GetUpVectorWs(Vector3 planetCenterWs, Vector3 worldPosition)
        {
            var relative = worldPosition - planetCenterWs;
            float sqrMagnitude = relative.sqrMagnitude;
            return sqrMagnitude <= MinUpVectorSqrMagnitude
                ? Vector3.up
                : relative / Mathf.Sqrt(sqrMagnitude);
        }
    }
}
