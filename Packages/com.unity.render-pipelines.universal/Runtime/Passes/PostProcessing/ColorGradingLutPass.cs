using System.Collections.Generic;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering.RenderGraphModule;

namespace UnityEngine.Rendering.Universal.Internal
{
    /// <summary>
    /// Builds the internal desktop color-grading transform into a true 3D LUT.
    /// </summary>
    public partial class ColorGradingLutPass : ScriptableRenderPass
    {
        private const int k_ThreadGroupSize = 4;
        private const int k_TonemapModeCount = 6;

        private const int k_MaxCachedCameras = 16;

        private sealed class LutCache
        {
            internal RTHandle texture;
            internal int width;
            internal GraphicsFormat format;
            internal int settingsHash = int.MinValue;
            internal int lastTouchedFrame;

            internal void Release()
            {
                texture?.Release();
                texture = null;
                settingsHash = int.MinValue;
            }
        }

        private readonly Dictionary<int, LutCache> m_LutCacheByCamera = new();

        private readonly ComputeShader m_LutBuilderLdr;
        private readonly ComputeShader m_LutBuilderHdr;
        private readonly int m_LdrKernel;
        private readonly int[] m_HdrKernels = new int[k_TonemapModeCount];
        private readonly int[] m_HdrOutputKernels = new int[k_TonemapModeCount];

        internal readonly GraphicsFormat m_HdrLutFormat;
        internal readonly GraphicsFormat m_LdrLutFormat;

        internal static bool RequiresColorGradingLut(VolumeStack stack)
        {
            var channelMixer = stack.GetComponent<ChannelMixer>();
            var colorAdjustments = stack.GetComponent<ColorAdjustments>();
            var colorLookup = stack.GetComponent<ColorLookup>();
            var curves = stack.GetComponent<ColorCurves>();
            var liftGammaGain = stack.GetComponent<LiftGammaGain>();
            var shadowsMidtonesHighlights = stack.GetComponent<ShadowsMidtonesHighlights>();
            var splitToning = stack.GetComponent<SplitToning>();
            var tonemapping = stack.GetComponent<Tonemapping>();
            var whiteBalance = stack.GetComponent<WhiteBalance>();

            bool curvesOverride = curves.active &&
                (curves.master.overrideState || curves.red.overrideState || curves.green.overrideState || curves.blue.overrideState ||
                 curves.hueVsHue.overrideState || curves.hueVsSat.overrideState || curves.satVsSat.overrideState || curves.lumVsSat.overrideState);

            bool colorAdjustmentsNeedLut = colorAdjustments.active &&
                (colorAdjustments.contrast.value != 0f ||
                 colorAdjustments.colorFilter.value != Color.white ||
                 colorAdjustments.hueShift.value != 0f ||
                 colorAdjustments.saturation.value != 0f);

            return (channelMixer.active && channelMixer.IsActive()) ||
                   colorAdjustmentsNeedLut ||
                   (colorLookup.active && colorLookup.IsActive()) ||
                   curvesOverride ||
                   (liftGammaGain.active && liftGammaGain.IsActive()) ||
                   (shadowsMidtonesHighlights.active && shadowsMidtonesHighlights.IsActive()) ||
                   (splitToning.active && splitToning.IsActive()) ||
                   (tonemapping.active && tonemapping.IsActive()) ||
                   (whiteBalance.active && whiteBalance.IsActive());
        }

