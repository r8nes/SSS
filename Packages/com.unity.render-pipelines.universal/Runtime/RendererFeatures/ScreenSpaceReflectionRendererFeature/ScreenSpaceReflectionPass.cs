using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using static UnityEngine.Rendering.Universal.ScreenSpaceReflectionVolumeSettings;

namespace UnityEngine.Rendering.Universal
{
    public sealed class ScreenSpaceReflectionReceiverData : ContextItem
    {
        public TextureHandle depth = TextureHandle.nullHandle;
        public TextureHandle normalSmoothness = TextureHandle.nullHandle;
        public bool isValid;

        public override void Reset()
        {
            depth = normalSmoothness = TextureHandle.nullHandle;
            isValid = false;
        }
    }

    internal class ScreenSpaceReflectionPass : ScriptableRenderPass, IDepthPyramidConsumer, IDepthPyramidFarthestConsumer
    {
        public class SharedSSRData : ContextItem
        {
            public TextureHandle depthTransparentTexture = TextureHandle.nullHandle;
            public TextureHandle normalTransparentTexture = TextureHandle.nullHandle;
            public bool inputsInitialized;

            public override void Reset()
            {
                depthTransparentTexture = normalTransparentTexture = TextureHandle.nullHandle;
                inputsInitialized = false;
            }
        }

        internal static class ShaderConstants
        {
            internal static readonly int ReflectionParam = Shader.PropertyToID("_ScreenSpaceReflectionParam");
            internal static readonly int ReflectionParam2 = Shader.PropertyToID("_ScreenSpaceReflectionParam2");
            internal static readonly int ColorSource = Shader.PropertyToID("_ScreenSpaceReflectionColorTexture");
            internal static readonly int SourceScaleAndLimit = Shader.PropertyToID("_ScreenSpaceReflectionSourceScaleAndLimit");
            internal static readonly int OutputTexture = Shader.PropertyToID("_SsrOutputTexture");
            internal static readonly int InputTexture = Shader.PropertyToID("_SsrInputTexture");
            internal static readonly int TraceSize = Shader.PropertyToID("_SsrTraceSize");
            internal static readonly int InputSize = Shader.PropertyToID("_SsrInputSize");
            internal static readonly int CameraDepth = Shader.PropertyToID("_CameraDepthTexture");
            internal static readonly int CameraNormals = Shader.PropertyToID("_CameraNormalsTexture");
            internal static readonly int ReceiverDepth = Shader.PropertyToID("_ScreenSpaceReflectionReceiverDepthTexture");
            internal static readonly int ReceiverNormals = Shader.PropertyToID("_ScreenSpaceReflectionReceiverNormalSmoothnessTexture");
            internal static readonly int Motion = Shader.PropertyToID("_MotionVectorColorTexture");
            internal static readonly int Projection = Shader.PropertyToID("_CameraProjection");
            internal static readonly int InverseProjection = Shader.PropertyToID("_CameraInverseProjection");
            internal static readonly int View = Shader.PropertyToID("_CameraView");
            internal static readonly int CurrentToPreviousClip = Shader.PropertyToID("_SsrCurrentToPreviousClip");
            internal static readonly int SkyCurrentToPreviousClip = Shader.PropertyToID("_SsrSkyCurrentToPreviousClip");
            internal static readonly int SurfaceSize = Shader.PropertyToID("_SsrSurfaceSize");
            internal static readonly int CameraNearAndJitter = Shader.PropertyToID("_SsrCameraNearAndJitter");
            internal static readonly int Thickness = Shader.PropertyToID("_ThicknessScaleAndBias");
            internal static readonly int Smoothness = Shader.PropertyToID("_SmoothnessAndStrength");
            internal static readonly int EdgeAndView = Shader.PropertyToID("_ScreenEdgeFadeAndViewConeDot");
            internal static readonly int RayLength = Shader.PropertyToID("_MaxRayLength");
            internal static readonly int RaySteps = Shader.PropertyToID("_MaxRaySteps");
            internal static readonly int Sky = Shader.PropertyToID("_ReflectSky");
            internal static readonly int ReceiverActive = Shader.PropertyToID("_SsrReceiverActive");
            internal static readonly int UseMotion = Shader.PropertyToID("_SsrUseMotionVectors");
            internal static readonly int HasColor = Shader.PropertyToID("_SsrHasValidColorSource");
            internal static readonly int ProjectionMode = Shader.PropertyToID("_SsrProjectionMode");
            internal static readonly int DepthMipData = Shader.PropertyToID("_SsrDepthMipData");
            internal static readonly int DepthMipCount = Shader.PropertyToID("_SsrDepthMipCount");
            internal static readonly int RayQueue = Shader.PropertyToID("_SsrRayQueue");
            internal static readonly int RayCounters = Shader.PropertyToID("_SsrRayCounters");
            internal static readonly int IndirectArgs = Shader.PropertyToID("_SsrIndirectArgs");
            internal static readonly int MaxRayCount = Shader.PropertyToID("_SsrMaxRayCount");
        }

