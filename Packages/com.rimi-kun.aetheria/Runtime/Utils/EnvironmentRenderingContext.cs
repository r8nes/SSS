using System;
using Aetheria.Runtime.Utils;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;

namespace Aetheria.Runtime.Rendering
{
    internal sealed class EnvironmentRenderingContext : IDisposable
    {
        internal const int AmbientProbeCoefficientCount = 27;
        internal const GraphicsFormat LightingFormat = GraphicsFormat.B10G11R11_UFloatPack32;

        private readonly bool _retainRadiance;

        private RTHandle _previousLightingCubemapHandle;
        private RTHandle _targetLightingCubemapHandle;
        private RTHandle _targetRadianceCubemapHandle;
        private RTHandle _lightingCubemapHandle;

        private GraphicsBuffer _previousAmbientProbe;
        private GraphicsBuffer _targetAmbientProbe;
        private GraphicsBuffer _nextAmbientProbe;

        private bool _loggedAllocationFailure;

        public int OutputResolution { get; private set; }

        public int CloudResolution { get; private set; }

        public RenderTexture LightingCubemap { get; private set; }
        public RenderTexture PreviousLightingCubemap { get; private set; }
        public RenderTexture TargetLightingCubemap { get; private set; }
        public RenderTexture TargetRadianceCubemap { get; private set; }

        public GraphicsBuffer PreviousAmbientProbe => _previousAmbientProbe;
        public GraphicsBuffer TargetAmbientProbe => _targetAmbientProbe;
        public GraphicsBuffer NextAmbientProbe => _nextAmbientProbe;

        public bool HasValidResources =>
            IsCreated(PreviousLightingCubemap) &&
            IsCreated(TargetLightingCubemap) &&
            (!_retainRadiance || IsCreated(TargetRadianceCubemap)) &&
            IsCreated(LightingCubemap) &&
            _previousLightingCubemapHandle != null &&
            _targetLightingCubemapHandle != null &&
            (!_retainRadiance || _targetRadianceCubemapHandle != null) &&
            _lightingCubemapHandle != null &&
            IsValid(_previousAmbientProbe) &&
            IsValid(_targetAmbientProbe) &&
            IsValid(_nextAmbientProbe);

        public EnvironmentRenderingContext(bool retainRadiance) => _retainRadiance = retainRadiance;

        public bool RequiresReallocation(int outputResolution, int cloudResolution)
        {
            NormalizeResolutions(ref outputResolution, ref cloudResolution);
            return !IsValid(outputResolution, cloudResolution);
        }

        public bool Ensure(int outputResolution, int cloudResolution)
        {
            NormalizeResolutions(ref outputResolution, ref cloudResolution);

            if (IsValid(outputResolution, cloudResolution))
                return false;

            Release();
            OutputResolution = outputResolution;
            CloudResolution = cloudResolution;

            int lightingMipCount = EnvironmentConstants.ConvolutionMipCount;
            LightingCubemap = CreateCubemap(
                "_AetheriaLightingSky",
                outputResolution,
                lightingMipCount,
                enableRandomWrite: false);
            PreviousLightingCubemap = CreateCubemap(
                "_AetheriaLightingSkyPrevious",
                outputResolution,
                lightingMipCount,
                enableRandomWrite: false);
            TargetLightingCubemap = CreateCubemap(
                "_AetheriaLightingSkyTarget",
                outputResolution,
                lightingMipCount,
                enableRandomWrite: false);
            
            if (_retainRadiance)
            {
                int sourceMipCount =
                    CoreUtils.GetMipCount(outputResolution);
                TargetRadianceCubemap = CreateCubemap(
                    "_AetheriaLightingSkyRadianceTarget",
                    outputResolution,
                    sourceMipCount,
                    enableRandomWrite: false);
            }

            _previousAmbientProbe = CreateAmbientProbeBuffer("Aetheria Ambient Probe Previous");
            _targetAmbientProbe = CreateAmbientProbeBuffer("Aetheria Ambient Probe Target");
            _nextAmbientProbe = CreateAmbientProbeBuffer("Aetheria Ambient Probe Next");

            if (!IsCreated(PreviousLightingCubemap) ||
                !IsCreated(TargetLightingCubemap) ||
                (_retainRadiance && !IsCreated(TargetRadianceCubemap)) ||
                !IsCreated(LightingCubemap) ||
                !IsValid(_previousAmbientProbe) ||
                !IsValid(_targetAmbientProbe) ||
                !IsValid(_nextAmbientProbe))
            {
                if (!_loggedAllocationFailure)
                {
                    Debug.LogError(
                        "Aetheria: failed to allocate one or more persistent environment-lighting resources.");
                    _loggedAllocationFailure = true;
                }

                Release();
                return true;
            }

            _loggedAllocationFailure = false;
            _previousLightingCubemapHandle = AllocateHandle(PreviousLightingCubemap);
            _targetLightingCubemapHandle = AllocateHandle(TargetLightingCubemap);
            
            if (_retainRadiance)
                _targetRadianceCubemapHandle = AllocateHandle(TargetRadianceCubemap);
            
            _lightingCubemapHandle = AllocateHandle(LightingCubemap);
            return true;
        }

        public TextureHandle ImportLightingCubemap(RenderGraph graph) =>
            graph.ImportTexture(_lightingCubemapHandle);

