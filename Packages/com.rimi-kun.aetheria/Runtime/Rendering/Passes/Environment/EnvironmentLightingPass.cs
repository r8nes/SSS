using System;
using Aetheria.Runtime.Data;
using Aetheria.Runtime.Overrides;
using Aetheria.Runtime.Sky;
using Aetheria.Runtime.Utils;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace Aetheria.Runtime.Rendering.Passes
{
    internal sealed class EnvironmentLightingPass : IDisposable
    {
        private static readonly AetheriaProfiler.Marker CloudTraceProfile =
            AetheriaProfiler.CreateMarker("Lighting", "Trace Clouds");

        private static readonly AetheriaProfiler.Marker SkyCompositeProfile =
            AetheriaProfiler.CreateMarker("Lighting", "Composite Sky");

        private static readonly AetheriaProfiler.Marker AmbientProfile =
            AetheriaProfiler.CreateMarker("Lighting", "Project Ambient Probe");

        private static readonly AetheriaProfiler.Marker TemporalBlendProfile =
            AetheriaProfiler.CreateMarker("Environment/Lighting", "Blend Cubemap");

        private static readonly AetheriaProfiler.Marker FaceCopyProfile =
            AetheriaProfiler.CreateMarker("Environment/Lighting", "Copy Cubemap Faces");

        private static readonly AetheriaProfiler.Marker AmbientReadbackProfile =
            AetheriaProfiler.CreateMarker("Environment/Lighting", "Ambient Probe Readback");

        private const float AbruptCelestialAngularDeltaDegrees = 10.0f;
        private const float AbruptCelestialColorDelta = 0.1f;
        private const int CloudAmbientProbeResolution = 16;

        private struct SkyCompositionPlan
        {
            public ComputeShader TraceShader;
            public ComputeShader CompositeShader;
            public int TraceKernel;
            public int TraceMicroErosionKernel;
            public int CompositeKernel;
            public int CompositeCloudsKernel;
            public int CompositeCloudLayerKernel;
            public int CompositeCloudsCloudLayerKernel;
            public TextureHandle CurrentClouds;
            public TextureHandle CurrentCloudDepth;
            public TextureHandle Source;
            public TextureHandle Transmittance;
            public TextureHandle MultiScattering;
            public TextureHandle GroundIrradiance;
            public TextureHandle SpaceCubemap;
            public TextureHandle CloudLayerTexture;
            public BufferHandle CloudAmbientProbe;
            public SkyRenderGraphResources SkyResources;
            public SkySettings SkySettings;
            public VisualEnvironment VisualEnvironment;
            public SkyRenderer SkyRenderer;
            public AtmosphereFrameData Atmosphere;
            public PhysicallyBasedSky Volume;
            public CelestialBodyPass.GlobalStateData CelestialData;
            public VolumetricCloudsPass.EnvironmentData Clouds;
            public EnvironmentView View;
            public int PerPixelRaymarchSamples;
            public int SpaceRaymarchSamples;
            public int OutputResolution;
            public int CloudResolution;
            public bool CloudsEnabled;
            public bool CloudLayerEnabled;
            public bool IncludeSunInBaking;
            public CloudLayerShaderData CloudLayer;
        }

        private sealed class CloudTracePassData
        {
            public ComputeShader Shader;
            public int Kernel;
            public TextureHandle Output;
            public TextureHandle OutputDepth;
            public TextureHandle Transmittance;
            public PhysicallyBasedSky Volume;
            public float BottomRadius;
            public VolumetricCloudsPass.EnvironmentData Clouds;
            public EnvironmentView View;
            public int Resolution;
        }

        private sealed class SkyCompositePassData
        {
            public ComputeShader Shader;
            public int Kernel;
            public TextureHandle Output;
            public TextureHandle Transmittance;
            public TextureHandle MultiScattering;
            public TextureHandle GroundIrradiance;
            public TextureHandle SpaceCubemap;
            public TextureHandle ResolvedClouds;
            public TextureHandle ResolvedCloudDepth;
            public TextureHandle CloudLayerTexture;
            public BufferHandle CloudAmbientProbe;
            public SkyRenderGraphResources SkyResources;
            public SkySettings SkySettings;
            public VisualEnvironment VisualEnvironment;
            public SkyRenderer SkyRenderer;
            public AtmosphereFrameData Atmosphere;
            public PhysicallyBasedSky Volume;
            public CelestialBodyPass.GlobalStateData CelestialData;
            public EnvironmentView View;
            public int PerPixelRaymarchSamples;
            public int SpaceRaymarchSamples;
            public int OutputResolution;
            public bool HasClouds;
            public bool HasCloudLayer;
            public bool IncludeSunInBaking;
            public CloudLayerShaderData CloudLayer;
        }

        private readonly struct EnvironmentView
        {
            public readonly Vector3 ObserverPositionWs;

            public EnvironmentView(Vector3 observerPositionWs) => ObserverPositionWs = observerPositionWs;
        }

        private sealed class AmbientPassData
        {
            public ComputeShader Shader;
            public int Kernel;
            public int SourceProperty;
            public TextureHandle Source;
            public BufferHandle Output;
        }

        private sealed class AmbientReadbackPassData
        {
            public GraphicsBuffer Buffer;
            public Action<AsyncGPUReadbackRequest> Callback;
        }

        private sealed class TemporalBlendPassData
        {
            public ComputeShader Shader;
            public int Kernel;
            public TextureHandle Previous;
            public TextureHandle Target;
            public TextureHandle Output;
            public int Resolution;
            public float BlendFactor;
        }

        private sealed class CopyCubemapPassData
        {
            public TextureHandle Source;
            public TextureHandle Destination;
            public int MipCount;
        }

        private sealed class AmbientReadbackSlot
        {
            private readonly EnvironmentLightingPass _owner;

            public bool InFlight;
            public ulong Generation;
            public readonly Action<AsyncGPUReadbackRequest> Callback;

            public AmbientReadbackSlot(EnvironmentLightingPass owner)
            {
                _owner = owner;
                Callback = OnReadback;
            }

            public void OnReadback(AsyncGPUReadbackRequest request)
            {
                ulong completedGeneration = Generation;
                InFlight = false;
                _owner.OnAmbientReadback(request, completedGeneration);
            }
        }

        private static readonly BaseRenderFunc<TemporalBlendPassData, ComputeGraphContext>
            TemporalBlendRenderFunction = static (data, context) =>
            {
                var writer = ShaderValueWriter.ForCompute(context.cmd, data.Shader);
                writer.SetFloat(
                    ShaderProperties.EnvironmentLighting.LightingBlendFactor,
                    data.BlendFactor);

                for (int mip = 0;
                     mip < EnvironmentConstants.ConvolutionMipCount;
                     ++mip)
                {
                    int mipResolution = Mathf.Max(1, data.Resolution >> mip);
                    context.cmd.SetComputeTextureParam(
                        data.Shader,
                        data.Kernel,
                        ShaderProperties.EnvironmentLighting.PreviousLightingCubemap,
                        data.Previous);
                    context.cmd.SetComputeTextureParam(
                        data.Shader,
                        data.Kernel,
                        ShaderProperties.EnvironmentLighting.TargetLightingCubemap,
                        data.Target);
                    context.cmd.SetComputeTextureParam(
                        data.Shader,
                        data.Kernel,
                        ShaderProperties.EnvironmentLighting.Output,
                        data.Output,
                        mip);
                    writer.SetInt(
                        ShaderProperties.EnvironmentLighting.LightingMipResolution,
                        mipResolution);
                    writer.SetInt(
                        ShaderProperties.EnvironmentLighting.LightingMipLevel,
                        mip);
                    context.cmd.DispatchCompute(
                        data.Shader,
                        data.Kernel,
                        CoreUtils.DivRoundUp(mipResolution, 8),
                        CoreUtils.DivRoundUp(mipResolution, 8),
                        6);
                }
            };

        private static readonly BaseRenderFunc<CopyCubemapPassData, UnsafeGraphContext>
            FaceMipCopyRenderFunction = static (data, context) =>
            {
                var command = CommandBufferHelpers.GetNativeCommandBuffer(context.cmd);
                RTHandle sourceHandle = data.Source;
                RTHandle destinationHandle = data.Destination;
                var sourceTexture = sourceHandle.rt;
                var destinationTexture = destinationHandle.rt;

                for (int mip = 0; mip < data.MipCount; ++mip)
                for (int face = 0; face < 6; ++face)
                    command.CopyTexture(
                        sourceTexture, face, mip,
                        destinationTexture, face, mip);
            };

        private static readonly BaseRenderFunc<CloudTracePassData, ComputeGraphContext>
            CloudTraceRenderFunction = static (data, context) =>
            {
                BindCloudTrace(context.cmd, data);
                BindTexture(
                    context.cmd,
                    data.Shader,
                    data.Kernel,
                    ShaderProperties.EnvironmentLighting.CloudTraceOutput,
                    data.Output);
                BindTexture(
                    context.cmd,
                    data.Shader,
                    data.Kernel,
                    ShaderProperties.EnvironmentLighting.CloudTraceDepthOutput,
                    data.OutputDepth);
                Dispatch(context.cmd, data.Shader, data.Kernel, data.Resolution);
            };

        private static readonly BaseRenderFunc<SkyCompositePassData, ComputeGraphContext>
            SkyCompositeRenderFunction = static (data, context) =>
            {
                BindComposite(context.cmd, data);
                if (data.HasClouds)
                {
                    BindTexture(
                        context.cmd,
                        data.Shader,
                        data.Kernel,
                        ShaderProperties.EnvironmentLighting.ResolvedClouds,
                        data.ResolvedClouds);
                    BindTexture(
                        context.cmd,
                        data.Shader,
                        data.Kernel,
                        ShaderProperties.EnvironmentLighting.ResolvedCloudDepth,
                        data.ResolvedCloudDepth);
                }
                BindTexture(
                    context.cmd,
                    data.Shader,
                    data.Kernel,
                    ShaderProperties.EnvironmentLighting.Output,
                    data.Output);
                Dispatch(context.cmd, data.Shader, data.Kernel, data.OutputResolution);
            };

        private static readonly BaseRenderFunc<AmbientPassData, ComputeGraphContext>
            AmbientProbeRenderFunction = static (data, context) =>
            {
                context.cmd.SetComputeTextureParam(data.Shader, data.Kernel,
                    data.SourceProperty, data.Source);
                context.cmd.SetComputeBufferParam(
                    data.Shader, data.Kernel,
                    ShaderProperties.Textures.AmbientProbeOutputBufferId,
                    data.Output);
                context.cmd.DispatchCompute(data.Shader, data.Kernel, 1, 1, 1);
            };


        private static readonly BaseRenderFunc<AmbientReadbackPassData, UnsafeGraphContext>
            AmbientReadbackRenderFunction = static (data, context) =>
                CommandBufferHelpers.GetNativeCommandBuffer(context.cmd)
                    .RequestAsyncReadback(data.Buffer, data.Callback);

        private readonly ComputeShader _traceShader;
        private readonly ComputeShader _compositeShader;
        private readonly ComputeShader _temporalBlendShader;
        private readonly ComputeShader _ambientProbeShader;
        private readonly VolumetricCloudsPass _cloudsPass;
        private readonly EnvironmentRenderingContext _context;
        private readonly EnvironmentCubemapConditioner _cubemapConditioner;
        private readonly AmbientReadbackSlot[] _ambientReadbacks;
        private readonly int _traceKernel;
        private readonly int _traceMicroErosionKernel;
        private readonly int _blendLightingCubemapKernel;
        private readonly int _ambientProbeKernel;
        private readonly int _ambientProbeArrayKernel;

        private LightingSkyResolution _resolution;
        private EnvironmentUpdateMode _updateMode = EnvironmentUpdateMode.Realtime;
        private float _updatePeriod;
        private bool _includeSunInBaking;
        private bool _wasActive;
        private int _lastConfigurationHash = -1;
        private int _lastDynamicHash = -1;
        private float _updateElapsed;
        private float _lastRealtime = -1.0f;
        private float _transitionStartRealtime = -1.0f;
        private float _lastCaptureRealtime = -1.0f;
        private float _transitionDuration;
        private bool _updateRequested = true;
        private bool _hasValidEnvironment;
        private bool _transitionUsesHistory;
        private int _capturedCelestialCount = -1;

        private readonly Vector3[] _capturedCelestialDirections =
            new Vector3[AtmosphereData.MaxCelestialLights];

        private readonly Color[] _capturedCelestialColors =
            new Color[AtmosphereData.MaxCelestialLights];

        private ulong _nextAmbientProbeGeneration;
        private ulong _latestAppliedAmbientProbeGeneration;
        private SphericalHarmonicsL2 _latestAmbientProbe;
        private SphericalHarmonicsL2 _previousCpuAmbientProbe;
        private SphericalHarmonicsL2 _targetCpuAmbientProbe;
        private bool _hasLatestAmbientProbe;
        private bool _hasCpuAmbientTarget;
        private int _ambientProbeBindingSceneHandle = -1;
        private bool _acceptAmbientProbeReadback;
        private bool _disposed;
        private ulong _completedUpdateGeneration;
        private Vector3? _captureObserverPositionOverride;

        public EnvironmentLightingPass(
            EnvironmentLightingResources resources,
            VolumetricCloudsPass cloudsPass,
            bool retainRadiance = false)
        {
            _traceShader = resources.CloudTrace;
            _compositeShader = resources.Composite;
            _temporalBlendShader = resources.TemporalBlend;
            _ambientProbeShader = resources.AmbientProbeConvolution;
            _cloudsPass = cloudsPass;
            _context = new EnvironmentRenderingContext(retainRadiance);
            _cubemapConditioner = new EnvironmentCubemapConditioner(resources.GgxConvolve);

            _traceKernel = _traceShader.FindKernel("TraceFullSphere");
            _traceMicroErosionKernel = _traceShader.FindKernel("TraceFullSphereMicroErosion");
            _blendLightingCubemapKernel = _temporalBlendShader.FindKernel("BlendLightingCubemap");
            _ambientProbeKernel = _ambientProbeShader.FindKernel("ProjectFromCubemap");
            _ambientProbeArrayKernel = _ambientProbeShader.FindKernel("ProjectFromCubemapArray");
            _ambientReadbacks = new[]
            {
                new AmbientReadbackSlot(this),
                new AmbientReadbackSlot(this)
            };
        }

        public LightingSkyResolution Resolution
        {
            get => _resolution;
            set
            {
                if (_resolution == value)
                    return;
                _resolution = value;
                RequestUpdate();
            }
        }

        public EnvironmentUpdateMode UpdateMode
        {
            get => _updateMode;
            set
            {
                if (_updateMode == value)
                    return;
                _updateMode = value;
                RequestUpdate();
            }
        }

        public float UpdatePeriod
        {
            get => _updatePeriod;
            set
            {
                float period = Mathf.Max(0.0f, value);
                if (Mathf.Approximately(_updatePeriod, period))
                    return;
                _updatePeriod = period;
                RequestUpdate();
            }
        }

        public bool IncludeSunInBaking
        {
            get => _includeSunInBaking;
            set
            {
                if (_includeSunInBaking == value)
                    return;
                _includeSunInBaking = value;
                RequestUpdate();
            }
        }

        public bool UpdateAllowed { get; set; } = true;
        public bool UseSkySettingsUpdate { get; set; } = true;

        public RenderTexture RadianceTexture =>
            IsReady ? _context.TargetRadianceCubemap : null;

        public bool IsReady =>
            _hasValidEnvironment && _context.HasValidResources;

        public ulong CompletedUpdateGeneration => _completedUpdateGeneration;

        public Vector3? CaptureObserverPositionOverride
        {
            get => _captureObserverPositionOverride;
            set
            {
                if (_captureObserverPositionOverride == value)
                    return;

                _captureObserverPositionOverride = value;
                RequestUpdate();
            }
        }

        public void RequestUpdate() => _updateRequested = true;

        public void PrepareForCamera(bool active)
        {
            if (!active)
            {
                if (_wasActive)
                    ResetUpdateState();

                _wasActive = false;
                _acceptAmbientProbeReadback = false;
                _latestAppliedAmbientProbeGeneration = _nextAmbientProbeGeneration;

                EnvironmentSettings.ApplyDefaultReflections();
                EnvironmentSettings.ApplyDefaultAmbientSource();
                return;
            }

            _wasActive = true;
            _acceptAmbientProbeReadback = true;

            EnsureContext();

            if (IsReady)
            {
                EnvironmentSettings.ApplyReflection(_context.LightingCubemap);
                UpdateCpuAmbientProbe(forceSceneBinding: false);
            }
            else
            {
                EnvironmentSettings.ApplyDefaultReflections();
                EnvironmentSettings.ApplyDefaultAmbientSource();
            }
        }

        public void RecordRenderGraph(RenderGraph renderGraph,
            ContextContainer frameData)
        {
            if (!frameData.Contains<SkyFrameData>())
                return;

            var sky = frameData.Get<SkyFrameData>();
            ref var output = ref sky.Environment;
            output.Reset();

            var atmosphere = frameData.Contains<AtmosphereFrameData>()
                ? frameData.Get<AtmosphereFrameData>()
                : null;

            if (!UpdateAllowed || !sky.IsActive || sky.Settings == null ||
                sky.Renderer == null || !sky.VisualEnvironment)
            {
                PopulateCachedFrameData(renderGraph, ref output);
                return;
            }

            if (UseSkySettingsUpdate)
            {
                UpdateMode = sky.Settings.updateMode.value;
                UpdatePeriod = sky.Settings.updatePeriod.value;
                _includeSunInBaking = sky.Settings.includeSunInBaking.value;
            }

            if (!sky.Renderer.IsEnvironmentReady(sky.Settings, sky.VisualEnvironment, atmosphere))
            {
                PopulateCachedFrameData(renderGraph, ref output);
                return;
            }

            var cameraData = frameData.Get<UniversalCameraData>();
            var camera = cameraData.camera;
            if (!camera)
            {
                PopulateCachedFrameData(renderGraph, ref output);
                return;
            }

            var view = new EnvironmentView(
                sky.Renderer.RequiresAtmosphere && atmosphere != null
                    ? GetCaptureObserverPosition(atmosphere)
                    : camera.transform.position);

            var preset = EnvironmentLightingPreset.From(_resolution);
            EnsureContext(preset);
            if (!_context.HasValidResources)
                return;

            bool atmosphereAvailable =
                atmosphere is { IsActive: true, CameraData: not null, Volume: not null };
            VolumetricCloudsPass.EnvironmentState cloudState = default;
            bool cloudsEnabled =
                sky.Renderer.RequiresAtmosphere && atmosphereAvailable &&
                _cloudsPass.TryBuildEnvironmentState(atmosphere, view.ObserverPositionWs, out cloudState);
            var clouds = cloudsEnabled
                ? _cloudsPass.ImportEnvironmentResources(renderGraph, cloudState)
                : default;
            var cloudLayer =
                sky.Renderer.RequiresAtmosphere && frameData.Contains<CloudLayerFrameData>()
                    ? frameData.Get<CloudLayerFrameData>()
                    : null;
            bool cloudLayerEnabled =
                cloudLayer is { IsActive: true } && cloudLayer.Texture.IsValid();

            int configurationHash = ComputeConfigurationHash(
                sky, atmosphere, cloudState, cloudsEnabled, cloudLayer,
                _includeSunInBaking, view);
            int dynamicHash = ComputeDynamicHash(
                sky, atmosphere, cloudState, cloudsEnabled, camera);

            TickUpdateTimer(Time.realtimeSinceStartup);
            bool abruptCelestialChange = atmosphereAvailable && HasAbruptCelestialChange(atmosphere);
            bool shouldUpdate = ShouldUpdate(
                configurationHash, dynamicHash, abruptCelestialChange, out bool immediate);

            if (shouldUpdate)
            {
                var retainedRadiance = _context.TargetRadianceCubemap;
                var targetRadiance = retainedRadiance
                    ? _context.ImportTargetRadianceCubemap(renderGraph)
                    : CreateRadianceCubemap(renderGraph, preset.OutputResolution);
                var skyPlan = CreateSkyPassData(
                    sky, atmosphere, view, preset, clouds, cloudsEnabled,
                    cloudLayer, cloudLayerEnabled, renderGraph);
                var targetLighting = _context.ImportTargetLightingCubemap(renderGraph);
                var targetAmbientProbe = _context.ImportNextAmbientProbeTarget(renderGraph);

                if (_cubemapConditioner.Prepare(
                        targetRadiance,
                        preset.OutputResolution,
                        EnvironmentRenderingContext.LightingFormat,
                        CoreUtils.GetMipCount(preset.OutputResolution)))
                {
                    if (cloudLayerEnabled)
                    {
                        var backgroundSky = skyPlan;
                        backgroundSky.Source = CreateCubemapArrayTexture(
                            renderGraph,
                            "Cloud Layer Ambient Sky",
                            CloudAmbientProbeResolution,
                            format: EnvironmentRenderingContext.LightingFormat);
                        backgroundSky.OutputResolution = CloudAmbientProbeResolution;
                        backgroundSky.CurrentClouds = TextureHandle.nullHandle;
                        backgroundSky.CurrentCloudDepth = TextureHandle.nullHandle;
                        backgroundSky.CloudsEnabled = false;
                        backgroundSky.CloudLayerEnabled = false;
                        backgroundSky.CloudAmbientProbe = BufferHandle.nullHandle;

                        var cloudAmbientProbe = renderGraph.CreateBuffer(new BufferDesc(
                            EnvironmentRenderingContext.AmbientProbeCoefficientCount,
                            sizeof(float),
                            GraphicsBuffer.Target.Structured)
                        {
                            name = "Cloud Layer Background Ambient Probe"
                        });

                        RecordSkyComposite(renderGraph, backgroundSky);
                        RecordAmbientProbeFromArray(
                            renderGraph,
                            backgroundSky.Source,
                            cloudAmbientProbe);
                        skyPlan.CloudAmbientProbe = cloudAmbientProbe;
                    }

                    RecordSkyComposition(renderGraph, skyPlan);
                    _cubemapConditioner.RecordPrepared(
                        renderGraph,
                        skyPlan.Source,
                        targetRadiance,
                        targetLighting,
                        preset.OutputResolution,
                        EnvironmentRenderingContext.LightingFormat);

                    RecordAmbientProbe(renderGraph, targetRadiance, targetAmbientProbe);
                    output.PendingTargetCubemap = targetLighting;
                    output.PendingTargetAmbientProbe = targetAmbientProbe;
                    CommitUpdate(configurationHash, dynamicHash, immediate);
                    if (atmosphereAvailable)
                        CaptureCelestialState(atmosphere);
                    _context.CommitAmbientTargetRotation();
                }
            }

            PopulateCachedFrameData(renderGraph, ref output);
        }

        internal void RecordTemporalBlend(
            RenderGraph graph,
            ref EnvironmentRadianceFrameData environment,
            bool targetUpdated)
        {
            if (!IsReady) return;

            var previousLighting = _context.ImportPreviousLightingCubemap(graph);
            var targetLighting = targetUpdated && environment.PendingTargetCubemap.IsValid()
                ? environment.PendingTargetCubemap
                : _context.ImportTargetLightingCubemap(graph);
            var currentLighting = environment.LightingCubemap;
            var previousAmbient = _context.ImportPreviousAmbientProbe(graph);
            var targetAmbient = targetUpdated && environment.PendingTargetAmbientProbe.IsValid()
                ? environment.PendingTargetAmbientProbe
                : _context.ImportTargetAmbientProbe(graph);

            if (!previousLighting.IsValid() ||
                !targetLighting.IsValid() ||
                !currentLighting.IsValid())
            {
                return;
            }

            float blendFactor = _transitionUsesHistory
                ? (targetUpdated ? 0.0f : GetTransitionBlendFactor())
                : 1.0f;

            if (!_transitionUsesHistory)
            {
                if (targetUpdated)
                    RecordFaceMipCopy(graph, targetLighting, currentLighting, EnvironmentConstants.ConvolutionMipCount);
            }
            else if (targetUpdated)
            {
                // Start at the currently displayed environment without reading the
                // freshly generated target until the next frame.
                RecordFaceMipCopy(graph, currentLighting, previousLighting, EnvironmentConstants.ConvolutionMipCount);
            }
            else
            {
                var blendedFaces = CreateCubemapArrayTexture(graph, "Blended Lighting Cubemap Faces",
                    _context.OutputResolution, useMipMap: true, format: EnvironmentRenderingContext.LightingFormat);
                RecordTemporalBlendPass(graph, previousLighting, targetLighting, blendedFaces, blendFactor);
                RecordFaceMipCopy(graph, blendedFaces, currentLighting, EnvironmentConstants.ConvolutionMipCount);
            }
            if (_transitionUsesHistory && !targetUpdated && blendFactor >= 1.0f)
                _transitionUsesHistory = false;

            environment.PreviousAmbientProbe = previousAmbient;
            environment.TargetAmbientProbe = targetAmbient;
            environment.AmbientProbeBlendFactor = blendFactor;

            if (targetUpdated && targetAmbient.IsValid())
                RecordAmbientReadback(graph, targetAmbient, _context.TargetAmbientProbe);
        }

        private void RecordTemporalBlendPass(
            RenderGraph graph,
            TextureHandle previous,
            TextureHandle target,
            TextureHandle output,
            float blendFactor)
        {
            using var profiledPass = TemporalBlendProfile.AddComputePass<TemporalBlendPassData>(
                graph, out var pass);

            var builder = profiledPass.Builder;
            pass.Shader = _temporalBlendShader;
            pass.Kernel = _blendLightingCubemapKernel;
            pass.Previous = previous;
            pass.Target = target;
            pass.Output = output;
            pass.Resolution = _context.OutputResolution;
            pass.BlendFactor = blendFactor;
            builder.UseTexture(pass.Previous);
            builder.UseTexture(pass.Target);
            builder.UseTexture(pass.Output, AccessFlags.Write);
            builder.AllowPassCulling(false);
            builder.SetRenderFunc(TemporalBlendRenderFunction);
        }

        private static void RecordFaceMipCopy(
            RenderGraph graph,
            TextureHandle source,
            TextureHandle destination,
            int mipCount)
        {
            using var profiledPass = FaceCopyProfile.AddUnsafePass<CopyCubemapPassData>(
                graph, out var pass);
            
            var builder = profiledPass.Builder;
            pass.Source = source;
            pass.Destination = destination;
            pass.MipCount = mipCount;
            builder.UseTexture(pass.Source);
            builder.UseTexture(pass.Destination, AccessFlags.Write);
            builder.AllowPassCulling(false);
            builder.SetRenderFunc(FaceMipCopyRenderFunction);
        }

        private void PopulateCachedFrameData(
            RenderGraph renderGraph,
            ref EnvironmentRadianceFrameData output)
        {
            if (!IsReady) return;

            var lighting = _context.ImportLightingCubemap(renderGraph);
            var previousAmbientProbe = _context.ImportPreviousAmbientProbe(renderGraph);
            var targetAmbientProbe = _context.ImportTargetAmbientProbe(renderGraph);

            if (!lighting.IsValid() ||
                !previousAmbientProbe.IsValid() ||
                !targetAmbientProbe.IsValid())
                return;

            output.IsReady = true;
            output.PreviousAmbientProbe = previousAmbientProbe;
            output.TargetAmbientProbe = targetAmbientProbe;
            output.AmbientProbeBlendFactor = GetTransitionBlendFactor();
            output.LightingCubemap = lighting;
        }

        private void EnsureContext() => EnsureContext(EnvironmentLightingPreset.From(_resolution));

        private void EnsureContext(in EnvironmentLightingPreset preset)
        {
            if (_context.RequiresReallocation(
                    preset.OutputResolution,
                    preset.CloudTraceResolution))
                EnvironmentSettings.ApplyDefaultReflections();

            if (_context.Ensure(preset.OutputResolution, preset.CloudTraceResolution))
                ResetUpdateState();
        }

        private void ResetUpdateState()
        {
            _lastConfigurationHash = -1;
            _lastDynamicHash = -1;
            _updateElapsed = 0.0f;
            _lastRealtime = -1.0f;
            _transitionStartRealtime = -1.0f;
            _lastCaptureRealtime = -1.0f;
            _hasValidEnvironment = false;
            _updateRequested = true;
            _transitionUsesHistory = false;
            _capturedCelestialCount = -1;
            _hasCpuAmbientTarget = false;
        }

        private void TickUpdateTimer(float realtime)
        {
            if (_lastRealtime >= 0.0f)
                _updateElapsed += Mathf.Max(0.0f, realtime - _lastRealtime);

            _lastRealtime = realtime;
        }

        private bool ShouldUpdate(
            int configurationHash,
            int dynamicHash,
            bool abruptCelestialChange,
            out bool immediate)
        {
            immediate = !_hasValidEnvironment || _updateRequested ||
                        (_updateMode == EnvironmentUpdateMode.Realtime && _updatePeriod <= 0.0f);
            
            if (immediate)
                return true;

            if (_updateMode == EnvironmentUpdateMode.OnDemand)
                return false;

            if (configurationHash != _lastConfigurationHash)
            {
                immediate = true;
                return true;
            }

            if (abruptCelestialChange)
            {
                immediate = true;
                return true;
            }

            if (_updateMode == EnvironmentUpdateMode.OnChanged)
            {
                immediate = true;
                return dynamicHash != _lastDynamicHash;
            }

            // Realtime normally follows the user-selected period. A celestial state that
            // has diverged sharply from the last captured target is handled above as an
            // immediate rebuild, so stale lighting is never held for a full interval.
            return _updateElapsed >= _updatePeriod;
        }

        private void CommitUpdate(
            int configurationHash,
            int dynamicHash,
            bool immediate)
        {
            if (_hasLatestAmbientProbe)
                _previousCpuAmbientProbe = _latestAmbientProbe;

            _lastConfigurationHash = configurationHash;
            _lastDynamicHash = dynamicHash;
            _updateElapsed = 0.0f;
            _hasValidEnvironment = true;
            _updateRequested = false;
            _completedUpdateGeneration++;
            
            float now = Time.realtimeSinceStartup;
            
            _transitionDuration = _lastCaptureRealtime >= 0.0f
                ? Mathf.Max(0.0f, now - _lastCaptureRealtime)
                : 0.0f;
            _transitionUsesHistory = !immediate &&
                                     _transitionDuration > 0.0f;
            _transitionStartRealtime = now;
            _lastCaptureRealtime = now;
        }

        private float GetTransitionBlendFactor()
        {
            if (!_transitionUsesHistory || _transitionDuration <= 0.0f)
                return 1.0f;

            return Mathf.Clamp01(
                (Time.realtimeSinceStartup - _transitionStartRealtime) /
                _transitionDuration);
        }

        private bool HasAbruptCelestialChange(AtmosphereFrameData atmosphere)
        {
            int count = atmosphere.CelestialData.LightCount;

            if (_capturedCelestialCount < 0)
                return false;

            if (count != _capturedCelestialCount)
                return true;

            for (int i = 0; i < count; ++i)
            {
                var directionData = atmosphere.CameraData.CelestialDirs[i];
                var direction = new Vector3(directionData.x, directionData.y, directionData.z);

                if (direction.sqrMagnitude > Mathf.Epsilon &&
                    Vector3.Angle(
                        _capturedCelestialDirections[i],
                        direction.normalized) > AbruptCelestialAngularDeltaDegrees)
                    return true;

                if (ColorDistance(
                        _capturedCelestialColors[i],
                        atmosphere.CameraData.CelestialColors[i]) > AbruptCelestialColorDelta)
                    return true;
            }

            return false;
        }

        private void CaptureCelestialState(AtmosphereFrameData atmosphere)
        {
            int count = atmosphere.CelestialData.LightCount;

            for (int i = 0; i < count; ++i)
            {
                var directionData = atmosphere.CameraData.CelestialDirs[i];
                var direction = new Vector3(directionData.x, directionData.y, directionData.z);
                _capturedCelestialDirections[i] = direction.sqrMagnitude > Mathf.Epsilon
                    ? direction.normalized
                    : Vector3.zero;
                _capturedCelestialColors[i] = atmosphere.CameraData.CelestialColors[i];
            }

            _capturedCelestialCount = count;
        }

        private static float ColorDistance(Color a, Color b)
        {
            float redDelta = Mathf.Abs(a.r - b.r);
            float greenDelta = Mathf.Abs(a.g - b.g);
            float blueDelta = Mathf.Abs(a.b - b.b);
            return Mathf.Max(redDelta, Mathf.Max(greenDelta, blueDelta));
        }

        private SkyCompositionPlan CreateSkyPassData(
            SkyFrameData sky,
            AtmosphereFrameData atmosphere,
            EnvironmentView view,
            in EnvironmentLightingPreset preset,
            VolumetricCloudsPass.EnvironmentData clouds,
            bool cloudsEnabled,
            CloudLayerFrameData cloudLayer,
            bool cloudLayerEnabled,
            RenderGraph renderGraph)
        {
            var currentClouds = cloudsEnabled
                ? CreateCubemapArrayTexture(renderGraph, "Current Clouds", preset.CloudTraceResolution)
                : TextureHandle.nullHandle;
            var currentCloudDepth = cloudsEnabled
                ? CreateCubemapArrayTexture(
                    renderGraph,
                    "Current Cloud Weighted Distance",
                    preset.CloudTraceResolution,
                    format: GraphicsFormat.R32_SFloat)
                : TextureHandle.nullHandle;

            return new SkyCompositionPlan
            {
                TraceShader = _traceShader,
                CompositeShader = _compositeShader,
                TraceKernel = _traceKernel,
                TraceMicroErosionKernel = _traceMicroErosionKernel,
                CompositeKernel = sky.Renderer.GetEnvironmentKernel(false, false),
                CompositeCloudsKernel = sky.Renderer.GetEnvironmentKernel(true, false),
                CompositeCloudLayerKernel = sky.Renderer.GetEnvironmentKernel(false, true),
                CompositeCloudsCloudLayerKernel = sky.Renderer.GetEnvironmentKernel(true, true),
                CurrentClouds = currentClouds,
                CurrentCloudDepth = currentCloudDepth,
                Source = CreateCubemapArrayTexture(
                    renderGraph, "Sky Capture", preset.OutputResolution,
                    format: EnvironmentRenderingContext.LightingFormat),
                Transmittance = atmosphere?.Transmittance ?? TextureHandle.nullHandle,
                MultiScattering = atmosphere?.MultiScatter ?? TextureHandle.nullHandle,
                GroundIrradiance = atmosphere?.GroundIrradiance ?? TextureHandle.nullHandle,
                SpaceCubemap = atmosphere?.SpaceCubemap ?? TextureHandle.nullHandle,
                CloudLayerTexture = cloudLayerEnabled ? cloudLayer.Texture : TextureHandle.nullHandle,
                CloudAmbientProbe = BufferHandle.nullHandle,
                SkyResources = sky.Renderer.ImportEnvironmentResources(renderGraph, sky.Settings),
                SkySettings = sky.Settings,
                VisualEnvironment = sky.VisualEnvironment,
                SkyRenderer = sky.Renderer,
                Atmosphere = atmosphere,
                Volume = atmosphere?.Volume,
                CelestialData = atmosphere?.CelestialData ?? default,
                Clouds = clouds,
                View = view,
                PerPixelRaymarchSamples = atmosphere?.PerPixelRaymarchSamples ?? 0,
                SpaceRaymarchSamples = atmosphere?.SpaceRaymarchSamples ?? 0,
                OutputResolution = preset.OutputResolution,
                CloudResolution = preset.CloudTraceResolution,
                CloudsEnabled = cloudsEnabled,
                CloudLayerEnabled = cloudLayerEnabled,
                IncludeSunInBaking = _includeSunInBaking,
                CloudLayer = cloudLayerEnabled ? cloudLayer.ShaderData : default
            };
        }

        private static TextureHandle CreateCubemapArrayTexture(
            RenderGraph graph,
            string name,
            int resolution,
            bool useMipMap = false,
            GraphicsFormat format = GraphicsFormat.R16G16B16A16_SFloat) =>
            graph.CreateTexture(new TextureDesc(resolution, resolution)
            {
                name = name,
                dimension = TextureDimension.Tex2DArray,
                slices = 6,
                format = format,
                enableRandomWrite = true,
                useMipMap = useMipMap,
                autoGenerateMips = false,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            });

        private static TextureHandle CreateRadianceCubemap(RenderGraph graph, int resolution) =>
            graph.CreateTexture(new TextureDesc(resolution, resolution)
            {
                name = "Lighting Sky Radiance",
                dimension = TextureDimension.Cube,
                slices = 1,
                format = EnvironmentRenderingContext.LightingFormat,
                enableRandomWrite = false,
                useMipMap = true,
                autoGenerateMips = false,
                filterMode = FilterMode.Trilinear,
                wrapMode = TextureWrapMode.Clamp,
                clearBuffer = false,
                discardBuffer = true
            });

        private static void RecordSkyComposition(RenderGraph graph, SkyCompositionPlan sky)
        {
            if (sky.CloudsEnabled)
                RecordCloudTrace(graph, sky);

            RecordSkyComposite(graph, sky);
        }

        private static void RecordCloudTrace(RenderGraph graph, SkyCompositionPlan sky)
        {
            using var profiledPass = CloudTraceProfile.AddComputePass<CloudTracePassData>(
                graph, out var pass);
            
            var builder = profiledPass.Builder;
            pass.Shader = sky.TraceShader;
            pass.Kernel = sky.Clouds.Payload.MicroErosion ? sky.TraceMicroErosionKernel : sky.TraceKernel;
            pass.Output = sky.CurrentClouds;
            pass.OutputDepth = sky.CurrentCloudDepth;
            pass.Transmittance = sky.Transmittance;
            pass.Volume = sky.Volume;
            pass.BottomRadius = sky.Atmosphere?.VisualEnvironment ? sky.Atmosphere.VisualEnvironment.planetRadius.value : 0.0f;
            pass.Clouds = sky.Clouds;
            pass.View = sky.View;
            pass.Resolution = sky.CloudResolution;
            builder.UseTexture(pass.Transmittance);
            DeclareCloudSourceResources(builder, pass.Clouds);
            builder.UseTexture(pass.Output, AccessFlags.Write);
            builder.UseTexture(pass.OutputDepth, AccessFlags.Write);
            // builder.EnableAsyncCompute(true);
            builder.SetRenderFunc(CloudTraceRenderFunction);
        }

        private static void RecordSkyComposite(RenderGraph graph, SkyCompositionPlan sky)
        {
            using var profiledPass = SkyCompositeProfile.AddComputePass<SkyCompositePassData>(
                graph, out var pass);
            
            var builder = profiledPass.Builder;
            pass.Shader = sky.CompositeShader;
            pass.Kernel = SelectCompositeKernel(sky);
            pass.Output = sky.Source;
            pass.Transmittance = sky.Transmittance;
            pass.MultiScattering = sky.MultiScattering;
            pass.GroundIrradiance = sky.GroundIrradiance;
            pass.SpaceCubemap = sky.SpaceCubemap;
            pass.ResolvedClouds = sky.CurrentClouds;
            pass.ResolvedCloudDepth = sky.CurrentCloudDepth;
            pass.CloudLayerTexture = sky.CloudLayerTexture;
            pass.CloudAmbientProbe = sky.CloudAmbientProbe;
            pass.SkyResources = sky.SkyResources;
            pass.SkySettings = sky.SkySettings;
            pass.VisualEnvironment = sky.VisualEnvironment;
            pass.SkyRenderer = sky.SkyRenderer;
            pass.Atmosphere = sky.Atmosphere;
            pass.Volume = sky.Volume;
            pass.CelestialData = sky.CelestialData;
            pass.View = sky.View;
            pass.PerPixelRaymarchSamples = sky.PerPixelRaymarchSamples;
            pass.SpaceRaymarchSamples = sky.SpaceRaymarchSamples;
            pass.OutputResolution = sky.OutputResolution;
            pass.HasClouds = sky.CloudsEnabled;
            pass.HasCloudLayer = sky.CloudLayerEnabled;
            pass.IncludeSunInBaking = sky.IncludeSunInBaking;
            pass.CloudLayer = sky.CloudLayer;
            pass.SkyRenderer.DeclareEnvironmentResources(builder, pass.Atmosphere, pass.SkyResources);
            if (pass.HasCloudLayer)
            {
                builder.UseTexture(pass.CloudLayerTexture);
                if (pass.CloudAmbientProbe.IsValid())
                    builder.UseBuffer(pass.CloudAmbientProbe);
            }
                
            if (pass.HasClouds)
            {
                builder.UseTexture(pass.ResolvedClouds);
                builder.UseTexture(pass.ResolvedCloudDepth);
            }
                
            builder.UseTexture(pass.Output, AccessFlags.Write);
            // builder.EnableAsyncCompute(true);
            builder.SetRenderFunc(SkyCompositeRenderFunction);
        }

        private void RecordAmbientProbe(
            RenderGraph graph,
            TextureHandle radianceCubemap,
            BufferHandle output)
        {
            using var profiledPass = AmbientProfile.AddComputePass<AmbientPassData>(
                graph, out var pass);
            
            var builder = profiledPass.Builder;
            pass.Shader = _ambientProbeShader;
            pass.Kernel = _ambientProbeKernel;
            pass.SourceProperty = ShaderProperties.EnvironmentLighting.AmbientProbeInputCubemap;
            pass.Source = radianceCubemap;
            pass.Output = output;
            
            builder.UseTexture(pass.Source);
            builder.UseBuffer(pass.Output, AccessFlags.Write);
            // builder.EnableAsyncCompute(true);
            builder.SetRenderFunc(AmbientProbeRenderFunction);
        }

        private void RecordAmbientProbeFromArray(
            RenderGraph graph,
            TextureHandle radianceFaces,
            BufferHandle output)
        {
            using var profiledPass = AmbientProfile.AddComputePass<AmbientPassData>(
                graph, out var pass);

            var builder = profiledPass.Builder;
            pass.Shader = _ambientProbeShader;
            pass.Kernel = _ambientProbeArrayKernel;
            pass.SourceProperty = ShaderProperties.EnvironmentLighting.AmbientProbeInputCubemapArray;
            pass.Source = radianceFaces;
            pass.Output = output;

            builder.UseTexture(pass.Source);
            builder.UseBuffer(pass.Output, AccessFlags.Write);
            builder.SetRenderFunc(AmbientProbeRenderFunction);
        }

        private void RecordAmbientReadback(
            RenderGraph graph,
            BufferHandle source,
            GraphicsBuffer sourceBuffer)
        {
            var slot = AcquireAmbientSlot();
            if (slot == null) return;

            using var profiledPass = AmbientReadbackProfile.AddUnsafePass<AmbientReadbackPassData>(
                graph, out var pass);

            var builder = profiledPass.Builder;
            pass.Buffer = sourceBuffer;
            pass.Callback = slot.Callback;
            builder.UseBuffer(source);
            builder.AllowPassCulling(false);
            builder.SetRenderFunc(AmbientReadbackRenderFunction);
        }

        private AmbientReadbackSlot AcquireAmbientSlot()
        {
            for (int i = 0; i < _ambientReadbacks.Length; i++)
            {
                var slot = _ambientReadbacks[i];
                
                if (slot.InFlight) continue;
                
                slot.InFlight = true;
                slot.Generation = ++_nextAmbientProbeGeneration;
                return slot;
            }

            return null;
        }

        private static void BindCloudTrace(
            ComputeCommandBuffer command,
            CloudTracePassData data)
        {
            var writer = ShaderValueWriter.ForCompute(command, data.Shader);
            writer.SetInt(ShaderProperties.EnvironmentLighting.CloudResolution, data.Resolution);
            writer.SetVector(ShaderProperties.EnvironmentLighting.OriginWs, data.View.ObserverPositionWs);
            writer.SetFloat(ShaderProperties.Atmosphere.BottomRadius, data.BottomRadius);
            writer.SetFloat(ShaderProperties.Atmosphere.TopRadius, data.Volume.TopRadius.value);
            
            BindTexture(
                command,
                data.Shader,
                data.Kernel,
                LutDefinitions.TransmittanceId,
                data.Transmittance);
            
            VolumetricCloudsPass.BindEnvironmentCompute(
                command,
                data.Shader,
                data.Kernel,
                data.Clouds);
        }

        private static void BindComposite(ComputeCommandBuffer command,
            SkyCompositePassData data)
        {
            var writer = ShaderValueWriter.ForCompute(command, data.Shader);
            writer.SetInt(ShaderProperties.EnvironmentLighting.OutputResolution, data.OutputResolution);
            writer.SetFloat(ShaderProperties.EnvironmentLighting.IncludeSunInBaking,
                data.IncludeSunInBaking ? 1.0f : 0.0f);
            data.SkyRenderer.BindEnvironmentCompute(
                command, data.Shader, data.Kernel, data.SkySettings,
                data.VisualEnvironment, data.Atmosphere, data.SkyResources, data.View.ObserverPositionWs);

            writer.SetInt(ShaderProperties.CloudLayer.UseAmbientProbeBuffer, 0);

            if (!data.HasCloudLayer) return;

            BindTexture(
                command,
                data.Shader,
                data.Kernel,
                ShaderProperties.CloudLayer.TextureId,
                data.CloudLayerTexture);

            writer.Write(data.CloudLayer);
            writer.SetFloat(
                ShaderProperties.CloudLayer.PlanetaryRadius,
                data.VisualEnvironment ? data.VisualEnvironment.PlanetRadiusMeters : 0.0f);

            if (data.CloudAmbientProbe.IsValid())
            {
                command.SetComputeBufferParam(
                    data.Shader,
                    data.Kernel,
                    ShaderProperties.CloudLayer.AmbientProbeBuffer,
                    data.CloudAmbientProbe);
                writer.SetInt(ShaderProperties.CloudLayer.UseAmbientProbeBuffer, 1);
            }
        }

        private static void DeclareCloudSourceResources(
            IComputeRenderGraphBuilder builder,
            VolumetricCloudsPass.EnvironmentData clouds)
        {
            builder.UseTexture(clouds.CurveLut);
            builder.UseTexture(clouds.CloudIntegrationNoise);
            builder.UseTexture(clouds.CloudShapeNoise);
            builder.UseTexture(clouds.CloudDetailNoise);
        }

        private static int SelectCompositeKernel(SkyCompositionPlan data)
        {
            if (data.CloudsEnabled)
                return data.CloudLayerEnabled
                    ? data.CompositeCloudsCloudLayerKernel
                    : data.CompositeCloudsKernel;

            return data.CloudLayerEnabled
                ? data.CompositeCloudLayerKernel
                : data.CompositeKernel;
        }

        private static int ComputeConfigurationHash(
            SkyFrameData sky,
            AtmosphereFrameData atmosphere,
            in VolumetricCloudsPass.EnvironmentState clouds,
            bool cloudsEnabled,
            CloudLayerFrameData cloudLayer,
            bool includeSunInBaking,
            in EnvironmentView view)
        {
            var hash = new HashCode();
            hash.Add(view.ObserverPositionWs);
            hash.Add(sky.VisualEnvironment.GetHashCode());
            hash.Add(sky.Settings.GetHashCode());
            hash.Add(sky.Renderer.GetType());

            if (atmosphere != null)
            {
                hash.Add(atmosphere.SpaceCubemap.IsValid());
                hash.Add(ComputeCloudStructuralHash(atmosphere, clouds, cloudsEnabled));
            }

            hash.Add(includeSunInBaking);
            if (cloudLayer is not { IsActive: true })
                return hash.ToHashCode();

            hash.Add(cloudLayer.TextureResource ? cloudLayer.TextureResource.GetInstanceID() : 0);
            // Match HDRP: runtime scrollFactor and the current ambient probe are rendering state,
            // not cloud-settings state. Including them here forces an immediate environment
            // rebuild every frame and creates a lighting feedback loop.
            hash.Add(cloudLayer.SettingsHash);
            return hash.ToHashCode();
        }

        private static int ComputeDynamicHash(
            SkyFrameData sky,
            AtmosphereFrameData atmosphere,
            in VolumetricCloudsPass.EnvironmentState clouds,
            bool cloudsEnabled,
            Camera camera)
        {
            var hash = new HashCode();
            hash.Add(sky.Renderer.GetDynamicHash(sky.Settings, sky.VisualEnvironment, camera));
            if (atmosphere is { IsActive: true, CameraData: not null })
            {
                hash.Add(atmosphere.CameraData.CelestialStateHash);
                hash.Add(ComputeCloudLightingHash(clouds, cloudsEnabled));
            }
            return hash.ToHashCode();
        }

        private Vector3 GetCaptureObserverPosition(AtmosphereFrameData atmosphere)
        {
            if (_captureObserverPositionOverride.HasValue)
                return _captureObserverPositionOverride.Value;

            if (atmosphere.RenderingSpace == RenderingSpace.Camera)
                return Vector3.zero;

            return atmosphere.PlanetCenterWS + Vector3.up * atmosphere.PlanetRadiusMeters;
        }

        private static int ComputeCloudStructuralHash(
            AtmosphereFrameData atmosphere,
            in VolumetricCloudsPass.EnvironmentState clouds,
            bool cloudsEnabled)
        {
            var hash = new HashCode();
            hash.Add(atmosphere.PlanetCenterWS);
            hash.Add(atmosphere.PlanetRadiusMeters);
            hash.Add(cloudsEnabled);

            if (!cloudsEnabled)
                return hash.ToHashCode();

            hash.Add(clouds.Payload.OriginWs);
            hash.Add(clouds.Payload.Parameters.CameraSpace);
            hash.Add(clouds.Payload.MicroErosion);
            hash.Add(clouds.CurveLut ? clouds.CurveLut.GetInstanceID() : 0);
            hash.Add(clouds.CloudIntegrationNoise
                ? clouds.CloudIntegrationNoise.GetInstanceID()
                : 0);
            hash.Add(clouds.CloudShapeNoise
                ? clouds.CloudShapeNoise.GetInstanceID()
                : 0);
            hash.Add(clouds.CloudDetailNoise
                ? clouds.CloudDetailNoise.GetInstanceID()
                : 0);
            hash.Add(clouds.Payload.Parameters.LowestAltitude);
            hash.Add(clouds.Payload.Parameters.HighestAltitude);
            hash.Add(clouds.Payload.Parameters.PlanetCenterWS);
            hash.Add(clouds.Payload.Parameters.PlanetRadius);
            return hash.ToHashCode();
        }

        private static int ComputeCloudLightingHash(
            in VolumetricCloudsPass.EnvironmentState clouds,
            bool cloudsEnabled)
        {
            if (!cloudsEnabled) return 0;

            var p = clouds.Payload.Parameters;
            var hash = new HashCode();
            hash.Add(clouds.Payload.OriginWs);
            hash.Add(p.MaxStepSize);
            hash.Add(p.NumPrimarySteps);
            hash.Add(p.NumLightSteps);
            hash.Add(p.HighestAltitude);
            hash.Add(p.LowestAltitude);
            hash.Add(p.PlanetCenterWS);
            hash.Add(p.PlanetRadius);
            hash.Add(p.CameraSpace);
            hash.Add(p.ShapeNoiseOffset);
            hash.Add(p.VerticalShapeNoiseOffset);
            hash.Add(p.WindDirection);
            hash.Add(p.WindVector);
            hash.Add(p.VerticalShapeDisplacement);
            hash.Add(p.VerticalErosionDisplacement);
            hash.Add(p.MediumWindSpeed);
            hash.Add(p.SmallWindSpeed);
            hash.Add(p.AltitudeDistortion);
            hash.Add(p.DensityMultiplier);
            hash.Add(p.PowderEffectIntensity);
            hash.Add(p.MultiScattering);
            hash.Add(p.ShapeScale);
            hash.Add(p.ShapeFactor);
            hash.Add(p.ErosionScale);
            hash.Add(p.ErosionFactor);
            hash.Add(p.ErosionOcclusion);
            hash.Add(p.MicroErosionScale);
            hash.Add(p.MicroErosionFactor);
            hash.Add(p.FadeInStart);
            hash.Add(p.FadeInDistance);
            hash.Add(p.ScatteringTint);
            hash.Add(p.SunColor);
            hash.Add(p.SunDirection);
            hash.Add(p.AmbientTop);
            hash.Add(p.AmbientBottom);
            return hash.ToHashCode();
        }

        private void OnAmbientReadback(
            AsyncGPUReadbackRequest request,
            ulong generation)
        {
            if (_disposed ||
                !_acceptAmbientProbeReadback ||
                request.hasError ||
                generation <= _latestAppliedAmbientProbeGeneration)
                return;

            var values = request.GetData<float>();

            if (values.Length < EnvironmentRenderingContext.AmbientProbeCoefficientCount)
                return;

            var radianceProbe = new SphericalHarmonicsL2();

            for (int channel = 0; channel < 3; ++channel)
            for (int coefficient = 0; coefficient < 9; ++coefficient)
                radianceProbe[channel, coefficient] = values[channel * 9 + coefficient];

            _latestAppliedAmbientProbeGeneration = generation;
            _targetCpuAmbientProbe =
                AmbientProbeShaderData.ConvertRadianceToUnityDiffuseProbe(radianceProbe);
            _hasCpuAmbientTarget = true;
            UpdateCpuAmbientProbe(forceSceneBinding: true);
        }

        private void UpdateCpuAmbientProbe(bool forceSceneBinding)
        {
            if (!_hasCpuAmbientTarget ||
                EnvironmentSettings.StaticLightingBindingActive)
                return;

            float blendFactor = GetTransitionBlendFactor();

            if (_transitionUsesHistory && _hasLatestAmbientProbe)
            {
                _latestAmbientProbe = LerpAmbientProbe(
                    _previousCpuAmbientProbe,
                    _targetCpuAmbientProbe,
                    blendFactor);
            }
            else
            {
                _latestAmbientProbe = _targetCpuAmbientProbe;
            }

            _hasLatestAmbientProbe = true;
            ApplyLatestAmbientProbe(
                forceSceneBinding || _transitionUsesHistory);
        }

        private static SphericalHarmonicsL2 LerpAmbientProbe(
            SphericalHarmonicsL2 previous,
            SphericalHarmonicsL2 target,
            float factor)
        {
            factor = Mathf.Clamp01(factor);
            var result = new SphericalHarmonicsL2();

            for (int channel = 0; channel < 3; ++channel)
            for (int coefficient = 0; coefficient < 9; ++coefficient)
            {
                result[channel, coefficient] = Mathf.Lerp(
                    previous[channel, coefficient],
                    target[channel, coefficient],
                    factor);
            }

            return result;
        }

        private void ApplyLatestAmbientProbe(bool force)
        {
            if (!_hasLatestAmbientProbe ||
                EnvironmentSettings.StaticLightingBindingActive)
                return;

            int activeSceneHandle = SceneManager.GetActiveScene().handle;
            if (!force &&
                _ambientProbeBindingSceneHandle == activeSceneHandle &&
                RenderSettings.ambientMode == AmbientMode.Custom)
            {
                return;
            }

            EnvironmentSettings.ApplyAmbientProbe(_latestAmbientProbe);
            _ambientProbeBindingSceneHandle = activeSceneHandle;
        }

        private static void BindTexture(
            ComputeCommandBuffer command,
            ComputeShader shader,
            int kernel,
            int id,
            TextureHandle texture) =>
            command.SetComputeTextureParam(shader, kernel, id, texture);

        private static void Dispatch(
            ComputeCommandBuffer command,
            ComputeShader shader,
            int kernel,
            int size) =>
            command.DispatchCompute(
                shader,
                kernel,
                CoreUtils.DivRoundUp(size, 8),
                CoreUtils.DivRoundUp(size, 8),
                6);

        public void Dispose()
        {
            if (_disposed) return;

            _acceptAmbientProbeReadback = false;
            _disposed = true;
            
            EnvironmentSettings.ApplyDefaultReflections();
            EnvironmentSettings.ApplyDefaultAmbientSource();

            _cubemapConditioner.Dispose();
            _context.Dispose();
        }

    }
}