        static readonly string[] k_ResultFilteringMipNames =
        {
            "Result Filtering - Mip 1",
            "Result Filtering - Mip 2",
            "Result Filtering - Mip 3",
            "Result Filtering - Mip 4",
            "Result Filtering - Mip 5",
            "Result Filtering - Mip 6",
            "Result Filtering - Mip 7",
            "Result Filtering - Mip 8",
            "Result Filtering - Mip 9",
            "Result Filtering - Mip 10",
            "Result Filtering - Mip 11",
            "Result Filtering - Mip 12",
            "Result Filtering - Mip 13",
            "Result Filtering - Mip 14"
        };

        readonly Vector4[] m_TraceMipData = new Vector4[DepthPyramidPackedMipChainInfo.MaxMipLevels];
        readonly ProfilingSampler m_ProfilingSampler = new("Screen Space Reflections");
        readonly ProfilingSampler m_TraceSampler = new("Hi-Z");
        readonly ProfilingSampler m_ClassificationSampler = new("Classification");
        readonly ProfilingSampler m_NearestTraceSampler = new("Nearest");
        readonly ProfilingSampler m_TowardsTraceSampler = new("Towards Eye");
        readonly ProfilingSampler m_BilinearUpscaleSampler = new("Bilinear Upscale");
        readonly ProfilingSampler m_BilateralUpscaleSampler = new("Bilateral Upscale");
        readonly ProfilingSampler m_ColorPyramidSampler = new("ColorPyramid");
        readonly ProfilingSampler m_FilterSampler = new("Result Filtering");

        ComputeShader m_Compute;
        ColorPyramidGenerator m_ColorPyramidGenerator;
        GraphicsBuffer m_RayQueue;
        GraphicsBuffer m_RayCounters;
        GraphicsBuffer m_IndirectArgs;
        int m_RayQueueCapacity;
        int m_ResetRayQueueKernel = -1;
        int m_ClassifyRaysKernel = -1;
        int m_BuildIndirectArgsKernel = -1;
        int m_TraceNearestKernel = -1;
        int m_TraceTowardsEyeKernel = -1;
        int m_BilinearUpscaleKernel = -1;
        int m_BilateralUpscaleKernel = -1;
        bool m_Active;
        ScreenSpaceReflectionVolumeSettings m_Settings;
        bool m_RequireReceiver;

        public bool requiresDepthPyramid => m_Active;
        public bool requiresFarthestDepthPyramid => m_Active;
        public int requiredDepthPyramidMipCount => DepthPyramidPackedMipChainInfo.MaxMipLevels;
        public int requiredDepthPyramidCheckerboardMipCount => 0;

        bool FilterRoughness => m_Settings.roughnessFilter.value != RoughReflectionsQuality.Disabled &&
                                m_ColorPyramidGenerator != null && m_ColorPyramidGenerator.isValid;

        internal bool Setup(ScriptableRenderer renderer, ComputeShader compute,
            UniversalRenderingData renderingData, UniversalCameraData cameraData,
            RenderPassEvent passEvent = RenderPassEvent.AfterRenderingPrePasses + 5,
            bool requireReceiver = false)
        {
            m_Active = false;
            m_Settings = VolumeManager.instance.stack.GetComponent<ScreenSpaceReflectionVolumeSettings>();
            m_ColorPyramidGenerator = (renderer as UniversalRenderer)?.colorPyramidGenerator;

            if (cameraData.xr.enabled || cameraData.cameraTargetDescriptor.msaaSamples > 1 ||
                cameraData.cameraTargetDescriptor.dimension != TextureDimension.Tex2D ||
                !SystemInfo.supportsIndirectArgumentsBuffer)
                return false;

            if (m_Compute != compute)
            {
                m_Compute = compute;
                m_ResetRayQueueKernel = FindKernel(compute, "KResetRayQueue");
                m_ClassifyRaysKernel = FindKernel(compute, "KClassifyRays");
                m_BuildIndirectArgsKernel = FindKernel(compute, "KBuildIndirectArgs");
                m_TraceNearestKernel = FindKernel(compute, "KTraceNearest");
                m_TraceTowardsEyeKernel = FindKernel(compute, "KTraceTowardsEye");
                m_BilinearUpscaleKernel = FindKernel(compute, "KUpscaleBilinear");
                m_BilateralUpscaleKernel = FindKernel(compute, "KUpscaleBilateral");
            }

            if (compute == null || m_ResetRayQueueKernel < 0 || m_ClassifyRaysKernel < 0 ||
                m_BuildIndirectArgsKernel < 0 || m_TraceNearestKernel < 0 || m_TraceTowardsEyeKernel < 0 ||
                m_BilinearUpscaleKernel < 0 || m_BilateralUpscaleKernel < 0)
                return false;

            renderPassEvent = passEvent;
            m_RequireReceiver = requireReceiver;
            renderingData.writesSmoothnessToDepthNormalsAlpha = true;
            var inputs = ScriptableRenderPassInput.Depth | ScriptableRenderPassInput.Normal;
            if (cameraData.cameraType == CameraType.Game)
                inputs |= ScriptableRenderPassInput.Motion;
            ConfigureInput(inputs);
            m_Active = true;
            return true;
        }