        public ColorGradingLutPass(RenderPassEvent evt, PostProcessData data)
        {
            profilingSampler = new ProfilingSampler("Color Grading LUT 3D");
            renderPassEvent = evt;

            m_LutBuilderLdr = data.shaders.lutBuilderLdrCS;
            m_LutBuilderHdr = data.shaders.lutBuilderHdrCS;

            if (m_LutBuilderLdr == null || m_LutBuilderHdr == null)
                Debug.LogError("Missing compute shader. ColorGradingLutPass cannot build the desktop 3D grading LUT.");
            else
            {
                m_LdrKernel = m_LutBuilderLdr.FindKernel("KNone");
                string[] names = { "None", "Neutral", "ACES", "ACESFilmic", "GTSport", "GT7" };
                for (int i = 0; i < names.Length; ++i)
                {
                    m_HdrKernels[i] = m_LutBuilderHdr.FindKernel("K" + names[i]);
                    m_HdrOutputKernels[i] = m_LutBuilderHdr.FindKernel("K" + names[i] + "HDR");
                }
            }

            // FP16 is the canonical desktop LUT format. Unlike the old 2D strip this resource is tiny
            // (normally 32^3 or 33^3), so preserving grading precision is more valuable than saving a
            // few hundred KiB. Capability selection is retained for legitimate DX11 hardware variance.
            m_HdrLutFormat = SelectLutFormat(GraphicsFormat.R16G16B16A16_SFloat,
                GraphicsFormat.R32G32B32A32_SFloat);
            m_LdrLutFormat = SelectLutFormat(GraphicsFormat.R16G16B16A16_SFloat,
                GraphicsFormat.R8G8B8A8_UNorm, GraphicsFormat.R32G32B32A32_SFloat);
        }

        private static GraphicsFormat SelectLutFormat(params GraphicsFormat[] formats)
        {
            for (int i = 0; i < formats.Length; ++i)
            {
                GraphicsFormat format = formats[i];
                if (SystemInfo.IsFormatSupported(format, GraphicsFormatUsage.LoadStore) &&
                    SystemInfo.IsFormatSupported(format, GraphicsFormatUsage.Sample) &&
                    SystemInfo.IsFormatSupported(format, GraphicsFormatUsage.Linear))
                    return format;
            }

            // Shader Model 5 desktop hardware is expected to support at least RGBA32F UAV + sampling.
            // Returning the explicit format keeps failure visible instead of silently selecting a
            // different color pipeline.
            return GraphicsFormat.R32G32B32A32_SFloat;
        }

        public void ConfigureDescriptor(in UniversalPostProcessingData postProcessingData, out RenderTextureDescriptor descriptor, out FilterMode filterMode)
        {
            bool hdr = postProcessingData.gradingMode == ColorGradingMode.HighDynamicRange;
            int lutSize = postProcessingData.lutSize;
            descriptor = new RenderTextureDescriptor(lutSize, lutSize, hdr ? m_HdrLutFormat : m_LdrLutFormat, 0)
            {
                dimension = TextureDimension.Tex3D,
                volumeDepth = lutSize,
                msaaSamples = 1,
                sRGB = false,
                enableRandomWrite = true,
                useMipMap = false,
                autoGenerateMips = false,
                mipCount = 1
            };
            filterMode = FilterMode.Bilinear;
        }

        private static int TonemapKernelIndex(TonemappingMode mode)
        {
            switch (mode)
            {
                case TonemappingMode.Neutral: return 1;
                case TonemappingMode.ACES: return 2;
                case TonemappingMode.ACESFilmic: return 3;
                case TonemappingMode.GTSport: return 4;
                case TonemappingMode.GT7: return 5;
                default: return 0;
            }
        }

        private sealed class PassData
        {
            internal TextureHandle output;
            internal ComputeShader shader;
            internal int kernel;
            internal int lutSize;

            internal Vector4 colorBalance;
            internal Color colorFilter;
            internal Vector4 channelMixerRed;
            internal Vector4 channelMixerGreen;
            internal Vector4 channelMixerBlue;
            internal Vector4 hueSatCon;
            internal Vector4 lift;
            internal Vector4 gamma;
            internal Vector4 gain;
            internal Vector4 shadows;
            internal Vector4 midtones;
            internal Vector4 highlights;
            internal Vector4 shadowsHighlightsLimits;
            internal Vector4 splitShadows;
            internal Vector4 splitHighlights;

            internal Texture curveMaster;
            internal Texture curveRed;
            internal Texture curveGreen;
            internal Texture curveBlue;
            internal Texture curveHueVsHue;
            internal Texture curveHueVsSat;
            internal Texture curveLumVsSat;
            internal Texture curveSatVsSat;

