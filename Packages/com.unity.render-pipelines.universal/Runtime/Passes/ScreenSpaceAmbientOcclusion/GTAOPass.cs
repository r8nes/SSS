using System;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering.RenderGraphModule;

namespace UnityEngine.Rendering.Universal
{
    // Ground Truth Ambient Occlusion (GTAO) Pass using compute shaders.
    internal class GTAOPass : ScriptableRenderPass, IDepthPyramidConsumer, IDisposable
    {
        // Shared state
        private readonly bool m_SupportsR8RenderTextureFormat = SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.R8);
        private readonly bool m_SupportsComputeShader = SystemInfo.supportsComputeShaders;
        private int m_BlueNoiseTextureIndex = 0;
        private Material m_Material;
        private Texture2D[] m_BlueNoiseTextures;
        private SSAOUtils.CameraViewData m_CameraViewData = SSAOUtils.CameraViewData.Create();
        private SSAOUtils.BlurTypes m_BlurType = SSAOUtils.BlurTypes.Bilateral;
        private ProfilingSampler m_ProfilingSampler = ProfilingSampler.Get(URPProfileId.SSAO);
        private ScreenSpaceAmbientOcclusionSettings m_CurrentSettings;

        private ComputePathState m_ComputePathState;

        // Constants
        private const float k_DegreesPerRotation = 360.0f;
        private const int k_TemporalOffsetCount = 4;
        private const int k_RequiredDepthPyramidMipCount = 2;
        private const int k_ComputeThreadGroupSize = 8;

        public bool requiresDepthPyramid => true;
        public int requiredDepthPyramidMipCount => k_RequiredDepthPyramidMipCount;
        public int requiredDepthPyramidCheckerboardMipCount => 0;

        // GTAO-only Shader Property IDs
        internal static class ShaderIDs
        {
            // Compute kernel I/O bindings
            public static readonly int _CameraDepthTexture = Shader.PropertyToID("_CameraDepthTexture");
            public static readonly int _AOOutput = Shader.PropertyToID("_AOOutput");
            public static readonly int _BlurOutput = Shader.PropertyToID("_BlurOutput");
            public static readonly int _OcclusionTexture = Shader.PropertyToID("_OcclusionTexture");
            public static readonly int _AOOutputHistory = Shader.PropertyToID("_AOOutputHistory");
            public static readonly int _FinalOutput = Shader.PropertyToID("_FinalOutput");

            // Temporal filter
            public static readonly int _AOPackedHistory = Shader.PropertyToID("_AOPackedHistory");
            public static readonly int _MotionVectorTexture = Shader.PropertyToID("_MotionVectorTexture");
            public static readonly int _SSAOHistoryLength = Shader.PropertyToID("_SSAOHistoryLength");
            public static readonly int _SSAOGhostingMitigation = Shader.PropertyToID("_SSAOGhostingMitigation");
            public static readonly int _SSAOTemporalRotation = Shader.PropertyToID("_SSAOTemporalRotation");
            public static readonly int _SSAOTemporalOffset = Shader.PropertyToID("_SSAOTemporalOffset");

            // GTAO compute parameters
            public static readonly int _GTAODirectionCount = Shader.PropertyToID("_GTAODirectionCount");
            public static readonly int _GTAOStepCount = Shader.PropertyToID("_GTAOStepCount");
            public static readonly int _GTAOBoxFilterStep = Shader.PropertyToID("_GTAOBoxFilterStep");
            public static readonly int _GTAOTemporalHistoryValid = Shader.PropertyToID("_GTAOTemporalHistoryValid");
            public static readonly int _GTAOProjectionChanged = Shader.PropertyToID("_GTAOProjectionChanged");
            public static readonly int _GTAOUpsampleOutputFinal = Shader.PropertyToID("_GTAOUpsampleOutputFinal");
        }

        // Temporal rotation/offset arrays for GTAO temporal filtering
        private static readonly float[] s_TemporalRotations = { 60.0f, 300.0f, 180.0f, 240.0f, 120.0f, 0.0f };

        // Structs
        private struct LocalKeywordSet
        {
            public LocalKeyword blueNoise;
            public LocalKeyword interleavedGradient;
            public LocalKeyword temporalFiltering;
            public LocalKeyword orthographic;
            public LocalKeyword depthPyramid;

            public void Init(ComputeShader cs)
            {
                blueNoise = new(cs, ScreenSpaceAmbientOcclusionKeywords.k_AOBlueNoiseKeyword);
                interleavedGradient = new(cs, ScreenSpaceAmbientOcclusionKeywords.k_AOInterleavedGradientKeyword);
                temporalFiltering = new(cs, ScreenSpaceAmbientOcclusionKeywords.k_TemporalFilteringKeyword);
                orthographic = new(cs, ScreenSpaceAmbientOcclusionKeywords.k_OrthographicCameraKeyword);
                depthPyramid = new(cs, "_GTAO_DEPTH_PYRAMID");
            }
        }

        private struct ComputePathState
        {
            public ComputeShader shader;
            public int gtaoKernel;
            public int gtaoLowKernel;
            public int gtaoMediumKernel;
            public int gtaoHighKernel;
            public int spatialDenoiseKernel;
            public int spatialDenoiseFinalKernel;
            public int boxSpatialFilterKernel;
            public int temporalKernel;
            public int temporalPrefilteredKernel;
            public int temporalFinalKernel;
            public int temporalFinalPrefilteredKernel;
            public int bilateralUpsampleKernel;
            public int finalBlitKernel;
            public LocalKeywordSet keywords;
            public RTHandle blueNoiseRTHandle;

