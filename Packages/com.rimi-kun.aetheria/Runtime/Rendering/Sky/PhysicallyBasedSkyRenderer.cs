using Aetheria.Runtime.Data;
using Aetheria.Runtime.Overrides;
using Aetheria.Runtime.Rendering.Passes;
using Aetheria.Runtime.Sky;
using Aetheria.Runtime.Utils;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;

namespace Aetheria.Runtime.Rendering.Sky
{
    internal sealed class PhysicallyBasedSkyRenderer : SkyRenderer
    {
        private Material _material;
        private int _compositeKernel;
        private int _compositeCloudsKernel;
        private int _compositeCloudLayerKernel;
        private int _compositeCloudsCloudLayerKernel;

        public override Material SkyboxMaterial => _material;
        public override bool RequiresAtmosphere => true;

        public override void Build(in ResourceSnapshot resources)
        {
            _material = CoreUtils.CreateEngineMaterial(resources.Scene.PhysicallyBasedSky);
            var shader = resources.EnvironmentLighting.Composite;
            _compositeKernel = shader.FindKernel("Composite");
            _compositeCloudsKernel = shader.FindKernel("CompositeClouds");
            _compositeCloudLayerKernel = shader.FindKernel("CompositeCloudLayer");
            _compositeCloudsCloudLayerKernel = shader.FindKernel("CompositeCloudsCloudLayer");
        }

        public override void Cleanup()
        {
            CoreUtils.Destroy(_material);
            _material = null;
        }

        public override void PrepareVisualSky(
            SkySettings settings,
            VisualEnvironment environment,
            Camera camera)
        {
        }

        public override int GetEnvironmentKernel(bool cloudsEnabled, bool cloudLayerEnabled)
        {
            if (cloudsEnabled)
                return cloudLayerEnabled ? _compositeCloudsCloudLayerKernel : _compositeCloudsKernel;
            return cloudLayerEnabled ? _compositeCloudLayerKernel : _compositeKernel;
        }

        public override bool IsEnvironmentReady(
            SkySettings settings,
            VisualEnvironment environment,
            AtmosphereFrameData atmosphere) =>
            settings is PhysicallyBasedSky &&
            environment &&
            atmosphere is { IsActive: true, CameraData: not null } &&
            atmosphere.Transmittance.IsValid() &&
            atmosphere.MultiScatter.IsValid() &&
            atmosphere.GroundIrradiance.IsValid() &&
            atmosphere.SkyView.IsValid();

        public override void DeclareEnvironmentResources(
            IComputeRenderGraphBuilder builder,
            AtmosphereFrameData atmosphere,
            in SkyRenderGraphResources resources)
        {
            if (atmosphere == null)
                return;

            builder.UseTexture(atmosphere.Transmittance);
            builder.UseTexture(atmosphere.MultiScatter);
            builder.UseTexture(atmosphere.GroundIrradiance);
            builder.UseTexture(atmosphere.SkyView);
            if (atmosphere.SpaceCubemap.IsValid())
                builder.UseTexture(atmosphere.SpaceCubemap);
            if (atmosphere.CelestialData.CookieAtlas.IsValid())
                builder.UseTexture(atmosphere.CelestialData.CookieAtlas);
        }

        public override void BindEnvironmentCompute(
            ComputeCommandBuffer command,
            ComputeShader shader,
            int kernel,
            SkySettings settings,
            VisualEnvironment environment,
            AtmosphereFrameData atmosphere,
            in SkyRenderGraphResources resources,
            Vector3 observerPositionWs)
        {
            if (settings is not PhysicallyBasedSky volume || atmosphere == null)
                return;

            command.SetComputeTextureParam(shader, kernel, LutDefinitions.TransmittanceId, atmosphere.Transmittance);
            command.SetComputeTextureParam(shader, kernel, LutDefinitions.MultiScatteringId, atmosphere.MultiScatter);
            command.SetComputeTextureParam(shader, kernel, LutDefinitions.GroundIrradianceId, atmosphere.GroundIrradiance);
            command.SetComputeTextureParam(shader, kernel, LutDefinitions.SkyViewId, atmosphere.SkyView);
            CelestialBodyPass.BindComputeParams(command, shader, kernel, atmosphere.CelestialData);
            AtmosphereLutPass.BindComputeParams(
                command,
                shader,
                kernel,
                volume,
                environment,
                observerPositionWs,
                atmosphere.SpaceCubemap,
                atmosphere.PerPixelRaymarchSamples,
                atmosphere.SpaceRaymarchSamples);
        }
    }
}
