using System;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Serialization;

namespace UnityEngine.Rendering.Universal
{
    // All of TAA here, work on TAA == work on this file.

    /// <summary>
    /// Temporal Anti-aliasing quality setting.
    /// </summary>
    public enum TemporalAAQuality
    {
        // Quality levels trade reconstruction cost for history stability and filtering quality.

        /// <summary>
        /// 5-tap RGB clamp. No motion dilation. Suitable for no/low motion scenes.
        /// </summary>
        VeryLow = 0,

        /// <summary>
        /// 5-tap RGB clamp. 5-tap motion dilation.
        /// </summary>
        Low,

        /// <summary>
        /// 9-tap YCoCg variance clamp. 9-tap motion dilation.
        /// </summary>
        Medium,

        /// <summary>
        /// 9-tap YCoCg variance clamp and bicubic history.
        /// </summary>
        High,

        // VeryHigh is a catch all option for enabling all the implemented features regardless of cost.
        // Currently, 9-tap YCoCg variance clip, bicubic history and center sample filtering.
        // In the future, VeryHigh mode could read additional buffers to improve the quality further.

        /// <summary>
        /// Best quality, everything enabled.
        /// </summary>
        VeryHigh
    }

    /// <summary>
    /// Temporal anti-aliasing.
    /// </summary>
    public static class TemporalAA
    {
        internal static class ShaderConstants
        {
            public static readonly int _InputTexture = Shader.PropertyToID("_InputTexture");
            public static readonly int _OutputTexture = Shader.PropertyToID("_OutputTexture");
            public static readonly int _CameraDepthTexture = Shader.PropertyToID("_CameraDepthTexture");
            public static readonly int _TaaAccumulationTex = Shader.PropertyToID("_TaaAccumulationTex");
            public static readonly int _TaaMotionVectorTex = Shader.PropertyToID("_TaaMotionVectorTex");
            public static readonly int _SourceSize = Shader.PropertyToID("_SourceSize");
            public static readonly int _TaaMotionVectorTex_TexelSize = Shader.PropertyToID("_TaaMotionVectorTex_TexelSize");
            public static readonly int _TaaAccumulationTex_TexelSize = Shader.PropertyToID("_TaaAccumulationTex_TexelSize");
            public static readonly int _TaaFilterWeights0 = Shader.PropertyToID("_TaaFilterWeights0");
            public static readonly int _TaaFilterWeights1 = Shader.PropertyToID("_TaaFilterWeights1");
            public static readonly int _TaaFilterWeights2 = Shader.PropertyToID("_TaaFilterWeights2");
            public static readonly int _TaaFrameInfluence = Shader.PropertyToID("_TaaFrameInfluence");
            public static readonly int _TaaVarianceClampScale = Shader.PropertyToID("_TaaVarianceClampScale");
            public static readonly int _TaaLowPrecisionSource = Shader.PropertyToID("_TaaLowPrecisionSource");
            public static readonly int _TaaAlphaOutput = Shader.PropertyToID("_TaaAlphaOutput");
        }

        /// <summary>
        /// Temporal anti-aliasing settings.
        /// </summary>
        [Serializable]
        public struct Settings
        {
            [SerializeField]
            [FormerlySerializedAs("quality")]
            internal TemporalAAQuality m_Quality;
            [SerializeField]
            [FormerlySerializedAs("frameInfluence")]
            internal float m_FrameInfluence;
            [SerializeField]
            [FormerlySerializedAs("jitterScale")]
            internal float m_JitterScale;
            [SerializeField]
            [FormerlySerializedAs("mipBias")]
            internal float m_MipBias;
            [SerializeField]
            [FormerlySerializedAs("varianceClampScale")]
            internal float m_VarianceClampScale;
            [SerializeField]
            [FormerlySerializedAs("contrastAdaptiveSharpening")]
            internal float m_ContrastAdaptiveSharpening;

