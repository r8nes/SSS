using System;
using System.Collections.Generic;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering.RenderGraphModule;

namespace UnityEngine.Rendering.Universal.Internal
{
    internal readonly struct AutoExposureState
    {
        public readonly TextureHandle Texture;
        public readonly bool IsActive;

        public AutoExposureState(TextureHandle texture, bool isActive)
        {
            Texture = texture;
            IsActive = isActive;
        }
    }

    internal sealed class AutoExposurePass : PostProcessPass
    {
        private const int HistogramBins = 128;
        private const int HistogramThreadX = 16;
        private const int HistogramThreadY = 8;
        private const int CurvePrecision = 128;
        private const int MaxCameraResources = 16;

        private static class ShaderIds
        {
            public static readonly int PreviousAutoExposureTexture = Shader.PropertyToID("_PreviousAutoExposureTexture");
            public static readonly int OutputAutoExposureTexture = Shader.PropertyToID("_OutputAutoExposureTexture");
            public static readonly int SourceTexture = Shader.PropertyToID("_SourceTexture");
            public static readonly int HistogramBuffer = Shader.PropertyToID("_HistogramBuffer");
            public static readonly int ExposureCurveTexture = Shader.PropertyToID("_ExposureCurveTexture");
            public static readonly int ExposureWeightMask = Shader.PropertyToID("_ExposureWeightMask");
            public static readonly int HistogramExposureParams = Shader.PropertyToID("_HistogramExposureParams");
            public static readonly int ExposureParams = Shader.PropertyToID("_ExposureParams");
            public static readonly int ExposureCurveParams = Shader.PropertyToID("_ExposureCurveParams");
            public static readonly int AdaptationParams = Shader.PropertyToID("_AdaptationParams");
            public static readonly int ProceduralMaskParams = Shader.PropertyToID("_ProceduralMaskParams");
            public static readonly int ProceduralMaskParams2 = Shader.PropertyToID("_ProceduralMaskParams2");
            public static readonly int Variants = Shader.PropertyToID("_Variants");
        }

        private struct Settings
        {
            public float InitialExposureStops;
            public float DeltaTime;
            public float LowPercentile;
            public float HighPercentile;
            public float Compensation;
            public float LimitMin;
            public float LimitMax;
            public float TargetMidGrayStops;
            public float AdaptationSpeedDarkToLight;
            public float AdaptationSpeedLightToDark;

            public bool UseCurveRemapping;

            public AutoExposureAdaptationMode AdaptationMode;
            public AutoExposureMeteringMode MeteringMode;

            public Texture MeteringMask;

            public Vector2 ProceduralCenter;
            public Vector2 ProceduralRadii;

            public float ProceduralSoftness;
            public float ProceduralMinIntensity;
            public float ProceduralMaxIntensity;

            public AnimationCurve CurveMap;
            public AnimationCurve LimitMinCurveMap;
            public AnimationCurve LimitMaxCurveMap;

