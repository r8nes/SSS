using Aetheria.Runtime.Data;
using Aetheria.Runtime.Overrides;
using Aetheria.Runtime.Utils;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace Aetheria.Runtime.Rendering.Passes
{
    public sealed class AtmosphereLutPass
    {
        private static readonly AetheriaProfiler.Marker TransmittanceProfile =
            AetheriaProfiler.CreateMarker("Atmosphere", "Precompute Transmittance");

        private static readonly AetheriaProfiler.Marker MultiScatterProfile =
            AetheriaProfiler.CreateMarker("Atmosphere", "Precompute Multi-Scatter");

        private static readonly AetheriaProfiler.Marker GroundIrradianceProfile =
            AetheriaProfiler.CreateMarker("Atmosphere", "Precompute Ground Irradiance");

        private static readonly AetheriaProfiler.Marker SkyViewProfile =
            AetheriaProfiler.CreateMarker("Atmosphere", "Precompute Sky View");

        private class TransmittancePassData
        {
            public AtmosphereData CameraData;
            public ComputeShader Shader;
            public int Kernel;
            public TextureHandle Transmittance;
            public TextureHandle SpaceCubemap;
            public PhysicallyBasedSky Volume;
            public VisualEnvironment VisualEnvironment;
            public Vector3 CameraPositionWs;
            public int PerPixelRaymarchSamples;
            public int SpaceRaymarchSamples;
        }

        private class MultiScatterPassData : TransmittancePassData
        {
            public TextureHandle MultiScatter;
        }

        private class GroundIrradiancePassData : MultiScatterPassData
        {
            public TextureHandle GroundIrradiance;
        }

        private sealed class SkyViewPassData : GroundIrradiancePassData
        {
            public TextureHandle CameraSkyView;
            public CelestialBodyPass.GlobalStateData CelestialData;
        }

        private readonly struct PrecomputeContext
        {
            public readonly AtmosphereData CameraData;
            public readonly ComputeShader LutShader;
            public readonly ComputeShader MultiScatteringShader;
            public readonly int TransmittanceKernel;
            public readonly int SkyViewKernel;
            public readonly int MultiScatterKernel;
            public readonly int GroundIrradianceKernel;
            public readonly ImportedLuts Luts;
            public readonly TextureHandle SpaceCubemap;
            public readonly PhysicallyBasedSky Volume;
            public readonly VisualEnvironment VisualEnvironment;
            public readonly CelestialBodyPass.GlobalStateData CelestialData;
            public readonly Vector3 CameraPositionWs;
            public readonly int PerPixelRaymarchSamples;
            public readonly int SpaceRaymarchSamples;

            public PrecomputeContext(
                AtmosphereData cameraData,
                ComputeShader lutShader,
                ComputeShader multiScatteringShader,
                int transmittanceKernel,
                int skyViewKernel,
                int multiScatterKernel,
                int groundIrradianceKernel,
                in ImportedLuts luts,
                TextureHandle spaceCubemap,
                PhysicallyBasedSky volume,
                VisualEnvironment visualEnvironment,
                in CelestialBodyPass.GlobalStateData celestialData,
                Vector3 cameraPositionWs,
                int perPixelRaymarchSamples,
                int spaceRaymarchSamples)
            {
                CameraData = cameraData;
                LutShader = lutShader;
                MultiScatteringShader = multiScatteringShader;
                TransmittanceKernel = transmittanceKernel;
                SkyViewKernel = skyViewKernel;
                MultiScatterKernel = multiScatterKernel;
                GroundIrradianceKernel = groundIrradianceKernel;
                Luts = luts;
                SpaceCubemap = spaceCubemap;
                Volume = volume;
                VisualEnvironment = visualEnvironment;
                CelestialData = celestialData;
                CameraPositionWs = cameraPositionWs;
                PerPixelRaymarchSamples = perPixelRaymarchSamples;
                SpaceRaymarchSamples = spaceRaymarchSamples;
            }
        }

        internal readonly struct ImportedLuts
        {
            public readonly TextureHandle Transmittance;
            public readonly TextureHandle MultiScatter;
            public readonly TextureHandle GroundIrradiance;
            public readonly TextureHandle SkyView;

            public ImportedLuts(TextureHandle t, TextureHandle ms, TextureHandle gi, TextureHandle sv)
            {
                Transmittance = t;
                MultiScatter = ms;
                GroundIrradiance = gi;
                SkyView = sv;
            }
        }

        private const int DefaultRaymarchSamples = 30;

        private const int MinRaymarchSamples = 8;
        private const int MaxRaymarchSamples = 64;

        internal const float ScatteringMaxDistance = 200f;

        private readonly ComputeShader _multiScatteringShader;
        private readonly ComputeShader _lutGeneratorShader;

        private readonly int _transmittanceKernel;
        private readonly int _skyViewKernel;
        private readonly int _multiScatterKernel;
        private readonly int _groundIrradianceKernel;

        private readonly SharedLutCache _sharedLutCache = new();
        private readonly PerCameraDataManager<AtmosphereData> _perCameraData;

        private int _perPixelRaymarchSamples = DefaultRaymarchSamples;
        private int _spaceRaymarchSamples = DefaultRaymarchSamples;

        private RenderGraphTextureHandleCache _spaceCubemapCache;
        private RenderGraphTextureHandleCache _defaultSpaceCubemapCache;
        private Cubemap _defaultSpaceCubemap;

        private struct GlobalParameters : IShaderValueSource
        {
            public float BottomRadiusValue;
            public float TopRadiusValue;
            public float RayleighDensityExpScaleValue;
            public Color RayleighScatteringValue;
            public float MieDensityExpScaleValue;
            public Color MieScatteringValue;
            public Color MieAbsorptionValue;
            public float MiePhaseGValue;
            public Color OzoneAbsorptionExtinctionValue;
            public Color GroundAlbedoValue;
            public Vector3 CameraPositionWs;
            public Vector3 PlanetCenterWs;
            public float ScatteringMaxDistance;
            public int PerPixelRaymarchSamples;
            public int SpaceRaymarchSamples;
            public float IntensityMultiplier;
            public Vector4 SpaceParams0;
            public Vector4 SpaceParams1;
            public Color SpaceTint;
            public Vector4 ArtisticParams;
            public Vector4 ArtisticPhaseParams;
            public Color SkyHueColor;
            public Color HorizonHueColor;
            public Vector4 SunsetParams;
            public Vector4 PatternParams;
            public Vector4 PatternParams2;
            public float ArtisticAffectEnvironment;

            public void WriteTo(ref ShaderValueWriter writer) =>
                BindScalarShaderParameters(ref writer, this);
        }

        public AtmosphereLutPass(ComputeShader multiScatteringShader, ComputeShader lutGeneratorShader)
        {
            _multiScatteringShader = multiScatteringShader;
            _lutGeneratorShader = lutGeneratorShader;
            _transmittanceKernel = lutGeneratorShader.FindKernel("TransmittanceLut");
            _skyViewKernel = lutGeneratorShader.FindKernel("SkyViewLut");
            _multiScatterKernel = multiScatteringShader.FindKernel("MultiScatter");
            _groundIrradianceKernel = multiScatteringShader.FindKernel("GroundIrradiance");
            _perCameraData = new PerCameraDataManager<AtmosphereData>(() => new AtmosphereData(_sharedLutCache));
        }


        public int PerPixelRaymarchSamples
        {
            get => _perPixelRaymarchSamples;
            set
            {
                int clamped = Mathf.Clamp(value, MinRaymarchSamples, MaxRaymarchSamples);
                if (_perPixelRaymarchSamples == clamped)
                    return;

                _perPixelRaymarchSamples = clamped;
                _perCameraData.ForEach(static data => data.InvalidateSkyView());
            }
        }

        public int SpaceRaymarchSamples
        {
            get => _spaceRaymarchSamples;
            set
            {
                int clamped = Mathf.Clamp(value, MinRaymarchSamples, MaxRaymarchSamples);
                _spaceRaymarchSamples = clamped;
            }
        }

        internal AtmosphereData GetOrAddCameraData(Camera camera, PhysicallyBasedSky volume, VisualEnvironment visualEnvironment)
        {
            var data = _perCameraData.GetOrAddData(camera);
            data.BindSharedResources(volume, visualEnvironment);
            return data;
        }

        internal bool IsEnvironmentCapturePrepared(Camera camera)
        {
            return _perCameraData.TryGetData(camera, out var data) &&
                   data.IsValid(LutResource.Shared | LutResource.SkyView);
        }

        internal void PrepareFrame(
            RenderGraph renderGraph,
            ContextContainer frameData,
            PhysicallyBasedSky volume,
            VisualEnvironment visualEnvironment)
        {
            var cameraData = frameData.Get<UniversalCameraData>();
            var camera = cameraData.camera;

            if (!camera || !volume || !volume.IsActive() || !visualEnvironment || !visualEnvironment.active)
            {
                frameData.GetOrCreate<AtmosphereFrameData>().IsActive = false;
                return;
            }

            var lightData = frameData.Get<UniversalLightData>();
            var primaryCelestialLight = CelestialBodyPass.PrepareFrameData(lightData, volume, visualEnvironment, camera);

            var data = GetOrAddCameraData(camera, volume, visualEnvironment);

            CelestialBodyPass.PopulateAtmosphereData(data);
            data.PrepareFrame(volume, visualEnvironment, camera, PerPixelRaymarchSamples);

            var spaceCubemap = ImportSpaceCubemap(renderGraph, volume.Space.Cubemap.value);
            var cameraPositionWs = camera.transform.position;

            var luts = new ImportedLuts(
                data.ImportTransmittance(renderGraph),
                data.ImportMultiScatter(renderGraph),
                data.ImportGroundIrradiance(renderGraph),
                data.ImportSkyView(renderGraph));

            var atmosphereFrame = frameData.GetOrCreate<AtmosphereFrameData>();
            atmosphereFrame.IsActive = true;
            atmosphereFrame.Camera = camera;
            atmosphereFrame.Volume = volume;
            atmosphereFrame.VisualEnvironment = visualEnvironment;
            atmosphereFrame.CameraData = data;
            atmosphereFrame.CelestialData = default;
            atmosphereFrame.PrimaryCelestialLight = primaryCelestialLight;
            atmosphereFrame.Transmittance = luts.Transmittance;
            atmosphereFrame.MultiScatter = luts.MultiScatter;
            atmosphereFrame.GroundIrradiance = luts.GroundIrradiance;
            atmosphereFrame.SkyView = luts.SkyView;
            atmosphereFrame.SpaceCubemap = spaceCubemap;
            atmosphereFrame.CameraPositionWs = cameraPositionWs;
            atmosphereFrame.PerPixelRaymarchSamples = PerPixelRaymarchSamples;
            atmosphereFrame.SpaceRaymarchSamples = SpaceRaymarchSamples;
        }

        public void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            if (!frameData.Contains<AtmosphereFrameData>())
                return;

            var atmosphereFrame = frameData.Get<AtmosphereFrameData>();

            if (!atmosphereFrame.IsActive ||
                !atmosphereFrame.Volume ||
                atmosphereFrame.CameraData == null)
                return;

            CelestialBodyPass.RecordCookiePass(renderGraph);

            var data = atmosphereFrame.CameraData;
            atmosphereFrame.CelestialData = CelestialBodyPass.BuildGlobalStateData(renderGraph, data);
            var luts = new ImportedLuts(
                atmosphereFrame.Transmittance,
                atmosphereFrame.MultiScatter,
                atmosphereFrame.GroundIrradiance,
                atmosphereFrame.SkyView);

            bool buildTransmittance = !data.IsValid(LutResource.Transmittance);
            bool buildMultiScatter = !data.IsValid(LutResource.MultiScattering);
            bool buildGroundIrradiance = !data.IsValid(LutResource.GroundIrradiance);
            bool buildCameraSkyView = !data.IsValid(LutResource.SkyView);

            RecordPrecomputePass(
                renderGraph,
                atmosphereFrame.Volume,
                atmosphereFrame.VisualEnvironment,
                atmosphereFrame.CameraPositionWs,
                data,
                luts,
                atmosphereFrame.SpaceCubemap,
                atmosphereFrame.CelestialData,
                buildTransmittance,
                buildMultiScatter,
                buildGroundIrradiance,
                buildCameraSkyView);

        }

        private void RecordPrecomputePass(
            RenderGraph renderGraph,
            PhysicallyBasedSky volume,
            VisualEnvironment visualEnvironment,
            Vector3 cameraPositionWs,
            AtmosphereData data,
            in ImportedLuts luts,
            TextureHandle spaceCubemap,
            in CelestialBodyPass.GlobalStateData celestialData,
            bool buildTransmittance,
            bool buildMultiScatter,
            bool buildGroundIrradiance,
            bool buildCameraSkyView)
        {
            if (!buildTransmittance &&
                !buildMultiScatter &&
                !buildGroundIrradiance &&
                !buildCameraSkyView)
                return;

            var common = new PrecomputeContext(
                data,
                _lutGeneratorShader,
                _multiScatteringShader,
                _transmittanceKernel,
                _skyViewKernel,
                _multiScatterKernel,
                _groundIrradianceKernel,
                luts,
                spaceCubemap,
                volume,
                visualEnvironment,
                celestialData,
                cameraPositionWs,
                PerPixelRaymarchSamples,
                SpaceRaymarchSamples);

            if (buildTransmittance)
                RecordTransmittance(renderGraph, common);
            if (buildMultiScatter)
                RecordMultiScatter(renderGraph, common);
            if (buildGroundIrradiance)
                RecordGroundIrradiance(renderGraph, common);
            if (buildCameraSkyView)
                RecordSkyView(renderGraph, common);
        }

        private static void RecordTransmittance(RenderGraph renderGraph, in PrecomputeContext common)
        {
            using (var profiledPass = TransmittanceProfile.AddComputePass<TransmittancePassData>(
                       renderGraph, out var pass))
            {
                var builder = profiledPass.Builder;
                Populate(pass, common);
                pass.Shader = common.LutShader;
                pass.Kernel = common.TransmittanceKernel;
                pass.Transmittance = common.Luts.Transmittance;
                builder.UseTexture(pass.Transmittance, AccessFlags.Write);
                DeclareSpaceCubemap(builder, pass.SpaceCubemap);
                // Cached LUTs are read in later frames; retain this producer even when
                // the current graph has no direct consumer.
                builder.AllowPassCulling(false);
                // builder.EnableAsyncCompute(true);
                builder.SetRenderFunc(static (TransmittancePassData data, ComputeGraphContext context) =>
                    DispatchTransmittance(context.cmd, data));
            }
        }

        private static void RecordMultiScatter(RenderGraph renderGraph, in PrecomputeContext common)
        {
            using (var profiledPass = MultiScatterProfile.AddComputePass<MultiScatterPassData>(
                       renderGraph, out var pass))
            {
                var builder = profiledPass.Builder;
                Populate(pass, common);
                pass.Shader = common.MultiScatteringShader;
                pass.Kernel = common.MultiScatterKernel;
                pass.Transmittance = common.Luts.Transmittance;
                pass.MultiScatter = common.Luts.MultiScatter;
                builder.UseTexture(pass.Transmittance);
                builder.UseTexture(pass.MultiScatter, AccessFlags.Write);
                DeclareSpaceCubemap(builder, pass.SpaceCubemap);
                // Cached LUTs are read in later frames; retain this producer even when
                // the current graph has no direct consumer.
                builder.AllowPassCulling(false);
                // builder.EnableAsyncCompute(true);
                builder.SetRenderFunc(static (MultiScatterPassData data, ComputeGraphContext context) =>
                    DispatchMultiScatter(context.cmd, data));
            }
        }

        private static void RecordGroundIrradiance(RenderGraph renderGraph, in PrecomputeContext common)
        {
            using (var profiledPass = GroundIrradianceProfile.AddComputePass<GroundIrradiancePassData>(
                       renderGraph, out var pass))
            {
                var builder = profiledPass.Builder;
                Populate(pass, common);
                pass.Shader = common.MultiScatteringShader;
                pass.Kernel = common.GroundIrradianceKernel;
                pass.Transmittance = common.Luts.Transmittance;
                pass.MultiScatter = common.Luts.MultiScatter;
                pass.GroundIrradiance = common.Luts.GroundIrradiance;
                builder.UseTexture(pass.Transmittance);
                builder.UseTexture(pass.MultiScatter);
                builder.UseTexture(pass.GroundIrradiance, AccessFlags.Write);
                DeclareSpaceCubemap(builder, pass.SpaceCubemap);
                // Cached LUTs are read in later frames; retain this producer even when
                // the current graph has no direct consumer.
                builder.AllowPassCulling(false);
                // builder.EnableAsyncCompute(true);
                builder.SetRenderFunc(static (GroundIrradiancePassData data, ComputeGraphContext context) =>
                    DispatchGroundIrradiance(context.cmd, data));
            }
        }

        private static void RecordSkyView(RenderGraph renderGraph, in PrecomputeContext common)
        {
            using (var profiledPass = SkyViewProfile.AddComputePass<SkyViewPassData>(
                       renderGraph, out var pass))
            {
                var builder = profiledPass.Builder;
                Populate(pass, common);
                pass.Shader = common.LutShader;
                pass.Kernel = common.SkyViewKernel;
                pass.Transmittance = common.Luts.Transmittance;
                pass.MultiScatter = common.Luts.MultiScatter;
                pass.GroundIrradiance = common.Luts.GroundIrradiance;
                pass.CameraSkyView = common.Luts.SkyView;
                pass.CelestialData = common.CelestialData;
                builder.UseTexture(pass.Transmittance);
                builder.UseTexture(pass.MultiScatter);
                builder.UseTexture(pass.GroundIrradiance);
                builder.UseTexture(pass.CameraSkyView, AccessFlags.Write);
                DeclareSpaceCubemap(builder, pass.SpaceCubemap);
                if (pass.CelestialData.CookieAtlas.IsValid())
                    builder.UseTexture(pass.CelestialData.CookieAtlas);
                // Cached LUTs are read in later frames; retain this producer even when
                // the current graph has no direct consumer.
                builder.AllowPassCulling(false);
                // builder.EnableAsyncCompute(true);
                builder.SetRenderFunc(static (SkyViewPassData data, ComputeGraphContext context) =>
                    DispatchSkyView(
                        context.cmd,
                        data,
                        data.CameraData,
                        data.CameraPositionWs,
                        data.CameraSkyView));
            }
        }

        private static void DeclareSpaceCubemap(
            IComputeRenderGraphBuilder builder,
            TextureHandle spaceCubemap)
        {
            if (spaceCubemap.IsValid())
                builder.UseTexture(spaceCubemap);
        }

        private static void Populate(TransmittancePassData destination, in PrecomputeContext source)
        {
            destination.CameraData = source.CameraData;
            destination.SpaceCubemap = source.SpaceCubemap;
            destination.Volume = source.Volume;
            destination.VisualEnvironment = source.VisualEnvironment;
            destination.CameraPositionWs = source.CameraPositionWs;
            destination.PerPixelRaymarchSamples = source.PerPixelRaymarchSamples;
            destination.SpaceRaymarchSamples = source.SpaceRaymarchSamples;
        }

        private static void DispatchTransmittance(
            ComputeCommandBuffer command,
            TransmittancePassData data)
        {
            BindComputeParams(
                command,
                data.Shader,
                data.Kernel,
                data.Volume,
                data.VisualEnvironment,
                data.CameraPositionWs,
                data.SpaceCubemap,
                data.PerPixelRaymarchSamples,
                data.SpaceRaymarchSamples);

            command.SetComputeTextureParam(data.Shader, data.Kernel, ShaderProperties.Textures.OutputId,
                data.Transmittance);

            Dispatch2D(
                command,
                data.Shader,
                data.Kernel,
                LutDefinitions.TransmittanceWidth,
                LutDefinitions.TransmittanceHeight);

            data.CameraData.SetValid(LutResource.Transmittance, true);
        }

        private static void DispatchMultiScatter(
            ComputeCommandBuffer command,
            MultiScatterPassData data)
        {
            BindComputeParams(
                command,
                data.Shader,
                data.Kernel,
                data.Volume,
                data.VisualEnvironment,
                data.CameraPositionWs,
                data.SpaceCubemap,
                data.PerPixelRaymarchSamples,
                data.SpaceRaymarchSamples);
            var writer = ShaderValueWriter.ForCompute(command, data.Shader);
            command.SetComputeTextureParam(data.Shader, data.Kernel, LutDefinitions.TransmittanceId,
                data.Transmittance);
            writer.SetInt(
                ShaderProperties.Atmosphere.MultiScatterActiveSampleCount,
                LutDefinitions.MultiScatteringSampleCount);
            writer.SetInt(
                ShaderProperties.Atmosphere.MultiScatterRaymarchSampleCount,
                LutDefinitions.MultiScatteringRaymarchSampleCount);
            command.SetComputeTextureParam(data.Shader, data.Kernel, ShaderProperties.Textures.OutputId,
                data.MultiScatter);
            command.DispatchCompute(
                data.Shader,
                data.Kernel,
                LutDefinitions.MultiScatteringWidth,
                LutDefinitions.MultiScatteringHeight,
                1);
            data.CameraData.SetValid(LutResource.MultiScattering, true);
        }

        private static void DispatchGroundIrradiance(ComputeCommandBuffer command, GroundIrradiancePassData data)
        {
            BindComputeParams(
                command,
                data.Shader,
                data.Kernel,
                data.Volume,
                data.VisualEnvironment,
                data.CameraPositionWs,
                data.SpaceCubemap,
                data.PerPixelRaymarchSamples,
                data.SpaceRaymarchSamples);

            var writer = ShaderValueWriter.ForCompute(command, data.Shader);
            command.SetComputeTextureParam(data.Shader, data.Kernel, LutDefinitions.TransmittanceId,
                data.Transmittance);
            command.SetComputeTextureParam(data.Shader, data.Kernel, LutDefinitions.MultiScatteringId,
                data.MultiScatter);
            writer.SetInt(
                ShaderProperties.Atmosphere.MultiScatterRaymarchSampleCount,
                LutDefinitions.MultiScatteringRaymarchSampleCount);
            command.SetComputeTextureParam(data.Shader, data.Kernel, ShaderProperties.Textures.OutputId,
                data.GroundIrradiance);

            Dispatch2D(
                command,
                data.Shader,
                data.Kernel,
                LutDefinitions.GroundIrradianceWidth,
                LutDefinitions.GroundIrradianceHeight);

            data.CameraData.SetValid(LutResource.GroundIrradiance, true);
        }

        private static void DispatchSkyView(
            ComputeCommandBuffer command,
            SkyViewPassData data,
            AtmosphereData targetData,
            Vector3 cameraPositionWs,
            TextureHandle output)
        {
            command.SetComputeTextureParam(data.Shader, data.Kernel, LutDefinitions.TransmittanceId,
                data.Transmittance);
            command.SetComputeTextureParam(data.Shader, data.Kernel, LutDefinitions.MultiScatteringId,
                data.MultiScatter);
            command.SetComputeTextureParam(data.Shader, data.Kernel, LutDefinitions.GroundIrradianceId,
                data.GroundIrradiance);

            CelestialBodyPass.BindComputeParams(
                command,
                data.Shader,
                data.Kernel,
                data.CelestialData);

            BindComputeParams(
                command,
                data.Shader,
                data.Kernel,
                data.Volume,
                data.VisualEnvironment,
                cameraPositionWs,
                data.SpaceCubemap,
                data.PerPixelRaymarchSamples,
                data.SpaceRaymarchSamples);

            command.SetComputeTextureParam(data.Shader, data.Kernel, ShaderProperties.Textures.OutputId, output);

            Dispatch2D(
                command,
                data.Shader,
                data.Kernel,
                LutDefinitions.SkyViewWidth,
                LutDefinitions.SkyViewHeight);

            targetData.SetValid(LutResource.SkyView, true);
        }

        public static void BindComputeParams(
            ComputeCommandBuffer cmd,
            ComputeShader shader,
            int kernel,
            PhysicallyBasedSky volume,
            VisualEnvironment visualEnvironment,
            Vector3 cameraPositionWs,
            TextureHandle spaceCubemap,
            int perPixelRaymarchSamples = DefaultRaymarchSamples,
            int spaceRaymarchSamples = DefaultRaymarchSamples)
        {
            var parameters = BuildGlobalParameters(
                volume,
                visualEnvironment,
                cameraPositionWs,
                perPixelRaymarchSamples,
                spaceRaymarchSamples);

            var writer = ShaderValueWriter.ForCompute(cmd, shader);
            writer.Write(parameters);

            if (spaceCubemap.IsValid())
                cmd.SetComputeTextureParam(shader, kernel, ShaderProperties.Textures.SpaceCubemap, spaceCubemap);
        }

        public static void BindMaterialParams(
            MaterialPropertyBlock properties,
            PhysicallyBasedSky volume,
            VisualEnvironment visualEnvironment,
            Vector3 cameraPositionWs,
            int perPixelRaymarchSamples = DefaultRaymarchSamples,
            int spaceRaymarchSamples = DefaultRaymarchSamples)
        {
            var parameters = BuildGlobalParameters(
                volume,
                visualEnvironment,
                cameraPositionWs,
                perPixelRaymarchSamples,
                spaceRaymarchSamples);

            var writer = ShaderValueWriter.ForMaterialPropertyBlock(properties);
            writer.Write(parameters);
        }

        public static void BindMaterialParams(
            Material material,
            PhysicallyBasedSky volume,
            VisualEnvironment visualEnvironment,
            Vector3 cameraPositionWs,
            int perPixelRaymarchSamples = DefaultRaymarchSamples,
            int spaceRaymarchSamples = DefaultRaymarchSamples)
        {
            if (!material || !volume) return;

            var parameters = BuildGlobalParameters(
                volume,
                visualEnvironment,
                cameraPositionWs,
                perPixelRaymarchSamples,
                spaceRaymarchSamples);

            var writer = ShaderValueWriter.ForMaterial(material);
            writer.Write(parameters);
        }

        private static void BindScalarShaderParameters(
            ref ShaderValueWriter writer,
            in GlobalParameters parameters)
        {
            writer.SetFloat(ShaderProperties.Atmosphere.BottomRadius, parameters.BottomRadiusValue);
            writer.SetFloat(ShaderProperties.Atmosphere.TopRadius, parameters.TopRadiusValue);
            writer.SetFloat(
                ShaderProperties.Atmosphere.RayleighDensityExpScale,
                parameters.RayleighDensityExpScaleValue);
            writer.SetColor(
                ShaderProperties.Atmosphere.RayleighScattering,
                parameters.RayleighScatteringValue);
            writer.SetFloat(
                ShaderProperties.Atmosphere.MieDensityExpScale,
                parameters.MieDensityExpScaleValue);
            writer.SetColor(ShaderProperties.Atmosphere.MieScattering, parameters.MieScatteringValue);
            writer.SetColor(ShaderProperties.Atmosphere.MieAbsorption, parameters.MieAbsorptionValue);
            writer.SetFloat(ShaderProperties.Atmosphere.MiePhaseG, parameters.MiePhaseGValue);
            writer.SetColor(
                ShaderProperties.Atmosphere.OzoneAbsorptionExtinction,
                parameters.OzoneAbsorptionExtinctionValue);
            writer.SetColor(ShaderProperties.Atmosphere.GroundAlbedo, parameters.GroundAlbedoValue);
            writer.SetVector(ShaderProperties.Atmosphere.CameraPosition, parameters.CameraPositionWs);
            writer.SetVector(ShaderProperties.Atmosphere.PlanetCenterWs, parameters.PlanetCenterWs);
            writer.SetFloat(
                ShaderProperties.Atmosphere.ScatteringMaxDistance,
                parameters.ScatteringMaxDistance);
            writer.SetInt(
                ShaderProperties.Atmosphere.PerPixelRaymarchSamples,
                parameters.PerPixelRaymarchSamples);
            writer.SetInt(ShaderProperties.Atmosphere.SpaceRaymarchSamples, parameters.SpaceRaymarchSamples);
            writer.SetFloat(ShaderProperties.Atmosphere.IntensityMultiplier, parameters.IntensityMultiplier);
            writer.SetVector(ShaderProperties.Atmosphere.SpaceParams0, parameters.SpaceParams0);
            writer.SetVector(ShaderProperties.Atmosphere.SpaceParams1, parameters.SpaceParams1);
            writer.SetColor(ShaderProperties.Atmosphere.SpaceTint, parameters.SpaceTint);
            writer.SetVector(ShaderProperties.Atmosphere.ArtisticParams, parameters.ArtisticParams);
            writer.SetVector(ShaderProperties.Atmosphere.ArtisticPhaseParams, parameters.ArtisticPhaseParams);
            writer.SetColor(ShaderProperties.Atmosphere.SkyHueColor, parameters.SkyHueColor);
            writer.SetColor(ShaderProperties.Atmosphere.HorizonHueColor, parameters.HorizonHueColor);
            writer.SetVector(ShaderProperties.Atmosphere.SunsetParams, parameters.SunsetParams);
            writer.SetVector(ShaderProperties.Atmosphere.PatternParams, parameters.PatternParams);
            writer.SetVector(ShaderProperties.Atmosphere.PatternParams2, parameters.PatternParams2);
            writer.SetFloat(ShaderProperties.Atmosphere.ArtisticAffectEnvironment,
                parameters.ArtisticAffectEnvironment);
        }

        private static GlobalParameters BuildGlobalParameters(
            PhysicallyBasedSky volume,
            VisualEnvironment visualEnvironment,
            Vector3 cameraPositionWs,
            int perPixelRaymarchSamples,
            int spaceRaymarchSamples)
        {
            perPixelRaymarchSamples = Mathf.Clamp(perPixelRaymarchSamples, MinRaymarchSamples, MaxRaymarchSamples);
            spaceRaymarchSamples = Mathf.Clamp(spaceRaymarchSamples, MinRaymarchSamples, MaxRaymarchSamples);

            bool hasCubemap = volume.Space.Enabled.value && volume.Space.Cubemap.value &&
                              volume.Space.Cubemap.value.dimension == TextureDimension.Cube;

            float suppressEnd =
                Mathf.Max(volume.Space.SuppressionEnd.value, volume.Space.SuppressionStart.value + 1e-4f);

            return new GlobalParameters
            {
                BottomRadiusValue = visualEnvironment.planetRadius.value,
                TopRadiusValue = volume.TopRadius.value,
                RayleighDensityExpScaleValue = -1f / volume.RayleighScaleHeight.value,
                RayleighScatteringValue = volume.RayleighScatteringColor.value * volume.RayleighScatteringScale.value,
                MieDensityExpScaleValue = -1f / Mathf.Max(volume.MieScaleHeight.value, 0.0001f),
                MieScatteringValue = volume.MieScatteringColor.value * volume.MieScatteringScale.value,
                MieAbsorptionValue = volume.MieAbsorptionColor.value * volume.MieAbsorptionScale.value,
                MiePhaseGValue = volume.MiePhaseG.value,
                OzoneAbsorptionExtinctionValue = volume.OzoneAbsorptionExtinctionColor.value *
                                                 volume.OzoneAbsorptionExtinctionScale.value * volume.OzoneScale.value,
                GroundAlbedoValue = volume.GroundAlbedo.value,
                CameraPositionWs = cameraPositionWs,
                PlanetCenterWs = visualEnvironment.GetPlanetCenterWS(cameraPositionWs),
                ScatteringMaxDistance = ScatteringMaxDistance,
                PerPixelRaymarchSamples = perPixelRaymarchSamples,
                SpaceRaymarchSamples = spaceRaymarchSamples,
                IntensityMultiplier = volume.GetIntensityFromSettings() * Mathf.PI,
                SpaceParams0 = new Vector4(
                    volume.Space.Luminance.value,
                    volume.Space.Rotation.value * Mathf.Deg2Rad,
                    0f,
                    0f),
                SpaceParams1 = new Vector4(
                    hasCubemap ? 1f : 0f,
                    volume.Space.SuppressionStart.value,
                    suppressEnd,
                    Mathf.Max(volume.Space.SuppressionPower.value, 0.1f)),
                SpaceTint = volume.Space.Tint.value.linear,
                ArtisticParams = new Vector4(
                    volume.ArtisticSkyEnabled.value ? volume.SkyHueIntensity.value : 0f,
                    volume.ArtisticSkyEnabled.value ? volume.HorizonHueIntensity.value : 0f,
                    volume.ArtisticSkyEnabled.value ? volume.SkySaturation.value : 1f,
                    volume.ArtisticSkyEnabled.value ? volume.SkyContrast.value : 1f),
                ArtisticPhaseParams = ResolveArtisticPhaseWeights(
                    volume.ArtisticSkyPhaseMode.value,
                    volume.ArtisticSkyPhaseWeights.value),
                SkyHueColor = volume.SkyHueColor.value.linear,
                HorizonHueColor = volume.HorizonHueColor.value.linear,
                SunsetParams = new Vector4(
                    volume.ArtisticSunsetBoostEnabled.value ? volume.SunsetIntensity.value : 0f,
                    ResolveArtisticPhaseWeights(volume.ArtisticSunsetPhaseMode.value,
                        volume.ArtisticSunsetPhaseWeights.value).x,
                    ResolveArtisticPhaseWeights(volume.ArtisticSunsetPhaseMode.value,
                        volume.ArtisticSunsetPhaseWeights.value).y,
                    ResolveArtisticPhaseWeights(volume.ArtisticSunsetPhaseMode.value,
                        volume.ArtisticSunsetPhaseWeights.value).z),
                PatternParams = new Vector4(
                    volume.ArtisticSkyEnabled.value && volume.ArtisticPatternEnabled.value
                        ? volume.SkyPatternIntensity.value
                        : 0f,
                    volume.SkyPatternSaturation.value,
                    volume.SkyPatternScale.value,
                    0f),
                PatternParams2 = new Vector4(
                    volume.SkyPatternHorizontalSize.value,
                    volume.SkyPatternVerticalSize.value,
                    0f,
                    0f),
                ArtisticAffectEnvironment = volume.ArtisticSkyEnabled.value && volume.AffectEnvironment.value
                    ? volume.AffectEnvironmentMix.value
                    : 0f
            };
        }

        private static Vector3 ResolveArtisticPhaseWeights(AtmosphereArtisticPhaseMode mode, Vector3 custom)
        {
            return mode switch
            {
                AtmosphereArtisticPhaseMode.ZenithWeighted => new Vector3(1f, 0.35f, 0.1f),
                AtmosphereArtisticPhaseMode.SunsetWeighted => new Vector3(0.35f, 1f, 0.35f),
                AtmosphereArtisticPhaseMode.NightWeighted => new Vector3(0.1f, 0.35f, 1f),
                AtmosphereArtisticPhaseMode.Custom => Vector3.Max(custom, Vector3.zero),
                _ => Vector3.one
            };
        }

        internal TextureHandle ImportSpaceCubemap(RenderGraph renderGraph, Texture texture)
        {
            if (!texture || texture.dimension != TextureDimension.Cube)
                return ImportDefaultSpaceCubemap(renderGraph);

            return _spaceCubemapCache.Import(renderGraph, texture);
        }

        private TextureHandle ImportDefaultSpaceCubemap(RenderGraph renderGraph)
        {
            if (!_defaultSpaceCubemap)
            {
                _defaultSpaceCubemap = new Cubemap(1, TextureFormat.RGBA32, false)
                {
                    name = "Black Space Cubemap",
                    hideFlags = HideFlags.HideAndDontSave
                };

                var pixels = new[] { Color.black };

                for (int face = 0; face < 6; ++face)
                    _defaultSpaceCubemap.SetPixels(pixels, (CubemapFace)face);

                _defaultSpaceCubemap.Apply(false, true);
            }

            return _defaultSpaceCubemapCache.Import(renderGraph, _defaultSpaceCubemap);
        }

        public void Dispose()
        {
            _perCameraData.Dispose();
            _sharedLutCache.Dispose();

            _spaceCubemapCache.Dispose();
            _defaultSpaceCubemapCache.Dispose();
            if (_defaultSpaceCubemap)
                CoreUtils.Destroy(_defaultSpaceCubemap);
            _defaultSpaceCubemap = null;
        }

        private static void Dispatch2D(
            ComputeCommandBuffer command,
            ComputeShader shader,
            int kernel,
            int width,
            int height)
        {
            const int _groupSize = LutDefinitions.ThreadGroupSize;

            command.DispatchCompute(
                shader,
                kernel,
                CoreUtils.DivRoundUp(width, _groupSize),
                CoreUtils.DivRoundUp(height, _groupSize),
                1);
        }

    }
}
