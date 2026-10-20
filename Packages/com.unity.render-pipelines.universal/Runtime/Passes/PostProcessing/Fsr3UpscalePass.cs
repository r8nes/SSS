using System;
using System.Collections.Generic;
using FidelityFX;
using FidelityFX.FSR3;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering.RenderGraphModule;

namespace UnityEngine.Rendering.Universal
{
    internal sealed class Fsr3UpscalePass : PostProcessPass
    {
        private const string k_OutputName = "_CameraColorUpscaledFSR3";
        private static readonly ProfilingSampler s_ProfilingSampler = new("FidelityFX Super Resolution 3.1");

        private readonly Fsr3UpscalerShaders m_ShaderResources;
        private readonly Dictionary<int, CameraContext> m_CameraContexts = new();

        private sealed class CameraContext : IDisposable
        {
            internal Fsr3UpscalerContext upscaler;
            internal Fsr3UpscalerShaders shaders;
            internal Vector2Int outputSize;
            internal Vector2Int renderSize;
            internal bool hdrInput;
            internal bool resetHistory;

            public void Dispose()
            {
                upscaler?.Destroy();
                upscaler = null;
                shaders?.Dispose();
                shaders = null;
            }
        }

        private sealed class PassData
        {
            internal CameraContext cameraContext;
            internal TextureHandle color;
            internal TextureHandle opaqueColor;
            internal TextureHandle depth;
            internal TextureHandle motionVectors;
            internal TextureHandle exposure;
            internal TextureHandle output;
            internal TextureHandle reactive;
            internal Vector2Int renderSize;
            internal Vector2Int outputSize;
            internal Vector2 jitter;
            internal float deltaTime;
            internal float nearClip;
            internal float farClip;
            internal float verticalFovRadians;
            internal bool invertedDepth;
            internal bool enableSharpening;
            internal float sharpness;
            internal bool resetHistory;
            internal bool srgbInput;
        }

        private sealed class NormalizeInputPassData
        {
            internal TextureHandle source;
            internal TextureHandle destination;
        }

        internal Fsr3UpscalePass(PostProcessData data)
        {
            m_ShaderResources = new Fsr3UpscalerShaders
            {
                prepareInputsPass = data.shaders.fsr3PrepareInputsCS,
                lumaPyramidPass = data.shaders.fsr3LumaPyramidCS,
                shadingChangePyramidPass = data.shaders.fsr3ShadingChangePyramidCS,
                shadingChangePass = data.shaders.fsr3ShadingChangeCS,
                prepareReactivityPass = data.shaders.fsr3PrepareReactivityCS,
                lumaInstabilityPass = data.shaders.fsr3LumaInstabilityCS,
                accumulatePass = data.shaders.fsr3AccumulateCS,
                sharpenPass = data.shaders.fsr3SharpenCS,
                autoGenReactivePass = data.shaders.fsr3AutoReactiveCS,
                debugViewPass = data.shaders.fsr3DebugViewCS,
            };
        }

        public override void Dispose()
        {
            foreach (CameraContext context in m_CameraContexts.Values)
                context.Dispose();

            m_CameraContexts.Clear();
        }

