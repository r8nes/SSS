using Aetheria.Runtime.Data;
using Aetheria.Runtime.Overrides;
using Aetheria.Runtime.Utils;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace Aetheria.Runtime.Rendering.Passes
{
    internal sealed class AtmosphereViewComputePass : System.IDisposable
    {
        private static readonly AetheriaProfiler.Marker Profile =
            AetheriaProfiler.CreateMarker("Atmosphere", "Aerial Perspective");

        private sealed class PassData
        {
            public ComputeShader Shader;
            public int Kernel;
            public AtmosphereData Data;
            public PhysicallyBasedSky Volume;
            public VisualEnvironment VisualEnvironment;
            public TextureHandle AtmosphericScattering;
            public TextureHandle Transmittance;
            public TextureHandle MultiScatter;
            public TextureHandle SpaceCubemap;
            public CelestialBodyPass.GlobalStateData CelestialData;
            public Matrix4x4 InverseViewProjection;
            public Vector4 ViewForwardWs;
            public Vector3 CameraPositionWs;
            public int PerPixelRaymarchSamples;
            public int SpaceRaymarchSamples;
        }

        private readonly AtmosphereLutPass _lutPass;
        private readonly ComputeShader _shader;
        private readonly int _kernel;

        public AtmosphereViewComputePass(
            AtmosphereLutPass atmosphereLutPass,
            ComputeShader aerialPerspectiveShader)
        {
            _lutPass = atmosphereLutPass;
            _shader = aerialPerspectiveShader;
            _kernel = aerialPerspectiveShader.FindKernel("BuildLut");
        }

        public void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            if (!frameData.Contains<AtmosphereFrameData>())
                return;

            var cameraData = frameData.Get<UniversalCameraData>();
            var atmosphere = frameData.Get<AtmosphereFrameData>();
            var camera = atmosphere.Camera ? atmosphere.Camera : cameraData.camera;
            var volume = atmosphere.Volume;

            if (!atmosphere.IsActive || atmosphere.CameraData == null || !camera ||
                !volume || !volume.IsActive())
                return;

            var data = atmosphere.CameraData;
            var viewProjection =
                GL.GetGPUProjectionMatrix(camera.projectionMatrix, true) * camera.worldToCameraMatrix;

            data.PrepareAerialPerspectiveFrame(
                volume,
                atmosphere.VisualEnvironment,
                viewProjection,
                atmosphere.CameraPositionWs,
                atmosphere.PerPixelRaymarchSamples,
                atmosphere.SpaceRaymarchSamples);

            var atmosphericScattering = data.ImportAtmosphericScattering(renderGraph);
            var transmittance = atmosphere.Transmittance.IsValid()
                ? atmosphere.Transmittance
                : data.ImportTransmittance(renderGraph);
            var multiScatter = atmosphere.MultiScatter.IsValid()
                ? atmosphere.MultiScatter
                : data.ImportMultiScatter(renderGraph);
            var spaceCubemap = atmosphere.SpaceCubemap.IsValid()
                ? atmosphere.SpaceCubemap
                : _lutPass.ImportSpaceCubemap(renderGraph, volume.Space.Cubemap.value);

            atmosphere.AerialPerspective = atmosphericScattering;

            if (data.IsValid(LutResource.AtmosphericScattering))
                return;

            var forward = camera.transform.forward;

            using var profiledPass = Profile.AddComputePass<PassData>(
                renderGraph,
                out var pass);
            
            var builder = profiledPass.Builder;
            pass.Shader = _shader;
            pass.Kernel = _kernel;
            pass.Data = data;
            pass.Volume = volume;
            pass.VisualEnvironment = atmosphere.VisualEnvironment;
            pass.AtmosphericScattering = atmosphericScattering;
            pass.Transmittance = transmittance;
            pass.MultiScatter = multiScatter;
            pass.SpaceCubemap = spaceCubemap;
            pass.CelestialData = atmosphere.CelestialData;
            pass.InverseViewProjection = viewProjection.inverse;
            pass.ViewForwardWs = new Vector4(forward.x, forward.y, forward.z, 0.0f);
            pass.CameraPositionWs = atmosphere.CameraPositionWs;
            pass.PerPixelRaymarchSamples = atmosphere.PerPixelRaymarchSamples;
            pass.SpaceRaymarchSamples = atmosphere.SpaceRaymarchSamples;

            builder.UseTexture(transmittance);
            builder.UseTexture(multiScatter);

            if (spaceCubemap.IsValid())
                builder.UseTexture(spaceCubemap);
            if (pass.CelestialData.CookieAtlas.IsValid())
                builder.UseTexture(pass.CelestialData.CookieAtlas);
                
            builder.UseTexture(atmosphericScattering, AccessFlags.Write);
            // AtmosphericScattering is a persistent per-camera cache; it becomes valid
            // only when this render function executes.
            builder.AllowPassCulling(false);

            // builder.EnableAsyncCompute(true);
                
            builder.SetRenderFunc(static (PassData data, ComputeGraphContext context) =>
                DispatchAerialPerspective(context.cmd, data));
        }

        private static void DispatchAerialPerspective(ComputeCommandBuffer command, PassData data)
        {
            var writer = ShaderValueWriter.ForCompute(command, data.Shader);
            command.SetComputeTextureParam(data.Shader, data.Kernel, LutDefinitions.TransmittanceId, data.Transmittance);
            command.SetComputeTextureParam(data.Shader, data.Kernel, LutDefinitions.MultiScatteringId, data.MultiScatter);
            
            CelestialBodyPass.BindComputeParams(
                command,
                data.Shader,
                data.Kernel,
                data.CelestialData);
            
            AtmosphereLutPass.BindComputeParams(
                command,
                data.Shader,
                data.Kernel,
                data.Volume,
                data.VisualEnvironment,
                data.CameraPositionWs,
                data.SpaceCubemap,
                data.PerPixelRaymarchSamples,
                data.SpaceRaymarchSamples);
            
            writer.SetMatrix(
                ShaderProperties.Atmosphere.AerialPerspectiveInvViewProjection,
                data.InverseViewProjection);
            writer.SetVector(ShaderProperties.Atmosphere.AerialPerspectiveViewForward, data.ViewForwardWs);
            
            command.SetComputeTextureParam(data.Shader, data.Kernel, ShaderProperties.Textures.OutputId, data.AtmosphericScattering);
            
            command.DispatchCompute(
                data.Shader,
                data.Kernel,
                LutDefinitions.AtmosphericScatteringWidth,
                LutDefinitions.AtmosphericScatteringHeight,
                1);
            
            data.Data.SetValid(LutResource.AtmosphericScattering, true);
        }

        public void Dispose() { }
    }
}
