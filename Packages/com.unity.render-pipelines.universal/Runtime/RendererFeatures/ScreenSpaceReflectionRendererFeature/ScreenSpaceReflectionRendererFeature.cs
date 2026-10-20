using UnityEngine.Rendering.RenderGraphModule;

namespace UnityEngine.Rendering.Universal
{
    [SupportedOnRenderer(typeof(UniversalRendererData))]
    [DisallowMultipleRendererFeature("Screen Space Reflection")]
    [Tooltip("The Screen Space Reflection produces realtime reflections without the need for reflection probes.")]
    public class ScreenSpaceReflectionRendererFeature : ScriptableRendererFeature, IUniversalRenderGraphExtension
    {
        ScreenSpaceReflectionDepthNormalOnlyTransparentPass m_TransparentDepthNormalPass = null;
        ScreenSpaceReflectionPass m_SSRPass = null;
        ScreenSpaceReflectionPass m_TransparentSSRPass = null;
        ComputeShader m_ComputeShader = null;
        bool m_TransparentReceiverPassActive;

        public override void Create()
        {
            if (m_SSRPass == null)
                m_SSRPass = new ScreenSpaceReflectionPass();
            if (m_TransparentSSRPass == null)
                m_TransparentSSRPass = new ScreenSpaceReflectionPass();
        }

        protected override void Dispose(bool disposing)
        {
            m_SSRPass?.Dispose();
            m_TransparentSSRPass?.Dispose();
            m_TransparentDepthNormalPass = null;
            m_SSRPass = null;
            m_TransparentSSRPass = null;
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            m_TransparentReceiverPassActive = false;
            var universalRenderingData = renderingData.universalRenderingData;
            if (!TryGetActiveSettings(ref renderingData, out var universalCameraData, out var settings))
                return;

            if (!TryPrepareResources(universalRenderingData.transparentLayerMask, settings))
                return;

            bool shouldAdd = m_SSRPass.Setup(renderer, m_ComputeShader, universalRenderingData, universalCameraData);
            bool shouldAddTransparentReceiver = m_TransparentSSRPass.Setup(
                renderer,
                m_ComputeShader,
                universalRenderingData,
                universalCameraData,
                RenderPassEvent.BeforeRenderingTransparents + 1,
                true);
            if (shouldAdd)
            {
                if (settings.ShouldRenderTransparents())
                {
                    m_TransparentDepthNormalPass.UpdateRenderPassEvent(RenderPassEvent.AfterRenderingPrePasses);
                    renderer.EnqueuePass(m_TransparentDepthNormalPass);
                }

                renderer.EnqueuePass(m_SSRPass);
            }

            m_TransparentReceiverPassActive = shouldAddTransparentReceiver;
        }

        public void PrepareRenderGraph(
            RenderGraph renderGraph,
            ScriptableRenderer renderer,
            ContextContainer frameData,
            ref UniversalRenderGraphRequirements requirements)
        {
        }

        public void RecordRenderGraph(
            RenderGraph renderGraph,
            ContextContainer frameData,
            UniversalRenderGraphExtensionStage stage)
        {
            if (stage != UniversalRenderGraphExtensionStage.AfterTransparentPrepass || !m_TransparentReceiverPassActive)
                return;

            m_TransparentSSRPass.RecordRenderGraph(renderGraph, frameData);
        }

        public bool IsActiveForCamera(ref RenderingData renderingData)
        {
            return TryGetActiveSettings(ref renderingData, out _, out _);
        }

        public bool IsActiveForCamera(UniversalCameraData cameraData)
        {
            if (cameraData == null)
                return false;

            if (GraphicsSettings.TryGetRenderPipelineSettings<RenderGraphSettings>(out var renderGraphSettings)
                && renderGraphSettings.enableRenderCompatibilityMode)
                return false;

            if (!SystemInfo.supportsIndirectArgumentsBuffer
                || UniversalRenderer.IsOffscreenDepthTexture(cameraData)
                || cameraData.camera == null
                || cameraData.camera.orthographic
                || cameraData.xr.enabled
                || cameraData.cameraTargetDescriptor.msaaSamples > 1
                || cameraData.cameraTargetDescriptor.dimension != TextureDimension.Tex2D)
                return false;

            var settings = VolumeManager.instance.stack.GetComponent<ScreenSpaceReflectionVolumeSettings>();
            return settings != null && settings.IsActive();
        }

        static bool TryGetActiveSettings(
            ref RenderingData renderingData,
            out UniversalCameraData cameraData,
            out ScreenSpaceReflectionVolumeSettings settings)
        {
            cameraData = renderingData.frameData.Get<UniversalCameraData>();
            settings = null;

            if (GraphicsSettings.TryGetRenderPipelineSettings<RenderGraphSettings>(out var renderGraphSettings)
                && renderGraphSettings.enableRenderCompatibilityMode)
                return false;

            if (!SystemInfo.supportsIndirectArgumentsBuffer
                || UniversalRenderer.IsOffscreenDepthTexture(cameraData)
                || cameraData.camera == null
                || cameraData.camera.orthographic
                || cameraData.xr.enabled
                || cameraData.cameraTargetDescriptor.msaaSamples > 1
                || cameraData.cameraTargetDescriptor.dimension != TextureDimension.Tex2D)
                return false;

            settings = VolumeManager.instance.stack.GetComponent<ScreenSpaceReflectionVolumeSettings>();
            return settings != null && settings.IsActive();
        }

        bool TryPrepareResources(LayerMask transparentLayerMask, ScreenSpaceReflectionVolumeSettings settings)
        {
            if (settings.ShouldRenderTransparents())
            {
                if (m_TransparentDepthNormalPass == null)
                    m_TransparentDepthNormalPass = new(RenderPassEvent.AfterRenderingPrePasses, RenderQueueRange.transparent, transparentLayerMask);
            }

            if (!SystemInfo.supportsComputeShaders)
                return false;

            if (m_ComputeShader == null)
            {
                if (!GraphicsSettings.TryGetRenderPipelineSettings<ScreenSpaceReflectionPersistentResources>(out var ssrPersistentResources))
                {
                    Debug.LogErrorFormat(
                        $"Couldn't find the required resources for the {nameof(ScreenSpaceReflectionRendererFeature)}. If this exception appears in the Player, make sure at least one {nameof(ScreenSpaceReflectionRendererFeature)} is enabled or adjust your stripping settings.");
                    return false;
                }

                m_ComputeShader = ssrPersistentResources.ComputeShader;
            }

            if (m_ComputeShader == null)
            {
                Debug.LogError($"{GetType().Name}.AddRenderPasses(): Missing compute shader resource. {name} render pass will not be added.");
                return false;
            }

            return true;
        }
    }
}