        public TextureHandle ImportPreviousLightingCubemap(RenderGraph graph) =>
            graph.ImportTexture(_previousLightingCubemapHandle);

        public TextureHandle ImportTargetLightingCubemap(RenderGraph graph) =>
            graph.ImportTexture(_targetLightingCubemapHandle);

        public TextureHandle ImportTargetRadianceCubemap(RenderGraph graph) =>
            _targetRadianceCubemapHandle != null
                ? graph.ImportTexture(_targetRadianceCubemapHandle)
                : TextureHandle.nullHandle;

        public BufferHandle ImportPreviousAmbientProbe(RenderGraph graph) =>
            graph.ImportBuffer(_previousAmbientProbe);

        public BufferHandle ImportTargetAmbientProbe(RenderGraph graph) =>
            graph.ImportBuffer(_targetAmbientProbe);

        public BufferHandle ImportNextAmbientProbeTarget(RenderGraph graph) =>
            graph.ImportBuffer(_nextAmbientProbe);

        public void CommitAmbientTargetRotation() =>
            (_previousAmbientProbe, _targetAmbientProbe, _nextAmbientProbe) =
                (_targetAmbientProbe, _nextAmbientProbe, _previousAmbientProbe);

        public void Dispose() => Release();

        private bool IsValid(int outputResolution, int cloudResolution) =>
            OutputResolution == outputResolution &&
            CloudResolution == cloudResolution &&
            HasValidResources;

        private static void NormalizeResolutions(
            ref int outputResolution,
            ref int cloudResolution)
        {
            outputResolution = Mathf.NextPowerOfTwo(Mathf.Max(
                EnvironmentConstants.MinimumConvolutionResolution,
                outputResolution));
            cloudResolution = Mathf.NextPowerOfTwo(Mathf.Max(8, cloudResolution));
        }

        private static bool IsCreated(RenderTexture texture) =>
            texture && texture.IsCreated();

        private static bool IsValid(GraphicsBuffer buffer) =>
            buffer != null && buffer.IsValid();

        private void Release()
        {
            ReleaseHandle(ref _previousLightingCubemapHandle);
            ReleaseHandle(ref _targetLightingCubemapHandle);
            ReleaseHandle(ref _targetRadianceCubemapHandle);
            ReleaseHandle(ref _lightingCubemapHandle);

            AtmosphereTools.ReleaseRenderTexture(PreviousLightingCubemap);
            AtmosphereTools.ReleaseRenderTexture(TargetLightingCubemap);
            AtmosphereTools.ReleaseRenderTexture(TargetRadianceCubemap);
            AtmosphereTools.ReleaseRenderTexture(LightingCubemap);
            
            PreviousLightingCubemap = null;
            TargetLightingCubemap = null;
            TargetRadianceCubemap = null;
            LightingCubemap = null;

            ReleaseBuffer(ref _previousAmbientProbe);
            ReleaseBuffer(ref _targetAmbientProbe);
            ReleaseBuffer(ref _nextAmbientProbe);

            OutputResolution = 0;
            CloudResolution = 0;
        }

        private static RenderTexture CreateCubemap(
            string name,
            int resolution,
            int mipCount,
            bool enableRandomWrite)
        {
            var descriptor = new RenderTextureDescriptor(resolution, resolution)
            {
                graphicsFormat = LightingFormat,
                depthStencilFormat = GraphicsFormat.None,
                dimension = TextureDimension.Cube,
                volumeDepth = 1,
                enableRandomWrite = enableRandomWrite,
                useMipMap = true,
                autoGenerateMips = false,
                mipCount = mipCount,
                msaaSamples = 1
            };

            var texture = CreateTexture(
                name,
                descriptor,
                FilterMode.Trilinear,
                TextureWrapMode.Clamp);
            
            if (!texture)
                return null;

            texture.wrapModeU = TextureWrapMode.Clamp;
            texture.wrapModeV = TextureWrapMode.Clamp;
            texture.wrapModeW = TextureWrapMode.Clamp;
            texture.anisoLevel = 0;
            return texture;
        }

        private static RenderTexture CreateTexture(
            string name,
            RenderTextureDescriptor descriptor,
            FilterMode filterMode,
            TextureWrapMode wrapMode)
        {
            var texture = new RenderTexture(descriptor)
            {
                name = name,
                hideFlags = HideFlags.HideAndDontSave,
                filterMode = filterMode,
                wrapMode = wrapMode
            };
            
            if (texture.Create())
                return texture;

            CoreUtils.Destroy(texture);
            return null;
        }

        private static GraphicsBuffer CreateAmbientProbeBuffer(string name)
        {
            try
            {
                var buffer = new GraphicsBuffer(
                    GraphicsBuffer.Target.Structured,
                    AmbientProbeCoefficientCount,
                    sizeof(float))
                {
                    name = name
                };
                buffer.SetData(new float[AmbientProbeCoefficientCount]);
                return buffer;
            }
            catch (Exception exception)
            {
                Debug.LogError($"Aetheria failed to allocate '{name}': {exception.Message}");
                return null;
            }
        }

        private static RTHandle AllocateHandle(RenderTexture texture) =>
            RTHandles.Alloc(texture, transferOwnership: false);

        private static void ReleaseHandle(ref RTHandle handle)
        {
            handle?.Release();
            handle = null;
        }

        private static void ReleaseBuffer(ref GraphicsBuffer buffer)
        {
            buffer?.Release();
            buffer = null;
        }
    }
}
