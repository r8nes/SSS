using Aetheria.Runtime.Data;
using Aetheria.Runtime.Data.CelestialLight;
using Aetheria.Runtime.Overrides;
using Aetheria.Runtime.Rendering.Passes;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;

namespace Aetheria.Runtime.Rendering
{
    public sealed class AtmosphereFrameData : ContextItem
    {
        public bool IsActive;
        public Camera Camera;
        public VisualEnvironment VisualEnvironment;
        public PhysicallyBasedSky Volume;
        internal AtmosphereData CameraData;
        public CelestialBodyPass.GlobalStateData CelestialData;
        public CelestialLight PrimaryCelestialLight;
        public TextureHandle Transmittance;
        public TextureHandle MultiScatter;
        public TextureHandle GroundIrradiance;
        public TextureHandle SkyView;
        public TextureHandle AerialPerspective;
        public TextureHandle SpaceCubemap;
        public Vector3 CameraPositionWs;
        public int PerPixelRaymarchSamples;
        public int SpaceRaymarchSamples;

        public RenderingSpace RenderingSpace =>
            VisualEnvironment ? VisualEnvironment.renderingSpace.value : RenderingSpace.World;

        public Vector3 PlanetCenterWS =>
            VisualEnvironment ? VisualEnvironment.GetPlanetCenterWS(CameraPositionWs) : Vector3.zero;

        public float PlanetRadiusMeters =>
            VisualEnvironment ? VisualEnvironment.PlanetRadiusMeters : 1f;

        public float GetCloudLayerBottomRadiusMeters(float bottomAltitudeMeters) =>
            PlanetRadiusMeters + Mathf.Max(0f, bottomAltitudeMeters);

        public float GetCloudLayerTopRadiusMeters(float bottomAltitudeMeters, float altitudeRangeMeters) =>
            GetCloudLayerBottomRadiusMeters(bottomAltitudeMeters) + Mathf.Max(0f, altitudeRangeMeters);

        public override void Reset()
        {
            IsActive = false;
            Camera = null;
            VisualEnvironment = null;
            Volume = null;
            CameraData = null;
            CelestialData = default;
            PrimaryCelestialLight = default;
            Transmittance = TextureHandle.nullHandle;
            MultiScatter = TextureHandle.nullHandle;
            GroundIrradiance = TextureHandle.nullHandle;
            SkyView = TextureHandle.nullHandle;
            AerialPerspective = TextureHandle.nullHandle;
            SpaceCubemap = TextureHandle.nullHandle;
            CameraPositionWs = default;
            PerPixelRaymarchSamples = 0;
            SpaceRaymarchSamples = 0;
        }
    }
}