            internal bool configureHdrOutput;
            internal Vector4 hdrOutputLuminanceParams;
            internal Vector4 hdrOutputGradingParams;
            internal int hdrColorSpace;
            internal int hdrEncoding;
        }

        private static void ExecutePass(UnsafeCommandBuffer cmd, PassData data)
        {
            var shader = data.shader;
            int kernel = data.kernel;

            cmd.SetComputeVectorParam(shader, ShaderConstants._Lut_Params,
                new Vector4(data.lutSize, 1f / Mathf.Max(1f, data.lutSize - 1f), 0f, 0f));
            cmd.SetComputeVectorParam(shader, ShaderConstants._ColorBalance, data.colorBalance);
            cmd.SetComputeVectorParam(shader, ShaderConstants._ColorFilter, data.colorFilter.linear);
            cmd.SetComputeVectorParam(shader, ShaderConstants._ChannelMixerRed, data.channelMixerRed);
            cmd.SetComputeVectorParam(shader, ShaderConstants._ChannelMixerGreen, data.channelMixerGreen);
            cmd.SetComputeVectorParam(shader, ShaderConstants._ChannelMixerBlue, data.channelMixerBlue);
            cmd.SetComputeVectorParam(shader, ShaderConstants._HueSatCon, data.hueSatCon);
            cmd.SetComputeVectorParam(shader, ShaderConstants._Lift, data.lift);
            cmd.SetComputeVectorParam(shader, ShaderConstants._Gamma, data.gamma);
            cmd.SetComputeVectorParam(shader, ShaderConstants._Gain, data.gain);
            cmd.SetComputeVectorParam(shader, ShaderConstants._Shadows, data.shadows);
            cmd.SetComputeVectorParam(shader, ShaderConstants._Midtones, data.midtones);
            cmd.SetComputeVectorParam(shader, ShaderConstants._Highlights, data.highlights);
            cmd.SetComputeVectorParam(shader, ShaderConstants._ShaHiLimits, data.shadowsHighlightsLimits);
            cmd.SetComputeVectorParam(shader, ShaderConstants._SplitShadows, data.splitShadows);
            cmd.SetComputeVectorParam(shader, ShaderConstants._SplitHighlights, data.splitHighlights);

            cmd.SetComputeTextureParam(shader, kernel, ShaderConstants._CurveMaster, new RenderTargetIdentifier(data.curveMaster));
            cmd.SetComputeTextureParam(shader, kernel, ShaderConstants._CurveRed, new RenderTargetIdentifier(data.curveRed));
            cmd.SetComputeTextureParam(shader, kernel, ShaderConstants._CurveGreen, new RenderTargetIdentifier(data.curveGreen));
            cmd.SetComputeTextureParam(shader, kernel, ShaderConstants._CurveBlue, new RenderTargetIdentifier(data.curveBlue));
            cmd.SetComputeTextureParam(shader, kernel, ShaderConstants._CurveHueVsHue, new RenderTargetIdentifier(data.curveHueVsHue));
            cmd.SetComputeTextureParam(shader, kernel, ShaderConstants._CurveHueVsSat, new RenderTargetIdentifier(data.curveHueVsSat));
            cmd.SetComputeTextureParam(shader, kernel, ShaderConstants._CurveLumVsSat, new RenderTargetIdentifier(data.curveLumVsSat));
            cmd.SetComputeTextureParam(shader, kernel, ShaderConstants._CurveSatVsSat, new RenderTargetIdentifier(data.curveSatVsSat));

            if (data.configureHdrOutput)
            {
                cmd.SetComputeVectorParam(shader, ShaderPropertyId.hdrOutputLuminanceParams, data.hdrOutputLuminanceParams);
                cmd.SetComputeVectorParam(shader, ShaderPropertyId.hdrOutputGradingParams, data.hdrOutputGradingParams);
                cmd.SetComputeIntParam(shader, ShaderConstants._HDRColorspace, data.hdrColorSpace);
                cmd.SetComputeIntParam(shader, ShaderConstants._HDREncoding, data.hdrEncoding);
            }

            cmd.SetComputeTextureParam(shader, kernel, ShaderConstants._OutputTexture, data.output);
            int groups = (data.lutSize + k_ThreadGroupSize - 1) / k_ThreadGroupSize;
            cmd.DispatchCompute(shader, kernel, groups, groups, groups);
        }


