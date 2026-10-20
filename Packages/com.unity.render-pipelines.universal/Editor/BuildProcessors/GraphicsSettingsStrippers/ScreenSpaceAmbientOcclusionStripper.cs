using UnityEditor.Rendering.Universal;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace UnityEditor.Rendering
{
    class ScreenSpaceAmbientOcclusionBlueNoiseResourcesStripper : IRenderPipelineGraphicsSettingsStripper<ScreenSpaceAmbientOcclusionBlueNoiseResources>
    {
        public bool active => URPBuildData.instance.buildingPlayerForUniversalRenderPipeline;

        static bool RequiresBlueNoiseResources(ScreenSpaceAmbientOcclusion occlusion)
        {
            return true;
        }

        public bool CanRemoveSettings(ScreenSpaceAmbientOcclusionBlueNoiseResources resources)
        {
            if (GraphicsSettings.TryGetRenderPipelineSettings<URPShaderStrippingSetting>(out var urpShaderStrippingSettings) && !urpShaderStrippingSettings.stripUnusedVariants)
                return false;

            foreach (var asset in URPBuildData.instance.renderPipelineAssets)
            {
                foreach (var rendererData in asset.m_RendererDataList)
                {
                    if (rendererData is not UniversalRendererData)
                        continue;

                    foreach (var rendererFeature in rendererData.rendererFeatures)
                    {
                        if (rendererFeature is ScreenSpaceAmbientOcclusion { isActive: true } occlusion
                            && RequiresBlueNoiseResources(occlusion))
                            return false;
                    }
                }
            }

            return true;
        }
    }

    class ScreenSpaceAmbientOcclusionCoreResourcesStripper : IRenderPipelineGraphicsSettingsStripper<ScreenSpaceAmbientOcclusionCoreResources>
    {
        public bool active => URPBuildData.instance.buildingPlayerForUniversalRenderPipeline;

        public bool CanRemoveSettings(ScreenSpaceAmbientOcclusionCoreResources resources)
        {
            if (GraphicsSettings.TryGetRenderPipelineSettings<URPShaderStrippingSetting>(out var urpShaderStrippingSettings) && !urpShaderStrippingSettings.stripUnusedVariants)
                return false;

            foreach (var asset in URPBuildData.instance.renderPipelineAssets)
            {
                foreach (var rendererData in asset.m_RendererDataList)
                {
                    if (rendererData is not UniversalRendererData)
                        continue;

                    foreach (var rendererFeature in rendererData.rendererFeatures)
                    {
                        if (rendererFeature is ScreenSpaceAmbientOcclusion { isActive: true })
                            return false;
                    }
                }
            }

            return true;
        }
    }
}
