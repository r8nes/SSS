using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering.RenderGraphModule;

namespace UnityEngine.Rendering.Universal
{
    internal sealed class FinalPostProcessPass : PostProcessPass
    {
        private readonly FilmGrainPostProcessEffect m_FilmGrainEffect;
        private readonly DitheringPostProcessEffect m_DitheringEffect;

        private Tonemapping m_Tonemapping;
        private FilmGrain m_FilmGrain;
        private bool m_HasFinalPass;
        private bool m_EnableColorEncodingIfNeeded;

        public FinalPostProcessPass(PostProcessMaterialLibrary materials, FilmGrainPostProcessEffect filmGrainEffect, DitheringPostProcessEffect ditheringEffect)
            : base(materials)
        {
            m_FilmGrainEffect = filmGrainEffect;
            m_DitheringEffect = ditheringEffect;
        }

        private sealed class PostProcessingFinalSetupPassData : PostProcessFullscreenPassData
        {
            internal UniversalCameraData cameraData;
        }

        public void RenderFinalSetup(RenderGraph renderGraph, UniversalCameraData cameraData, in TextureHandle source, in TextureHandle destination, ref FinalBlitSettings settings)
        {
            var material = Materials.GetKeywordVariant(Materials.scalingSetup,
                PostProcessMaterialKey.ScalingSetup(settings.isFxaaEnabled, settings.isFsrEnabled,
                    settings.hdrOperations, settings.isAlphaOutputEnabled));

            if (settings.hdrOperations.HasFlag(HDROutputUtils.Operation.ColorEncoding))
                HDROutputPostProcessEffect.Setup(cameraData.hdrDisplayInformation, cameraData.hdrDisplayColorGamut, material, settings.hdrOperations, cameraData.rendersOverlayUI, m_Tonemapping);


            using (var builder = renderGraph.AddRasterRenderPass<PostProcessingFinalSetupPassData>("Postprocessing Final Setup Pass", out var passData, ProfilingSampler.Get(URPProfileId.FinalSetup)))
            {
                PostProcessGraphPasses.SetupFullscreenPass(builder, passData, source, destination, material, 0);
                passData.cameraData = cameraData;

                builder.SetRenderFunc(static (PostProcessingFinalSetupPassData data, RasterGraphContext context) =>
                {
                    PostProcessPassUtils.SetMaterialSourceSize(data.material, data.sourceTexture);
                    bool isActiveTargetBackBuffer = false;
                    PostProcessBlitUtils.ScaleViewportAndBlit(in context, in data.sourceTexture, in data.destinationTexture, data.cameraData, data.material, isActiveTargetBackBuffer);
                });
            }
        }

        private sealed class PostProcessingFinalFSRScalePassData : PostProcessFullscreenPassData
        {
            internal Vector2 fsrInputSize;
            internal Vector2 fsrOutputSize;
        }

        public void RenderFinalFSRScale(RenderGraph renderGraph, in TextureHandle source, in TextureDesc srcDesc, in TextureHandle destination, in TextureDesc dstDesc, bool enableAlphaOutput)
        {
            var material = Materials.GetKeywordVariant(Materials.easu, PostProcessMaterialKey.EASU(enableAlphaOutput));

            using (var builder = renderGraph.AddRasterRenderPass<PostProcessingFinalFSRScalePassData>("Postprocessing Final FSR Scale Pass", out var passData, ProfilingSampler.Get(URPProfileId.FinalFSRScale)))
            {
                PostProcessGraphPasses.SetupFullscreenPass(builder, passData, source, destination, material, 0);
                passData.fsrInputSize = new Vector2(srcDesc.width, srcDesc.height);
                passData.fsrOutputSize = new Vector2(dstDesc.width, dstDesc.height);

                builder.SetRenderFunc(static (PostProcessingFinalFSRScalePassData data, RasterGraphContext context) =>
                {
                    FSRUtils.SetEasuConstants(context.cmd, data.fsrInputSize, data.fsrInputSize, data.fsrOutputSize);
                    PostProcessGraphPasses.DrawFullscreen(data, context);
                });
            }
        }

        private sealed class PostProcessingFinalBlitPassData : PostProcessFullscreenPassData
        {
            internal TextureHandle overlayUITexture;
            internal UniversalCameraData cameraData;
            internal FinalBlitSettings settings;
        }

        public struct FinalBlitSettings
        {
            public bool isFxaaEnabled;
            public bool isFsrEnabled;
            public bool isTaaSharpeningEnabled;
            public bool requireHDROutput;
            public bool isAlphaOutputEnabled;
            public bool usePointSampling;
            public HDROutputUtils.Operation hdrOperations;

            public static FinalBlitSettings Create()
            {
                var s = new FinalBlitSettings();
                s.isFxaaEnabled = false;
                s.isFsrEnabled = false;
                s.isTaaSharpeningEnabled = false;
                s.requireHDROutput = false;
                s.isAlphaOutputEnabled = false;
                s.usePointSampling = false;
                s.hdrOperations = HDROutputUtils.Operation.None;
                return s;
            }
        };