        static int FindKernel(ComputeShader compute, string name) =>
            compute != null && compute.HasKernel(name) ? compute.FindKernel(name) : -1;

        struct TraceData
        {
            internal ComputeShader compute;
            internal int resetRayQueueKernel;
            internal int classifyRaysKernel;
            internal int buildIndirectArgsKernel;
            internal int traceNearestKernel;
            internal int traceTowardsEyeKernel;
            internal Vector2Int traceSize;
            internal Vector2Int surfaceSize;
            internal TextureHandle depth;
            internal TextureHandle normals;
            internal TextureHandle receiverDepth;
            internal TextureHandle receiverNormals;
            internal TextureHandle motion;
            internal TextureHandle color;
            internal TextureHandle target;
            internal TextureHandle pyramid;
            internal TextureHandle farthest;
            internal bool receiverActive;
            internal bool useMotion;
            internal bool hasColor;
            internal Vector4 sourceScaleAndLimit;
            internal Vector4 jitterAndNear;
            internal Vector4 thickness;
            internal Vector4 smoothness;
            internal Vector4 edgeAndView;
            internal Vector4 rayLength;
            internal Vector4 resolveParameters;
            internal int steps;
            internal int mipCount;
            internal int sky;
            internal int projectionMode;
            internal Matrix4x4 projection;
            internal Matrix4x4 inverseProjection;
            internal Matrix4x4 view;
            internal Matrix4x4 viewProjection;
            internal Matrix4x4 currentToPreviousClip;
            internal Matrix4x4 skyCurrentToPreviousClip;
            internal Vector4[] mipData;
        }

        sealed class ComputeData
        {
            internal TraceData trace;
            internal TextureHandle source;
            internal TextureHandle target;
            internal GraphicsBuffer rayQueue;
            internal GraphicsBuffer rayCounters;
            internal GraphicsBuffer indirectArgs;
            internal ProfilingSampler classificationSampler;
            internal ProfilingSampler nearestTraceSampler;
            internal ProfilingSampler towardsTraceSampler;
            internal int kernel;
            internal int maxRayCount;
            internal Vector2Int outputSize;
            internal Vector2Int inputSize;
        }