            // Internal API
            [NonSerialized] internal int resetHistoryFrames;      // Number of frames the history is reset. 0 no reset, 1 normal reset, -1 infinite (toggle on)
            [NonSerialized] internal int jitterFrameCountOffset;  // Jitter "seed" == Time.frameCount + jitterFrameCountOffset. Used for testing determinism.

            /// <summary>
            /// The quality level to use for the temporal anti-aliasing.
            /// </summary>
            public TemporalAAQuality quality
            {
                get => m_Quality;
                set => m_Quality = (TemporalAAQuality)Mathf.Clamp((int)value, (int)TemporalAAQuality.VeryLow, (int)TemporalAAQuality.VeryHigh);
            }
            /// <summary>
            /// Determines how much the history buffer is blended together with current frame result. Higher values means more history contribution, which leads to better anti aliasing, but also more prone to ghosting.
            /// Between 0.0 - 1.0.
            /// </summary>
            public float baseBlendFactor
            {
                // URP uses frame influence, amount of current frame to blend with history.
                // HDRP uses base blend factor, the amount of history to blend with current frame.
                // We flip the value here to match HDRP for consistent API/UI.
                get => 1.0f - m_FrameInfluence;
                set => m_FrameInfluence = Mathf.Clamp01(1.0f - value);
            }

            /// <summary>
            /// Determines the scale to the jitter applied when TAA is enabled. Lowering this value will lead to less visible flickering and jittering, but also will produce more aliased images.
            /// </summary>
            public float jitterScale
            {
                get => m_JitterScale;
                set => m_JitterScale = Mathf.Clamp01(value);
            }

            /// <summary>
            /// Determines how much texture mip map selection is biased when rendering. Lowering this can slightly reduce blur on textures at the cost of performance. Requires mip maps in textures.
            /// Between -1.0 - 0.0.
            /// </summary>
            public float mipBias
            {
                get => m_MipBias;
                set => m_MipBias = Mathf.Clamp(value, -1.0f, 0.0f);
            }

            /// <summary>
            /// Determines the strength of the history color rectification clamp. Lower values can reduce ghosting, but produce more flickering. Higher values reduce flickering, but are prone to blur and ghosting.
            /// Between 0.001 - 10.0.
            /// Good values around 1.0.
            /// </summary>
            public float varianceClampScale
            {
                get => m_VarianceClampScale;
                set => m_VarianceClampScale = Mathf.Clamp(value, 0.001f, 10.0f);
            }

            /// <summary>
            /// Enables high quality post sharpening to reduce TAA blur. The FSR upscaling overrides this setting if enabled.
            /// Between 0.0 - 1.0.
            /// Use 0.0 to disable.
            /// </summary>
            public float contrastAdaptiveSharpening
            {
                get => m_ContrastAdaptiveSharpening;
                set => m_ContrastAdaptiveSharpening = Mathf.Clamp01(value);
            }

            /// <summary>
            /// Creates a new instance of the settings with default values.
            /// </summary>
            /// <returns>Default settings.</returns>
            public static Settings Create()
            {
                Settings s;

                s.m_Quality                    = TemporalAAQuality.High;
                s.m_FrameInfluence             = 0.1f;
                s.m_JitterScale                = 1.0f;
                s.m_MipBias                    = 0.0f;
                s.m_VarianceClampScale         = 0.9f;
                s.m_ContrastAdaptiveSharpening = 0.0f; // Disabled

                s.resetHistoryFrames     = 0;
                s.jitterFrameCountOffset = 0;

                return s;
            }
        }


        /// <summary>
        /// A function delegate that returns a jitter offset for the provided frame
        /// This provides support for cases where a non-standard jitter pattern is desired
        /// </summary>
        /// <param name="frameIndex">index of the current frame</param>
        /// <param name="jitter">computed jitter offset</param>
        /// <param name="allowScaling">true if the jitter function's output supports scaling</param>
        internal delegate void JitterFunc(int frameIndex, out Vector2 jitter, out bool allowScaling);

