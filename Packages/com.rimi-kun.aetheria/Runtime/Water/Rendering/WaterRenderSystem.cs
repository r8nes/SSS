using Aetheria.Runtime.Rendering;
using Aetheria.Runtime.Utils;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;

namespace Aetheria.Runtime.Water
{
    internal sealed class WaterRenderSystem
    {
        private WaterResources _resources;
        private WaterSimulationResolution _resolution;
        private bool _fixedResourcesInitialized;
        private bool _resolutionInitialized;
        private readonly WaterSurface[] _activeSurfaces = new WaterSurface[WaterLimits.MaxSurfaceCount];
        private int _activeSurfaceCount;
        private readonly RenderGraphExternalTextureCache _textureCache = new();
        private WaterSimulationPipeline _simulationPipeline;
        private WaterSurfaceRenderer _surfaceRenderer;
        private WaterShadingResources _shading;
        private WaterUnderwaterRenderer _underwater;
        private GraphicsBuffer _currentSectorSwizzleBuffer;
        private bool _renderOwnerRegistered;
        private float _ambientProbeDimmer = 0.5f;
        private float _environmentReflectionIntensity = 1.0f;

        internal bool isInitialized { get; private set; }
        internal WaterSimulationPipeline simulationPipeline => _simulationPipeline;
        internal WaterSurfaceRenderer surfaceRenderer => _surfaceRenderer;
        internal WaterShadingResources shading => _shading;
        internal WaterUnderwaterRenderer underwater => _underwater;
        internal GraphicsBuffer currentSectorSwizzleBuffer => _currentSectorSwizzleBuffer;
        internal WaterSurface[] activeSurfaces => _activeSurfaces;
        internal int activeSurfaceCount => _activeSurfaceCount;

        internal bool Initialize(WaterResources resources)
        {
            Cleanup();
            if (resources == null)
                return false;

            _resources = resources;
            isInitialized = true;
            return true;
        }

        internal bool EnsureResolution(WaterSimulationResolution resolution)
        {
            if (!isInitialized)
                return false;

            if (_resolutionInitialized && _resolution == resolution
                && _simulationPipeline != null && _surfaceRenderer != null)
                return true;

            CleanupResolutionResources();
            _resolution = resolution;
            _simulationPipeline = new WaterSimulationPipeline(_textureCache);
            _surfaceRenderer = new WaterSurfaceRenderer();
            if (!_simulationPipeline.Initialize(_resources, resolution)
                || !_surfaceRenderer.Initialize(_resources, resolution))
            {
                CleanupResolutionResources();
                return false;
            }

            _resolutionInitialized = true;
            return true;
        }

        internal bool PrepareForCamera(WaterRenderingSettings settings)
        {
            if (!isInitialized || settings == null || !settings.enabled.value)
                return false;

            RefreshActiveSurfaces();
            if (_activeSurfaceCount == 0
                || !EnsureFixedResources()
                || !EnsureResolution(settings.simulationResolution.value))
                return false;

            _ambientProbeDimmer = settings.ambientProbeDimmer.value;
            _environmentReflectionIntensity = settings.environmentReflectionIntensity.value;
            return true;
        }

        internal void Cleanup()
        {
            isInitialized = false;
            _fixedResourcesInitialized = false;
            CleanupResolutionResources();
            _underwater?.Cleanup();
            _shading?.Cleanup();
            _underwater = null;
            _shading = null;
            _textureCache.Release();
            WaterBuoyancyScheduler.ConfigureGpu(null, null);
            CoreUtils.SafeRelease(_currentSectorSwizzleBuffer);
            _currentSectorSwizzleBuffer = null;
            _activeSurfaceCount = 0;
            for (int i = 0; i < _activeSurfaces.Length; ++i)
                _activeSurfaces[i] = null;
            _resources = null;

            if (_renderOwnerRegistered)
            {
                _renderOwnerRegistered = false;
                WaterSurfaceRegistry.UnregisterRenderOwner();
            }
        }

        internal void UpdateSurfacesForCamera(WaterCameraContext cameraContext)
        {
            _surfaceRenderer?.UpdateSurfacesForCamera(_activeSurfaces, _activeSurfaceCount, cameraContext);
        }

        internal void PrepareSurfacesForRender(WaterCameraContext cameraContext, WaterCameraRenderData renderData)
        {
            _surfaceRenderer?.PrepareSurfacesForRender(
                _activeSurfaces,
                _activeSurfaceCount,
                cameraContext,
                _currentSectorSwizzleBuffer,
                _ambientProbeDimmer,
                _environmentReflectionIntensity,
                renderData);
        }

        internal void RecordScreenSpaceShadowReceiver(
            RenderGraph renderGraph,
            ContextContainer frameData)
        {
            WaterRenderGraphRecorder.RecordScreenSpaceShadowReceiver(this, renderGraph, frameData);
        }

        internal void RecordTransparentPrepass(
            RenderGraph renderGraph,
            ContextContainer frameData,
            bool publishScreenSpaceReflectionReceiver)
        {
            WaterRenderGraphRecorder.RecordTransparentPrepass(
                this,
                renderGraph,
                frameData,
                publishScreenSpaceReflectionReceiver);
        }

        internal void RecordUnderwaterResources(RenderGraph renderGraph, ContextContainer frameData)
        {
            WaterRenderGraphRecorder.RecordUnderwaterResources(this, renderGraph, frameData);
        }

        internal void RecordDeferredLighting(RenderGraph renderGraph, ContextContainer frameData)
        {
            WaterRenderGraphRecorder.RecordDeferredLighting(this, renderGraph, frameData);
        }

        internal void RecordUnderwaterEffect(RenderGraph renderGraph, ContextContainer frameData)
        {
            WaterUnderwaterEffectPass.Record(this, renderGraph, frameData);
        }

        private void RefreshActiveSurfaces()
        {
            WaterSurfaceRegistry.ReleaseRetiredResources();
            _textureCache.BeginFrame(Time.frameCount);
            _activeSurfaceCount = WaterSurfaceRegistry.CopyActiveSurfacesTo(_activeSurfaces);
        }

        private bool EnsureFixedResources()
        {
            if (_fixedResourcesInitialized)
                return true;

            _shading = new WaterShadingResources();
            _underwater = new WaterUnderwaterRenderer();
            if (!_shading.Initialize(_resources) || !_underwater.Initialize(_resources))
            {
                _underwater?.Cleanup();
                _shading?.Cleanup();
                _underwater = null;
                _shading = null;
                return false;
            }

            InitializeCurrentSectorSwizzleBuffer();
            WaterBuoyancyScheduler.ConfigureGpu(_resources.Evaluation, _currentSectorSwizzleBuffer);
            WaterSurfaceRegistry.RegisterRenderOwner();
            _renderOwnerRegistered = true;
            _fixedResourcesInitialized = true;
            return true;
        }

        private void CleanupResolutionResources()
        {
            _resolutionInitialized = false;
            _surfaceRenderer?.Cleanup();
            _simulationPipeline?.Cleanup();
            _surfaceRenderer = null;
            _simulationPipeline = null;
        }

        private void InitializeCurrentSectorSwizzleBuffer()
        {
            CoreUtils.SafeRelease(_currentSectorSwizzleBuffer);
            _currentSectorSwizzleBuffer = new GraphicsBuffer(
                GraphicsBuffer.Target.Structured,
                WaterConstants.k_CurrentSectorSwizzles.Length,
                sizeof(float) * 4);
            _currentSectorSwizzleBuffer.SetData(WaterConstants.k_CurrentSectorSwizzles);
        }
    }
}