        internal TextureHandle Render(
            RenderGraph renderGraph,
            UniversalResourceData resourceData,
            UniversalCameraData cameraData,
            in TextureHandle source,
            in TextureHandle exposure)
        {
            TextureDesc sourceDesc = source.GetDescriptor(renderGraph);
            bool hdrInput = GraphicsFormatUtility.IsHDRFormat(sourceDesc.format);
            Vector2Int renderSize = new(Mathf.Max(1, cameraData.scaledWidth), Mathf.Max(1, cameraData.scaledHeight));
            Vector2Int outputSize = new(Mathf.Max(1, cameraData.pixelWidth), Mathf.Max(1, cameraData.pixelHeight));
            CameraContext cameraContext = GetOrCreateCameraContext(cameraData.camera.GetInstanceID(), renderSize, outputSize, hdrInput);

            TextureHandle fsrColor = NormalizeRenderResolutionInput(renderGraph, in source, in sourceDesc, renderSize);

            TextureDesc outputDesc = new TextureDesc(outputSize.x, outputSize.y, false, false)
            {
                format = GraphicsFormatUtility.GetLinearFormat(sourceDesc.format),
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                enableRandomWrite = true,
                useMipMap = false,
                autoGenerateMips = false,
                name = k_OutputName,
            };
            MakeComputeCompatible(ref outputDesc);
            TextureHandle output = renderGraph.CreateTexture(outputDesc);

            TextureDesc reactiveDesc = new TextureDesc(renderSize.x, renderSize.y, false, false)
            {
                format = GraphicsFormat.R8_UNorm,
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                enableRandomWrite = true,
                useMipMap = false,
                autoGenerateMips = false,
                name = "_Fsr3ReactiveMask",
            };
            TextureHandle reactive = renderGraph.CreateTexture(reactiveDesc);

            Vector2 jitter = Fsr3Utils.CalculateJitter(cameraData);
            cameraData.camera.TryGetComponent<UniversalAdditionalCameraData>(out var additionalCameraData);
            Debug.Assert(additionalCameraData != null);
            MotionVectorsPersistentData motionData = additionalCameraData.motionVectorsPersistentData;
            Debug.Assert(motionData != null);

            using (var builder = renderGraph.AddUnsafePass<PassData>("FidelityFX Super Resolution 3.1", out PassData passData, s_ProfilingSampler))
            {
                passData.cameraContext = cameraContext;
                passData.color = fsrColor;
                passData.opaqueColor = resourceData.fsr3OpaqueColor;
                passData.depth = resourceData.cameraDepth;
                passData.motionVectors = resourceData.motionVectorColor;
                passData.exposure = exposure;
                passData.output = output;
                passData.reactive = reactive;
                passData.renderSize = renderSize;
                passData.outputSize = outputSize;
                passData.jitter = jitter;
                passData.deltaTime = motionData.deltaTime;
                passData.nearClip = cameraData.camera.nearClipPlane;
                passData.farClip = cameraData.camera.farClipPlane;
                passData.verticalFovRadians = cameraData.camera.fieldOfView * Mathf.Deg2Rad;
                passData.invertedDepth = SystemInfo.usesReversedZBuffer;
                passData.enableSharpening = cameraData.fsr3EnableSharpening;
                passData.sharpness = cameraData.fsr3Sharpness;
                passData.resetHistory = cameraData.resetHistory || cameraContext.resetHistory;
                passData.srgbInput = GraphicsFormatUtility.IsSRGBFormat(sourceDesc.format);

                builder.UseTexture(passData.color, AccessFlags.Read);
                builder.UseTexture(passData.opaqueColor, AccessFlags.Read);
                builder.UseTexture(passData.depth, AccessFlags.Read);
                builder.UseTexture(passData.motionVectors, AccessFlags.Read);
                if (passData.exposure.IsValid())
                    builder.UseTexture(passData.exposure, AccessFlags.Read);
                builder.UseTexture(passData.output, AccessFlags.Write);
                builder.UseTexture(passData.reactive, AccessFlags.ReadWrite);
                builder.AllowGlobalStateModification(true);

                builder.SetRenderFunc(static (PassData data, UnsafeGraphContext context) =>
                {
                    CommandBuffer commandBuffer = CommandBufferHelpers.GetNativeCommandBuffer(context.cmd);
                    data.cameraContext.upscaler.GenerateReactiveMask(new Fsr3Upscaler.GenerateReactiveDescription
                    {
                        ColorOpaqueOnly = new ResourceView(data.opaqueColor),
                        ColorPreUpscale = new ResourceView(data.color),
                        OutReactive = new ResourceView(data.reactive),
                        RenderSize = data.renderSize,
                        Scale = 0.9f,
                        CutoffThreshold = 0.05f,
                        BinaryValue = 0.5f,
                    }, commandBuffer);

                    var dispatch = new Fsr3Upscaler.DispatchDescription
                    {
                        Color = new ResourceView(data.color),
                        Depth = new ResourceView(data.depth, RenderTextureSubElement.Depth),
                        MotionVectors = new ResourceView(data.motionVectors),
                        Exposure = data.exposure.IsValid() ? new ResourceView(data.exposure) : ResourceView.Unassigned,
                        Reactive = new ResourceView(data.reactive),
                        TransparencyAndComposition = ResourceView.Unassigned,
                        Output = new ResourceView(data.output),
                        JitterOffset = data.jitter,
                        MotionVectorScale = -data.renderSize,
                        RenderSize = data.renderSize,
                        UpscaleSize = data.outputSize,
                        EnableSharpening = data.enableSharpening,
                        Sharpness = data.sharpness,
                        FrameTimeDelta = data.deltaTime,
                        PreExposure = 1.0f,
                        Reset = data.resetHistory,
                        CameraNear = data.invertedDepth ? data.farClip : data.nearClip,
                        CameraFar = data.invertedDepth ? data.nearClip : data.farClip,
                        CameraFovAngleVertical = data.verticalFovRadians,
                        ViewSpaceToMetersFactor = 1.0f,
                        IsSrgbInput = data.srgbInput,
                    };

                    data.cameraContext.upscaler.Dispatch(dispatch, commandBuffer);
                    data.cameraContext.resetHistory = false;
                });
            }

            UpdateCameraResolution(renderGraph, cameraData, outputSize);
            return output;
        }