            public void Init(ComputeShader cs)
            {
                Debug.Assert(SystemInfo.supportsComputeShaders, "ComputePathState must not be initialized when the platform does not support compute shaders.");

                shader = cs;

                if (shader == null)
                    return;

                Debug.Assert(
                    shader.HasKernel("ComputeAO") &&
                    shader.HasKernel("ComputeAOLow") &&
                    shader.HasKernel("ComputeAOMedium") &&
                    shader.HasKernel("ComputeAOHigh") &&
                    shader.HasKernel("SpatialDenoise") &&
                    shader.HasKernel("SpatialDenoiseFinal") &&
                    shader.HasKernel("BoxSpatialFilter") &&
                    shader.HasKernel("TemporalFilter") &&
                    shader.HasKernel("TemporalFilterPrefiltered") &&
                    shader.HasKernel("TemporalFilterFinal") &&
                    shader.HasKernel("TemporalFilterFinalPrefiltered") &&
                    shader.HasKernel("BilateralUpsample") &&
                    shader.HasKernel("FinalBlit"),
                    "The GTAO compute shader does not have the expected kernels.");

                gtaoKernel = shader.FindKernel("ComputeAO");
                gtaoLowKernel = shader.FindKernel("ComputeAOLow");
                gtaoMediumKernel = shader.FindKernel("ComputeAOMedium");
                gtaoHighKernel = shader.FindKernel("ComputeAOHigh");
                spatialDenoiseKernel = shader.FindKernel("SpatialDenoise");
                spatialDenoiseFinalKernel = shader.FindKernel("SpatialDenoiseFinal");
                boxSpatialFilterKernel = shader.FindKernel("BoxSpatialFilter");
                temporalKernel = shader.FindKernel("TemporalFilter");
                temporalPrefilteredKernel = shader.FindKernel("TemporalFilterPrefiltered");
                temporalFinalKernel = shader.FindKernel("TemporalFilterFinal");
                temporalFinalPrefilteredKernel = shader.FindKernel("TemporalFilterFinalPrefiltered");
                bilateralUpsampleKernel = shader.FindKernel("BilateralUpsample");
                finalBlitKernel = shader.FindKernel("FinalBlit");
                keywords.Init(shader);
            }

            public void Release()
            {
                blueNoiseRTHandle?.Release();
                blueNoiseRTHandle = null;
            }
        }

        private struct GTAOComputeParams
        {
            public Vector4 ssaoParams;
            public Vector4 ssaoParams2;
            public Vector4 depthToViewParams;
            public Vector4 sourceSize;
            public Vector4 blueNoiseParams;
            public float historyLength;
            public float ghostingMitigation;
            public Vector4 projectionParams2;
            public bool orthographicCamera;
            public float temporalRotation;
            public int temporalOffset;
        }

        private struct TemporalHistoryState
        {
            public TextureHandle historyReadTexture;
            public TextureHandle historyWriteTexture;
            public SSAOHistory ssaoHistory;
            public int frameIndex;
            public bool historyReady;
            public bool isNewFrame;
            public bool projectionChanged;
        }

        // Pass Data Classes
        private class GTAOComputePassData
        {
            public ComputeShader cs;
            public int kernel;
            public Vector2Int dispatchSize;
            public bool useBlueNoise;
            public LocalKeywordSet localKeywords;
            public bool willRunTemporalFilter;
            public int directionCount;
            public int stepCount;
            public GTAOComputeParams computeParams;
            public Matrix4x4[] cameraViewProjections = new Matrix4x4[2];
            public Vector4[] cameraTopLeftCorner = new Vector4[2];
            public Vector4[] cameraXExtent = new Vector4[2];
            public Vector4[] cameraYExtent = new Vector4[2];
            public Vector4[] cameraZExtent = new Vector4[2];
            public TextureHandle depthTexture;
            public TextureHandle normalsTexture;
            public TextureHandle blueNoiseTexture;
            public TextureHandle depthPyramidTexture;
            public BufferHandle depthPyramidOffsets;
            public int depthPyramidMipCount;
            public Vector4 depthPyramidMip0Size;
            public bool useDepthPyramid;
            public TextureHandle aoTexture;
        }

        private class GTAOSingleDispatchPassData
        {
            public ComputeShader cs;
            public int kernel;
            public int dstPropertyId;
            public Vector2Int dispatchSize;
            public TextureHandle srcTexture;
            public TextureHandle dstTexture;
            public float boxFilterStep;
            public bool outputFinal;
            public float directLightingStrength;
        }

        private class GTAOTemporalPassData
        {
            public ComputeShader cs;
            public int kernel;
            public Vector2Int dispatchSize;
            public TextureHandle aoTexture;
            public TextureHandle historyReadTexture;
            public TextureHandle historyWriteTexture;
            public TextureHandle motionVectorTexture;
            public TextureHandle temporalTexture;
            public bool historyValid;
            public bool projectionChanged;
            public bool outputFinal;
            public float directLightingStrength;
        }

        private class GTAOFinalBlitPassData
        {
            public ComputeShader cs;
            public int kernel;
            public Vector2Int dispatchSize;
            public float directLightingStrength;
            public TextureHandle srcTexture;
            public TextureHandle depthTexture;
            public TextureHandle finalTexture;
            public bool publishAsSSAO;
        }

        private class GTAOAfterOpaqueBlitPassData
        {
            public Material material;
            public Vector4 sourceSize;
            public Vector4 ssaoParams;
            public int shaderPass;
            public TextureHandle sourceTexture;
            public TextureHandle targetTexture;
        }

        internal GTAOPass(Shader shader, Texture2D[] blueNoiseTextures)
        {
            m_CurrentSettings = new ScreenSpaceAmbientOcclusionSettings();
            m_Material = CoreUtils.CreateEngineMaterial(shader);
            m_BlueNoiseTextures = blueNoiseTextures;

            if (m_SupportsComputeShader && GraphicsSettings.TryGetRenderPipelineSettings<ScreenSpaceAmbientOcclusionCoreResources>(out var ssaoCoreResources))
                m_ComputePathState.Init(ssaoCoreResources.GTAOComputeShader);
        }

        // Sets up GTAO pass using active volume. Returns false if SSAO should be skipped.
        internal bool Setup(ScreenSpaceAmbientOcclusionVolumeOverride ssaoVolume)
        {
            ApplyVolumeSettings(m_CurrentSettings, ssaoVolume);
            m_BlurType = SSAOUtils.GetBlurType(m_CurrentSettings.BlurQuality);
            return ssaoVolume.IsActive() && m_ComputePathState.shader != null;
        }

        private static void ApplyVolumeSettings(ScreenSpaceAmbientOcclusionSettings settings, ScreenSpaceAmbientOcclusionVolumeOverride volume)
        {
            // Common parameters
            settings.Mode = ScreenSpaceAmbientOcclusionMode.GTAO;
            settings.Intensity = volume.intensity;
            settings.Radius = volume.radius;
            settings.DirectLightingStrength = volume.directLightingStrength;
            settings.Falloff = volume.falloffDistance;
            settings.Downsample = volume.downsample;
            settings.AfterOpaque = volume.afterOpaque;
            settings.AOMethod = volume.method == ScreenSpaceAmbientOcclusionNoiseMethod.BlueNoise ? ScreenSpaceAmbientOcclusionSettings.AOMethodOptions.BlueNoise : ScreenSpaceAmbientOcclusionSettings.AOMethodOptions.InterleavedGradient;
            settings.Samples = volume.sampleCount switch
            {
                ScreenSpaceAmbientOcclusionSampleCount.Low => ScreenSpaceAmbientOcclusionSettings.AOSampleOption.Low,
                ScreenSpaceAmbientOcclusionSampleCount.High => ScreenSpaceAmbientOcclusionSettings.AOSampleOption.High,
                _ => ScreenSpaceAmbientOcclusionSettings.AOSampleOption.Medium
            };

            // GTAO mode parameters
            settings.GTAOMinimumRadiusInPixels = volume.minimumRadiusInPixels;
            settings.GTAOMaximumRadiusInPixels = volume.maximumRadiusInPixels;
            settings.Source = ScreenSpaceAmbientOcclusionSettings.DepthSource.DepthNormals;
            settings.BlurQuality = ScreenSpaceAmbientOcclusionSettings.BlurQualityOptions.High;
            settings.NormalSamples = ScreenSpaceAmbientOcclusionSettings.NormalQuality.High;

            settings.GTAODirectionCount = volume.directionCount;
            settings.GTAOStepCount = volume.stepCount;
            settings.GTAOSpatialFilter = volume.spatialFilter;
            settings.GTAOTemporalFilterEnabled = volume.temporalFilter;
            settings.GTAOGhostingMitigation = volume.ghostingMitigation;
            settings.GTAOHistoryLength = volume.historyLength;
        }