        public override void RecordRenderGraph(RenderGraph graph, ContextContainer frameData)
        {
            if (!m_Active)
                return;

            if (m_RequireReceiver)
            {
                var receiver = frameData.GetOrCreate<ScreenSpaceReflectionReceiverData>();
                if (!receiver.isValid || !receiver.depth.IsValid() || !receiver.normalSmoothness.IsValid())
                    return;
            }

            var camera = frameData.Get<UniversalCameraData>();
            var resources = frameData.Get<UniversalResourceData>();
            var pyramid = frameData.GetOrCreate<DepthPyramidFrameData>();
            if (!pyramid.isValid || !pyramid.HasFarthestPyramid)
                return;

            using var profilingScope = new RenderGraphProfilingScope(graph, m_ProfilingSampler);
            TraceData data = PrepareTraceData(graph, frameData, camera, resources, pyramid);

            var descriptor = resources.cameraColor.GetDescriptor(graph);
            descriptor.width = data.traceSize.x;
            descriptor.height = data.traceSize.y;
            descriptor.sizeMode = TextureSizeMode.Explicit;
            descriptor.format = GraphicsFormat.R16G16B16A16_SFloat;
            descriptor.depthBufferBits = DepthBits.None;
            descriptor.msaaSamples = MSAASamples.None;
            descriptor.useDynamicScale = false;
            descriptor.clearBuffer = true;
            descriptor.clearColor = Color.clear;
            descriptor.filterMode = FilterMode.Bilinear;

            bool filter = FilterRoughness;
            bool fullResolution = m_Settings.resolution.value == ScreenSpaceReflectionVolumeSettings.Resolution.Full;
            descriptor.useMipMap = filter && fullResolution;
            descriptor.autoGenerateMips = false;
            descriptor.enableRandomWrite = true;
            descriptor.name = "SSR Radiance and Confidence";
            data.target = graph.CreateTexture(descriptor);

            RecordTrace(graph, data);
            TextureHandle reflection = data.target;

            if (!fullResolution)
            {
                descriptor.width = data.surfaceSize.x;
                descriptor.height = data.surfaceSize.y;
                descriptor.useMipMap = filter;
                descriptor.autoGenerateMips = false;
                descriptor.enableRandomWrite = true;
                descriptor.name = "SSR Reconstructed Radiance";
                descriptor.clearBuffer = false;
                TextureHandle reconstructed = graph.CreateTexture(descriptor);
                int kernel = m_Settings.upscalingMethod.value == UpscalingMethod.Bilateral
                    ? m_BilateralUpscaleKernel
                    : m_BilinearUpscaleKernel;
                var sampler = m_Settings.upscalingMethod.value == UpscalingMethod.Bilateral
                    ? m_BilateralUpscaleSampler
                    : m_BilinearUpscaleSampler;
                RecordUpscale(graph, data, reflection, reconstructed, kernel, sampler);
                reflection = reconstructed;
            }

            if (filter)
            {
                using var colorPyramidScope = new RenderGraphProfilingScope(graph, m_ColorPyramidSampler);
                using var filteringScope = new RenderGraphProfilingScope(graph, m_FilterSampler);
                m_ColorPyramidGenerator.RenderColorGaussianPyramid(
                    graph,
                    data.surfaceSize,
                    reflection,
                    reflection,
                    ColorPyramidGenerator.GetFullMipCount(data.surfaceSize),
                    false,
                    "Result Filtering",
                    k_ResultFilteringMipNames,
                    "Result Filtering Filter Ping",
                    "Result Filtering Filter Pong");
            }

            resources.ssrTexture = reflection;
        }