        internal static int CalculateTaaFrameIndex(ref Settings settings)
        {
            // URP supports adding an offset value to the TAA frame index for testing determinism.
            int taaFrameCountOffset = settings.jitterFrameCountOffset;
            return Time.frameCount + taaFrameCountOffset;
        }

        internal static Matrix4x4 CalculateJitterMatrix(UniversalCameraData cameraData, JitterFunc jitterFunc)
        {
            Matrix4x4 jitterMat = Matrix4x4.identity;

            bool isJitter = cameraData.IsTemporalAAEnabled();
            if (isJitter)
            {
                int taaFrameIndex = CalculateTaaFrameIndex(ref cameraData.taaSettings);

                float actualWidth = cameraData.cameraTargetDescriptor.width;
                float actualHeight = cameraData.cameraTargetDescriptor.height;
                float jitterScale = cameraData.taaSettings.jitterScale;

                Vector2 jitter;
                bool allowScaling;
                jitterFunc(taaFrameIndex, out jitter, out allowScaling);

                if (allowScaling)
                    jitter *= jitterScale;

                float offsetX = jitter.x * (2.0f / actualWidth);
                float offsetY = jitter.y * (2.0f / actualHeight);

                jitterMat = Matrix4x4.Translate(new Vector3(offsetX, offsetY, 0.0f));
            }

            return jitterMat;
        }

        internal static void CalculateJitter(int frameIndex, out Vector2 jitter, out bool allowScaling)
        {
            // The variance between 0 and the actual halton sequence values reveals noticeable
            // instability in Unity's shadow maps, so we avoid index 0.
            float jitterX = HaltonSequence.Get((frameIndex & 1023) + 1, 2) - 0.5f;
            float jitterY = HaltonSequence.Get((frameIndex & 1023) + 1, 3) - 0.5f;

            jitter = new Vector2(jitterX, jitterY);
            allowScaling = true;
        }

        // Static allocation of JitterFunc delegate to avoid GC
        internal static JitterFunc s_JitterFunc = CalculateJitter;

        private static readonly Vector2[] taaFilterOffsets = new Vector2[]
        {
            new Vector2(0.0f, 0.0f),

            new Vector2(0.0f, 1.0f),
            new Vector2(1.0f, 0.0f),
            new Vector2(-1.0f, 0.0f),
            new Vector2(0.0f, -1.0f),

            new Vector2(-1.0f, 1.0f),
            new Vector2(1.0f, -1.0f),
            new Vector2(1.0f, 1.0f),
            new Vector2(-1.0f, -1.0f)
        };

        private static readonly float[] taaFilterWeights = new float[taaFilterOffsets.Length + 1];

        internal static float[] CalculateFilterWeights(ref Settings settings)
        {
            int taaFrameIndex = CalculateTaaFrameIndex(ref settings);

            // Based on HDRP
            // Precompute weights used for the Blackman-Harris filter.
            float totalWeight = 0;
            for (int i = 0; i < 9; ++i)
            {
                // The internal jitter function used by TAA always allows scaling
                CalculateJitter(taaFrameIndex, out var jitter, out var _);
                jitter *= settings.jitterScale;

                // The rendered frame (pixel grid) is already jittered.
                // We sample 3x3 neighbors with int offsets, but weight the samples
                // relative to the distance to the non-jittered pixel center.
                // From the POV of offset[0] at (0,0), the original pixel center is at (-jitter.x, -jitter.y).
                float x = taaFilterOffsets[i].x - jitter.x;
                float y = taaFilterOffsets[i].y - jitter.y;
                float d2 = (x * x + y * y);

                taaFilterWeights[i] = Mathf.Exp((-0.5f / (0.22f)) * d2);
                totalWeight += taaFilterWeights[i];
            }

            // Normalize weights.
            for (int i = 0; i < 9; ++i)
            {
                taaFilterWeights[i] /= totalWeight;
            }

            return taaFilterWeights;
        }

