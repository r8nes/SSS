using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Akila.FPSFramework
{
    public class SettingsPresetURP : SettingsPreset
    {
        private UniversalRenderPipelineAsset GetUniversalRenderPipelineAsset()
        {
            UniversalRenderPipelineAsset asset = (UniversalRenderPipelineAsset)GraphicsSettings.currentRenderPipeline;

            if (asset != null)
                return asset;

            asset = (UniversalRenderPipelineAsset)QualitySettings.renderPipeline;

            if (asset != null)
                return asset;

            return null;
        }

        public void SetMSAA(int value)
        {
            UniversalRenderPipelineAsset asset = GetUniversalRenderPipelineAsset();

            int count = 0;
            if (value == 0) count = 8;
            if (value == 1) count = 4;
            if (value == 2) count = 2;
            if (value == 3) count = 1;

            asset.msaaSampleCount = count;
        }

        public void SetShadowResolution(int value)
        {
            UniversalRenderPipelineAsset asset = GetUniversalRenderPipelineAsset();
            BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            FieldInfo mainLightShadowResolutionFieldInfo = asset.GetType().GetField("m_MainLightShadowmapResolution", flags);
            FieldInfo additionalLightShadowResolutionFieldInfo = asset.GetType().GetField("m_AdditionalLightsShadowmapResolution", flags);
            FieldInfo mainLightShadowsSupportedFieldInfo = asset.GetType().GetField("m_MainLightShadowsSupported", flags);
            FieldInfo additionalLightShadowsSupportedFieldInfo = asset.GetType().GetField("m_AdditionalLightShadowsSupported", flags);

            int resolution = 0;
            bool shadowSupported = true;

            if (value == 0) resolution = 4096;
            if (value == 1) resolution = 2048;
            if (value == 2) resolution = 1024;
            if (value == 3) resolution = 512;
            if (value == 4) resolution = 256;
            if (value == 5 || value > 5)
            {
                shadowSupported = false;
                resolution = 256;
            }
            mainLightShadowResolutionFieldInfo.SetValue(asset, resolution);
            additionalLightShadowResolutionFieldInfo.SetValue(asset, resolution);

            mainLightShadowsSupportedFieldInfo.SetValue(asset, shadowSupported);
            additionalLightShadowsSupportedFieldInfo.SetValue(asset, shadowSupported);
        }

        public void SetShadowDistance(int value)
        {
            UniversalRenderPipelineAsset asset = GetUniversalRenderPipelineAsset();

            float distance = 0;
            if (value == 0) distance = 200;
            if (value == 1) distance = 150;
            if (value == 2) distance = 100;
            if (value == 3) distance = 50;
            if (value == 4) distance = 30;
            if (value == 5) distance = 0;

            asset.shadowDistance = distance;
        }

        public void SetShadowCascade(int value)
        {
            UniversalRenderPipelineAsset asset = GetUniversalRenderPipelineAsset();
            BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            FieldInfo shadowCascadeCountFieldInfo = asset.GetType().GetField("m_ShadowCascadeCount", flags);

            int count = 0;
            if (value == 0) count = 4;
            if (value == 1) count = 2;
            if (value == 2) count = 1;

            shadowCascadeCountFieldInfo.SetValue(asset, count);
        }

        public void SetSoftShadow(int value)
        {
            UniversalRenderPipelineAsset asset = GetUniversalRenderPipelineAsset();
            BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            FieldInfo softShadowFieldInfo = asset.GetType().GetField("m_SoftShadowsSupported", flags);

            bool softShadow = value == 0;

            softShadowFieldInfo.SetValue(asset, softShadow);

        }

        public void SetPostProcess(int value)
        {
            Volume volume = FindFirstObjectByType<Volume>();

            if (volume == null)
            {
                return;
            }

            float finalAmount = 1;

            if (value == 0) finalAmount = 1;
            if (value == 1) finalAmount = 0.8f;
            if (value == 2) finalAmount = 0.6f;
            if (value == 3) finalAmount = 0.5f;
            if (value == 4) finalAmount = 0.2f;
            if (value == 5) finalAmount = 0;

            volume.weight = finalAmount;
        }
    }
}