        TraceData PrepareTraceData(RenderGraph graph, ContextContainer frameData, UniversalCameraData camera,
            UniversalResourceData resources, DepthPyramidFrameData pyramid)
        {
            TraceData data = default;
            data.compute = m_Compute;
            data.resetRayQueueKernel = m_ResetRayQueueKernel;
            data.classifyRaysKernel = m_ClassifyRaysKernel;
            data.buildIndirectArgsKernel = m_BuildIndirectArgsKernel;
            data.traceNearestKernel = m_TraceNearestKernel;
            data.traceTowardsEyeKernel = m_TraceTowardsEyeKernel;
            data.mipData = m_TraceMipData;

            TextureHandle black = graph.defaultResources.blackTexture;
            var shared = frameData.GetOrCreate<SharedSSRData>();
            bool combined = shared.inputsInitialized && shared.depthTransparentTexture.IsValid() && shared.normalTransparentTexture.IsValid();
            data.depth = combined ? shared.depthTransparentTexture : resources.cameraDepthTexture;
            data.normals = combined ? shared.normalTransparentTexture : resources.cameraNormalsTexture;

            var receiver = frameData.GetOrCreate<ScreenSpaceReflectionReceiverData>();
            data.receiverActive = receiver.isValid && receiver.depth.IsValid() && receiver.normalSmoothness.IsValid();
            data.receiverDepth = data.receiverActive ? receiver.depth : black;
            data.receiverNormals = data.receiverActive ? receiver.normalSmoothness : black;
            data.surfaceSize = GetViewportSize(camera, data.depth.GetDescriptor(graph));

            int scale = (int)m_Settings.resolution.value;
            data.traceSize = new Vector2Int(
                Mathf.Max(1, (data.surfaceSize.x + scale - 1) / scale),
                Mathf.Max(1, (data.surfaceSize.y + scale - 1) / scale));
            data.color = black;
            data.motion = resources.motionVectorColor.IsValid() ? resources.motionVectorColor : black;

            data.projection = camera.GetGPUProjectionMatrix(true, 0);
            data.projectionMode = camera.camera.orthographic ? 1 : 0;
            data.inverseProjection = data.projection.inverse;
            data.view = camera.GetViewMatrix(0);
            data.viewProjection = data.projection * data.view;

            Matrix4x4 previousViewProjection = data.viewProjection;
            MotionVectorsPersistentData motionData = null;
            if (camera.camera.TryGetComponent<UniversalAdditionalCameraData>(out var additionalCameraData))
                motionData = additionalCameraData.motionVectorsPersistentData;
#if UNITY_EDITOR
            else if (camera.isSceneViewCamera)
                motionData = UniversalSceneViewCameraHistory.GetMotionVectorsPersistentData(camera.camera);
#endif
            if (motionData != null && motionData.hasValidPreviousFrameData)
                previousViewProjection = motionData.previousViewProjection;

            Vector2 previousJitter = Vector2.zero;
            if (camera.historyManager != null)
            {
                RTHandle previous = null;
                Vector2Int viewport = Vector2Int.zero;
                Vector2Int buffer = Vector2Int.zero;

                if (m_Settings.ShouldRenderTransparents())
                {
                    camera.historyManager.RequestAccess<RawColorHistory>();
                    var history = camera.historyManager.GetHistoryForRead<RawColorHistory>();
                    previous = history?.GetPreviousTexture(0);
                    if (history != null)
                    {
                        viewport = history.previousViewportSize;
                        buffer = history.previousRenderTargetSize;
                    }
                }
                else
                {
                    camera.historyManager.RequestAccess<BeforeTransparentsColorHistory>();
                    var history = camera.historyManager.GetHistoryForRead<BeforeTransparentsColorHistory>();
                    previous = history?.GetPreviousTexture(0);
                    if (history != null)
                    {
                        viewport = history.previousViewportSize;
                        buffer = history.previousRenderTargetSize;
                        previousJitter = history.GetPreviousJitter(0);
                    }
                }

                if (!camera.resetHistory && previous?.rt != null && viewport.x > 0 && viewport.y > 0)
                {
                    data.color = graph.ImportTexture(previous);
                    data.sourceScaleAndLimit = ViewportScaleAndLimit(viewport, buffer);
                    data.hasColor = true;
                }
            }

            data.currentToPreviousClip = previousViewProjection * data.viewProjection.inverse;
            Matrix4x4 directionToWorld = data.view.inverse;
            directionToWorld.SetColumn(3, Vector4.zero);
            data.skyCurrentToPreviousClip = previousViewProjection * directionToWorld * data.inverseProjection;
            data.useMotion = data.hasColor && !camera.isSceneViewCamera && resources.motionVectorColor.IsValid();
            Vector2 jitterDelta = (camera.jitter - previousJitter) * 0.5f;
            data.jitterAndNear = new Vector4(camera.camera.nearClipPlane, jitterDelta.x, jitterDelta.y, 0.0f);

            CalculateThickness(
                camera.camera.nearClipPlane,
                camera.camera.farClipPlane,
                m_Settings.objectThickness.value,
                out float thicknessScale,
                out float thicknessBias);
            data.thickness = new Vector4(thicknessScale, thicknessBias, 0.0f, 0.0f);
            data.smoothness = new Vector4(
                m_Settings.minimumSmoothness.value,
                m_Settings.smoothnessFadeStart.value,
                m_Settings.reflectionStrength.value,
                0.0f);

            float edge = Mathf.Max(0.001f, m_Settings.screenEdgeFadeDistance.value * 0.5f);
            data.edgeAndView = new Vector4(edge, 1.0f - edge, m_Settings.normalFade.value * 2.0f - 1.0f, 0.0f);
            data.rayLength = new Vector4(
                m_Settings.maxRayLength.value,
                Mathf.Max(0.0f, m_Settings.maxRayLength.value - m_Settings.rayLengthFade.value),
                0.0f,
                0.0f);
            data.steps = m_Settings.maxRaySteps.value;
            data.sky = m_Settings.reflectSky.value ? 1 : 0;

            float blurScale = 100.0f * Mathf.Pow(2.0f, m_Settings.roughnessScale.value) *
                              data.surfaceSize.x / 1024.0f * Mathf.Abs(data.projection.m00);
            int reflectionMaxMip = FilterRoughness ? ColorPyramidGenerator.GetFullMipCount(data.surfaceSize) - 1 : 0;
            data.resolveParameters = new Vector4(
                blurScale,
                reflectionMaxMip,
                (int)m_Settings.roughnessFilter.value,
                m_Settings.clampReflectedColor.value ? m_Settings.maxColorValue.value : 65504.0f);

            data.pyramid = pyramid.DepthPyramid;
            data.farthest = pyramid.DepthPyramidFarthest;
            data.mipCount = pyramid.MipCount;
            for (int mip = 0; mip < data.mipCount; ++mip)
                data.mipData[mip] = new Vector4(pyramid.MipLevelOffsets[mip].x, pyramid.MipLevelOffsets[mip].y, 0.0f, 0.0f);

            return data;
        }