        // ---- GTAO-specific helpers ----

        internal static void CalculateGTAOViewParams(ScreenSpaceAmbientOcclusionSettings settings, UniversalCameraData cameraData,
            bool orthographicCamera, float radius, in TextureDesc cameraColorDesc, out Vector4 ssaoParams2, out Vector4 depthToViewParams)
        {
            if (settings.Mode == ScreenSpaceAmbientOcclusionMode.SSAO)
            {
                ssaoParams2 = Vector4.zero;
                depthToViewParams = Vector4.zero;
                return;
            }

            float invHalfTanFOV = cameraData.camera.projectionMatrix.m11;
            float downsampleMultiplier = 1.0f / (settings.Downsample ? 2 : 1);
            Vector2 runningRes = new Vector2(cameraColorDesc.width, cameraColorDesc.height) * downsampleMultiplier;
            float aspectRatio = runningRes.y / runningRes.x;
            float fovCorrection = runningRes.y * invHalfTanFOV * 0.5f;

            float minimumRadius = settings.GTAOMinimumRadiusInPixels * downsampleMultiplier;
            float maximumRadius = Mathf.Max(minimumRadius, settings.GTAOMaximumRadiusInPixels * downsampleMultiplier);
            ssaoParams2 = new Vector4(minimumRadius, maximumRadius, fovCorrection, 0.0f);
            depthToViewParams = new Vector4(
                2.0f / (invHalfTanFOV * aspectRatio * runningRes.x),
                2.0f / (invHalfTanFOV * runningRes.y),
                1.0f / (invHalfTanFOV * aspectRatio),
                1.0f / invHalfTanFOV
            );
        }

        private static void CalculateTemporalParams(ScreenSpaceAmbientOcclusionSettings settings, out float temporalRotation, out int temporalOffset)
        {
            temporalRotation = 0.0f;
            temporalOffset = 0;

            if (!settings.IsTemporalFilterActive)
                return;

            uint temporalRotationCount = (uint)s_TemporalRotations.Length;
            temporalRotation = s_TemporalRotations[(uint)Time.frameCount % temporalRotationCount] / k_DegreesPerRotation;
            temporalOffset = (int)(((uint)Time.frameCount / temporalRotationCount) % k_TemporalOffsetCount);
        }

        private static Vector2Int CalculateDispatchSize(Vector2Int textureSize)
        {
            return new Vector2Int(
                Mathf.CeilToInt(textureSize.x / (float)k_ComputeThreadGroupSize),
                Mathf.CeilToInt(textureSize.y / (float)k_ComputeThreadGroupSize)
            );
        }

        private int GetGTAOKernel(int stepCount, int directionCount)
        {
            if (stepCount == 2 && directionCount == 1)
                return m_ComputePathState.gtaoLowKernel;
            if (stepCount == 4 && directionCount == 2)
                return m_ComputePathState.gtaoMediumKernel;
            if (stepCount == 6 && directionCount == 4)
                return m_ComputePathState.gtaoHighKernel;
            return m_ComputePathState.gtaoKernel;
        }

        private static GTAOComputeParams CreateGTAOComputeParams(ScreenSpaceAmbientOcclusionSettings settings, UniversalCameraData cameraData, in TextureDesc cameraColorDesc, Texture2D blueNoiseTexture)
        {
            GTAOComputeParams computeParams = new GTAOComputeParams();
            float radius = SSAOUtils.CalculateRadius(settings);

            computeParams.orthographicCamera = cameraData.camera.orthographic;
            computeParams.ssaoParams = SSAOUtils.CalculateCommonParams(settings, radius);
            CalculateGTAOViewParams(settings, cameraData, computeParams.orthographicCamera, radius, cameraColorDesc, out computeParams.ssaoParams2, out computeParams.depthToViewParams);

            computeParams.sourceSize = PostProcessUtils.CalcShaderSourceSize(cameraColorDesc.width, cameraColorDesc.height, cameraColorDesc.useDynamicScale);
            computeParams.projectionParams2 = SSAOUtils.CalculateProjectionParams2(cameraData);

            if (settings.AOMethod == ScreenSpaceAmbientOcclusionSettings.AOMethodOptions.BlueNoise && blueNoiseTexture != null)
                computeParams.blueNoiseParams = SSAOUtils.CalculateBlueNoiseParams(cameraData, blueNoiseTexture);

            computeParams.historyLength = settings.GTAOHistoryLength;
            computeParams.ghostingMitigation = settings.GTAOGhostingMitigation;
            CalculateTemporalParams(settings, out computeParams.temporalRotation, out computeParams.temporalOffset);
            return computeParams;
        }