        private static int CombineHash(int hash, int value)
        {
            unchecked { return (hash * 397) ^ value; }
        }

        private static int CombineHash(int hash, float value) => CombineHash(hash, value.GetHashCode());
        private static int CombineHash(int hash, Vector4 value) => CombineHash(hash, value.GetHashCode());
        private static int CombineHash(int hash, Color value) => CombineHash(hash, value.GetHashCode());
        private static int CombineHash(int hash, Texture value)
        {
            if (!value)
                return CombineHash(hash, 0);

            hash = CombineHash(hash, value.GetInstanceID());
            return CombineHash(hash, unchecked((int)value.updateCount));
        }

        private static int ComputeLutHash(
            int kernel, int lutSize, GraphicsFormat format,
            Vector4 colorBalance, Color colorFilter, Vector4 channelMixerRed, Vector4 channelMixerGreen,
            Vector4 channelMixerBlue, Vector4 hueSatCon, Vector4 lift, Vector4 gamma, Vector4 gain,
            Vector4 shadows, Vector4 midtones, Vector4 highlights, Vector4 shadowsHighlightsLimits,
            Vector4 splitShadows, Vector4 splitHighlights, ColorCurves curves,
            bool hdrOutput, Vector4 hdrOutputLuminanceParams, Vector4 hdrOutputGradingParams, int hdrColorSpace, int hdrEncoding)
        {
            unchecked
            {
                int hash = 17;
                hash = CombineHash(hash, kernel);
                hash = CombineHash(hash, lutSize);
                hash = CombineHash(hash, (int)format);
                hash = CombineHash(hash, colorBalance);
                hash = CombineHash(hash, colorFilter);
                hash = CombineHash(hash, channelMixerRed);
                hash = CombineHash(hash, channelMixerGreen);
                hash = CombineHash(hash, channelMixerBlue);
                hash = CombineHash(hash, hueSatCon);
                hash = CombineHash(hash, lift);
                hash = CombineHash(hash, gamma);
                hash = CombineHash(hash, gain);
                hash = CombineHash(hash, shadows);
                hash = CombineHash(hash, midtones);
                hash = CombineHash(hash, highlights);
                hash = CombineHash(hash, shadowsHighlightsLimits);
                hash = CombineHash(hash, splitShadows);
                hash = CombineHash(hash, splitHighlights);

                // Curve textures keep the same Unity object while their generated pixels change,
                // so hash both parameter state and the texture objects consumed by the compute shader.
                hash = CombineHash(hash, curves.master.GetHashCode());
                hash = CombineHash(hash, curves.red.GetHashCode());
                hash = CombineHash(hash, curves.green.GetHashCode());
                hash = CombineHash(hash, curves.blue.GetHashCode());
                hash = CombineHash(hash, curves.hueVsHue.GetHashCode());
                hash = CombineHash(hash, curves.hueVsSat.GetHashCode());
                hash = CombineHash(hash, curves.lumVsSat.GetHashCode());
                hash = CombineHash(hash, curves.satVsSat.GetHashCode());
                hash = CombineHash(hash, curves.master.value.GetTexture());
                hash = CombineHash(hash, curves.red.value.GetTexture());
                hash = CombineHash(hash, curves.green.value.GetTexture());
                hash = CombineHash(hash, curves.blue.value.GetTexture());
                hash = CombineHash(hash, curves.hueVsHue.value.GetTexture());
                hash = CombineHash(hash, curves.hueVsSat.value.GetTexture());
                hash = CombineHash(hash, curves.lumVsSat.value.GetTexture());
                hash = CombineHash(hash, curves.satVsSat.value.GetTexture());

                hash = CombineHash(hash, hdrOutput ? 1 : 0);
                hash = CombineHash(hash, hdrOutputLuminanceParams);
                hash = CombineHash(hash, hdrOutputGradingParams);
                hash = CombineHash(hash, hdrColorSpace);
                hash = CombineHash(hash, hdrEncoding);
                return hash;
            }
        }