        void EnsureRayQueueCapacity(int requiredCount)
        {
            if (m_RayQueue == null || !m_RayQueue.IsValid() || m_RayQueueCapacity < requiredCount)
            {
                m_RayQueue?.Release();
                m_RayQueue = new GraphicsBuffer(GraphicsBuffer.Target.Structured, requiredCount, sizeof(uint));
                m_RayQueue.name = "SSR Ray Queue";
                m_RayQueueCapacity = requiredCount;
            }

            if (m_RayCounters == null || !m_RayCounters.IsValid())
            {
                m_RayCounters = new GraphicsBuffer(GraphicsBuffer.Target.Structured, 2, sizeof(uint));
                m_RayCounters.name = "SSR Ray Counters";
            }

            if (m_IndirectArgs == null || !m_IndirectArgs.IsValid())
            {
                m_IndirectArgs = new GraphicsBuffer(GraphicsBuffer.Target.Structured | GraphicsBuffer.Target.IndirectArguments, 6, sizeof(uint));
                m_IndirectArgs.name = "SSR Indirect Args";
            }
        }

        internal void Dispose()
        {
            m_RayQueue?.Release();
            m_RayCounters?.Release();
            m_IndirectArgs?.Release();
            m_RayQueue = null;
            m_RayCounters = null;
            m_IndirectArgs = null;
            m_RayQueueCapacity = 0;
        }

        void RecordTrace(RenderGraph graph, TraceData trace)
        {
            int maxRayCount = Mathf.Max(1, trace.traceSize.x * trace.traceSize.y);
            EnsureRayQueueCapacity(maxRayCount);
            BufferHandle rayQueueHandle = graph.ImportBuffer(m_RayQueue);
            BufferHandle rayCountersHandle = graph.ImportBuffer(m_RayCounters);
            BufferHandle indirectArgsHandle = graph.ImportBuffer(m_IndirectArgs);

            using var builder = graph.AddUnsafePass<ComputeData>("Hi-Z", out var data, m_TraceSampler);
            data.trace = trace;
            data.target = trace.target;
            data.outputSize = trace.traceSize;
            data.maxRayCount = maxRayCount;
            data.rayQueue = m_RayQueue;
            data.rayCounters = m_RayCounters;
            data.indirectArgs = m_IndirectArgs;
            data.classificationSampler = m_ClassificationSampler;
            data.nearestTraceSampler = m_NearestTraceSampler;
            data.towardsTraceSampler = m_TowardsTraceSampler;
            builder.UseTexture(trace.target, AccessFlags.Write);
            DeclareSurfaceInputs(builder, trace);
            builder.UseTexture(trace.pyramid, AccessFlags.Read);
            builder.UseTexture(trace.farthest, AccessFlags.Read);
            builder.UseTexture(trace.color, AccessFlags.Read);
            builder.UseTexture(trace.motion, AccessFlags.Read);
            builder.UseBuffer(rayQueueHandle, AccessFlags.ReadWrite);
            builder.UseBuffer(rayCountersHandle, AccessFlags.ReadWrite);
            builder.UseBuffer(indirectArgsHandle, AccessFlags.ReadWrite);
            builder.AllowGlobalStateModification(true);
            builder.SetRenderFunc(static (ComputeData data, UnsafeGraphContext context) =>
            {
                CommandBuffer cmd = CommandBufferHelpers.GetNativeCommandBuffer(context.cmd);
                var trace = data.trace;
                UploadParameters(cmd, trace);
                cmd.SetComputeVectorParam(trace.compute, ShaderConstants.TraceSize,
                    new Vector4(data.outputSize.x, data.outputSize.y, 0.0f, 0.0f));
                cmd.SetComputeIntParam(trace.compute, ShaderConstants.MaxRayCount, data.maxRayCount);

                using (new ProfilingScope(cmd, data.classificationSampler))
                {
                    cmd.SetComputeBufferParam(trace.compute, trace.resetRayQueueKernel, ShaderConstants.RayCounters, data.rayCounters);
                    cmd.DispatchCompute(trace.compute, trace.resetRayQueueKernel, 1, 1, 1);

                    BindSurfaceInputs(cmd, trace, trace.classifyRaysKernel);
                    BindRayQueue(cmd, trace.compute, trace.classifyRaysKernel, data);
                    cmd.DispatchCompute(trace.compute, trace.classifyRaysKernel,
                        DivUp(data.outputSize.x, 8), DivUp(data.outputSize.y, 8), 1);

                    cmd.SetComputeBufferParam(trace.compute, trace.buildIndirectArgsKernel, ShaderConstants.RayCounters, data.rayCounters);
                    cmd.SetComputeBufferParam(trace.compute, trace.buildIndirectArgsKernel, ShaderConstants.IndirectArgs, data.indirectArgs);
                    cmd.DispatchCompute(trace.compute, trace.buildIndirectArgsKernel, 1, 1, 1);
                }

                using (new ProfilingScope(cmd, data.nearestTraceSampler))
                {
                    BindTraceResources(cmd, trace, trace.traceNearestKernel, data, false);
                    cmd.DispatchCompute(trace.compute, trace.traceNearestKernel, data.indirectArgs, 0u);
                }

                using (new ProfilingScope(cmd, data.towardsTraceSampler))
                {
                    BindTraceResources(cmd, trace, trace.traceTowardsEyeKernel, data, true);
                    cmd.DispatchCompute(trace.compute, trace.traceTowardsEyeKernel, data.indirectArgs, 12u);
                }
                PublishParameters(cmd, trace);
            });
        }