        private static TemporalHistoryState GetTemporalHistoryState(RenderGraph renderGraph, ScreenSpaceAmbientOcclusionSettings settings, UniversalCameraData cameraData)
        {
            var state = new TemporalHistoryState
            {
                historyReadTexture = TextureHandle.nullHandle,
                historyWriteTexture = TextureHandle.nullHandle,
            };

            if (!settings.IsTemporalFilterActive || cameraData.historyManager == null)
                return state;

            cameraData.historyManager.RequestAccess<SSAOHistory>();
            state.ssaoHistory = cameraData.historyManager.GetHistoryForWrite<SSAOHistory>();
            if (state.ssaoHistory == null)
                return state;

            bool xrMultipassEnabled = false;
#if ENABLE_VR && ENABLE_XR_MODULE
            xrMultipassEnabled = cameraData.xr.enabled && !cameraData.xr.singlePassEnabled;
#endif
            bool wasReallocated = state.ssaoHistory.Update(cameraData, settings.Downsample, xrMultipassEnabled);

            int multipassId = 0;
#if ENABLE_VR && ENABLE_XR_MODULE
            multipassId = cameraData.xr.multipassId;
#endif
            int frameIndex = cameraData.temporalFrameIndex;
            state.frameIndex = frameIndex;
            int accumulationVersion = state.ssaoHistory.GetAccumulationVersion(multipassId);
            bool isPreview = cameraData.camera.cameraType == CameraType.Preview;
            bool isRenderRequest = cameraData.camera.isProcessingRenderRequest;
            state.isNewFrame = !isPreview && !isRenderRequest && accumulationVersion != frameIndex;
            RTHandle historyWrite = state.ssaoHistory.GetCurrentTexture(multipassId);
            RTHandle historyRead = state.ssaoHistory.GetPreviousTexture(multipassId);
            Debug.Assert(historyWrite != null && historyRead != null, "SSAO temporal history textures should be allocated here.");

            state.historyWriteTexture = renderGraph.ImportTexture(historyWrite);
            state.historyReadTexture = renderGraph.ImportTexture(historyRead);
            // History is valid only when it was produced by the immediately preceding
            // frame. This rejects stale data after temporal AO was disabled or skipped.
            state.historyReady = !wasReallocated && accumulationVersion == frameIndex - 1 && !cameraData.resetHistory;
            state.projectionChanged = state.ssaoHistory.UpdateProjectionState(cameraData.camera);

            return state;
        }

        private TextureHandle ImportBlueNoiseTexture(RenderGraph renderGraph, Texture2D blueNoiseTexture)
        {
            if (blueNoiseTexture == null)
                return TextureHandle.nullHandle;

            if (m_ComputePathState.blueNoiseRTHandle == null || m_ComputePathState.blueNoiseRTHandle.externalTexture != blueNoiseTexture)
            {
                m_ComputePathState.blueNoiseRTHandle?.Release();
                m_ComputePathState.blueNoiseRTHandle = RTHandles.Alloc(blueNoiseTexture);
            }

            return renderGraph.ImportTexture(m_ComputePathState.blueNoiseRTHandle);
        }

        // ---- RecordRenderGraph ----

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();
            UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();
            DepthPyramidFrameData depthPyramidData = frameData.GetOrCreate<DepthPyramidFrameData>();

            // Builds a TextureDesc compatible with the active camera color target, safe for both intermediate render textures and imported back-buffer handles.
            TextureDesc cameraColorDesc = SSAOUtils.GetCameraColorDescriptor(renderGraph, resourceData.activeColorTexture, cameraData.camera.allowDynamicResolution);

            SSAOUtils.CreateRenderTextureHandles(renderGraph, resourceData, cameraColorDesc,
                m_CurrentSettings, m_SupportsR8RenderTextureFormat, m_BlurType, enableRandomWrite: true,
                out TextureHandle aoTexture, out TextureHandle blurTexture, out TextureHandle temporalTexture, out TextureHandle finalTexture);

            var temporalHistory = GetTemporalHistoryState(renderGraph, m_CurrentSettings, cameraData);

            SSAOUtils.SetupCameraViewMatrices(cameraData, ref m_CameraViewData);
            m_BlueNoiseTextureIndex = SSAOUtils.AdvanceBlueNoiseIndex(m_CurrentSettings, m_BlueNoiseTextures, m_BlueNoiseTextureIndex);