        private LutCache GetOrCreateCache(int cameraId, in RenderTextureDescriptor descriptor, FilterMode filterMode)
        {
            if (!m_LutCacheByCamera.TryGetValue(cameraId, out var cache))
            {
                cache = new LutCache();
                m_LutCacheByCamera.Add(cameraId, cache);
            }

            cache.lastTouchedFrame = Time.frameCount;
            if (cache.texture == null || cache.width != descriptor.width || cache.format != descriptor.graphicsFormat)
            {
                cache.Release();
                cache.texture = RTHandles.Alloc(descriptor, filterMode, TextureWrapMode.Clamp, name: "URP Cached Color Grading LUT 3D");
                cache.width = descriptor.width;
                cache.format = descriptor.graphicsFormat;
            }

            if (m_LutCacheByCamera.Count > k_MaxCachedCameras)
            {
                int oldestId = cameraId;
                int oldestFrame = int.MaxValue;
                foreach (var pair in m_LutCacheByCamera)
                {
                    if (pair.Key == cameraId)
                        continue;
                    if (pair.Value.lastTouchedFrame < oldestFrame)
                    {
                        oldestFrame = pair.Value.lastTouchedFrame;
                        oldestId = pair.Key;
                    }
                }

                if (oldestId != cameraId && m_LutCacheByCamera.TryGetValue(oldestId, out var oldest))
                {
                    oldest.Release();
                    m_LutCacheByCamera.Remove(oldestId);
                }
            }

            return cache;
        }