            public static Settings From(AutoExposure volume)
            {
                float low = Mathf.Clamp(volume.lowPercentile.value, 0f, 99f);
                float high = Mathf.Clamp(volume.highPercentile.value, low + 0.1f, 100f);

                float limitMin = volume.limitMin.value;
                float limitMax = volume.limitMax.value;

                if (limitMin > limitMax)
                    (limitMin, limitMax) = (limitMax, limitMin);

                var meteringMask = volume.weightTextureMask.value;

                if (meteringMask && meteringMask.dimension != TextureDimension.Tex2D)
                    throw new InvalidOperationException("AutoExposure weightTextureMask must be a 2D texture.");

                var curveMap = volume.curveMap.value;
                var limitMinCurve = volume.limitMinCurveMap.value;
                var limitMaxCurve = volume.limitMaxCurveMap.value;

                if (!volume.histogramUseCurveRemapping.value)
                    return new Settings
                    {
                        InitialExposureStops = volume.initialExposureStops.value,
                        LowPercentile = low,
                        HighPercentile = high,
                        Compensation = volume.compensation.value,
                        LimitMin = limitMin,
                        LimitMax = limitMax,
                        TargetMidGrayStops = AutoExposure.GetTargetMidGrayStops(volume.targetMidGray.value),
                        AdaptationSpeedDarkToLight = Mathf.Max(0.001f, volume.adaptationSpeedDarkToLight.value),
                        AdaptationSpeedLightToDark = Mathf.Max(0.001f, volume.adaptationSpeedLightToDark.value),
                        UseCurveRemapping = volume.histogramUseCurveRemapping.value,
                        AdaptationMode = volume.adaptationMode.value,
                        MeteringMode = volume.meteringMode.value,
                        MeteringMask = meteringMask != null ? meteringMask : Texture2D.whiteTexture,
                        ProceduralCenter = volume.proceduralCenter.value,
                        ProceduralRadii = volume.proceduralRadii.value,
                        ProceduralSoftness = Mathf.Max(0.001f, volume.proceduralSoftness.value),
                        ProceduralMinIntensity = volume.maskMinIntensity.value,
                        ProceduralMaxIntensity = volume.maskMaxIntensity.value,
                        CurveMap = curveMap,
                        LimitMinCurveMap = limitMinCurve,
                        LimitMaxCurveMap = limitMaxCurve
                    };
                if (curveMap == null || curveMap.length == 0)
                    throw new InvalidOperationException(
                        "AutoExposure requires a valid remap curve when Use Curve Remapping is enabled.");
                if (limitMinCurve == null || limitMinCurve.length == 0)
                    throw new InvalidOperationException(
                        "AutoExposure requires a valid minimum remap limit curve when Use Curve Remapping is enabled.");
                if (limitMaxCurve == null || limitMaxCurve.length == 0)
                    throw new InvalidOperationException(
                        "AutoExposure requires a valid maximum remap limit curve when Use Curve Remapping is enabled.");

                return new Settings
                {
                    InitialExposureStops = volume.initialExposureStops.value,
                    LowPercentile = low,
                    HighPercentile = high,
                    Compensation = volume.compensation.value,
                    LimitMin = limitMin,
                    LimitMax = limitMax,
                    TargetMidGrayStops = AutoExposure.GetTargetMidGrayStops(volume.targetMidGray.value),
                    AdaptationSpeedDarkToLight = Mathf.Max(0.001f, volume.adaptationSpeedDarkToLight.value),
                    AdaptationSpeedLightToDark = Mathf.Max(0.001f, volume.adaptationSpeedLightToDark.value),
                    UseCurveRemapping = volume.histogramUseCurveRemapping.value,
                    AdaptationMode = volume.adaptationMode.value,
                    MeteringMode = volume.meteringMode.value,
                    MeteringMask = meteringMask != null ? meteringMask : Texture2D.whiteTexture,
                    ProceduralCenter = volume.proceduralCenter.value,
                    ProceduralRadii = volume.proceduralRadii.value,
                    ProceduralSoftness = Mathf.Max(0.001f, volume.proceduralSoftness.value),
                    ProceduralMinIntensity = volume.maskMinIntensity.value,
                    ProceduralMaxIntensity = volume.maskMaxIntensity.value,
                    CurveMap = curveMap,
                    LimitMinCurveMap = limitMinCurve,
                    LimitMaxCurveMap = limitMaxCurve
                };
            }
        }

        private sealed class Resources
        {
            public GraphicsBuffer HistogramBuffer;
            public RTHandle CurrentExposureTexture;
            public RTHandle NextExposureTexture;
            public Texture2D ExposureCurveTexture;
            public RTHandle ExposureCurveHandle;
            public Texture MeteringMaskTexture;
            public RTHandle MeteringMaskHandle;
            public Color[] ExposureCurvePixels;
            public int ExposureCurveHash = int.MinValue;
            public float ExposureCurveMin;
            public float ExposureCurveMax;
            public readonly int[] Variants = new int[4];
            public bool NeedsReset = true;
            public float ResetExposureStops;
            public int LastWidth;
            public int LastHeight;
            public bool HasResolution;
            public float LastUpdateTime;
            public bool HasLastUpdateTime;
            public int LastTouchedFrame;