        internal static GraphicsFormat[] AccumulationFormatList = new GraphicsFormat[]
        {
            GraphicsFormat.R16G16B16A16_SFloat,
            GraphicsFormat.B10G11R11_UFloatPack32,
            GraphicsFormat.R8G8B8A8_UNorm,
            GraphicsFormat.B8G8R8A8_UNorm,
        };

        internal static RenderTextureDescriptor TemporalAADescFromCameraDesc(ref RenderTextureDescriptor cameraDesc)
        {
            RenderTextureDescriptor taaDesc = cameraDesc;

            // Explicitly set from cameraDesc.* for clarity.
            taaDesc.width = cameraDesc.width;
            taaDesc.height = cameraDesc.height;
            taaDesc.msaaSamples = 1;
            taaDesc.volumeDepth = 1;
            taaDesc.mipCount = 0;
            taaDesc.graphicsFormat = cameraDesc.graphicsFormat;
            taaDesc.sRGB = false;
            taaDesc.depthStencilFormat = GraphicsFormat.None;
            taaDesc.dimension = UnityEngine.Rendering.TextureDimension.Tex2D;
            taaDesc.memoryless = RenderTextureMemoryless.None;
            taaDesc.useMipMap = false;
            taaDesc.autoGenerateMips = false;
            taaDesc.enableRandomWrite = true;
            taaDesc.bindMS = false;
            taaDesc.useDynamicScale = false;

            if (!SystemInfo.IsFormatSupported(taaDesc.graphicsFormat, GraphicsFormatUsage.LoadStore))
            {
                taaDesc.graphicsFormat = GraphicsFormat.None;
                for (int i = 0; i < AccumulationFormatList.Length; i++)
                    if (SystemInfo.IsFormatSupported(AccumulationFormatList[i], GraphicsFormatUsage.LoadStore))
                    {
                        taaDesc.graphicsFormat = AccumulationFormatList[i];
                        break;
                    }
            }

            return taaDesc;
        }

        static uint s_warnCounter = 0;

        internal static string ValidateAndWarn(UniversalCameraData cameraData, bool isSTPRequested = false)
        {
            string reasonWarning = null;

            if(reasonWarning == null && !cameraData.postProcessEnabled)
                reasonWarning = "because camera has post-processing disabled.";

            if (cameraData.taaHistory == null)
            {
                reasonWarning = "due to invalid persistent data.";
            }

            if (reasonWarning == null && cameraData.cameraTargetDescriptor.msaaSamples != 1)
                reasonWarning = "because MSAA is on. Turn MSAA off on the camera or current URP Asset.";

            if(reasonWarning == null && cameraData.camera.TryGetComponent<UniversalAdditionalCameraData>(out var additionalCameraData))
            {
                if (additionalCameraData.renderType == CameraRenderType.Overlay ||
                    additionalCameraData.cameraStack.Count > 0)
                {
                    reasonWarning = "because camera is stacked.";
                }
            }

            if (reasonWarning == null && cameraData.camera.allowDynamicResolution)
                reasonWarning = "because camera has dynamic resolution enabled. You can use a constant render scale instead.";

            if(reasonWarning == null && !cameraData.renderer.SupportsMotionVectors())
                reasonWarning = "because the renderer does not implement motion vectors. Motion vectors are required.";

            if (reasonWarning != null)
            {
                const int warningThrottleFrames = 60 * 1; // 60 FPS * 1 sec
                if (s_warnCounter % warningThrottleFrames == 0)
                    Debug.LogWarning("Disabling TAA " + (isSTPRequested ? "and STP " : "") + reasonWarning);
                s_warnCounter++;
            }

            return reasonWarning;
        }

        private const int k_ThreadGroupSize = 16;

        private class TaaPassData
        {
            internal TextureHandle dstTex;
            internal TextureHandle srcColorTex;
            internal TextureHandle srcDepthTex;
            internal TextureHandle srcMotionVectorTex;
            internal TextureHandle srcTaaAccumTex;