        private static PostProcessMaterialFlags HdrOutputFlags(HDROutputUtils.Operation operations)
        {
            if (operations.HasFlag(HDROutputUtils.Operation.ColorEncoding))
                return PostProcessMaterialFlags.HDREncoding;

            if (operations.HasFlag(HDROutputUtils.Operation.ColorConversion))
                return PostProcessMaterialFlags.HDRInput;

            return PostProcessMaterialFlags.None;
        }

        private Material CreateFinalBlitMaterial(UniversalCameraData cameraData, ref FinalBlitSettings settings, in TextureDesc upscaledDesc)
        {
            var rcasKeyword = settings.isFsrEnabled && cameraData.fsrSharpness > 0.0f
                ? (settings.requireHDROutput ? ShaderKeywordStrings.EasuRcasAndHDRInput : ShaderKeywordStrings.Rcas)
                : settings.isTaaSharpeningEnabled ? ShaderKeywordStrings.Rcas : null;

            var flags = HdrOutputFlags(settings.hdrOperations);
            if (settings.isFxaaEnabled)
                flags |= PostProcessMaterialFlags.FXAA;
            if (settings.usePointSampling)
                flags |= PostProcessMaterialFlags.PointSampling;
            if (settings.isAlphaOutputEnabled)
                flags |= PostProcessMaterialFlags.AlphaOutput;
            if (PostProcessPassUtils.RequireSRGBConversionBlitToBackBuffer(cameraData.requireSrgbConversion, m_EnableColorEncodingIfNeeded))
                flags |= PostProcessMaterialFlags.LinearToSRGBConversion;
            if (settings.requireHDROutput && cameraData.rendersOverlayUI)
                flags |= PostProcessMaterialFlags.HDROverlay;
            if (m_FilmGrain.IsActive())
                flags |= PostProcessMaterialFlags.FilmGrain;
            if (cameraData.isDitheringEnabled)
                flags |= PostProcessMaterialFlags.Dithering;
            if (rcasKeyword == ShaderKeywordStrings.EasuRcasAndHDRInput)
                flags |= PostProcessMaterialFlags.EasuRcasAndHDRInput;
            else if (rcasKeyword == ShaderKeywordStrings.Rcas)
                flags |= PostProcessMaterialFlags.RCAS;

            var material = Materials.GetKeywordVariant(Materials.finalPass,
                PostProcessMaterialKey.FinalPost(flags));

            m_FilmGrainEffect.SetupFinalPass(material, Materials.resources, m_FilmGrain, upscaledDesc.width, upscaledDesc.height);
            m_DitheringEffect.SetupFinalPass(material, Materials.resources, cameraData.isDitheringEnabled, upscaledDesc.width, upscaledDesc.height);

            if (settings.requireHDROutput)
            {
                HDROutputPostProcessEffect.Setup(cameraData.hdrDisplayInformation, cameraData.hdrDisplayColorGamut, material, settings.hdrOperations, cameraData.rendersOverlayUI, m_Tonemapping);
                RenderingUtils.SetupOffscreenUIViewportParams(material, ref cameraData.pixelRect, cameraData.resolveFinalTarget);
            }

            return material;
        }

        public void RenderFinalBlit(RenderGraph renderGraph, UniversalCameraData cameraData, in TextureHandle source, in TextureHandle overlayUITexture, in TextureHandle postProcessingTarget, Material material, ref FinalBlitSettings settings)
        {

            using (var builder = renderGraph.AddRasterRenderPass<PostProcessingFinalBlitPassData>("Postprocessing Final Blit Pass", out var passData, ProfilingSampler.Get(URPProfileId.FinalBlit)))
            {
                PostProcessGraphPasses.SetupFullscreenPass(builder, passData, source, postProcessingTarget, material, 0);
                passData.overlayUITexture = overlayUITexture;
                passData.cameraData = cameraData;
                passData.settings = settings;

                if (settings.requireHDROutput && m_EnableColorEncodingIfNeeded && cameraData.rendersOverlayUI)
                    builder.UseTexture(overlayUITexture, AccessFlags.Read);

                builder.SetRenderFunc(static (PostProcessingFinalBlitPassData data, RasterGraphContext context) =>
                {
                    var cmd = context.cmd;
                    var material = data.material;
                    PostProcessPassUtils.SetMaterialSourceSize(material, data.sourceTexture);
                    if (data.overlayUITexture.IsValid())
                        material.SetTexture(PostProcessShaderConstants._OverlayUITexture, data.overlayUITexture);

                    if (data.settings.isFsrEnabled && data.cameraData.fsrSharpness > 0.0f)
                    {
                        float sharpness = data.cameraData.fsrOverrideSharpness ? data.cameraData.fsrSharpness : FSRUtils.kDefaultSharpnessLinear;
                        FSRUtils.SetRcasConstantsLinear(cmd, sharpness);
                    }
                    else if (data.settings.isTaaSharpeningEnabled)
                    {
                        FSRUtils.SetRcasConstantsLinear(cmd, data.cameraData.taaSettings.contrastAdaptiveSharpening);
                    }

                    var scaleBias = RenderingUtils.GetFinalBlitScaleBias(context, data.sourceTexture, data.destinationTexture);
                    cmd.SetViewport(data.cameraData.pixelRect);
                    Blitter.BlitTexture(cmd, (RTHandle)data.sourceTexture, scaleBias, material, 0);
                });
            }
        }

