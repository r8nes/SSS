using Aetheria.Runtime.Components;
using Aetheria.Runtime.Rendering;
using Aetheria.Runtime.Rendering.Fog.Volumetrics;
using Aetheria.Runtime.Rendering.Passes;
using Aetheria.Runtime.Overrides;
using Aetheria.Runtime.Sky;
using Aetheria.Runtime.Utils;
using Aetheria.Runtime.Water;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace Aetheria.Runtime
{
    public sealed class AetheriaFeature : ScriptableRendererFeature,
        IUniversalRenderGraphExtension,
        IUniversalCameraSetupRenderGraphExtension,
        IUniversalSkyboxRenderGraphExtension
    {
        private static readonly AetheriaProfiler.Marker FogGlobalsProfile =
            AetheriaProfiler.CreateMarker("Fog", "Globals");

        [SerializeField] private AetheriaResources _resources;

        [Header("Atmosphere Raymarching")]
        [SerializeField, Range(8, 64)] private int _perPixelRaymarchSamples = 30;
        [SerializeField, Range(8, 64)] private int _spaceRaymarchSamples = 30;

        [Header("Static Lighting Bake")] [SerializeField]
        private LightingSkyResolution _staticLightingSkyResolution =
            LightingSkyResolution.Resolution512;

        [Header("Volumetric Clouds")] [SerializeField]
        private bool _cloudRenderingDebugger;

        private AtmosphereLutPass _lutPass;
        private EnvironmentPass _environmentPass;
        private AtmosphereViewComputePass _atmosphereViewComputePass;
        private CompositePass _compositePass;
        private ScreenSpaceLightShaftsPass _lightShaftsPass;
        private FogPass _fogPass;
        private CloudLayerPass _cloudLayerPass;
        private CelestialDiskRenderer _celestialDiskPass;
        private VolumetricCloudsPass _volumetricCloudsPass;
        private EnvironmentLightingPass _environmentLightingPass;
        private EnvironmentLightingResolvePass _environmentLightingResolvePass;
        private EnvironmentLightingPass _staticEnvironmentLightingPass;
        private EnvironmentLightingResolvePass _staticEnvironmentLightingResolvePass;
        private WaterRenderSystem _waterSystem;
        private string _lastConfigurationError;
        private bool _pipelineReady;
        private bool _volumetricCloudsPreparedActive;
        private bool _cloudDebugLogPrinted;

        public int PerPixelRaymarchSamples => Mathf.Clamp(_perPixelRaymarchSamples, 8, 64);
        public int SpaceRaymarchSamples => Mathf.Clamp(_spaceRaymarchSamples, 8, 64);

        internal AetheriaResources ResourcesAsset => _resources;
        internal Material GetSkyboxMaterial(Camera camera) => _environmentPass?.PrepareSkyboxMaterial(camera);
        internal bool PipelineReady => _pipelineReady;

        public bool RenderingDebugger
        {
            get => _cloudRenderingDebugger;
            set => _cloudRenderingDebugger = value;
        }

        public void SetAtmosphereRaymarchSamples(int perPixelSamples, int spaceSamples)
        {
            _perPixelRaymarchSamples = Mathf.Clamp(perPixelSamples, 8, 64);
            _spaceRaymarchSamples = Mathf.Clamp(spaceSamples, 8, 64);
        }

#if UNITY_EDITOR
        [InitializeOnLoadMethod]
        private static void RegisterEditorCleanup()
        {
            AssemblyReloadEvents.beforeAssemblyReload -= PrepareForAssemblyReload;
            AssemblyReloadEvents.beforeAssemblyReload += PrepareForAssemblyReload;

            EditorApplication.wantsToQuit -= PrepareForEditorQuit;
            EditorApplication.wantsToQuit += PrepareForEditorQuit;

            EditorApplication.quitting -= ConfirmEditorQuit;
            EditorApplication.quitting += ConfirmEditorQuit;
        }

        private static void PrepareForAssemblyReload()
        {
            EnvironmentSettings.PrepareForEditorShutdown();
            CelestialBodyPass.ReleaseSharedResources();
        }

        private static bool PrepareForEditorQuit()
        {
            EnvironmentSettings.PrepareForEditorShutdown();

            EditorApplication.delayCall -= CancelPendingEditorQuit;
            EditorApplication.delayCall += CancelPendingEditorQuit;
            return true;
        }

        private static void ConfirmEditorQuit()
        {
            EditorApplication.delayCall -= CancelPendingEditorQuit;
            EnvironmentSettings.ConfirmEditorShutdown();
            CelestialBodyPass.ReleaseSharedResources();
        }

        private static void CancelPendingEditorQuit() =>
            EnvironmentSettings.CancelEditorShutdown();
#endif

        public override void Create()
        {
            ReleasePipeline();

            if (!SystemInfo.supportsComputeShaders)
            {
                ReportConfigurationError(
                    "AetheriaFeature requires compute shader support on the active platform.");
                return;
            }

            if (!_resources)
            {
                ReportConfigurationError(
                    "AetheriaFeature requires an AetheriaResources asset.");
                return;
            }

            if (!_resources.TryBuildSnapshot(out var resources, out string resourceError))
            {
                ReportConfigurationError($"AetheriaFeature: {resourceError}");
                return;
            }

            _lutPass = new AtmosphereLutPass(
                resources.Atmosphere.MultiScattering,
                resources.Atmosphere.LutGenerator)
            {
                PerPixelRaymarchSamples = PerPixelRaymarchSamples,
                SpaceRaymarchSamples = SpaceRaymarchSamples
            };

            _atmosphereViewComputePass = new AtmosphereViewComputePass(
                _lutPass,
                resources.Atmosphere.AerialPerspective);

            _waterSystem = new WaterRenderSystem();
            if (!_waterSystem.Initialize(resources.Water))
            {
                ReportConfigurationError("AetheriaFeature could not initialize the water rendering pipeline.");
                ReleasePipeline();
                return;
            }

            _compositePass = new CompositePass(resources.Scene.Composite);
            _lightShaftsPass = new ScreenSpaceLightShaftsPass(resources.Scene.ScreenSpaceLightShafts);
            _fogPass = new FogPass(resources.VolumetricFog);
            _environmentPass = new EnvironmentPass(_lutPass, resources);
            _celestialDiskPass = new CelestialDiskRenderer(resources.Scene.CelestialDisks);
            _cloudLayerPass = new CloudLayerPass(resources.CloudLayer);

            _volumetricCloudsPass = new VolumetricCloudsPass(resources.VolumetricClouds);

            _environmentLightingPass = new EnvironmentLightingPass(
                resources.EnvironmentLighting,
                _volumetricCloudsPass)
            {
                Resolution = LightingSkyResolution.Resolution512,
                UseSkySettingsUpdate = true
            };
            
            Aetheria.RegisterEnvironmentUpdateRequest(_environmentLightingPass.RequestUpdate);
            
            _environmentLightingResolvePass =
                new EnvironmentLightingResolvePass(_environmentLightingPass);

            _staticEnvironmentLightingPass = new EnvironmentLightingPass(
                resources.EnvironmentLighting,
                _volumetricCloudsPass,
                retainRadiance: true)
            {
                Resolution = _staticLightingSkyResolution,
                UpdateMode = EnvironmentUpdateMode.OnDemand,
                UpdatePeriod = 0.0f,
                IncludeSunInBaking = false,
                UpdateAllowed = true,
                UseSkySettingsUpdate = false
            };
            
            _staticEnvironmentLightingResolvePass =
                new EnvironmentLightingResolvePass(_staticEnvironmentLightingPass);

            _pipelineReady = true;
            _lastConfigurationError = null;
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
        }

        public void PrepareRenderGraph(
            RenderGraph renderGraph,
            ScriptableRenderer renderer,
            ContextContainer frameData,
            ref UniversalRenderGraphRequirements requirements)
        {
            if (!_pipelineReady)
                return;

            var cameraData = frameData.Get<UniversalCameraData>();
            var camera = cameraData.camera;
            var nativeFrame = frameData.GetOrCreate<AetheriaNativeFrameData>();
            nativeFrame.Reset();

            if (!camera)
                return;

            if (camera.TryGetComponent<StaticLightingCaptureCamera>(out _))
            {
                PrepareStaticLightingCaptureRenderGraph(renderGraph, frameData, camera, nativeFrame);
                return;
            }

            var cameraType = cameraData.cameraType;

#if UNITY_EDITOR
            if (cameraType == CameraType.SceneView)
            {
                if (!camera.allowHDR)
                {
                    camera.allowHDR = true;
                    SceneView.RepaintAll();
                }

                if (CoreUtils.IsSceneLightingDisabled(camera))
                    return;
            }
#endif

            bool lightingCamera = cameraType is CameraType.Game or CameraType.SceneView;
            bool reflectionCamera = cameraType == CameraType.Reflection;
            if (!lightingCamera && !reflectionCamera)
                return;

            bool staticLightingReflection =
                reflectionCamera && EnvironmentSettings.StaticLightingBindingActive;
            bool liveEnvironmentLayers = !staticLightingReflection;
            bool finalLightingCamera = lightingCamera && cameraData.resolveFinalTarget;
            bool baseGameCamera = cameraType == CameraType.Game &&
                                  cameraData.renderType == CameraRenderType.Base;
            bool environmentDriver = baseGameCamera ||
                                     (!Application.isPlaying && cameraType == CameraType.SceneView);

            nativeFrame.IsRelevant = true;
            nativeFrame.LightingCamera = lightingCamera;
            nativeFrame.ReflectionCamera = reflectionCamera;
            nativeFrame.FinalLightingCamera = finalLightingCamera;
            nativeFrame.EnvironmentDriver = environmentDriver;
            nativeFrame.LiveEnvironmentLayers = liveEnvironmentLayers;

            var stack = AtmosphereTools.GetVolumeStack(camera);
            nativeFrame.WaterActive = PrepareWaterForCamera(
                renderer,
                cameraData,
                stack,
                lightingCamera,
                out bool waterSsrActive);
            nativeFrame.WaterSsrActive = waterSsrActive;

            if (nativeFrame.WaterActive)
                requirements.RequireTransparentRefraction();

            var visualEnvironment = stack?.GetComponent<VisualEnvironment>();
            var selectedSky = SkyManager.GetSkySetting(stack);

            if (!visualEnvironment || selectedSky == null)
            {
                if (environmentDriver)
                    _environmentLightingPass.PrepareForCamera(false);

                _volumetricCloudsPreparedActive = false;
                _volumetricCloudsPass.Volume = null;
                EnvironmentSettings.ApplySkybox(null);
                return;
            }

            nativeFrame.EnvironmentActive = true;

            _environmentPass.Setup(PerPixelRaymarchSamples, SpaceRaymarchSamples);
            _environmentPass.RecordRenderGraph(renderGraph, frameData);

            _environmentLightingPass.Resolution = visualEnvironment.lightingSkyResolution.value;
            _environmentLightingPass.UseSkySettingsUpdate = true;
            _environmentLightingPass.UpdateAllowed = environmentDriver;
            if (environmentDriver)
                _environmentLightingPass.PrepareForCamera(true);

            var physicallyBasedSky = selectedSky as PhysicallyBasedSky;
            nativeFrame.AtmosphereActive =
                physicallyBasedSky &&
                physicallyBasedSky.TopRadius.value > visualEnvironment.planetRadius.value;

            var fogVolume = finalLightingCamera
                ? AtmosphereTools.GetVolumeComponent<FogVolume>(camera)
                : null;
            nativeFrame.FogActive = fogVolume && fogVolume.IsActive();

            if (nativeFrame.FogActive)
            {
                requirements.RequireIntermediateColor();

                if (fogVolume.IsVolumetricActive())
                {
                    requirements.RequireDepth(UniversalRenderGraphExtensionStage.VolumetricLighting);
                    requirements.RequireDepthPyramid(
                        UniversalRenderGraphExtensionStage.VolumetricLighting,
                        6,
                        0,
                        farthest: true);
                }
            }

            if (nativeFrame.AtmosphereActive)
            {
                requirements.RequireIntermediateColor();

                if (liveEnvironmentLayers)
                {
                    PrepareVolumetricCloudsPass(camera);
                    if (_volumetricCloudsPreparedActive)
                    {
                        requirements.RequireDepth(UniversalRenderGraphExtensionStage.VolumetricLighting);
                        requirements.RequireDepthPyramid(
                            UniversalRenderGraphExtensionStage.VolumetricLighting,
                            _volumetricCloudsPass.requiredDepthPyramidMipCount,
                            _volumetricCloudsPass.requiredDepthPyramidCheckerboardMipCount);
                    }
                }
                else
                {
                    _volumetricCloudsPreparedActive = false;
                    _volumetricCloudsPass.Volume = null;
                }
            }
            else
            {
                _volumetricCloudsPreparedActive = false;
                _volumetricCloudsPass.Volume = null;
            }

            nativeFrame.LightShaftsActive =
                finalLightingCamera && _lightShaftsPass.PrepareForCamera(camera);

            if (nativeFrame.LightShaftsActive)
            {
                requirements.RequireDepth(UniversalRenderGraphExtensionStage.VolumetricLighting);
                requirements.RequireIntermediateColor();
            }
        }

        public void RecordRenderGraph(
            RenderGraph renderGraph,
            ContextContainer frameData,
            UniversalRenderGraphExtensionStage stage)
        {
            if (!_pipelineReady || !frameData.Contains<AetheriaNativeFrameData>())
                return;

            var nativeFrame = frameData.Get<AetheriaNativeFrameData>();
            if (!nativeFrame.IsRelevant)
                return;

            if (nativeFrame.StaticLightingCapture)
            {
                RecordStaticLightingCaptureRenderGraph(renderGraph, frameData, nativeFrame, stage);
                return;
            }

            switch (stage)
            {
                case UniversalRenderGraphExtensionStage.BeforeShadows:
                    if (nativeFrame.AtmosphereActive && nativeFrame.LiveEnvironmentLayers)
                        _cloudLayerPass.RecordRenderGraph(renderGraph, frameData);
                    break;

                case UniversalRenderGraphExtensionStage.AfterShadows:
                    if (nativeFrame.AtmosphereActive)
                    {
                        _lutPass.RecordRenderGraph(renderGraph, frameData);
                        if (!nativeFrame.ReflectionCamera)
                            _atmosphereViewComputePass.RecordRenderGraph(renderGraph, frameData);
                    }

                    if (nativeFrame.EnvironmentActive && nativeFrame.LightingCamera)
                        _environmentLightingPass.RecordRenderGraph(renderGraph, frameData);
                    break;

                case UniversalRenderGraphExtensionStage.AfterPrepasses:
                    if (nativeFrame.EnvironmentActive && nativeFrame.LightingCamera)
                        _environmentLightingResolvePass.RecordRenderGraph(renderGraph, frameData);
                    break;

                case UniversalRenderGraphExtensionStage.BeforeScreenSpaceShadows:
                    if (nativeFrame.WaterActive)
                    {
                        _waterSystem.RecordScreenSpaceShadowReceiver(renderGraph, frameData);
                    }
                    break;

                case UniversalRenderGraphExtensionStage.TransparentPrepass:
                    if (nativeFrame.WaterActive)
                    {
                        _waterSystem.RecordTransparentPrepass(
                            renderGraph,
                            frameData,
                            nativeFrame.WaterSsrActive);
                        _waterSystem.RecordUnderwaterResources(renderGraph, frameData);
                    }
                    break;

                case UniversalRenderGraphExtensionStage.VolumetricLighting:
                    if (nativeFrame.AtmosphereActive && _volumetricCloudsPreparedActive)
                    {
                        _volumetricCloudsPass.RecordRenderGraph(renderGraph, frameData);
                        _cloudDebugLogPrinted = false;
                    }

                    if (nativeFrame.FogActive)
                        _fogPass.RecordRenderGraph(renderGraph, frameData);

                    if (nativeFrame.LightShaftsActive)
                        _lightShaftsPass.RecordRenderGraph(renderGraph, frameData);
                    break;

                case UniversalRenderGraphExtensionStage.OpaqueAtmosphere:
                    break;

                case UniversalRenderGraphExtensionStage.BeforePreRefraction:
                    _compositePass.RecordSceneComposite(renderGraph, frameData);
                    RecordFogGlobals(renderGraph, frameData, nativeFrame);
                    break;

                case UniversalRenderGraphExtensionStage.WaterLighting:
                    if (nativeFrame.WaterActive)
                    {
                        _waterSystem.RecordDeferredLighting(renderGraph, frameData);
                        _waterSystem.RecordUnderwaterEffect(renderGraph, frameData);
                    }
                    break;

            }
        }

        public void ExecuteCameraSetup(
            RasterCommandBuffer cmd,
            ContextContainer frameData)
        {
            if (!_pipelineReady ||
                !frameData.Contains<AetheriaNativeFrameData>() ||
                !frameData.Get<AetheriaNativeFrameData>().IsRelevant)
                return;

            DisableFogGlobals(cmd);
        }

        public bool PrepareSkyboxPass(
            IRasterRenderGraphBuilder builder,
            ContextContainer frameData,
            Material skyboxMaterial)
        {
            if (!TryGetActiveAtmosphereSky(
                    frameData,
                    skyboxMaterial,
                    out var nativeFrame,
                    out var atmosphere))
                return false;

            AtmosphereRenderGraphResources.UseCoreTextures(builder, atmosphere, true);

            if (!nativeFrame.StaticLightingCapture &&
                nativeFrame.LiveEnvironmentLayers &&
                atmosphere.CameraData != null &&
                atmosphere.CelestialData.BodyCount > 0 &&
                _celestialDiskPass != null &&
                _celestialDiskPass.Material)
            {
                var properties = atmosphere.CameraData.CelestialDiskProperties;
                properties.Clear();

                AtmosphereLutPass.BindMaterialParams(
                    properties,
                    atmosphere.Volume,
                    atmosphere.VisualEnvironment,
                    atmosphere.CameraPositionWs,
                    atmosphere.PerPixelRaymarchSamples,
                    atmosphere.SpaceRaymarchSamples);
                CelestialBodyPass.BindMaterialParams(properties, atmosphere.CelestialData);
            }

            return true;
        }

        public void ExecuteBeforeSkybox(
            RasterCommandBuffer cmd,
            ContextContainer frameData,
            Material skyboxMaterial)
        {
            if (!TryGetActiveAtmosphereSky(
                    frameData,
                    skyboxMaterial,
                    out _,
                    out var atmosphere))
                return;

            AtmosphereRenderGraphResources.BindCoreTextures(cmd, atmosphere, true);
            AtmosphereLutPass.BindMaterialParams(
                skyboxMaterial,
                atmosphere.Volume,
                atmosphere.VisualEnvironment,
                atmosphere.CameraPositionWs,
                atmosphere.PerPixelRaymarchSamples,
                atmosphere.SpaceRaymarchSamples);
            CelestialBodyPass.BindMaterialParams(skyboxMaterial, atmosphere.CelestialData);
        }

        public void ExecuteAfterSkybox(
            RasterCommandBuffer cmd,
            ContextContainer frameData,
            Material skyboxMaterial)
        {
            if (!TryGetActiveAtmosphereSky(
                    frameData,
                    skyboxMaterial,
                    out var nativeFrame,
                    out var atmosphere) ||
                nativeFrame.StaticLightingCapture ||
                !nativeFrame.LiveEnvironmentLayers ||
                atmosphere.CameraData == null ||
                atmosphere.CelestialData.BodyCount <= 0 ||
                _celestialDiskPass == null ||
                !_celestialDiskPass.Material)
                return;

            cmd.DrawProcedural(
                Matrix4x4.identity,
                _celestialDiskPass.Material,
                0,
                MeshTopology.Triangles,
                6,
                atmosphere.CelestialData.BodyCount,
                atmosphere.CameraData.CelestialDiskProperties);
        }

        private sealed class FogGlobalsPassData
        {
            public FogFrameData FogFrame;
            public bool Active;
            public bool MediumActive;
            public Vector3 MediumExtinction;
            public Vector3 MediumScatteringAlbedo;
        }

        private void RecordFogGlobals(
            RenderGraph renderGraph,
            ContextContainer frameData,
            AetheriaNativeFrameData nativeFrame)
        {
            FogFrameData fogFrame = nativeFrame.FogActive && frameData.Contains<FogFrameData>()
                ? frameData.Get<FogFrameData>()
                : null;

            using var profiledPass = FogGlobalsProfile.AddRasterPass<FogGlobalsPassData>(
                renderGraph,
                out var passData);
            var builder = profiledPass.Builder;
            passData.FogFrame = fogFrame;
            passData.Active = fogFrame is { IsActive: true };
            passData.MediumActive = false;
            if (nativeFrame.WaterActive && _waterSystem != null && frameData.Contains<WaterFrameResources>())
            {
                WaterFrameResources waterFrame = frameData.Get<WaterFrameResources>();
                int surfaceIndex = waterFrame.underwaterSurfaceIndex;
                if (surfaceIndex >= 0 && surfaceIndex < _waterSystem.activeSurfaceCount &&
                    waterFrame.underwaterLineParameters.cameraHeight < 0.0f)
                {
                    WaterSurface surface = _waterSystem.activeSurfaces[surfaceIndex];
                    if (surface)
                    {
                        float submersionDepth = Mathf.Max(0.0f, -waterFrame.underwaterLineParameters.cameraHeight);
                        passData.MediumActive = true;
                        passData.MediumExtinction = surface.extinction * Mathf.Max(surface.underwaterExtinctionMultiplier, 0.0f);
                        passData.MediumScatteringAlbedo = surface.EvaluateStylizedScatteringAlbedo(submersionDepth);
                    }
                }
            }
            if (passData.Active)
            {
                if (fogFrame.FilteredSky.IsValid())
                    builder.UseTexture(fogFrame.FilteredSky, AccessFlags.Read);
                if (fogFrame.IntegratedFog.IsValid())
                    builder.UseTexture(fogFrame.IntegratedFog, AccessFlags.Read);
            }
            builder.AllowGlobalStateModification(true);
            builder.AllowPassCulling(false);
            builder.SetRenderFunc(static (FogGlobalsPassData data, RasterGraphContext context) =>
            {
                if (!data.Active)
                {
                    DisableFogGlobals(context.cmd);
                }
                else
                {
                    var fogFrame = data.FogFrame;
                    var writer = ShaderValueWriter.ForGlobals(context.cmd);
                    writer.Write(fogFrame.Parameters);

                    if (fogFrame.FilteredSky.IsValid())
                        context.cmd.SetGlobalTexture(ShaderProperties.Fog.FilteredSky, fogFrame.FilteredSky);
                    context.cmd.SetGlobalFloat(
                        ShaderProperties.Fog.SkyColorEnabled,
                        Mathf.Approximately(fogFrame.Parameters.ColorMode, (float)FogColorMode.SkyColor) && fogFrame.FilteredSky.IsValid() ? 1.0f : 0.0f);
                    if (fogFrame.IntegratedFog.IsValid())
                        context.cmd.SetGlobalTexture(ShaderProperties.VolumetricFog.IntegratedFogTexture, fogFrame.IntegratedFog);

                    context.cmd.SetGlobalFloat(
                        ShaderProperties.VolumetricFog.FogEnabled,
                        fogFrame.UseVolumetricFog ? 1.0f : 0.0f);

                    if (fogFrame.UseVolumetricFog)
                        VolumetricPassCommon.WriteSamplingContext(ref writer, fogFrame.VolumetricContext);

                    PublishUniversalExternalFog(context.cmd, fogFrame);
                }

                PublishUniversalExternalMedium(
                    context.cmd,
                    data.MediumActive,
                    data.MediumExtinction,
                    data.MediumScatteringAlbedo);
            });
        }

        private static void PublishUniversalExternalFog(
            RasterCommandBuffer cmd,
            FogFrameData fogFrame)
        {
            ref readonly var fog = ref fogFrame.Parameters;
            bool volumeEnabled = fogFrame.UseVolumetricFog && fogFrame.IntegratedFog.IsValid();
            bool skyColorEnabled = Mathf.Approximately(fog.ColorMode, (float)FogColorMode.SkyColor) && fogFrame.FilteredSky.IsValid();

            cmd.SetGlobalFloat(UniversalExternalFogShader.Enabled, 1.0f);
            cmd.SetGlobalFloat(UniversalExternalFogShader.VolumeEnabled, volumeEnabled ? 1.0f : 0.0f);

            if (volumeEnabled)
            {
                ref readonly var volumetric = ref fogFrame.VolumetricContext;
                cmd.SetGlobalTexture(UniversalExternalFogShader.Texture, fogFrame.IntegratedFog);
                cmd.SetGlobalFloat(UniversalExternalFogShader.LastSliceDistance, volumetric.LastSliceDistance);
                cmd.SetGlobalVector(UniversalExternalFogShader.GridParams, volumetric.GridParams);
                cmd.SetGlobalVector(UniversalExternalFogShader.DepthEncodingParams, volumetric.DepthEncodingParams);
                cmd.SetGlobalVector(UniversalExternalFogShader.VBufferViewportSize, volumetric.VBufferViewportSize);
                cmd.SetGlobalFloat(UniversalExternalFogShader.UseBiquadraticReconstruction, volumetric.UseBiquadraticReconstruction ? 1.0f : 0.0f);
            }
            else
            {
                cmd.SetGlobalFloat(UniversalExternalFogShader.LastSliceDistance, 0.0f);
                cmd.SetGlobalFloat(UniversalExternalFogShader.UseBiquadraticReconstruction, 0.0f);
            }

            cmd.SetGlobalColor(UniversalExternalFogShader.Color, fog.Color);
            cmd.SetGlobalVector(UniversalExternalFogShader.MipParameters, fog.MipParameters);
            cmd.SetGlobalFloat(UniversalExternalFogShader.ConvolutionMipCount, fog.ConvolutionMipCount);
            cmd.SetGlobalFloat(UniversalExternalFogShader.SkyColorEnabled, skyColorEnabled ? 1.0f : 0.0f);
            cmd.SetGlobalFloat(UniversalExternalFogShader.BaseExtinction, fog.BaseExtinction);
            cmd.SetGlobalVector(UniversalExternalFogShader.BaseScattering, new Vector4(fog.BaseScattering.x, fog.BaseScattering.y, fog.BaseScattering.z, 0.0f));
            cmd.SetGlobalFloat(UniversalExternalFogShader.BaseHeight, fog.BaseHeight);
            cmd.SetGlobalVector(UniversalExternalFogShader.HeightExponents, fog.HeightExponents);
            cmd.SetGlobalVector(UniversalExternalFogShader.PlanetUpAltitude, fog.PlanetUpAltitude);

            if (skyColorEnabled)
                cmd.SetGlobalTexture(UniversalExternalFogShader.FilteredSky, fogFrame.FilteredSky);
        }

        private static void PublishUniversalExternalMedium(
            RasterCommandBuffer cmd,
            bool active,
            Vector3 extinction,
            Vector3 scatteringAlbedo)
        {
            cmd.SetGlobalFloat(UniversalExternalFogShader.MediumEnabled, active ? 1.0f : 0.0f);
            cmd.SetGlobalFloat(UniversalExternalFogShader.MediumCameraInside, active ? 1.0f : 0.0f);
            cmd.SetGlobalVector(UniversalExternalFogShader.MediumExtinction, new Vector4(extinction.x, extinction.y, extinction.z, 0.0f));
            cmd.SetGlobalVector(UniversalExternalFogShader.MediumScatteringAlbedo, new Vector4(scatteringAlbedo.x, scatteringAlbedo.y, scatteringAlbedo.z, 0.0f));
        }

        private bool TryGetActiveAtmosphereSky(
            ContextContainer frameData,
            Material skyboxMaterial,
            out AetheriaNativeFrameData nativeFrame,
            out AtmosphereFrameData atmosphere)
        {
            nativeFrame = null;
            atmosphere = null;

            if (!_pipelineReady || !skyboxMaterial ||
                !frameData.Contains<AetheriaNativeFrameData>() ||
                !frameData.Contains<SkyFrameData>() ||
                !frameData.Contains<AtmosphereFrameData>())
                return false;

            nativeFrame = frameData.Get<AetheriaNativeFrameData>();
            var sky = frameData.Get<SkyFrameData>();
            atmosphere = frameData.Get<AtmosphereFrameData>();

            return nativeFrame.IsRelevant &&
                   sky.IsActive && sky.Settings is PhysicallyBasedSky &&
                   atmosphere.IsActive && atmosphere.Volume && atmosphere.VisualEnvironment &&
                   atmosphere.Camera && sky.Renderer != null &&
                   atmosphere.Transmittance.IsValid() &&
                   atmosphere.MultiScatter.IsValid() &&
                   atmosphere.GroundIrradiance.IsValid() &&
                   atmosphere.SkyView.IsValid();
        }

        private static void DisableFogGlobals(RasterCommandBuffer cmd)
        {
            cmd.SetGlobalFloat(ShaderProperties.Fog.Enabled, 0.0f);
            cmd.SetGlobalFloat(ShaderProperties.Fog.SkyColorEnabled, 0.0f);
            cmd.SetGlobalFloat(ShaderProperties.VolumetricFog.FogEnabled, 0.0f);
            cmd.SetGlobalFloat(UniversalExternalFogShader.Enabled, 0.0f);
            cmd.SetGlobalFloat(UniversalExternalFogShader.VolumeEnabled, 0.0f);
            cmd.SetGlobalFloat(UniversalExternalFogShader.MediumEnabled, 0.0f);
            cmd.SetGlobalFloat(UniversalExternalFogShader.MediumCameraInside, 0.0f);
        }

        protected override void Dispose(bool disposing)
        {
            EnvironmentSettings.ApplyDefaults();

            ReleasePipeline();
            CelestialBodyPass.ReleaseSharedResources();
        }

        private void OnDestroy() => Dispose(true);

        internal ulong PrepareStaticLightingCapture(
            LightingSkyResolution resolution,
            bool includeSunInBaking,
            Vector3 observerPositionWs)
        {
            if (!_pipelineReady)
                return 0;

            ulong previousGeneration =
                _staticEnvironmentLightingPass.CompletedUpdateGeneration;
            
            _staticEnvironmentLightingPass.Resolution = resolution;
            _staticEnvironmentLightingPass.UpdateMode = EnvironmentUpdateMode.OnDemand;
            _staticEnvironmentLightingPass.UpdatePeriod = 0.0f;
            _staticEnvironmentLightingPass.IncludeSunInBaking = includeSunInBaking;
            _staticEnvironmentLightingPass.CaptureObserverPositionOverride = observerPositionWs;
            _staticEnvironmentLightingPass.UpdateAllowed = true;
            _staticEnvironmentLightingPass.RequestUpdate();
            
            return previousGeneration;
        }

        internal bool TryGetStaticLightingRadiance(
            ulong previousGeneration,
            out RenderTexture cubemap)
        {
            cubemap = _pipelineReady ? _staticEnvironmentLightingPass.RadianceTexture : null;
            
            return cubemap &&
                   _staticEnvironmentLightingPass.CompletedUpdateGeneration >
                   previousGeneration;
        }

        internal bool IsStaticLightingSkyPrepared(Camera camera)
        {
            if (!_pipelineReady) return false;
            var selectedSky = SkyManager.GetSkySetting(AtmosphereTools.GetVolumeStack(camera));
            return selectedSky is HDRISky ||
                   selectedSky is PhysicallyBasedSky && _lutPass.IsEnvironmentCapturePrepared(camera);
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            _perPixelRaymarchSamples = Mathf.Clamp(_perPixelRaymarchSamples, 8, 64);
            _spaceRaymarchSamples = Mathf.Clamp(_spaceRaymarchSamples, 8, 64);
            Create();
        }
#endif

        private void PrepareStaticLightingCaptureRenderGraph(
            RenderGraph renderGraph,
            ContextContainer frameData,
            Camera camera,
            AetheriaNativeFrameData nativeFrame)
        {
            nativeFrame.IsRelevant = true;
            nativeFrame.StaticLightingCapture = true;
            nativeFrame.ReflectionCamera = true;
            nativeFrame.LiveEnvironmentLayers = false;

            var stack = AtmosphereTools.GetVolumeStack(camera);
            var visualEnvironment = stack?.GetComponent<VisualEnvironment>();
            var selectedSky = SkyManager.GetSkySetting(stack);
            if (!visualEnvironment || selectedSky == null)
                return;

            nativeFrame.EnvironmentActive = true;
            _environmentPass.Setup(PerPixelRaymarchSamples, SpaceRaymarchSamples);
            _environmentPass.RecordRenderGraph(renderGraph, frameData);

            nativeFrame.AtmosphereActive =
                selectedSky is PhysicallyBasedSky physicallyBasedSky &&
                physicallyBasedSky.TopRadius.value > visualEnvironment.planetRadius.value;

            if (nativeFrame.AtmosphereActive)
                PrepareVolumetricCloudsPass(camera);
            else
            {
                _volumetricCloudsPreparedActive = false;
                _volumetricCloudsPass.Volume = null;
            }
        }

        private void RecordStaticLightingCaptureRenderGraph(
            RenderGraph renderGraph,
            ContextContainer frameData,
            AetheriaNativeFrameData nativeFrame,
            UniversalRenderGraphExtensionStage stage)
        {
            switch (stage)
            {
                case UniversalRenderGraphExtensionStage.BeforeShadows:
                    if (nativeFrame.AtmosphereActive)
                        _cloudLayerPass.RecordRenderGraph(renderGraph, frameData);
                    break;

                case UniversalRenderGraphExtensionStage.AfterShadows:
                    if (nativeFrame.AtmosphereActive)
                        _lutPass.RecordRenderGraph(renderGraph, frameData);

                    if (nativeFrame.EnvironmentActive)
                        _staticEnvironmentLightingPass.RecordRenderGraph(renderGraph, frameData);
                    break;

                case UniversalRenderGraphExtensionStage.AfterPrepasses:
                    if (nativeFrame.EnvironmentActive)
                        _staticEnvironmentLightingResolvePass.RecordRenderGraph(renderGraph, frameData);
                    break;
            }
        }

        private bool PrepareWaterForCamera(
            ScriptableRenderer renderer,
            UniversalCameraData cameraData,
            VolumeStack stack,
            bool allowScreenSpaceReflections,
            out bool ssrActive)
        {
            ssrActive = false;

            if (_waterSystem == null)
                return false;

            var settings = stack?.GetComponent<WaterRenderingSettings>();
            if (!_waterSystem.PrepareForCamera(settings))
                return false;

            ssrActive =
                allowScreenSpaceReflections &&
                renderer.TryGetActiveRendererFeature<ScreenSpaceReflectionRendererFeature>(
                    out var reflectionFeature)
                && reflectionFeature.IsActiveForCamera(cameraData);

            return true;
        }

        private void PrepareVolumetricCloudsPass(Camera camera)
        {
#if UNITY_EDITOR
            bool isEditingPrefab =
                UnityEditor.SceneManagement.PrefabStageUtility.GetCurrentPrefabStage();
            bool isSceneViewFocused =
                SceneView.lastActiveSceneView && SceneView.lastActiveSceneView.hasFocus;

            if (isEditingPrefab && isSceneViewFocused)
            {
                _volumetricCloudsPreparedActive = false;
                _volumetricCloudsPass.Volume = null;
                return;
            }
#endif

            var volume = AtmosphereTools.GetVolumeComponent<VolumetricClouds>(camera);
            bool isDebugger = DebugManager.instance.isAnyDebugUIActive;
            
            _volumetricCloudsPreparedActive =
                volume && volume.IsActive() && (!isDebugger || _cloudRenderingDebugger);

            if (_volumetricCloudsPreparedActive)
            {
                _volumetricCloudsPass.Volume = volume;
                return;
            }

            _volumetricCloudsPass.Volume = null;

#if UNITY_EDITOR || DEBUG
            if (!isDebugger || _cloudRenderingDebugger || _cloudDebugLogPrinted)
                return;

            Debug.Log(
                "AetheriaFeature: disabled volumetric clouds to avoid affecting rendering debugging.",
                this);
            _cloudDebugLogPrinted = true;
#endif
        }

        private void ReleasePipeline()
        {
            _pipelineReady = false;
            Aetheria.RegisterEnvironmentUpdateRequest(null);

            _fogPass?.Dispose();
            _fogPass = null;

            _cloudLayerPass?.Dispose();
            _cloudLayerPass = null;

            _celestialDiskPass?.Dispose();
            _celestialDiskPass = null;

            _atmosphereViewComputePass?.Dispose();
            _atmosphereViewComputePass = null;

            _lightShaftsPass?.Dispose();
            _lightShaftsPass = null;

            _environmentLightingPass?.Dispose();
            _environmentLightingPass = null;
            _environmentLightingResolvePass = null;

            _staticEnvironmentLightingPass?.Dispose();
            _staticEnvironmentLightingPass = null;
            _staticEnvironmentLightingResolvePass = null;

            _lutPass?.Dispose();
            _lutPass = null;
            _environmentPass?.Dispose();
            _environmentPass = null;
            _compositePass?.Dispose();
            _compositePass = null;

            _volumetricCloudsPass?.Dispose();
            _volumetricCloudsPass = null;
            _volumetricCloudsPreparedActive = false;

            _waterSystem?.Cleanup();
            _waterSystem = null;
        }

        private void ReportConfigurationError(string message)
        {
            if (_lastConfigurationError == message)
                return;

            Debug.LogError(message, this);
            _lastConfigurationError = message;
        }
    }
}
