using System;
using Aetheria.Runtime.Overrides;
using Aetheria.Runtime.Rendering.Fog.Volumetrics;
using Aetheria.Runtime.Utils;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace Aetheria.Runtime.Rendering.Passes
{
    internal sealed class FogPass : IDisposable
    {
        private readonly VolumetricFogSystem _volumetricFog;

        public FogPass(VolumetricFogResources resources)
        {
            _volumetricFog = new VolumetricFogSystem(resources);
        }

        public void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            var fogFrame = frameData.GetOrCreate<FogFrameData>();
            fogFrame.Reset();

            var cameraData = frameData.Get<UniversalCameraData>();
            var fog = AtmosphereTools.GetVolumeComponent<FogVolume>(cameraData);
            
            if (!fog || !fog.IsActive())
                return;

            var resources = frameData.Get<UniversalResourceData>();
            var atmosphere = frameData.Contains<AtmosphereFrameData>()
                ? frameData.Get<AtmosphereFrameData>()
                : null;
            var sky = frameData.Contains<SkyFrameData>() ? frameData.Get<SkyFrameData>() : null;
            var visualEnvironment = sky?.VisualEnvironment;

            if (!visualEnvironment)
                return;

            bool usesSkyColor = fog.ColorMode.value == FogColorMode.SkyColor;
            var filteredSky = TextureHandle.nullHandle;
            int fogSkyMipCount = 0;
            if (usesSkyColor)
            {
                if (sky == null || !sky.Environment.IsReady)
                    return;

                filteredSky = sky.Environment.LightingCubemap;
                fogSkyMipCount = EnvironmentConstants.ConvolutionMipCount;
                if (!filteredSky.IsValid() || fogSkyMipCount <= 1)
                    return;
            }

            var cameraPositionWs = cameraData.camera.transform.position;
            
            ComputePlanetParameters(
                visualEnvironment,
                cameraPositionWs,
                out var planetUp,
                out float cameraAltitude);
            
            var planetUpAltitude = new Vector4(
                planetUp.x,
                planetUp.y,
                planetUp.z,
                cameraAltitude);
            var parameters = FogParameters.Build(
                fog,
                planetUp,
                cameraAltitude,
                fogSkyMipCount);

            VolumetricFogSystem.Output volumetricOutput = default;
            
            bool useVolumetricFog =
                fog.IsVolumetricActive() &&
                resources.cameraDepthTexture.IsValid() &&
                _volumetricFog.Record(
                    renderGraph,
                    frameData,
                    fog,
                    atmosphere,
                    planetUpAltitude,
                    out volumetricOutput);

            fogFrame.IsActive = true;
            fogFrame.UseVolumetricFog = useVolumetricFog;
            fogFrame.CompositeOpaque = cameraData.resolveFinalTarget;
            fogFrame.Parameters = parameters;
            fogFrame.FilteredSky = filteredSky;
            fogFrame.IntegratedFog = useVolumetricFog
                ? volumetricOutput.IntegratedFog
                : TextureHandle.nullHandle;
            fogFrame.VolumetricContext = useVolumetricFog
                ? volumetricOutput.Context
                : default;
        }

        private static void ComputePlanetParameters(
            VisualEnvironment visualEnvironment,
            Vector3 cameraPositionWs,
            out Vector3 planetUp,
            out float cameraAltitude)
        {
            var centerToCamera = cameraPositionWs - visualEnvironment.GetPlanetCenterWS(cameraPositionWs);
            float centerDistance = centerToCamera.magnitude;
            planetUp = centerDistance > 1.0e-5f ? centerToCamera / centerDistance : Vector3.up;
            cameraAltitude = centerDistance - visualEnvironment.PlanetRadiusMeters;
        }

        public void Dispose() => _volumetricFog.Dispose();
    }
}
