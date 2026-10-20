using System;
using Aetheria.Runtime.Overrides;
using Aetheria.Runtime.Sky;
using Aetheria.Runtime.Utils;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace Aetheria.Runtime.Rendering.Passes
{
    internal sealed class EnvironmentPass : IDisposable
    {
        private readonly AtmosphereLutPass _lutPass;
        private readonly SkyContextManager _skyContexts;
        private int _perPixelRaymarchSamples;
        private int _spaceRaymarchSamples;

        public EnvironmentPass(
            AtmosphereLutPass atmosphereLutPass,
            in ResourceSnapshot resources)
        {
            _lutPass = atmosphereLutPass;
            _skyContexts = new SkyContextManager(resources);
        }

        public void Setup(int perPixelRaymarchSamples, int spaceRaymarchSamples)
        {
            _perPixelRaymarchSamples = perPixelRaymarchSamples;
            _spaceRaymarchSamples = spaceRaymarchSamples;
        }

        public Material PrepareSkyboxMaterial(Camera camera)
        {
            if (!camera)
                return null;

            var stack = AtmosphereTools.GetVolumeStack(camera);
            var visualEnvironment = stack?.GetComponent<VisualEnvironment>();
            var selectedSky = SkyManager.GetSkySetting(stack);
            var context = _skyContexts.Prepare(camera, visualEnvironment, selectedSky);
            return context?.SkyboxMaterial;
        }

        public void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            var cameraData = frameData.Get<UniversalCameraData>();
            var camera = cameraData.camera;
            var stack = AtmosphereTools.GetVolumeStack(cameraData);
            var visualEnvironment = stack?.GetComponent<VisualEnvironment>();
            var selectedSky = SkyManager.GetSkySetting(stack);
            var context = _skyContexts.Prepare(camera, visualEnvironment, selectedSky);
            var skyFrame = frameData.GetOrCreate<SkyFrameData>();
            var atmosphereFrame = frameData.GetOrCreate<AtmosphereFrameData>();

            atmosphereFrame.Camera = camera;
            atmosphereFrame.VisualEnvironment = visualEnvironment;
            atmosphereFrame.CameraPositionWs = camera ? camera.transform.position : Vector3.zero;

            skyFrame.IsActive = selectedSky != null && context?.SkyRenderer != null;
            skyFrame.Camera = camera;
            skyFrame.VisualEnvironment = visualEnvironment;
            skyFrame.Settings = selectedSky;
            skyFrame.Renderer = context?.SkyRenderer;

            if (!skyFrame.IsActive || !camera || !visualEnvironment)
            {
                atmosphereFrame.IsActive = false;
                EnvironmentSettings.ApplySkybox(null);
                return;
            }

            EnvironmentSettings.ApplySkybox(context.SkyboxMaterial);

            if (selectedSky is HDRISky)
            {
                atmosphereFrame.IsActive = false;
                atmosphereFrame.PrimaryCelestialLight = CelestialBodyPass.ResolvePrimaryLight(
                    frameData.Get<UniversalLightData>());
                return;
            }

            if (selectedSky is not PhysicallyBasedSky physicallyBasedSky)
            {
                atmosphereFrame.IsActive = false;
                return;
            }

            if (physicallyBasedSky.TopRadius.value <= visualEnvironment.planetRadius.value)
            {
                atmosphereFrame.IsActive = false;
                return;
            }

            _lutPass.PerPixelRaymarchSamples = _perPixelRaymarchSamples;
            _lutPass.SpaceRaymarchSamples = _spaceRaymarchSamples;
            _lutPass.PrepareFrame(renderGraph, frameData, physicallyBasedSky, visualEnvironment);
        }

        public void Dispose() => _skyContexts.Dispose();
    }
}