            public void EnsureAllocated(float initialExposureStops)
            {
                ResetExposureStops = initialExposureStops;

                if (HistogramBuffer == null || !HistogramBuffer.IsValid())
                {
                    HistogramBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, HistogramBins, sizeof(uint));
                    // The reduction kernel clears the histogram after every use. Only a newly allocated
                    // buffer needs an explicit initialization before its first frame.
                    HistogramBuffer.SetData(new uint[HistogramBins]);
                }

                if (CurrentExposureTexture == null)
                {
                    CurrentExposureTexture = AllocateExposureTexture("URP Auto Exposure Current");
                    NeedsReset = true;
                }

                if (NextExposureTexture == null)
                {
                    NextExposureTexture = AllocateExposureTexture("URP Auto Exposure Next");
                    NeedsReset = true;
                }

                if (!ExposureCurveTexture)
                {
                    ExposureCurveTexture = new Texture2D(CurvePrecision, 1, GraphicsFormat.R16G16B16A16_SFloat,
                        TextureCreationFlags.None)
                    {
                        name = "URP Auto Exposure Curve",
                        filterMode = FilterMode.Bilinear,
                        wrapMode = TextureWrapMode.Clamp,
                        hideFlags = HideFlags.HideAndDontSave
                    };
                }

                ExposureCurveHandle ??= RTHandles.Alloc(ExposureCurveTexture);
                ExposureCurvePixels ??= new Color[CurvePrecision];
            }

            public void SwapExposureTextures()
            {
                (CurrentExposureTexture, NextExposureTexture) = (NextExposureTexture, CurrentExposureTexture);
            }

            public void Reset(float initialExposureStops)
            {
                ResetExposureStops = initialExposureStops;
                NeedsReset = true;
                HasLastUpdateTime = false;
            }

            public void EnsureMeteringMaskHandle(Texture texture)
            {
                if (MeteringMaskHandle != null && MeteringMaskTexture == texture)
                    return;

                MeteringMaskHandle?.Release();
                MeteringMaskHandle = RTHandles.Alloc(texture);
                MeteringMaskTexture = texture;
            }

            private static int ComputeCurveHash(AnimationCurve curve)
            {
                unchecked
                {
                    if (curve == null)
                        return 0;

                    int hash = 17;
                    hash = hash * 397 ^ (int)curve.preWrapMode;
                    hash = hash * 397 ^ (int)curve.postWrapMode;
                    var keys = curve.keys;
                    hash = hash * 397 ^ keys.Length;
                    for (int i = 0; i < keys.Length; ++i)
                    {
                        var key = keys[i];
                        hash = hash * 397 ^ key.time.GetHashCode();
                        hash = hash * 397 ^ key.value.GetHashCode();
                        hash = hash * 397 ^ key.inTangent.GetHashCode();
                        hash = hash * 397 ^ key.outTangent.GetHashCode();
                        hash = hash * 397 ^ key.inWeight.GetHashCode();
                        hash = hash * 397 ^ key.outWeight.GetHashCode();
                        hash = hash * 397 ^ (int)key.weightedMode;
                    }
                    return hash;
                }
            }

            public void UpdateExposureCurveTexture(in Settings settings, out float curveMin, out float curveMax)
            {
                EnsureAllocated(settings.InitialExposureStops);

                var curve = settings.CurveMap;
                var minCurve = settings.LimitMinCurveMap;
                var maxCurve = settings.LimitMaxCurveMap;

                curveMin = 0f;
                curveMax = 0f;

                if (!settings.UseCurveRemapping)
                    return;

                curveMin = curve[0].time;
                curveMax = curve[curve.length - 1].time;

                if (curveMax <= curveMin + 1e-5f)
                    throw new InvalidOperationException("AutoExposure remap curve must span a non-zero X range.");

                unchecked
                {
                    int hash = ComputeCurveHash(curve);
                    hash = hash * 397 ^ ComputeCurveHash(minCurve);
                    hash = hash * 397 ^ ComputeCurveHash(maxCurve);
                    if (hash == ExposureCurveHash && Mathf.Approximately(curveMin, ExposureCurveMin) && Mathf.Approximately(curveMax, ExposureCurveMax))
                        return;

                    ExposureCurveHash = hash;
                    ExposureCurveMin = curveMin;
                    ExposureCurveMax = curveMax;
                }

                float step = (curveMax - curveMin) / (CurvePrecision - 1f);

                for (int i = 0; i < CurvePrecision; i++)
                {
                    float x = curveMin + step * i;
                    ExposureCurvePixels[i] = new Color(
                        curve.Evaluate(x),
                        minCurve.Evaluate(x),
                        maxCurve.Evaluate(x),
                        0f);
                }

                ExposureCurveTexture.SetPixels(ExposureCurvePixels);
                ExposureCurveTexture.Apply(false, false);
            }