        void RecordUpscale(RenderGraph graph, TraceData trace, TextureHandle source, TextureHandle target, int kernel, ProfilingSampler sampler)
        {
            using var builder = graph.AddUnsafePass<ComputeData>(sampler.name, out var data, sampler);
            data.trace = trace;
            data.source = source;
            data.target = target;
            data.kernel = kernel;
            data.outputSize = trace.surfaceSize;
            data.inputSize = trace.traceSize;
            builder.UseTexture(source, AccessFlags.Read);
            builder.UseTexture(target, AccessFlags.Write);
            DeclareSurfaceInputs(builder, trace);
            builder.AllowGlobalStateModification(true);
            builder.SetRenderFunc(static (ComputeData data, UnsafeGraphContext context) =>
            {
                CommandBuffer cmd = CommandBufferHelpers.GetNativeCommandBuffer(context.cmd);
                var trace = data.trace;
                UploadParameters(cmd, trace);
                BindSurfaceInputs(cmd, trace, data.kernel);
                cmd.SetComputeTextureParam(trace.compute, data.kernel, ShaderConstants.InputTexture, data.source);
                cmd.SetComputeTextureParam(trace.compute, data.kernel, ShaderConstants.OutputTexture, data.target);
                cmd.SetComputeVectorParam(trace.compute, ShaderConstants.InputSize, Size(data.inputSize));
                cmd.DispatchCompute(trace.compute, data.kernel, DivUp(data.outputSize.x, 8), DivUp(data.outputSize.y, 8), 1);
                PublishParameters(cmd, trace);
            });
        }

        static void DeclareSurfaceInputs(IBaseRenderGraphBuilder builder, TraceData data)
        {
            builder.UseTexture(data.depth, AccessFlags.Read);
            builder.UseTexture(data.normals, AccessFlags.Read);
            builder.UseTexture(data.receiverDepth, AccessFlags.Read);
            builder.UseTexture(data.receiverNormals, AccessFlags.Read);
        }

        static void BindSurfaceInputs(CommandBuffer cmd, TraceData data, int kernel)
        {
            cmd.SetComputeTextureParam(data.compute, kernel, ShaderConstants.CameraDepth, data.depth);
            cmd.SetComputeTextureParam(data.compute, kernel, ShaderConstants.CameraNormals, data.normals);
            cmd.SetComputeTextureParam(data.compute, kernel, ShaderConstants.ReceiverDepth, data.receiverDepth);
            cmd.SetComputeTextureParam(data.compute, kernel, ShaderConstants.ReceiverNormals, data.receiverNormals);
        }

        static void BindRayQueue(CommandBuffer cmd, ComputeShader compute, int kernel, ComputeData data)
        {
            cmd.SetComputeBufferParam(compute, kernel, ShaderConstants.RayQueue, data.rayQueue);
            cmd.SetComputeBufferParam(compute, kernel, ShaderConstants.RayCounters, data.rayCounters);
        }

        static void BindTraceResources(CommandBuffer cmd, TraceData trace, int kernel, ComputeData data, bool useFarthest)
        {
            BindSurfaceInputs(cmd, trace, kernel);
            BindRayQueue(cmd, trace.compute, kernel, data);
            cmd.SetComputeTextureParam(trace.compute, kernel, ShaderConstants.ColorSource, trace.color);
            cmd.SetComputeTextureParam(trace.compute, kernel, ShaderConstants.Motion, trace.motion);
            cmd.SetComputeTextureParam(trace.compute, kernel, DepthPyramidShaderIDs.DepthPyramid, trace.pyramid);
            if (useFarthest)
                cmd.SetComputeTextureParam(trace.compute, kernel, DepthPyramidShaderIDs.DepthPyramidFarthest, trace.farthest);
            cmd.SetComputeTextureParam(trace.compute, kernel, ShaderConstants.OutputTexture, data.target);
        }

