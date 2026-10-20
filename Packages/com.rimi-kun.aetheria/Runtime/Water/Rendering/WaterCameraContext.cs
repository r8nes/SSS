using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Aetheria.Runtime.Water
{
    internal readonly struct WaterCameraContext
    {
        private const int k_LightingTileSize = 8;

        internal readonly Camera camera;
        internal readonly int width;
        internal readonly int height;
        internal readonly int lightingTileCountX;
        internal readonly int lightingTileCountY;
        internal readonly Vector4 renderResolution;

        private WaterCameraContext(
            Camera camera,
            int width,
            int height)
        {
            this.camera = camera;
            this.width = width;
            this.height = height;
            lightingTileCountX = CoreUtils.DivRoundUp(width, k_LightingTileSize);
            lightingTileCountY = CoreUtils.DivRoundUp(height, k_LightingTileSize);
            renderResolution = new Vector4(width, height, 1.0f / width, 1.0f / height);

        }

        internal static WaterCameraContext Create(UniversalCameraData cameraData)
        {
            return new WaterCameraContext(
                cameraData.camera,
                Mathf.Max(cameraData.scaledWidth, 1),
                Mathf.Max(cameraData.scaledHeight, 1));
        }

        internal Vector3 GetSurfaceUp(WaterSurface surface) =>
            surface != null ? surface.GetUpVector() : Vector3.up;

        internal float GetSignedSurfaceHeight(WaterSurface surface, Vector3 positionWS) =>
            surface == null
                ? float.PositiveInfinity
                : Vector3.Dot(positionWS - surface.transform.position, surface.transform.up);

    }
}