            public void Dispose()
            {
                HistogramBuffer?.Release();
                HistogramBuffer = null;
                CurrentExposureTexture?.Release();
                CurrentExposureTexture = null;
                NextExposureTexture?.Release();
                NextExposureTexture = null;
                ExposureCurveHandle?.Release();
                ExposureCurveHandle = null;
                MeteringMaskHandle?.Release();
                MeteringMaskHandle = null;

                if (ExposureCurveTexture)
                {
                    CoreUtils.Destroy(ExposureCurveTexture);
                    ExposureCurveTexture = null;
                }

                ExposureCurvePixels = null;
                ExposureCurveHash = int.MinValue;
                MeteringMaskTexture = null;
            }
        }

        private sealed class ComputePassData
        {
            public ComputeShader ComputeShader;
            public int HistogramKernel;
            public int ReductionKernel;
            public TextureHandle SourceTexture;
            public TextureHandle ExposureCurveTexture;
            public TextureHandle MeteringMaskTexture;
            public BufferHandle HistogramBuffer;
            public TextureHandle PreviousExposureTexture;
            public TextureHandle OutputExposureTexture;
            public Vector2Int SourceSize;
            public Vector4 HistogramExposureParams;
            public Vector4 ExposureParams;
            public Vector4 ExposureCurveParams;
            public Vector4 AdaptationParams;
            public Vector4 ProceduralMaskParams;
            public Vector4 ProceduralMaskParams2;
            public int[] Variants;
            public Resources Resources;
        }

        private readonly ComputeShader m_ComputeShader;
        private readonly Texture2D m_NeutralExposureTextureSource;
        private readonly RTHandle m_NeutralExposureTexture;

        private readonly int[] m_HistogramKernels = new int[5];
        private readonly int m_ReductionKernel;

        private readonly bool m_IsValid;

        private readonly Dictionary<int, Resources> m_ResourcesByCamera = new();


        public AutoExposurePass(PostProcessData data)
            : base()
        {
            m_ComputeShader = data?.shaders.autoExposureCS;
            m_NeutralExposureTextureSource = new Texture2D(1, 1, GraphicsFormat.R16G16B16A16_SFloat,
                TextureCreationFlags.None)
            {
                name = "URP Neutral Auto Exposure",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };
            m_NeutralExposureTextureSource.SetPixel(0, 0, new Color(1f, 0f, 0f, 0f));
            m_NeutralExposureTextureSource.Apply(false, true);
            m_NeutralExposureTexture = RTHandles.Alloc(m_NeutralExposureTextureSource);

            bool kernelsValid = m_ComputeShader
                                && TryFindKernel(m_ComputeShader, "KHistogramAverage", out m_HistogramKernels[0])
                                && TryFindKernel(m_ComputeShader, "KHistogramSpot", out m_HistogramKernels[1])
                                && TryFindKernel(m_ComputeShader, "KHistogramCenterWeighted", out m_HistogramKernels[2])
                                && TryFindKernel(m_ComputeShader, "KHistogramMaskWeighted", out m_HistogramKernels[3])
                                && TryFindKernel(m_ComputeShader, "KHistogramProcedural", out m_HistogramKernels[4])
                                && TryFindKernel(m_ComputeShader, "KHistogramReduce", out m_ReductionKernel);
            m_IsValid = SystemInfo.supportsComputeShaders && kernelsValid;
        }