            internal ComputeShader shader;
            internal int kernel;
            internal int width;
            internal int height;
            internal Vector4 sourceSize;
            internal Vector4 motionVectorTexelSize;
            internal Vector4 accumulationTexelSize;
            internal Vector4 filterWeights0;
            internal Vector4 filterWeights1;
            internal Vector4 filterWeights2;
            internal float taaFrameInfluence;
            internal float taaVarianceClampScale;
            internal int taaLowPrecisionSource;
            internal int taaAlphaOutput;
        }

        internal static TextureHandle Render(RenderGraph renderGraph, ComputeShader taaShader, int kernel,
            UniversalCameraData cameraData, ref TextureHandle srcColor, ref TextureHandle srcDepth,
            ref TextureHandle srcMotionVectors)
        {
            renderGraph.BeginProfilingSampler(ProfilingSampler.Get(URPProfileId.TemporalAntiAliasing));

            ref var taa = ref cameraData.taaSettings;
            bool isNewFrame = cameraData.taaHistory.GetAccumulationVersion() != Time.frameCount;
            float taaInfluence = taa.resetHistoryFrames == 0 ? taa.m_FrameInfluence : 1.0f;

            RTHandle currentAccumulation = cameraData.taaHistory.GetAccumulationTexture();
            TextureHandle dstColor = renderGraph.ImportTexture(currentAccumulation);
            TextureHandle srcAccumulation;
            if (taa.resetHistoryFrames != 0)
            {
                // A reset has no temporal contribution from history. Bind the initialized current frame
                // instead of reading a freshly allocated history texture with undefined contents.
                srcAccumulation = srcColor;
            }
            else
            {
                RTHandle previousAccumulation = cameraData.taaHistory.GetPreviousAccumulationTexture();
                srcAccumulation = renderGraph.ImportTexture(previousAccumulation);
            }

            // Repeated rendering in the same camera frame still resolves against the immutable previous
            // history. Suppressing motion prevents applying the same temporal displacement twice.
            TextureHandle activeMotionVectors = isNewFrame ? srcMotionVectors : renderGraph.defaultResources.blackTexture;

            int width = Mathf.Max(1, currentAccumulation.rt.width);
            int height = Mathf.Max(1, currentAccumulation.rt.height);
            Vector4 sourceSize = new Vector4(width, height, 1.0f / width, 1.0f / height);
            Vector4 accumulationTexelSize = new Vector4(1.0f / width, 1.0f / height, width, height);

            // Motion vectors are camera-resolution in URP. On repeated renders the 1x1 black default
            // texture is intentionally sampled with these texel offsets; the result remains zero.
            Vector4 motionVectorTexelSize = accumulationTexelSize;

            Vector4 filterWeights0 = Vector4.zero;
            Vector4 filterWeights1 = Vector4.zero;
            Vector4 filterWeights2 = Vector4.zero;
            if (taa.quality == TemporalAAQuality.VeryHigh)
            {
                float[] weights = CalculateFilterWeights(ref taa);
                filterWeights0 = new Vector4(weights[0], weights[1], weights[2], weights[3]);
                filterWeights1 = new Vector4(weights[4], weights[5], weights[6], weights[7]);
                filterWeights2 = new Vector4(weights[8], 0.0f, 0.0f, 0.0f);
            }

            using (var builder = renderGraph.AddComputePass<TaaPassData>("Temporal Anti-aliasing", out var passData,
                       ProfilingSampler.Get(URPProfileId.TAA)))
            {
                passData.dstTex = dstColor;
                passData.srcColorTex = srcColor;
                passData.srcDepthTex = srcDepth;
                passData.srcMotionVectorTex = activeMotionVectors;
                passData.srcTaaAccumTex = srcAccumulation;
                passData.shader = taaShader;
                passData.kernel = kernel;
                passData.width = width;
                passData.height = height;
                passData.sourceSize = sourceSize;
                passData.motionVectorTexelSize = motionVectorTexelSize;
                passData.accumulationTexelSize = accumulationTexelSize;
                passData.filterWeights0 = filterWeights0;
                passData.filterWeights1 = filterWeights1;
                passData.filterWeights2 = filterWeights2;
                passData.taaFrameInfluence = taaInfluence;
                passData.taaVarianceClampScale = taa.varianceClampScale;
                passData.taaLowPrecisionSource = currentAccumulation.rt.graphicsFormat switch
                {
                    GraphicsFormat.B10G11R11_UFloatPack32 => 1,
                    GraphicsFormat.R8G8B8A8_UNorm => 1,
                    GraphicsFormat.B8G8R8A8_UNorm => 1,
                    _ => 0
                };
                passData.taaAlphaOutput = cameraData.isAlphaOutputEnabled ? 1 : 0;

                builder.UseTexture(srcColor, AccessFlags.Read);
                builder.UseTexture(srcDepth, AccessFlags.Read);
                builder.UseTexture(activeMotionVectors, AccessFlags.Read);
                builder.UseTexture(srcAccumulation, AccessFlags.Read);
                builder.UseTexture(dstColor, AccessFlags.Write);
                builder.SetRenderFunc(static (TaaPassData data, ComputeGraphContext context) => ExecutePass(data, context.cmd));
            }

            if (isNewFrame)
                cameraData.taaHistory.SetAccumulationVersion(Time.frameCount);

            renderGraph.EndProfilingSampler(ProfilingSampler.Get(URPProfileId.TemporalAntiAliasing));
            return dstColor;
        }