        internal static TextureHandle NormalizeRenderResolutionInput(RenderGraph renderGraph, in TextureHandle source, in TextureDesc sourceDesc,
            Vector2Int renderSize, string name = "_Fsr3RenderResolutionColor", bool forceCopy = false)
        {
            bool alreadyExplicit = sourceDesc.sizeMode == TextureSizeMode.Explicit
                                   && !sourceDesc.useDynamicScale
                                   && sourceDesc.width == renderSize.x
                                   && sourceDesc.height == renderSize.y;
            if (!forceCopy && alreadyExplicit)
                return source;

            TextureDesc desc = sourceDesc;
            MakeCompatible(ref desc);
            desc.width = renderSize.x;
            desc.height = renderSize.y;
            desc.sizeMode = TextureSizeMode.Explicit;
            desc.useDynamicScale = false;
            desc.enableRandomWrite = false;
            desc.useMipMap = false;
            desc.autoGenerateMips = false;
            desc.filterMode = FilterMode.Bilinear;
            desc.name = name;

            TextureHandle normalized = renderGraph.CreateTexture(desc);
            using (var builder = renderGraph.AddRasterRenderPass<NormalizeInputPassData>("FSR 3.1 Normalize Color Input", out var passData))
            {
                passData.source = source;
                passData.destination = normalized;
                builder.UseTexture(source, AccessFlags.Read);
                builder.SetRenderAttachment(normalized, 0, AccessFlags.Write);
                builder.SetRenderFunc(static (NormalizeInputPassData data, RasterGraphContext context) =>
                {
                    Vector4 scaleBias = RenderingUtils.GetFinalBlitScaleBias(in context, in data.source, in data.destination);
                    Blitter.BlitTexture(context.cmd, data.source, scaleBias, 0.0f, true);
                });
            }

            return normalized;
        }

        private CameraContext GetOrCreateCameraContext(int cameraId, Vector2Int renderSize, Vector2Int outputSize, bool hdrInput)
        {
            if (m_CameraContexts.TryGetValue(cameraId, out CameraContext context)
                && context.renderSize == renderSize
                && context.outputSize == outputSize
                && context.hdrInput == hdrInput)
            {
                return context;
            }

            context?.Dispose();

            Fsr3UpscalerShaders shaders = m_ShaderResources.CloneForContext();
            Fsr3Upscaler.InitializationFlags flags = 0;
            if (hdrInput)
                flags |= Fsr3Upscaler.InitializationFlags.EnableHighDynamicRange;
            if (SystemInfo.usesReversedZBuffer)
                flags |= Fsr3Upscaler.InitializationFlags.EnableDepthInverted;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            flags |= Fsr3Upscaler.InitializationFlags.EnableDebugChecking;
#endif

            var upscaler = new Fsr3UpscalerContext();
            upscaler.Create(new Fsr3Upscaler.ContextDescription
            {
                Flags = flags,
                MaxRenderSize = renderSize,
                MaxUpscaleSize = outputSize,
                Shaders = shaders,
            });

            context = new CameraContext
            {
                upscaler = upscaler,
                shaders = shaders,
                outputSize = outputSize,
                renderSize = renderSize,
                hdrInput = hdrInput,
                resetHistory = true,
            };
            m_CameraContexts[cameraId] = context;
            return context;
        }

    }
}