        internal void Render(RenderGraph renderGraph, ContextContainer frameData, out TextureHandle internalColorLut)
        {
            renderGraph.BeginProfilingSampler(ProfilingSampler.Get(URPProfileId.PostProcessing));

            var cameraData = frameData.Get<UniversalCameraData>();
            var postProcessingData = frameData.Get<UniversalPostProcessingData>();
            var stack = VolumeManager.instance.stack;
            var tonemapping = stack.GetComponent<Tonemapping>();
            var channelMixer = stack.GetComponent<ChannelMixer>();
            var colorAdjustments = stack.GetComponent<ColorAdjustments>();
            var curves = stack.GetComponent<ColorCurves>();
            var liftGammaGain = stack.GetComponent<LiftGammaGain>();
            var shadowsMidtonesHighlights = stack.GetComponent<ShadowsMidtonesHighlights>();
            var splitToning = stack.GetComponent<SplitToning>();
            var whiteBalance = stack.GetComponent<WhiteBalance>();

            bool hdr = postProcessingData.gradingMode == ColorGradingMode.HighDynamicRange;
            bool hdrOutput = hdr && cameraData.isHDROutputActive;
            int modeIndex = TonemapKernelIndex(tonemapping.mode.value);
            ComputeShader shader = hdr ? m_LutBuilderHdr : m_LutBuilderLdr;
            int kernel = hdr ? (hdrOutput ? m_HdrOutputKernels[modeIndex] : m_HdrKernels[modeIndex]) : m_LdrKernel;

            ConfigureDescriptor(in postProcessingData, out var lutDesc, out var filterMode);

            var (shadows, midtones, highlights) = ColorUtils.PrepareShadowsMidtonesHighlights(
                shadowsMidtonesHighlights.shadows.value,
                shadowsMidtonesHighlights.midtones.value,
                shadowsMidtonesHighlights.highlights.value);
            var (lift, gamma, gain) = ColorUtils.PrepareLiftGammaGain(
                liftGammaGain.lift.value,
                liftGammaGain.gamma.value,
                liftGammaGain.gain.value);
            var (splitShadows, splitHighlights) = ColorUtils.PrepareSplitToning(
                splitToning.shadows.value,
                splitToning.highlights.value,
                splitToning.balance.value);

            Vector4 hdrOutputLuminanceParams = Vector4.zero;
            Vector4 hdrOutputGradingParams = Vector4.zero;
            int hdrColorSpace = 0;
            int hdrEncoding = 0;
            if (hdrOutput)
            {
                UniversalRenderPipeline.GetHDROutputLuminanceParameters(cameraData.hdrDisplayInformation,
                    cameraData.hdrDisplayColorGamut, tonemapping, out hdrOutputLuminanceParams);
                UniversalRenderPipeline.GetHDROutputGradingParameters(tonemapping, out hdrOutputGradingParams);
                HDROutputUtils.GetColorSpaceForGamut(cameraData.hdrDisplayColorGamut, out hdrColorSpace);
                HDROutputUtils.GetColorEncodingForGamut(cameraData.hdrDisplayColorGamut, out hdrEncoding);
            }

            Vector4 colorBalance = ColorUtils.ColorBalanceToLMSCoeffs(whiteBalance.temperature.value, whiteBalance.tint.value);
            Color colorFilter = colorAdjustments.colorFilter.value;
            Vector4 channelMixerRed = new Vector4(channelMixer.redOutRedIn.value / 100f, channelMixer.redOutGreenIn.value / 100f, channelMixer.redOutBlueIn.value / 100f, 0f);
            Vector4 channelMixerGreen = new Vector4(channelMixer.greenOutRedIn.value / 100f, channelMixer.greenOutGreenIn.value / 100f, channelMixer.greenOutBlueIn.value / 100f, 0f);
            Vector4 channelMixerBlue = new Vector4(channelMixer.blueOutRedIn.value / 100f, channelMixer.blueOutGreenIn.value / 100f, channelMixer.blueOutBlueIn.value / 100f, 0f);
            Vector4 hueSatCon = new Vector4(colorAdjustments.hueShift.value / 360f, colorAdjustments.saturation.value / 100f + 1f, colorAdjustments.contrast.value / 100f + 1f, 0f);
            Vector4 shadowsHighlightsLimits = new Vector4(
                shadowsMidtonesHighlights.shadowsStart.value,
                shadowsMidtonesHighlights.shadowsEnd.value,
                shadowsMidtonesHighlights.highlightsStart.value,
                shadowsMidtonesHighlights.highlightsEnd.value);

            int settingsHash = ComputeLutHash(kernel, postProcessingData.lutSize, lutDesc.graphicsFormat,
                colorBalance, colorFilter, channelMixerRed, channelMixerGreen, channelMixerBlue, hueSatCon,
                lift, gamma, gain, shadows, midtones, highlights, shadowsHighlightsLimits, splitShadows, splitHighlights,
                curves, hdrOutput, hdrOutputLuminanceParams, hdrOutputGradingParams, hdrColorSpace, hdrEncoding);

            int cameraId = cameraData.camera != null ? cameraData.camera.GetInstanceID() : 0;
            var cache = GetOrCreateCache(cameraId, lutDesc, filterMode);
            internalColorLut = renderGraph.ImportTexture(cache.texture);

            if (cache.settingsHash == settingsHash)
            {
                renderGraph.EndProfilingSampler(ProfilingSampler.Get(URPProfileId.PostProcessing));
                return;
            }

            using (var builder = renderGraph.AddUnsafePass<PassData>("Color Grading LUT 3D", out var passData,
                       ProfilingSampler.Get(URPProfileId.ColorGradingLUT)))
            {
                passData.output = internalColorLut;
                passData.shader = shader;
                passData.kernel = kernel;
                passData.lutSize = postProcessingData.lutSize;

                passData.colorBalance = colorBalance;
                passData.colorFilter = colorFilter;
                passData.channelMixerRed = channelMixerRed;
                passData.channelMixerGreen = channelMixerGreen;
                passData.channelMixerBlue = channelMixerBlue;
                passData.hueSatCon = hueSatCon;
                passData.lift = lift;
                passData.gamma = gamma;
                passData.gain = gain;
                passData.shadows = shadows;
                passData.midtones = midtones;
                passData.highlights = highlights;
                passData.shadowsHighlightsLimits = shadowsHighlightsLimits;
                passData.splitShadows = splitShadows;
                passData.splitHighlights = splitHighlights;

                passData.curveMaster = curves.master.value.GetTexture();
                passData.curveRed = curves.red.value.GetTexture();
                passData.curveGreen = curves.green.value.GetTexture();
                passData.curveBlue = curves.blue.value.GetTexture();
                passData.curveHueVsHue = curves.hueVsHue.value.GetTexture();
                passData.curveHueVsSat = curves.hueVsSat.value.GetTexture();
                passData.curveLumVsSat = curves.lumVsSat.value.GetTexture();
                passData.curveSatVsSat = curves.satVsSat.value.GetTexture();

                passData.configureHdrOutput = hdrOutput;
                passData.hdrOutputLuminanceParams = hdrOutputLuminanceParams;
                passData.hdrOutputGradingParams = hdrOutputGradingParams;
                passData.hdrColorSpace = hdrColorSpace;
                passData.hdrEncoding = hdrEncoding;

                builder.UseTexture(internalColorLut, AccessFlags.Write);
                builder.SetRenderFunc(static (PassData data, UnsafeGraphContext context) => ExecutePass(context.cmd, data));
            }

            cache.settingsHash = settingsHash;
            renderGraph.EndProfilingSampler(ProfilingSampler.Get(URPProfileId.PostProcessing));
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            var resourceData = frameData.Get<UniversalResourceData>();
            Render(renderGraph, frameData, out var colorLut);
            resourceData.internalColorLut = colorLut;
        }