        public override void Dispose()
        {
            m_NeutralExposureTexture?.Release();
            CoreUtils.Destroy(m_NeutralExposureTextureSource);

            foreach (var kvp in m_ResourcesByCamera)
                kvp.Value.Dispose();

            m_ResourcesByCamera.Clear();
        }

        public AutoExposureState Render(RenderGraph renderGraph, ContextContainer frameData, in TextureHandle sourceTexture)
        {
            var neutralTexture = renderGraph.ImportTexture(m_NeutralExposureTexture);
            var cameraData = frameData.Get<UniversalCameraData>();

            if (!TryPrepare(cameraData.postProcessEnabled, cameraData.renderType,
                    cameraData.resetHistory, cameraData.camera, cameraData.cameraTargetDescriptor, out var settings, out var resources))
            {
                return new AutoExposureState(neutralTexture, false);
            }

            var sourceDesc = sourceTexture.GetDescriptor(renderGraph);
            var sourceSize = new Vector2Int(Mathf.Max(1, sourceDesc.width), Mathf.Max(1, sourceDesc.height));

            resources.EnsureMeteringMaskHandle(settings.MeteringMask);
            resources.UpdateExposureCurveTexture(settings, out float curveMin, out float curveMax);

            BuildShaderParameters(settings, sourceSize, curveMin, curveMax, resources.NeedsReset,
                resources.ResetExposureStops, resources.Variants,
                out var histogramExposureParams, out var exposureParams, out var exposureCurveParams,
                out var adaptationParams, out var proceduralMaskParams, out var proceduralMaskParams2);

            var importedHistogram = renderGraph.ImportBuffer(resources.HistogramBuffer);
            var currentExposure = renderGraph.ImportTexture(resources.CurrentExposureTexture);
            var nextExposure = renderGraph.ImportTexture(resources.NextExposureTexture);
            var importedCurve = renderGraph.ImportTexture(resources.ExposureCurveHandle);
            var importedMask = renderGraph.ImportTexture(resources.MeteringMaskHandle);

            using (var builder = renderGraph.AddComputePass<ComputePassData>("Auto Exposure", out var passData))
            {
                builder.UseTexture(sourceTexture, AccessFlags.Read);
                builder.UseTexture(currentExposure, AccessFlags.Read);
                builder.UseTexture(nextExposure, AccessFlags.Write);
                builder.UseTexture(importedCurve, AccessFlags.Read);
                builder.UseTexture(importedMask, AccessFlags.Read);

                builder.UseBuffer(importedHistogram, AccessFlags.ReadWrite);

                passData.ComputeShader = m_ComputeShader;
                passData.HistogramKernel = m_HistogramKernels[(int)settings.MeteringMode];
                passData.ReductionKernel = m_ReductionKernel;
                passData.SourceTexture = sourceTexture;
                passData.PreviousExposureTexture = currentExposure;
                passData.OutputExposureTexture = nextExposure;
                passData.ExposureCurveTexture = importedCurve;
                passData.MeteringMaskTexture = importedMask;
                passData.HistogramBuffer = importedHistogram;
                passData.SourceSize = sourceSize;
                passData.HistogramExposureParams = histogramExposureParams;
                passData.ExposureParams = exposureParams;
                passData.ExposureCurveParams = exposureCurveParams;
                passData.AdaptationParams = adaptationParams;
                passData.ProceduralMaskParams = proceduralMaskParams;
                passData.ProceduralMaskParams2 = proceduralMaskParams2;
                passData.Variants = resources.Variants;
                passData.Resources = resources;

                builder.SetRenderFunc(static (ComputePassData data, ComputeGraphContext ctx) =>
                    ExecuteRenderGraphCompute(ctx.cmd, data));
            }

            resources.SwapExposureTextures();
            // The reduction kernel writes nextExposure. Returning currentExposure disconnects every
            // downstream consumer from this pass and adds an unintended extra frame of exposure lag.
            return new AutoExposureState(nextExposure, true);
        }