        static void UploadParameters(CommandBuffer cmd, TraceData data)
        {
            cmd.SetComputeMatrixParam(data.compute, ShaderConstants.Projection, data.projection);
            cmd.SetComputeMatrixParam(data.compute, ShaderConstants.InverseProjection, data.inverseProjection);
            cmd.SetComputeMatrixParam(data.compute, ShaderConstants.View, data.view);
            cmd.SetComputeMatrixParam(data.compute, ShaderConstants.CurrentToPreviousClip, data.currentToPreviousClip);
            cmd.SetComputeMatrixParam(data.compute, ShaderConstants.SkyCurrentToPreviousClip, data.skyCurrentToPreviousClip);
            cmd.SetComputeVectorParam(data.compute, ShaderConstants.ReflectionParam2, data.resolveParameters);
            cmd.SetComputeVectorParam(data.compute, ShaderConstants.SourceScaleAndLimit, data.sourceScaleAndLimit);
            cmd.SetComputeVectorParam(data.compute, ShaderConstants.SurfaceSize, Size(data.surfaceSize));
            cmd.SetComputeVectorParam(data.compute, ShaderConstants.CameraNearAndJitter, data.jitterAndNear);
            cmd.SetComputeVectorParam(data.compute, ShaderConstants.Thickness, data.thickness);
            cmd.SetComputeVectorParam(data.compute, ShaderConstants.Smoothness, data.smoothness);
            cmd.SetComputeVectorParam(data.compute, ShaderConstants.EdgeAndView, data.edgeAndView);
            cmd.SetComputeVectorParam(data.compute, ShaderConstants.RayLength, data.rayLength);
            cmd.SetComputeIntParam(data.compute, ShaderConstants.RaySteps, data.steps);
            cmd.SetComputeIntParam(data.compute, ShaderConstants.Sky, data.sky);
            cmd.SetComputeIntParam(data.compute, ShaderConstants.ReceiverActive, data.receiverActive ? 1 : 0);
            cmd.SetComputeIntParam(data.compute, ShaderConstants.UseMotion, data.useMotion ? 1 : 0);
            cmd.SetComputeIntParam(data.compute, ShaderConstants.HasColor, data.hasColor ? 1 : 0);
            cmd.SetComputeIntParam(data.compute, ShaderConstants.ProjectionMode, data.projectionMode);
            cmd.SetComputeIntParam(data.compute, ShaderConstants.DepthMipCount, data.mipCount);
            cmd.SetComputeVectorArrayParam(data.compute, ShaderConstants.DepthMipData, data.mipData);
        }

        static void PublishParameters(CommandBuffer cmd, TraceData data)
        {
            cmd.SetGlobalVector(ShaderConstants.ReflectionParam, new Vector4(1, data.smoothness.x, data.smoothness.y, 0));
            cmd.SetGlobalVector(ShaderConstants.ReflectionParam2, data.resolveParameters);
        }

        static Vector4 Size(Vector2Int size) =>
            new(size.x, size.y, 1.0f / Mathf.Max(1, size.x), 1.0f / Mathf.Max(1, size.y));

        static int DivUp(int value, int divisor) => (value + divisor - 1) / divisor;

        static Vector2Int GetViewportSize(UniversalCameraData camera, TextureDesc desc)
        {
            Vector2Int size = new(camera.cameraTargetDescriptor.width, camera.cameraTargetDescriptor.height);
            if (DynamicResolutionHandler.instance.HardwareDynamicResIsEnabled())
                size = DynamicResolutionHandler.instance.ApplyScalesOnSize(size);
            return new Vector2Int(Mathf.Clamp(size.x, 1, desc.width), Mathf.Clamp(size.y, 1, desc.height));
        }

        static Vector4 ViewportScaleAndLimit(Vector2Int viewport, Vector2Int buffer)
        {
            Vector4 inverse = Size(buffer);
            return new Vector4(
                viewport.x * inverse.z,
                viewport.y * inverse.w,
                (viewport.x - 0.5f) * inverse.z,
                (viewport.y - 0.5f) * inverse.w);
        }

        static void CalculateThickness(float near, float far, float thickness, out float scale, out float bias)
        {
            scale = 1.0f / (1.0f + thickness);
            bias = (SystemInfo.usesReversedZBuffer ? -near : far) / (far - near) * thickness * scale;
        }
    }
}
