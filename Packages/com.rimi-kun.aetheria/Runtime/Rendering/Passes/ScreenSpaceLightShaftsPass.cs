using System.Collections.Generic;
using Aetheria.Runtime.Data.CelestialLight;
using Aetheria.Runtime.Overrides;
using Aetheria.Runtime.Utils;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace Aetheria.Runtime.Rendering.Passes
{
    internal sealed class ScreenSpaceLightShaftsPass : System.IDisposable
    {
        internal const float SunViewportPadding = 0.3f;
        private const GraphicsFormat SignalFormat = GraphicsFormat.R16G16_SFloat;
        private const GraphicsFormat MaskFormat = GraphicsFormat.R16_SFloat;

        private static readonly AetheriaProfiler.Marker MaskProfile =
            AetheriaProfiler.CreateMarker("Composite/Screen Space Light Shafts", "Mask");
        private static readonly AetheriaProfiler.Marker TemporalProfile =
            AetheriaProfiler.CreateMarker("Composite/Screen Space Light Shafts", "Temporal");
        private static readonly AetheriaProfiler.Marker BlurProfile =
            AetheriaProfiler.CreateMarker("Composite/Screen Space Light Shafts", "Radial Blur");

        private static readonly int DepthId = Shader.PropertyToID("_Depth");
        private static readonly int SignalId = Shader.PropertyToID("_Signal");
        private static readonly int HistorySignalId = Shader.PropertyToID("_HistorySignal");
        private static readonly int MaskId = Shader.PropertyToID("_Mask");
        private static readonly int SignalOutId = Shader.PropertyToID("_SignalOut");
        private static readonly int MaskOutId = Shader.PropertyToID("_MaskOut");
        private static readonly int HalfSizeId = Shader.PropertyToID("_LightShaftsHalfSize");
        private static readonly int RadialSizeId = Shader.PropertyToID("_LightShaftsRadialSize");
        private static readonly int DepthSizeId = Shader.PropertyToID("_LightShaftsDepthSize");
        private static readonly int InverseViewProjectionId = Shader.PropertyToID("_LightShaftsInverseViewProjection");
        private static readonly int NonJitteredViewProjectionId = Shader.PropertyToID("_LightShaftsNonJitteredViewProjection");
        private static readonly int PreviousViewProjectionId = Shader.PropertyToID("_LightShaftsPreviousViewProjection");
        private static readonly int OcclusionDepthRangeId = Shader.PropertyToID("_LightShaftsOcclusionDepthRange");
        private static readonly int HistoryValidId = Shader.PropertyToID("_LightShaftsHistoryValid");
        private static readonly int SunViewportPaddingId = Shader.PropertyToID("_LightShaftsSunViewportPadding");
        private static readonly int LightDirectionId = Shader.PropertyToID("_LightShaftsLightDirectionWS");

        private readonly ComputeShader _computeShader;
        private readonly ComputeKernel _buildMaskKernel;
        private readonly ComputeKernel _temporalKernel;
        private readonly ComputeKernel _blurNearKernel;
        private readonly ComputeKernel _blurFarKernel;
        private readonly Dictionary<int, TemporalHistory> _histories = new();

        internal ScreenSpaceLightShaftsPass(ComputeShader computeShader)
        {
            _computeShader = computeShader;
            _buildMaskKernel = FindKernel(computeShader, "BuildMask");
            _temporalKernel = FindKernel(computeShader, "StabilizeSignal");
            _blurNearKernel = FindKernel(computeShader, "BlurNear");
            _blurFarKernel = FindKernel(computeShader, "BlurFar");
        }

        internal bool PrepareForCamera(Camera camera)
        {
            if (!camera || camera.orthographic)
                return false;

            var volume = AtmosphereTools.GetVolumeComponent<ScreenSpaceLightShafts>(camera);
            return volume && volume.IsActive();
        }

        internal void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            var output = frameData.GetOrCreate<ScreenSpaceLightShaftsFrameData>();
            output.Reset();

            var resources = frameData.Get<UniversalResourceData>();
            var cameraData = frameData.Get<UniversalCameraData>();
            var camera = cameraData.camera;
            var volume = AtmosphereTools.GetVolumeComponent<ScreenSpaceLightShafts>(cameraData);
            var atmosphere = frameData.Contains<AtmosphereFrameData>() ? frameData.Get<AtmosphereFrameData>() : null;

            if (!volume || !volume.IsActive() || !resources.activeColorTexture.IsValid() ||
                !resources.cameraDepthTexture.IsValid() ||
                camera.cameraType is not (CameraType.Game or CameraType.SceneView) ||
                atmosphere is not { VisualEnvironment: not null })
                return;

            LightContext lightContext;
            bool hasLightContext = atmosphere.IsActive && atmosphere.Volume
                ? LightResolver.TryResolveContext(
                    atmosphere.PrimaryCelestialLight,
                    atmosphere.Volume,
                    atmosphere.VisualEnvironment,
                    camera,
                    out lightContext)
                : LightResolver.TryResolveContext(atmosphere.PrimaryCelestialLight, out lightContext);
            if (!hasLightContext)
                return;

            Vector3 lightDirection = lightContext.DirectionToLightWS;
            if (!CanRenderLight(camera, lightDirection))
                return;

            var depth = resources.cameraDepthTexture;
            var depthDescriptor = renderGraph.GetTextureDesc(depth);
            if (depthDescriptor.dimension != TextureDimension.Tex2D)
                return;

            var depthSize = depthDescriptor.CalculateFinalDimensions();
            if (depthSize.x <= 0 || depthSize.y <= 0)
                return;

            int halfWidth = Mathf.Max(1, (depthSize.x + 1) / 2);
            int halfHeight = Mathf.Max(1, (depthSize.y + 1) / 2);
            int radialWidth = Mathf.Max(1, (depthSize.x + 3) / 4);
            int radialHeight = Mathf.Max(1, (depthSize.y + 3) / 4);

            var viewMatrix = cameraData.GetViewMatrix();
            var viewProjection = GL.GetGPUProjectionMatrix(cameraData.GetProjectionMatrix(), true) * viewMatrix;
            var nonJitteredViewProjection =
                GL.GetGPUProjectionMatrix(camera.nonJitteredProjectionMatrix, true) * viewMatrix;

            var context = new FrameContext(
                depth,
                camera.GetInstanceID(),
                halfWidth,
                halfHeight,
                radialWidth,
                radialHeight,
                depthSize.x,
                depthSize.y,
                viewProjection.inverse,
                nonJitteredViewProjection,
                lightDirection,
                volume.occlusionDepthRange.value);

            var signal = RecordMask(renderGraph, in context);
            signal = RecordTemporalStabilization(renderGraph, in context, signal);
            var mask = RecordRadialFilter(renderGraph, in context, signal, in _blurNearKernel, SignalId, "Blur Near");
            mask = RecordRadialFilter(renderGraph, in context, mask, in _blurFarKernel, MaskId, "Blur Far");

            output.IsActive = true;
            output.Mask = mask;
            output.LightDirectionWS = lightDirection;
            output.LightColor = lightContext.SurfaceLightColor;
            output.Tint = volume.tint.value;
            output.Intensity = volume.intensity.value;
            output.Anisotropy = volume.anisotropy.value;
            output.OcclusionMaskDarkness = volume.occlusionMaskDarkness.value;
            output.OcclusionDepthRange = volume.occlusionDepthRange.value;
            output.SunViewportPadding = SunViewportPadding;
        }

        private TextureHandle RecordMask(RenderGraph renderGraph, in FrameContext context)
        {
            var output = context.CreateRadialTexture(renderGraph, "Light Shafts Mask Signal", SignalFormat);
            using var profiled = MaskProfile.AddComputePass<MaskPassData>(renderGraph, out var pass);
            var builder = profiled.Builder;
            pass.ComputeShader = _computeShader;
            pass.Kernel = _buildMaskKernel.Id;
            pass.Depth = context.Depth;
            pass.Output = output;
            pass.HalfSize = context.HalfSize;
            pass.RadialSize = context.RadialSize;
            pass.DepthSize = context.DepthSize;
            pass.OcclusionDepthRange = context.OcclusionDepthRange;
            pass.GroupCountX = GetGroupCount(context.RadialWidth, _buildMaskKernel.ThreadsX);
            pass.GroupCountY = GetGroupCount(context.RadialHeight, _buildMaskKernel.ThreadsY);
            builder.UseTexture(pass.Depth, AccessFlags.Read);
            builder.UseTexture(pass.Output, AccessFlags.WriteAll);
            builder.SetRenderFunc(static (MaskPassData data, ComputeGraphContext graphContext) =>
            {
                var cmd = graphContext.cmd;
                int kernel = data.Kernel;
                cmd.SetComputeTextureParam(data.ComputeShader, kernel, DepthId, data.Depth);
                cmd.SetComputeTextureParam(data.ComputeShader, kernel, SignalOutId, data.Output);
                cmd.SetComputeVectorParam(data.ComputeShader, HalfSizeId, data.HalfSize);
                cmd.SetComputeVectorParam(data.ComputeShader, RadialSizeId, data.RadialSize);
                cmd.SetComputeVectorParam(data.ComputeShader, DepthSizeId, data.DepthSize);
                cmd.SetComputeFloatParam(data.ComputeShader, OcclusionDepthRangeId, data.OcclusionDepthRange);
                cmd.DispatchCompute(data.ComputeShader, kernel, data.GroupCountX, data.GroupCountY, 1);
            });
            return output;
        }

        private TextureHandle RecordTemporalStabilization(RenderGraph renderGraph, in FrameContext context, TextureHandle currentSignal)
        {
            var history = GetOrCreateHistory(context.CameraId);
            var descriptor = CreateHistoryDescriptor(context.RadialWidth, context.RadialHeight, SignalFormat);
            bool readReallocated = RenderingUtils.ReAllocateHandleIfNeeded(ref history.SignalRead, descriptor, FilterMode.Bilinear, TextureWrapMode.Clamp, name: $"Light Shafts Signal History A ({context.CameraId})");
            bool writeReallocated = RenderingUtils.ReAllocateHandleIfNeeded(ref history.SignalWrite, descriptor, FilterMode.Bilinear, TextureWrapMode.Clamp, name: $"Light Shafts Signal History B ({context.CameraId})");
            int frameIndex = Time.frameCount;
            bool historyValid = history.Valid && !readReallocated && !writeReallocated && history.LastFrameIndex >= 0 && frameIndex - history.LastFrameIndex <= 1;
            var historyRead = renderGraph.ImportTexture(history.SignalRead);
            var historyWrite = renderGraph.ImportTexture(history.SignalWrite);
            using var profiled = TemporalProfile.AddComputePass<TemporalPassData>(renderGraph, out var pass);
            var builder = profiled.Builder;
            pass.ComputeShader = _computeShader;
            pass.Kernel = _temporalKernel.Id;
            pass.CurrentSignal = currentSignal;
            pass.HistorySignal = historyRead;
            pass.Output = historyWrite;
            pass.RadialSize = context.RadialSize;
            pass.InverseViewProjection = context.InverseViewProjection;
            pass.NonJitteredViewProjection = context.NonJitteredViewProjection;
            pass.PreviousViewProjection = historyValid ? history.PreviousViewProjection : context.NonJitteredViewProjection;
            pass.OcclusionDepthRange = context.OcclusionDepthRange;
            pass.HistoryValid = historyValid ? 1 : 0;
            pass.GroupCountX = GetGroupCount(context.RadialWidth, _temporalKernel.ThreadsX);
            pass.GroupCountY = GetGroupCount(context.RadialHeight, _temporalKernel.ThreadsY);
            builder.UseTexture(pass.CurrentSignal, AccessFlags.Read);
            builder.UseTexture(pass.HistorySignal, AccessFlags.Read);
            builder.UseTexture(pass.Output, AccessFlags.WriteAll);
            builder.SetRenderFunc(static (TemporalPassData data, ComputeGraphContext graphContext) =>
            {
                var cmd = graphContext.cmd;
                int kernel = data.Kernel;
                cmd.SetComputeTextureParam(data.ComputeShader, kernel, SignalId, data.CurrentSignal);
                cmd.SetComputeTextureParam(data.ComputeShader, kernel, HistorySignalId, data.HistorySignal);
                cmd.SetComputeTextureParam(data.ComputeShader, kernel, SignalOutId, data.Output);
                cmd.SetComputeVectorParam(data.ComputeShader, RadialSizeId, data.RadialSize);
                cmd.SetComputeMatrixParam(data.ComputeShader, InverseViewProjectionId, data.InverseViewProjection);
                cmd.SetComputeMatrixParam(data.ComputeShader, NonJitteredViewProjectionId, data.NonJitteredViewProjection);
                cmd.SetComputeMatrixParam(data.ComputeShader, PreviousViewProjectionId, data.PreviousViewProjection);
                cmd.SetComputeFloatParam(data.ComputeShader, OcclusionDepthRangeId, data.OcclusionDepthRange);
                cmd.SetComputeIntParam(data.ComputeShader, HistoryValidId, data.HistoryValid);
                cmd.DispatchCompute(data.ComputeShader, kernel, data.GroupCountX, data.GroupCountY, 1);
            });
            history.Swap();
            history.Valid = true;
            history.LastFrameIndex = frameIndex;
            history.PreviousViewProjection = context.NonJitteredViewProjection;
            return historyWrite;
        }

        private TextureHandle RecordRadialFilter(RenderGraph renderGraph, in FrameContext context, TextureHandle input,
            in ComputeKernel kernel, int inputPropertyId, string passName)
        {
            var output = context.CreateRadialTexture(renderGraph, $"Light Shafts {passName}", MaskFormat);
            using var profiled = BlurProfile.AddComputePass<BlurPassData>(renderGraph, out var pass);
            var builder = profiled.Builder;
            pass.ComputeShader = _computeShader;
            pass.Kernel = kernel.Id;
            pass.Input = input;
            pass.InputPropertyId = inputPropertyId;
            pass.Output = output;
            pass.RadialSize = context.RadialSize;
            pass.SunViewportPadding = SunViewportPadding;
            pass.LightDirection = context.LightDirection;
            pass.GroupCountX = GetGroupCount(context.RadialWidth, kernel.ThreadsX);
            pass.GroupCountY = GetGroupCount(context.RadialHeight, kernel.ThreadsY);
            builder.UseTexture(pass.Input, AccessFlags.Read);
            builder.UseTexture(pass.Output, AccessFlags.WriteAll);
            builder.SetRenderFunc(static (BlurPassData data, ComputeGraphContext graphContext) =>
            {
                var cmd = graphContext.cmd;
                int kernel = data.Kernel;
                cmd.SetComputeTextureParam(data.ComputeShader, kernel, data.InputPropertyId, data.Input);
                cmd.SetComputeTextureParam(data.ComputeShader, kernel, MaskOutId, data.Output);
                cmd.SetComputeVectorParam(data.ComputeShader, RadialSizeId, data.RadialSize);
                cmd.SetComputeFloatParam(data.ComputeShader, SunViewportPaddingId, data.SunViewportPadding);
                cmd.SetComputeVectorParam(data.ComputeShader, LightDirectionId, data.LightDirection);
                cmd.DispatchCompute(data.ComputeShader, kernel, data.GroupCountX, data.GroupCountY, 1);
            });
            return output;
        }

        private TemporalHistory GetOrCreateHistory(int cameraId)
        {
            if (_histories.TryGetValue(cameraId, out var history)) return history;
            history = new TemporalHistory();
            _histories.Add(cameraId, history);
            return history;
        }

        private static bool CanRenderLight(Camera camera, Vector3 direction)
        {
            float lengthSquared = direction.sqrMagnitude;
            if (lengthSquared <= 1e-8f) return false;
            direction /= Mathf.Sqrt(lengthSquared);
            if (Vector3.Dot(camera.transform.forward, direction) <= 0f) return false;
            var viewport = camera.WorldToViewportPoint(camera.transform.position + direction);
            if (viewport.z <= 0f) return false;
            float offscreenDistance = Mathf.Max(Mathf.Max(-viewport.x, viewport.x - 1f), Mathf.Max(-viewport.y, viewport.y - 1f));
            return offscreenDistance < SunViewportPadding;
        }

        private static RenderTextureDescriptor CreateHistoryDescriptor(int width, int height, GraphicsFormat format) =>
            new(width, height) { graphicsFormat = format, depthBufferBits = 0, msaaSamples = 1, volumeDepth = 1,
                dimension = TextureDimension.Tex2D, enableRandomWrite = true, useMipMap = false,
                autoGenerateMips = false, sRGB = false };

        private static ComputeKernel FindKernel(ComputeShader shader, string name)
        {
            int id = shader.FindKernel(name);
            shader.GetKernelThreadGroupSizes(id, out uint x, out uint y, out _);
            return new ComputeKernel(id, x, y);
        }

        private static int GetGroupCount(int size, uint threads) => (int)((size + threads - 1) / threads);
        private static Vector4 SizeVector(int width, int height) => new(width, height, 1f / width, 1f / height);

        public void Dispose()
        {
            foreach (var history in _histories.Values) history.Release();
            _histories.Clear();
        }

        private readonly struct FrameContext
        {
            internal readonly TextureHandle Depth;
            internal readonly int CameraId;
            internal readonly int RadialWidth, RadialHeight;
            internal readonly Vector4 HalfSize, RadialSize, DepthSize;
            internal readonly Matrix4x4 InverseViewProjection, NonJitteredViewProjection;
            internal readonly Vector3 LightDirection;
            internal readonly float OcclusionDepthRange;

            internal FrameContext(TextureHandle depth, int cameraId, int halfWidth, int halfHeight, int radialWidth,
                int radialHeight, int depthWidth, int depthHeight, Matrix4x4 inverseViewProjection,
                Matrix4x4 nonJitteredViewProjection, Vector3 lightDirection, float occlusionDepthRange)
            {
                Depth = depth; CameraId = cameraId; RadialWidth = radialWidth; RadialHeight = radialHeight;
                HalfSize = SizeVector(halfWidth, halfHeight); RadialSize = SizeVector(radialWidth, radialHeight);
                DepthSize = SizeVector(depthWidth, depthHeight); InverseViewProjection = inverseViewProjection;
                NonJitteredViewProjection = nonJitteredViewProjection; LightDirection = lightDirection;
                OcclusionDepthRange = occlusionDepthRange;
            }

            internal TextureHandle CreateRadialTexture(RenderGraph renderGraph, string name, GraphicsFormat format) =>
                renderGraph.CreateTexture(new TextureDesc(RadialWidth, RadialHeight)
                {
                    name = name, colorFormat = format, clearBuffer = false, discardBuffer = true,
                    enableRandomWrite = true, filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp,
                    useMipMap = false, autoGenerateMips = false, msaaSamples = MSAASamples.None
                });
        }

        private sealed class MaskPassData { internal ComputeShader ComputeShader; internal int Kernel; internal TextureHandle Depth, Output; internal Vector4 HalfSize, RadialSize, DepthSize; internal float OcclusionDepthRange; internal int GroupCountX, GroupCountY; }
        private sealed class TemporalPassData { internal ComputeShader ComputeShader; internal int Kernel; internal TextureHandle CurrentSignal, HistorySignal, Output; internal Vector4 RadialSize; internal Matrix4x4 InverseViewProjection, NonJitteredViewProjection, PreviousViewProjection; internal float OcclusionDepthRange; internal int HistoryValid, GroupCountX, GroupCountY; }
        private sealed class BlurPassData { internal ComputeShader ComputeShader; internal int Kernel; internal TextureHandle Input, Output; internal int InputPropertyId; internal Vector4 RadialSize; internal Vector3 LightDirection; internal float SunViewportPadding; internal int GroupCountX, GroupCountY; }
        private readonly struct ComputeKernel { internal readonly int Id; internal readonly uint ThreadsX, ThreadsY; internal ComputeKernel(int id, uint x, uint y) { Id = id; ThreadsX = x; ThreadsY = y; } }
        private sealed class TemporalHistory
        {
            internal RTHandle SignalRead, SignalWrite; internal Matrix4x4 PreviousViewProjection = Matrix4x4.identity;
            internal bool Valid; internal int LastFrameIndex = -1;
            internal void Swap() => (SignalRead, SignalWrite) = (SignalWrite, SignalRead);
            internal void Release() { SignalRead?.Release(); SignalWrite?.Release(); SignalRead = null; SignalWrite = null; Valid = false; LastFrameIndex = -1; }
        }
    }
}