        public void Cleanup()
        {
            foreach (var cache in m_LutCacheByCamera.Values)
                cache.Release();
            m_LutCacheByCamera.Clear();
        }

        private static class ShaderConstants
        {
            public static readonly int _OutputTexture = Shader.PropertyToID("_OutputTexture");
            public static readonly int _Lut_Params = Shader.PropertyToID("_Lut_Params");
            public static readonly int _ColorBalance = Shader.PropertyToID("_ColorBalance");
            public static readonly int _ColorFilter = Shader.PropertyToID("_ColorFilter");
            public static readonly int _ChannelMixerRed = Shader.PropertyToID("_ChannelMixerRed");
            public static readonly int _ChannelMixerGreen = Shader.PropertyToID("_ChannelMixerGreen");
            public static readonly int _ChannelMixerBlue = Shader.PropertyToID("_ChannelMixerBlue");
            public static readonly int _HueSatCon = Shader.PropertyToID("_HueSatCon");
            public static readonly int _Lift = Shader.PropertyToID("_Lift");
            public static readonly int _Gamma = Shader.PropertyToID("_Gamma");
            public static readonly int _Gain = Shader.PropertyToID("_Gain");
            public static readonly int _Shadows = Shader.PropertyToID("_Shadows");
            public static readonly int _Midtones = Shader.PropertyToID("_Midtones");
            public static readonly int _Highlights = Shader.PropertyToID("_Highlights");
            public static readonly int _ShaHiLimits = Shader.PropertyToID("_ShaHiLimits");
            public static readonly int _SplitShadows = Shader.PropertyToID("_SplitShadows");
            public static readonly int _SplitHighlights = Shader.PropertyToID("_SplitHighlights");
            public static readonly int _CurveMaster = Shader.PropertyToID("_CurveMaster");
            public static readonly int _CurveRed = Shader.PropertyToID("_CurveRed");
            public static readonly int _CurveGreen = Shader.PropertyToID("_CurveGreen");
            public static readonly int _CurveBlue = Shader.PropertyToID("_CurveBlue");
            public static readonly int _CurveHueVsHue = Shader.PropertyToID("_CurveHueVsHue");
            public static readonly int _CurveHueVsSat = Shader.PropertyToID("_CurveHueVsSat");
            public static readonly int _CurveLumVsSat = Shader.PropertyToID("_CurveLumVsSat");
            public static readonly int _CurveSatVsSat = Shader.PropertyToID("_CurveSatVsSat");
            public static readonly int _HDRColorspace = Shader.PropertyToID("_HDRColorspace");
            public static readonly int _HDREncoding = Shader.PropertyToID("_HDREncoding");
        }
    }
}