        private bool TryPrepare(bool postProcessEnabled, CameraRenderType renderType, bool resetHistory, Camera camera,
            RenderTextureDescriptor descriptor, out Settings settings, out Resources resources)
        {
            settings = default;
            resources = null;

            if (!m_IsValid)
                return false;

            if (!postProcessEnabled || renderType == CameraRenderType.Overlay || camera == null)
                return false;

            switch (camera.cameraType)
            {
                case CameraType.Game:
                case CameraType.SceneView:
                    break;
                default:
                    return false;
            }

            var stack = VolumeManager.instance.stack;
            var volume = stack?.GetComponent<AutoExposure>();

            if (!volume || !volume.IsActive())
                return false;

            settings = Settings.From(volume);
            if (resetHistory)
                settings.AdaptationMode = AutoExposureAdaptationMode.Fixed;

            int cameraId = camera.GetInstanceID();

            if (!m_ResourcesByCamera.TryGetValue(cameraId, out resources))
            {
                resources = new Resources();
                m_ResourcesByCamera.Add(cameraId, resources);
            }

            resources.EnsureAllocated(settings.InitialExposureStops);
            resources.LastTouchedFrame = Time.frameCount;

            if (resetHistory || (resources.HasResolution &&
                    (resources.LastWidth != descriptor.width || resources.LastHeight != descriptor.height)))
                resources.Reset(settings.InitialExposureStops);

            resources.LastWidth = descriptor.width;
            resources.LastHeight = descriptor.height;
            resources.HasResolution = true;

            float now = Time.unscaledTime;
            settings.DeltaTime = resources.HasLastUpdateTime ? Mathf.Max(0f, now - resources.LastUpdateTime) : 0f;
            resources.LastUpdateTime = now;
            resources.HasLastUpdateTime = true;

            TrimUnusedResources();
            return true;
        }

        private void TrimUnusedResources()
        {
            if (m_ResourcesByCamera.Count <= MaxCameraResources)
                return;

            var ids = new List<int>(m_ResourcesByCamera.Count);

            foreach (var kvp in m_ResourcesByCamera)
                ids.Add(kvp.Key);

            ids.Sort((a, b) =>
                m_ResourcesByCamera[a].LastTouchedFrame.CompareTo(m_ResourcesByCamera[b].LastTouchedFrame));

            int removeCount = m_ResourcesByCamera.Count - MaxCameraResources;

            for (int i = 0; i < removeCount; i++)
            {
                int key = ids[i];
                m_ResourcesByCamera[key].Dispose();
                m_ResourcesByCamera.Remove(key);
            }
        }

        private static void BuildShaderParameters(in Settings settings, Vector2Int sourceSize, float curveMin,
            float curveMax, bool resetExposure, float resetExposureStops,
            int[] variants, out Vector4 histogramExposureParams, out Vector4 exposureParams,
            out Vector4 exposureCurveParams, out Vector4 adaptationParams, out Vector4 proceduralMaskParams,
            out Vector4 proceduralMaskParams2)
        {
            float histogramMin =
                settings.UseCurveRemapping ? curveMin : settings.LimitMin - settings.TargetMidGrayStops;
            float histogramMax =
                settings.UseCurveRemapping ? curveMax : settings.LimitMax - settings.TargetMidGrayStops;

            if (histogramMax <= histogramMin + 1e-5f)
                histogramMax = histogramMin + 1e-5f;

            float histScale = 1f / (histogramMax - histogramMin);
            float histBias = -histogramMin * histScale;

            histogramExposureParams = new Vector4(histScale, histBias, settings.LowPercentile / 100f,
                settings.HighPercentile / 100f);
            exposureParams = new Vector4(settings.Compensation, settings.LimitMin, settings.LimitMax,
                settings.TargetMidGrayStops);
            exposureCurveParams = new Vector4(curveMin, curveMax, 0f, 0f);
            adaptationParams = new Vector4(settings.AdaptationSpeedLightToDark, settings.AdaptationSpeedDarkToLight,
                settings.DeltaTime, resetExposureStops);

            var proceduralCenter = new Vector2(Mathf.Clamp01(settings.ProceduralCenter.x) * sourceSize.x,
                Mathf.Clamp01(settings.ProceduralCenter.y) * sourceSize.y);
            var proceduralRadii = new Vector2(Mathf.Max(1e-3f, settings.ProceduralRadii.x) * sourceSize.x,
                Mathf.Max(1e-3f, settings.ProceduralRadii.y) * sourceSize.y);

            proceduralMaskParams =
                new Vector4(proceduralCenter.x, proceduralCenter.y, proceduralRadii.x, proceduralRadii.y);
            proceduralMaskParams2 = new Vector4(1f / settings.ProceduralSoftness, settings.ProceduralMinIntensity,
                settings.ProceduralMaxIntensity, 0f);

            variants[0] = resetExposure ? 1 : 0;
            variants[1] = (int)settings.MeteringMode;
            variants[2] = (int)settings.AdaptationMode;
            variants[3] = settings.UseCurveRemapping ? 2 : 1;
        }