            RecordGTAOComputePass(renderGraph, aoTexture, blurTexture, temporalTexture, finalTexture, temporalHistory, cameraData, resourceData, depthPyramidData, cameraColorDesc);
        }

        // ---- Compute Pass Recording ----

        private void RecordGTAOComputePass(RenderGraph renderGraph, TextureHandle aoTexture, TextureHandle blurTexture, TextureHandle temporalTexture, TextureHandle finalTexture,
            in TemporalHistoryState temporalHistory, UniversalCameraData cameraData, UniversalResourceData resourceData, DepthPyramidFrameData depthPyramidData, in TextureDesc cameraColorDesc)
        {
            TextureHandle cameraDepthTexture = resourceData.cameraDepthTexture;
            TextureHandle cameraNormalsTexture = resourceData.cameraNormalsTexture;
            TextureHandle motionVectorTexture = resourceData.motionVectorColor;
            bool temporalEnabled = m_CurrentSettings.IsTemporalFilterActive;
            bool isTemporalTextureValid = temporalTexture.IsValid();
            bool isHistoryReadTextureValid = temporalHistory.historyReadTexture.IsValid();
            bool isHistoryWriteTextureValid = temporalHistory.historyWriteTexture.IsValid();
            bool hasTemporalResources = temporalEnabled && isTemporalTextureValid && isHistoryReadTextureValid && isHistoryWriteTextureValid;
            bool willRunTemporalFilter = hasTemporalResources;

            TextureDesc aoDesc = aoTexture.GetDescriptor(renderGraph);
            Vector2Int aoSize = new Vector2Int(aoDesc.width, aoDesc.height);
            int gtaoKernel = GetGTAOKernel(m_CurrentSettings.GTAOStepCount, m_CurrentSettings.GTAODirectionCount);
            Vector2Int aoDispatchSize = CalculateDispatchSize(aoSize);

            Texture2D blueNoiseTexture = SSAOUtils.GetBlueNoiseTexture(m_BlueNoiseTextures, m_BlueNoiseTextureIndex);
            if (m_CurrentSettings.AOMethod == ScreenSpaceAmbientOcclusionSettings.AOMethodOptions.BlueNoise)
                Debug.Assert(blueNoiseTexture != null, "Blue noise texture is null. Blue noise mode requires a valid blue noise texture.");
            TextureHandle blueNoiseHandle = ImportBlueNoiseTexture(renderGraph, blueNoiseTexture ?? Texture2D.blackTexture);

            var computeParams = CreateGTAOComputeParams(m_CurrentSettings, cameraData, cameraColorDesc, blueNoiseTexture);

            // GTAO
            RecordGTAOMainPass(renderGraph, cameraData, computeParams, blueNoiseHandle, cameraDepthTexture, cameraNormalsTexture,
                depthPyramidData, aoTexture, gtaoKernel, aoDispatchSize, willRunTemporalFilter);

            TextureHandle spatialFilteredTexture = aoTexture;
            Debug.Assert(blurTexture.IsValid(), "GTAO compute path requires an allocated blur texture.");
            bool usesBoxFilter = m_CurrentSettings.GTAOSpatialFilter == ScreenSpaceAmbientOcclusionSpatialFilter.Box;

            // The three-stage a-trous box filter remains an explicit mode. The default
            // bilateral path is fused into temporal resolve below.
            if (usesBoxFilter)
            {
                RecordComputeDispatchPass(renderGraph, "GTAO Box Spatial Filter 1", m_ComputePathState.boxSpatialFilterKernel, aoDispatchSize, aoTexture, blurTexture, ShaderIDs._BlurOutput, boxFilterStep: 4.0f);
                RecordComputeDispatchPass(renderGraph, "GTAO Box Spatial Filter 2", m_ComputePathState.boxSpatialFilterKernel, aoDispatchSize, blurTexture, aoTexture, ShaderIDs._BlurOutput, boxFilterStep: 2.0f);
                RecordComputeDispatchPass(renderGraph, "GTAO Box Spatial Filter 3", m_ComputePathState.boxSpatialFilterKernel, aoDispatchSize, aoTexture, blurTexture, ShaderIDs._BlurOutput, boxFilterStep: 1.0f);
                spatialFilteredTexture = blurTexture;
            }
            else if (willRunTemporalFilter)
            {
                // Match HDRP's stage ordering: temporal accumulation consumes a
                // separately spatially denoised image, never the raw rotating sample.
                RecordComputeDispatchPass(renderGraph, "GTAO Spatial Denoise", m_ComputePathState.spatialDenoiseKernel,
                    aoDispatchSize, aoTexture, blurTexture, ShaderIDs._BlurOutput);
                spatialFilteredTexture = blurTexture;
            }

            if (willRunTemporalFilter)
            {
                // Scene View and editor redraws can render the same frame again after history has been advanced.
                // Keep resolving against history, but stop reprojection motion for those redraws.
                TextureHandle activeMotionVectorTexture = temporalHistory.isNewFrame ? motionVectorTexture : renderGraph.defaultResources.blackTexture;
                bool needsUpsample = m_CurrentSettings.Downsample;
                bool outputFinal = !m_CurrentSettings.AfterOpaque && !needsUpsample;
                RecordTemporalFilterPass(renderGraph, aoDispatchSize, spatialFilteredTexture,
                    temporalHistory.historyReadTexture, temporalHistory.historyWriteTexture, activeMotionVectorTexture,
                    temporalTexture, true, temporalHistory.historyReady, temporalHistory.projectionChanged, outputFinal,
                    m_CurrentSettings.DirectLightingStrength);

                if (outputFinal)
                    resourceData.ssaoTexture = temporalTexture;
                else if (needsUpsample)
                {
                    TextureHandle upsampledTexture = finalTexture;
                    if (m_CurrentSettings.AfterOpaque)
                    {
                        TextureDesc upsampledDesc = temporalTexture.GetDescriptor(renderGraph);
                        upsampledDesc.name = "_SSAO_TemporalUpsampled";
                        upsampledDesc.width = cameraColorDesc.width;
                        upsampledDesc.height = cameraColorDesc.height;
                        upsampledTexture = renderGraph.CreateTexture(upsampledDesc);
                    }

                    RecordBilateralUpsamplePass(renderGraph, temporalTexture, cameraDepthTexture, upsampledTexture,
                        m_CurrentSettings.DirectLightingStrength, aoDispatchSize, !m_CurrentSettings.AfterOpaque);

                    if (m_CurrentSettings.AfterOpaque)
                        RecordAfterOpaqueBlitPass(renderGraph, computeParams, upsampledTexture, resourceData.activeColorTexture, (int)SSAOUtils.ShaderPasses.BoxAfterOpaque);
                    else
                        resourceData.ssaoTexture = upsampledTexture;
                }
                else
                    RecordAfterOpaqueBlitPass(renderGraph, computeParams, temporalTexture, resourceData.activeColorTexture, (int)SSAOUtils.ShaderPasses.BoxAfterOpaque);
            }
            else if (!usesBoxFilter)
            {
                if (m_CurrentSettings.AfterOpaque)
                {
                    RecordComputeDispatchPass(renderGraph, "GTAO Spatial Denoise", m_ComputePathState.spatialDenoiseKernel,
                        aoDispatchSize, aoTexture, blurTexture, ShaderIDs._BlurOutput);
                    RecordAfterOpaqueBlitPass(renderGraph, computeParams, blurTexture, resourceData.activeColorTexture,
                        (int)SSAOUtils.ShaderPasses.BoxAfterOpaque);
                }
                else
                {
                    RecordComputeDispatchPass(renderGraph, "GTAO Spatial + Final", m_ComputePathState.spatialDenoiseFinalKernel,
                        aoDispatchSize, aoTexture, blurTexture, ShaderIDs._FinalOutput, outputFinal: true,
                        directLightingStrength: m_CurrentSettings.DirectLightingStrength);
                    resourceData.ssaoTexture = blurTexture;
                }
            }
            else if (m_CurrentSettings.AfterOpaque)
            {
                RecordAfterOpaqueBlitPass(renderGraph, computeParams, spatialFilteredTexture, resourceData.activeColorTexture,
                    (int)SSAOUtils.ShaderPasses.BoxAfterOpaque);
            }
            else if (finalTexture.IsValid())
            {
                TextureDesc finalDesc = finalTexture.GetDescriptor(renderGraph);
                Vector2Int finalSize = new Vector2Int(finalDesc.width, finalDesc.height);
                Vector2Int finalDispatchSize = CalculateDispatchSize(finalSize);
                RecordFinalBlitPass(renderGraph, spatialFilteredTexture, finalTexture, m_CurrentSettings.DirectLightingStrength, finalDispatchSize);
            }

            if (willRunTemporalFilter && temporalHistory.isNewFrame && temporalHistory.ssaoHistory != null)
            {
                int multipassId = 0;
#if ENABLE_VR && ENABLE_XR_MODULE
                multipassId = cameraData.xr.multipassId;
#endif
                temporalHistory.ssaoHistory.SetAccumulationVersion(multipassId, temporalHistory.frameIndex);
            }
        }

        private void RecordGTAOMainPass(RenderGraph renderGraph, UniversalCameraData cameraData, GTAOComputeParams computeParams,
            TextureHandle blueNoiseTexture, TextureHandle depthTexture, TextureHandle normalsTexture, DepthPyramidFrameData depthPyramidData,
            TextureHandle aoTexture, int gtaoKernel, Vector2Int dispatchSize, bool willRunTemporalFilter)
        {
            using (var builder = renderGraph.AddComputePass<GTAOComputePassData>("GTAO Compute", out var passData, m_ProfilingSampler))
            {
                passData.cs = m_ComputePathState.shader;
                passData.kernel = gtaoKernel;
                passData.dispatchSize = dispatchSize;
                passData.computeParams = computeParams;
                passData.useBlueNoise = m_CurrentSettings.AOMethod == ScreenSpaceAmbientOcclusionSettings.AOMethodOptions.BlueNoise;
                passData.localKeywords = m_ComputePathState.keywords;
                passData.willRunTemporalFilter = willRunTemporalFilter;
                passData.directionCount = m_CurrentSettings.GTAODirectionCount;
                passData.stepCount = m_CurrentSettings.GTAOStepCount;

#if ENABLE_VR && ENABLE_XR_MODULE
                int eyeCount = cameraData.xr.enabled && cameraData.xr.singlePassEnabled ? 2 : 1;
#else
                int eyeCount = 1;
#endif
                Array.Copy(m_CameraViewData.viewProjections, passData.cameraViewProjections, eyeCount);
                Array.Copy(m_CameraViewData.topLeftCorner, passData.cameraTopLeftCorner, eyeCount);
                Array.Copy(m_CameraViewData.xExtent, passData.cameraXExtent, eyeCount);
                Array.Copy(m_CameraViewData.yExtent, passData.cameraYExtent, eyeCount);
                Array.Copy(m_CameraViewData.zExtent, passData.cameraZExtent, eyeCount);

                passData.aoTexture = aoTexture;
                builder.UseTexture(aoTexture, AccessFlags.Write);

                Debug.Assert(depthTexture.IsValid(), "Camera depth texture is invalid. GTAO compute requires a depth texture.");
                passData.depthTexture = depthTexture;
                builder.UseTexture(depthTexture, AccessFlags.Read);

                Debug.Assert(normalsTexture.IsValid(), "Camera normals texture is invalid. GTAO compute requires a normals texture.");
                passData.normalsTexture = normalsTexture;
                builder.UseTexture(normalsTexture, AccessFlags.Read);

                passData.blueNoiseTexture = blueNoiseTexture;
                builder.UseTexture(blueNoiseTexture, AccessFlags.Read);

                passData.useDepthPyramid = depthPyramidData.isValid && depthPyramidData.MipCount >= k_RequiredDepthPyramidMipCount &&
                    depthPyramidData.MipLevelOffsetsBuffer != null;
                if (passData.useDepthPyramid)
                {
                    passData.depthPyramidTexture = depthPyramidData.DepthPyramid;
                    passData.depthPyramidOffsets = renderGraph.ImportBuffer(depthPyramidData.MipLevelOffsetsBuffer);
                    passData.depthPyramidMipCount = depthPyramidData.MipCount;
                    passData.depthPyramidMip0Size = depthPyramidData.Mip0Size;
                    builder.UseTexture(passData.depthPyramidTexture, AccessFlags.Read);
                    builder.UseBuffer(passData.depthPyramidOffsets, AccessFlags.Read);
                }

                builder.AllowGlobalStateModification(true);
                builder.SetRenderFunc(static (GTAOComputePassData data, ComputeGraphContext ctx) =>
                {
                    var cmd = ctx.cmd;
                    var cs = data.cs;
                    ref var computeParams = ref data.computeParams;

                    cmd.SetKeyword(cs, data.localKeywords.blueNoise, data.useBlueNoise);
                    cmd.SetKeyword(cs, data.localKeywords.interleavedGradient, !data.useBlueNoise);
                    cmd.SetKeyword(cs, data.localKeywords.temporalFiltering, data.willRunTemporalFilter);
                    cmd.SetKeyword(cs, data.localKeywords.orthographic, computeParams.orthographicCamera);
                    cmd.SetKeyword(cs, data.localKeywords.depthPyramid, data.useDepthPyramid);

                    cmd.SetComputeVectorParam(cs, SSAOUtils.ShaderConstants._SSAOParams, computeParams.ssaoParams);
                    cmd.SetComputeVectorParam(cs, SSAOUtils.ShaderConstants._SSAOParams2, computeParams.ssaoParams2);
                    cmd.SetComputeVectorParam(cs, SSAOUtils.ShaderConstants._AODepthToViewParams, computeParams.depthToViewParams);
                    cmd.SetComputeVectorParam(cs, SSAOUtils.ShaderConstants._SourceSize, computeParams.sourceSize);
                    cmd.SetComputeVectorParam(cs, SSAOUtils.ShaderConstants._ProjectionParams2, computeParams.projectionParams2);
                    cmd.SetComputeMatrixArrayParam(cs, SSAOUtils.ShaderConstants._CameraViewProjections, data.cameraViewProjections);
                    cmd.SetComputeVectorArrayParam(cs, SSAOUtils.ShaderConstants._CameraViewTopLeftCorner, data.cameraTopLeftCorner);
                    cmd.SetComputeVectorArrayParam(cs, SSAOUtils.ShaderConstants._CameraViewXExtent, data.cameraXExtent);
                    cmd.SetComputeVectorArrayParam(cs, SSAOUtils.ShaderConstants._CameraViewYExtent, data.cameraYExtent);
                    cmd.SetComputeVectorArrayParam(cs, SSAOUtils.ShaderConstants._CameraViewZExtent, data.cameraZExtent);
                    cmd.SetComputeIntParam(cs, ShaderIDs._GTAODirectionCount, data.directionCount);
                    cmd.SetComputeIntParam(cs, ShaderIDs._GTAOStepCount, data.stepCount);

                    cmd.SetComputeVectorParam(cs, SSAOUtils.ShaderConstants._SSAOBlueNoiseParams, computeParams.blueNoiseParams);
                    cmd.SetComputeTextureParam(cs, data.kernel, SSAOUtils.ShaderConstants._BlueNoiseTexture, data.blueNoiseTexture);

                    if (data.willRunTemporalFilter)
                    {
                        cmd.SetComputeFloatParam(cs, ShaderIDs._SSAOHistoryLength, computeParams.historyLength);
                        cmd.SetComputeFloatParam(cs, ShaderIDs._SSAOGhostingMitigation, computeParams.ghostingMitigation);
                        cmd.SetComputeFloatParam(cs, ShaderIDs._SSAOTemporalRotation, computeParams.temporalRotation);
                        cmd.SetComputeIntParam(cs, ShaderIDs._SSAOTemporalOffset, computeParams.temporalOffset);
                    }

                    cmd.SetComputeTextureParam(cs, data.kernel, ShaderIDs._CameraDepthTexture, data.depthTexture);
                    if (data.useDepthPyramid)
                    {
                        cmd.SetComputeTextureParam(cs, data.kernel, DepthPyramidShaderIDs.DepthPyramid, data.depthPyramidTexture);
                        cmd.SetComputeBufferParam(cs, data.kernel, DepthPyramidShaderIDs.DepthPyramidMipLevelOffsets, data.depthPyramidOffsets);
                        cmd.SetComputeIntParam(cs, DepthPyramidShaderIDs.DepthPyramidMipCount, data.depthPyramidMipCount);
                        cmd.SetComputeVectorParam(cs, DepthPyramidShaderIDs.DepthPyramidMip0Size, data.depthPyramidMip0Size);
                    }
                    cmd.SetComputeTextureParam(cs, data.kernel, SSAOUtils.ShaderConstants._CameraNormalsTexture, data.normalsTexture);
                    cmd.SetComputeTextureParam(cs, data.kernel, ShaderIDs._AOOutput, data.aoTexture);
                    cmd.DispatchCompute(cs, data.kernel, data.dispatchSize.x, data.dispatchSize.y, 1);
                });
            }
        }

        private void RecordComputeDispatchPass(RenderGraph renderGraph, string passName, int kernel, Vector2Int dispatchSize,
            TextureHandle srcTexture, TextureHandle dstTexture, int dstPropertyId, float boxFilterStep = 0.0f,
            bool outputFinal = false, float directLightingStrength = 0.0f)
        {
            using (var builder = renderGraph.AddComputePass<GTAOSingleDispatchPassData>(passName, out var passData, m_ProfilingSampler))
            {
                passData.cs = m_ComputePathState.shader;
                passData.kernel = kernel;
                passData.dispatchSize = dispatchSize;
                passData.srcTexture = srcTexture;
                passData.dstTexture = dstTexture;
                passData.dstPropertyId = dstPropertyId;
                passData.boxFilterStep = boxFilterStep;
                passData.outputFinal = outputFinal;
                passData.directLightingStrength = directLightingStrength;

                builder.UseTexture(srcTexture, AccessFlags.Read);
                builder.UseTexture(dstTexture, AccessFlags.Write);
                if (outputFinal)
                {
                    builder.SetGlobalTextureAfterPass(dstTexture, SSAOUtils.ShaderConstants._SSAOFinalTexture);
                    builder.AllowGlobalStateModification(true);
                }

                builder.SetRenderFunc(static (GTAOSingleDispatchPassData data, ComputeGraphContext ctx) =>
                {
                    ctx.cmd.SetComputeTextureParam(data.cs, data.kernel, SSAOUtils.ShaderConstants._BlitTexture, data.srcTexture);
                    ctx.cmd.SetComputeFloatParam(data.cs, ShaderIDs._GTAOBoxFilterStep, data.boxFilterStep);
                    ctx.cmd.SetComputeTextureParam(data.cs, data.kernel, data.dstPropertyId, data.dstTexture);
                    ctx.cmd.DispatchCompute(data.cs, data.kernel, data.dispatchSize.x, data.dispatchSize.y, 1);
                    if (data.outputFinal)
                    {
                        ctx.cmd.SetKeyword(ShaderGlobalKeywords.ScreenSpaceOcclusion, true);
                        ctx.cmd.SetGlobalVector(SSAOUtils.ShaderConstants._AmbientOcclusionParam,
                            new Vector4(1f, 0f, 0f, data.directLightingStrength));
                    }
                });
            }
        }

        private void RecordTemporalFilterPass(RenderGraph renderGraph, Vector2Int dispatchSize, TextureHandle aoTexture,
            TextureHandle historyReadTexture, TextureHandle historyWriteTexture,
            TextureHandle motionVectorTexture, TextureHandle outputTexture, bool inputIsSpatiallyFiltered,
            bool historyValid, bool projectionChanged, bool outputFinal, float directLightingStrength)
        {
            using (var builder = renderGraph.AddComputePass<GTAOTemporalPassData>("GTAO Temporal", out var passData, m_ProfilingSampler))
            {
                passData.cs = m_ComputePathState.shader;
                passData.kernel = outputFinal
                    ? (inputIsSpatiallyFiltered ? m_ComputePathState.temporalFinalPrefilteredKernel : m_ComputePathState.temporalFinalKernel)
                    : (inputIsSpatiallyFiltered ? m_ComputePathState.temporalPrefilteredKernel : m_ComputePathState.temporalKernel);
                passData.dispatchSize = dispatchSize;
                passData.aoTexture = aoTexture;
                passData.historyReadTexture = historyReadTexture;
                passData.historyWriteTexture = historyWriteTexture;
                passData.temporalTexture = outputTexture;
                passData.historyValid = historyValid;
                passData.projectionChanged = projectionChanged;
                passData.outputFinal = outputFinal;
                passData.directLightingStrength = directLightingStrength;

                builder.UseTexture(aoTexture, AccessFlags.Read);
                builder.UseTexture(historyReadTexture, AccessFlags.Read);
                builder.UseTexture(historyWriteTexture, AccessFlags.Write);
                builder.UseTexture(outputTexture, AccessFlags.Write);
                if (outputFinal)
                {
                    builder.SetGlobalTextureAfterPass(outputTexture, SSAOUtils.ShaderConstants._SSAOFinalTexture);
                    builder.AllowGlobalStateModification(true);
                }

                Debug.Assert(motionVectorTexture.IsValid(), "Motion vector texture is invalid. GTAO temporal filter requires a motion vector texture.");
                passData.motionVectorTexture = motionVectorTexture;
                builder.UseTexture(motionVectorTexture, AccessFlags.Read);

                builder.SetRenderFunc(static (GTAOTemporalPassData data, ComputeGraphContext ctx) =>
                {
                    var cmd = ctx.cmd;
                    cmd.SetComputeTextureParam(data.cs, data.kernel, SSAOUtils.ShaderConstants._BlitTexture, data.aoTexture);
                    cmd.SetComputeTextureParam(data.cs, data.kernel, ShaderIDs._AOPackedHistory, data.historyReadTexture);
                    cmd.SetComputeTextureParam(data.cs, data.kernel, ShaderIDs._AOOutputHistory, data.historyWriteTexture);
                    cmd.SetComputeTextureParam(data.cs, data.kernel, ShaderIDs._MotionVectorTexture, data.motionVectorTexture);
                    cmd.SetComputeTextureParam(data.cs, data.kernel,
                        data.outputFinal ? ShaderIDs._FinalOutput : ShaderIDs._OcclusionTexture, data.temporalTexture);
                    cmd.SetComputeIntParam(data.cs, ShaderIDs._GTAOTemporalHistoryValid, data.historyValid ? 1 : 0);
                    cmd.SetComputeIntParam(data.cs, ShaderIDs._GTAOProjectionChanged, data.projectionChanged ? 1 : 0);
                    cmd.DispatchCompute(data.cs, data.kernel, data.dispatchSize.x, data.dispatchSize.y, 1);
                    if (data.outputFinal)
                    {
                        cmd.SetKeyword(ShaderGlobalKeywords.ScreenSpaceOcclusion, true);
                        cmd.SetGlobalVector(SSAOUtils.ShaderConstants._AmbientOcclusionParam,
                            new Vector4(1f, 0f, 0f, data.directLightingStrength));
                    }
                });
            }
        }

        private void RecordBilateralUpsamplePass(RenderGraph renderGraph, TextureHandle srcTexture, TextureHandle depthTexture,
            TextureHandle finalTexture, float directLightingStrength, Vector2Int dispatchSize, bool publishAsSSAO)
        {
            using (var builder = renderGraph.AddComputePass<GTAOFinalBlitPassData>("GTAO Bilateral Upsample", out var passData, m_ProfilingSampler))
            {
                passData.cs = m_ComputePathState.shader;
                passData.kernel = m_ComputePathState.bilateralUpsampleKernel;
                passData.dispatchSize = dispatchSize;
                passData.directLightingStrength = directLightingStrength;
                passData.srcTexture = srcTexture;
                passData.depthTexture = depthTexture;
                passData.finalTexture = finalTexture;
                passData.publishAsSSAO = publishAsSSAO;

                builder.UseTexture(srcTexture, AccessFlags.Read);
                builder.UseTexture(depthTexture, AccessFlags.Read);
                builder.UseTexture(finalTexture, AccessFlags.Write);
                if (publishAsSSAO)
                {
                    builder.SetGlobalTextureAfterPass(finalTexture, SSAOUtils.ShaderConstants._SSAOFinalTexture);
                    builder.AllowGlobalStateModification(true);
                }

                builder.SetRenderFunc(static (GTAOFinalBlitPassData data, ComputeGraphContext ctx) =>
                {
                    ctx.cmd.SetComputeTextureParam(data.cs, data.kernel, SSAOUtils.ShaderConstants._BlitTexture, data.srcTexture);
                    ctx.cmd.SetComputeTextureParam(data.cs, data.kernel, ShaderIDs._CameraDepthTexture, data.depthTexture);
                    ctx.cmd.SetComputeTextureParam(data.cs, data.kernel, ShaderIDs._FinalOutput, data.finalTexture);
                    ctx.cmd.SetComputeIntParam(data.cs, ShaderIDs._GTAOUpsampleOutputFinal, data.publishAsSSAO ? 1 : 0);
                    ctx.cmd.DispatchCompute(data.cs, data.kernel, data.dispatchSize.x, data.dispatchSize.y, 1);
                    if (data.publishAsSSAO)
                    {
                        ctx.cmd.SetKeyword(ShaderGlobalKeywords.ScreenSpaceOcclusion, true);
                        ctx.cmd.SetGlobalVector(SSAOUtils.ShaderConstants._AmbientOcclusionParam,
                            new Vector4(1f, 0f, 0f, data.directLightingStrength));
                    }
                });
            }
        }

        private void RecordFinalBlitPass(RenderGraph renderGraph, TextureHandle srcTexture, TextureHandle finalTexture, float directLightingStrength, Vector2Int finalDispatchSize)
        {
            using (var builder = renderGraph.AddComputePass<GTAOFinalBlitPassData>("GTAO FinalBlit", out var passData, m_ProfilingSampler))
            {
                passData.cs = m_ComputePathState.shader;
                passData.kernel = m_ComputePathState.finalBlitKernel;
                passData.dispatchSize = finalDispatchSize;
                passData.directLightingStrength = directLightingStrength;
                passData.srcTexture = srcTexture;
                passData.finalTexture = finalTexture;

                builder.UseTexture(srcTexture, AccessFlags.Read);
                builder.UseTexture(finalTexture, AccessFlags.Write);
                builder.SetGlobalTextureAfterPass(finalTexture, SSAOUtils.ShaderConstants._SSAOFinalTexture);
                builder.AllowGlobalStateModification(true);

                builder.SetRenderFunc(static (GTAOFinalBlitPassData data, ComputeGraphContext ctx) =>
                {
                    ctx.cmd.SetComputeTextureParam(data.cs, data.kernel, SSAOUtils.ShaderConstants._BlitTexture, data.srcTexture);
                    ctx.cmd.SetComputeTextureParam(data.cs, data.kernel, ShaderIDs._FinalOutput, data.finalTexture);
                    ctx.cmd.DispatchCompute(data.cs, data.kernel, data.dispatchSize.x, data.dispatchSize.y, 1);
                    ctx.cmd.SetKeyword(ShaderGlobalKeywords.ScreenSpaceOcclusion, true);
                    ctx.cmd.SetGlobalVector(SSAOUtils.ShaderConstants._AmbientOcclusionParam, new Vector4(1f, 0f, 0f, data.directLightingStrength));
                });
            }
        }

        private void RecordAfterOpaqueBlitPass(RenderGraph renderGraph, GTAOComputeParams computeParams, TextureHandle sourceTexture, TextureHandle targetTexture, int shaderPass)
        {
            using (var builder = renderGraph.AddRasterRenderPass<GTAOAfterOpaqueBlitPassData>("GTAO AfterOpaque Blit", out var passData, m_ProfilingSampler))
            {
                passData.material = m_Material;
                passData.sourceSize = computeParams.sourceSize;
                passData.ssaoParams = computeParams.ssaoParams;
                passData.shaderPass = shaderPass;
                passData.sourceTexture = sourceTexture;
                passData.targetTexture = targetTexture;

                builder.UseTexture(passData.sourceTexture, AccessFlags.Read);
                builder.SetRenderAttachment(passData.targetTexture, 0, AccessFlags.Write);

                builder.SetRenderFunc(static (GTAOAfterOpaqueBlitPassData data, RasterGraphContext ctx) =>
                {
                    Vector4 viewScaleBias = SSAOUtils.ComputeScaleBias(data.sourceTexture, SSAOUtils.IsYFlip(ctx, in data.sourceTexture, in data.targetTexture));
                    data.material.SetVector(SSAOUtils.ShaderConstants._SourceSize, data.sourceSize);
                    data.material.SetVector(SSAOUtils.ShaderConstants._SSAOParams, data.ssaoParams);
                    Blitter.BlitTexture(ctx.cmd, data.sourceTexture, viewScaleBias, data.material, data.shaderPass);
                });
            }
        }

        public override void OnCameraCleanup(CommandBuffer cmd)
        {
            if (cmd == null)
                throw new ArgumentNullException("cmd");

            if (!m_CurrentSettings.AfterOpaque)
                cmd.SetKeyword(ShaderGlobalKeywords.ScreenSpaceOcclusion, false);
        }

        public void Dispose()
        {
            m_ComputePathState.Release();
            CoreUtils.Destroy(m_Material);
            m_Material = null;
        }
    }
}