        private static void ExecutePass(TaaPassData data, ComputeCommandBuffer commandBuffer)
        {
            commandBuffer.SetComputeTextureParam(data.shader, data.kernel, ShaderConstants._InputTexture, data.srcColorTex);
            commandBuffer.SetComputeTextureParam(data.shader, data.kernel, ShaderConstants._OutputTexture, data.dstTex);
            commandBuffer.SetComputeTextureParam(data.shader, data.kernel, ShaderConstants._CameraDepthTexture, data.srcDepthTex);
            commandBuffer.SetComputeTextureParam(data.shader, data.kernel, ShaderConstants._TaaMotionVectorTex, data.srcMotionVectorTex);
            commandBuffer.SetComputeTextureParam(data.shader, data.kernel, ShaderConstants._TaaAccumulationTex, data.srcTaaAccumTex);
            commandBuffer.SetComputeVectorParam(data.shader, ShaderConstants._SourceSize, data.sourceSize);
            commandBuffer.SetComputeVectorParam(data.shader, ShaderConstants._TaaMotionVectorTex_TexelSize, data.motionVectorTexelSize);
            commandBuffer.SetComputeVectorParam(data.shader, ShaderConstants._TaaAccumulationTex_TexelSize, data.accumulationTexelSize);
            commandBuffer.SetComputeVectorParam(data.shader, ShaderConstants._TaaFilterWeights0, data.filterWeights0);
            commandBuffer.SetComputeVectorParam(data.shader, ShaderConstants._TaaFilterWeights1, data.filterWeights1);
            commandBuffer.SetComputeVectorParam(data.shader, ShaderConstants._TaaFilterWeights2, data.filterWeights2);
            commandBuffer.SetComputeFloatParam(data.shader, ShaderConstants._TaaFrameInfluence, data.taaFrameInfluence);
            commandBuffer.SetComputeFloatParam(data.shader, ShaderConstants._TaaVarianceClampScale, data.taaVarianceClampScale);
            commandBuffer.SetComputeIntParam(data.shader, ShaderConstants._TaaLowPrecisionSource, data.taaLowPrecisionSource);
            commandBuffer.SetComputeIntParam(data.shader, ShaderConstants._TaaAlphaOutput, data.taaAlphaOutput);
            commandBuffer.DispatchCompute(data.shader, data.kernel,
                (data.width + k_ThreadGroupSize - 1) / k_ThreadGroupSize,
                (data.height + k_ThreadGroupSize - 1) / k_ThreadGroupSize,
                1);
        }
    }
}