        private static void ExecuteRenderGraphCompute(ComputeCommandBuffer cmd, ComputePassData data)
        {
            var shader = data.ComputeShader;
            cmd.SetComputeVectorParam(shader, ShaderIds.HistogramExposureParams, data.HistogramExposureParams);
            cmd.SetComputeVectorParam(shader, ShaderIds.ExposureParams, data.ExposureParams);
            cmd.SetComputeVectorParam(shader, ShaderIds.ExposureCurveParams, data.ExposureCurveParams);
            cmd.SetComputeVectorParam(shader, ShaderIds.AdaptationParams, data.AdaptationParams);
            cmd.SetComputeVectorParam(shader, ShaderIds.ProceduralMaskParams, data.ProceduralMaskParams);
            cmd.SetComputeVectorParam(shader, ShaderIds.ProceduralMaskParams2, data.ProceduralMaskParams2);
            cmd.SetComputeIntParams(shader, ShaderIds.Variants, data.Variants);

            cmd.SetComputeTextureParam(shader, data.HistogramKernel, ShaderIds.SourceTexture, data.SourceTexture);
            cmd.SetComputeTextureParam(shader, data.HistogramKernel, ShaderIds.ExposureWeightMask,
                data.MeteringMaskTexture);
            cmd.SetComputeBufferParam(shader, data.HistogramKernel, ShaderIds.HistogramBuffer, data.HistogramBuffer);

            int dispatchX = DivRoundUp((data.SourceSize.x + 1) / 2, HistogramThreadX);
            int dispatchY = DivRoundUp((data.SourceSize.y + 1) / 2, HistogramThreadY);

            cmd.DispatchCompute(shader, data.HistogramKernel, dispatchX, dispatchY, 1);

            cmd.SetComputeTextureParam(shader, data.ReductionKernel, ShaderIds.ExposureCurveTexture,
                data.ExposureCurveTexture);
            cmd.SetComputeTextureParam(shader, data.ReductionKernel, ShaderIds.PreviousAutoExposureTexture,
                data.PreviousExposureTexture);
            cmd.SetComputeTextureParam(shader, data.ReductionKernel, ShaderIds.OutputAutoExposureTexture,
                data.OutputExposureTexture);
            cmd.SetComputeBufferParam(shader, data.ReductionKernel, ShaderIds.HistogramBuffer, data.HistogramBuffer);
            cmd.DispatchCompute(shader, data.ReductionKernel, 1, 1, 1);
            data.Resources.NeedsReset = false;
        }

        private static RTHandle AllocateExposureTexture(string name)
        {
            return RTHandles.Alloc(1, 1, colorFormat: GraphicsFormat.R16G16B16A16_SFloat,
                enableRandomWrite: true, name: name);
        }

        private static int DivRoundUp(int value, int divisor) => Mathf.Max(1, (value + divisor - 1) / divisor);

        private static bool TryFindKernel(ComputeShader shader, string name, out int kernel)
        {
            kernel = -1;

            try
            {
                kernel = shader.FindKernel(name);
                return kernel >= 0;
            }
            catch
            {
                return false;
            }
        }
    }
}