        public void RenderFinalPassRenderGraph(RenderGraph renderGraph, ContextContainer frameData, in TextureHandle source, in TextureHandle overlayUITexture, in TextureHandle postProcessingTarget, bool enableColorEncodingIfNeeded)
        {
            var stack = VolumeManager.instance.stack;
            m_Tonemapping = stack.GetComponent<Tonemapping>();
            m_FilmGrain = stack.GetComponent<FilmGrain>();

            var cameraData = frameData.Get<UniversalCameraData>();
            var settings = FinalBlitSettings.Create();
            var srcDesc = renderGraph.GetTextureDesc(source);
            var upscaledDesc = srcDesc;
            upscaledDesc.width = cameraData.pixelWidth;
            upscaledDesc.height = cameraData.pixelHeight;

            m_HasFinalPass = false;
            m_EnableColorEncodingIfNeeded = enableColorEncodingIfNeeded;

            settings.hdrOperations = HDROutputUtils.Operation.None;
            settings.requireHDROutput = PostProcessPassUtils.RequireHDROutput(cameraData);
            if (settings.requireHDROutput)
            {
                settings.hdrOperations = m_EnableColorEncodingIfNeeded ? HDROutputUtils.Operation.ColorEncoding : HDROutputUtils.Operation.None;
                if (!cameraData.postProcessEnabled)
                    settings.hdrOperations |= HDROutputUtils.Operation.ColorConversion;
            }

            var debugHandler = ScriptableRenderPass.GetActiveDebugHandler(cameraData);
            bool resolveToDebugScreen = debugHandler != null && debugHandler.WriteToDebugScreenTexture(cameraData.resolveFinalTarget);
            debugHandler?.UpdateShaderGlobalPropertiesForFinalValidationPass(renderGraph, cameraData, !m_HasFinalPass && !resolveToDebugScreen);

            settings.isAlphaOutputEnabled = cameraData.isAlphaOutputEnabled;
            settings.isFxaaEnabled = cameraData.effectiveAntialiasing == AntialiasingMode.FastApproximateAntialiasing;
            settings.isFsrEnabled = cameraData.imageScalingMode == ImageScalingMode.Upscaling && cameraData.upscalingFilter == ImageUpscalingFilter.FSR;
            settings.isTaaSharpeningEnabled = (cameraData.IsTemporalAAEnabled() && cameraData.taaSettings.contrastAdaptiveSharpening > 0.0f) && !settings.isFsrEnabled && !cameraData.IsSTPEnabled() && !cameraData.IsFsr3Enabled() &&
#if ENABLE_UPSCALER_FRAMEWORK
                cameraData.upscalingFilter != ImageUpscalingFilter.IUpscaler
#else
                true
#endif
                ;

            var currentSource = source;
            if (cameraData.imageScalingMode != ImageScalingMode.None && !cameraData.IsFsr3Enabled())
            {
                var tempRtDesc = srcDesc;
                if (!settings.requireHDROutput)
                    tempRtDesc.format = UniversalRenderPipeline.MakeUnormRenderTextureGraphicsFormat();

                bool isSetupRequired = settings.isFxaaEnabled || settings.isFsrEnabled;
                if (isSetupRequired)
                {
                    var scalingSetupTarget = CreateCompatibleTexture(renderGraph, tempRtDesc, "scalingSetupTarget", false, FilterMode.Point);
                    RenderFinalSetup(renderGraph, cameraData, in currentSource, in scalingSetupTarget, ref settings);
                    currentSource = scalingSetupTarget;
                    settings.isFxaaEnabled = false;
                }

                switch (cameraData.imageScalingMode)
                {
                    case ImageScalingMode.Upscaling:
                    {
                        switch (cameraData.upscalingFilter)
                        {
                            case ImageUpscalingFilter.Point:
                            {
                                if (!settings.isTaaSharpeningEnabled)
                                    settings.usePointSampling = true;
                                break;
                            }
                            case ImageUpscalingFilter.FSR:
                            {
                                var upScaleTarget = CreateCompatibleTexture(renderGraph, upscaledDesc, "_UpscaledTexture", false, FilterMode.Point);
                                var currentDesc = currentSource.GetDescriptor(renderGraph);
                                RenderFinalFSRScale(renderGraph, in currentSource, in currentDesc, in upScaleTarget, in upscaledDesc, settings.isAlphaOutputEnabled);
                                currentSource = upScaleTarget;
                                break;
                            }
                        }
                        break;
                    }
                    case ImageScalingMode.Downscaling:
                    {
                        settings.isTaaSharpeningEnabled = false;
                        break;
                    }
                }
            }

            var finalMaterial = CreateFinalBlitMaterial(cameraData, ref settings, in upscaledDesc);
            RenderFinalBlit(renderGraph, cameraData, in currentSource, in overlayUITexture, in postProcessingTarget, finalMaterial, ref settings);
        }
    }
}
