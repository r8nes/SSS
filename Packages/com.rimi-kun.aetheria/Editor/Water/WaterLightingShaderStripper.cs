using System;
using System.Collections.Generic;
using UnityEditor.Build;
using UnityEditor.Rendering;
using UnityEngine;
using UnityEngine.Rendering;

namespace Aetheria.Editor.Water
{
    sealed class WaterLightingShaderStripper : IPreprocessComputeShaders
    {
        const string ShaderName = "WaterLighting";
        const string DeferredKernelName = "WaterDeferredLighting";
        const string RefractionKernelName = "WaterDeferredLightingRefraction";

        public int callbackOrder => -500;

        public void OnProcessComputeShader(ComputeShader shader, string kernelName, IList<ShaderCompilerData> data)
        {
            if (shader == null || !string.Equals(shader.name, ShaderName, StringComparison.Ordinal))
                return;
            if (kernelName != DeferredKernelName && kernelName != RefractionKernelName)
                return;

            KeywordSet keywords = new(shader);
            for (int index = data.Count - 1; index >= 0; --index)
            {
                ShaderKeywordSet variant = data[index].shaderKeywordSet;
                if (IsRedundant(variant, keywords))
                    data.RemoveAt(index);
            }
        }

        static bool IsRedundant(ShaderKeywordSet variant, KeywordSet keywords)
        {
            bool hasMainShadow =
                IsEnabled(variant, keywords.mainLightShadows) ||
                IsEnabled(variant, keywords.mainLightShadowsCascade) ||
                IsEnabled(variant, keywords.mainLightShadowsScreen);
            bool hasAdditionalShadow = IsEnabled(variant, keywords.additionalLightShadows);
            bool hasSoftShadow =
                IsEnabled(variant, keywords.softShadows) ||
                IsEnabled(variant, keywords.softShadowsLow) ||
                IsEnabled(variant, keywords.softShadowsMedium) ||
                IsEnabled(variant, keywords.softShadowsHigh);
            if (hasSoftShadow && !hasMainShadow && !hasAdditionalShadow)
                return true;

            bool probeAtlas = IsEnabled(variant, keywords.reflectionProbeAtlas);
            bool probeBlending = IsEnabled(variant, keywords.reflectionProbeBlending);
            bool probeBoxProjection = IsEnabled(variant, keywords.reflectionProbeBoxProjection);
            bool probeRotation = IsEnabled(variant, keywords.reflectionProbeRotation);
            if (!probeAtlas && (probeBlending || probeBoxProjection || probeRotation))
                return true;
            if (probeAtlas && !probeBlending)
                return true;

            return false;
        }

        static bool IsEnabled(ShaderKeywordSet variant, LocalKeyword keyword) =>
            keyword.isValid && variant.IsEnabled(keyword);

        readonly struct KeywordSet
        {
            internal readonly LocalKeyword mainLightShadows;
            internal readonly LocalKeyword mainLightShadowsCascade;
            internal readonly LocalKeyword mainLightShadowsScreen;
            internal readonly LocalKeyword additionalLightShadows;
            internal readonly LocalKeyword softShadows;
            internal readonly LocalKeyword softShadowsLow;
            internal readonly LocalKeyword softShadowsMedium;
            internal readonly LocalKeyword softShadowsHigh;
            internal readonly LocalKeyword reflectionProbeBlending;
            internal readonly LocalKeyword reflectionProbeBoxProjection;
            internal readonly LocalKeyword reflectionProbeAtlas;
            internal readonly LocalKeyword reflectionProbeRotation;

            internal KeywordSet(ComputeShader shader)
            {
                mainLightShadows = Find(shader, "_MAIN_LIGHT_SHADOWS");
                mainLightShadowsCascade = Find(shader, "_MAIN_LIGHT_SHADOWS_CASCADE");
                mainLightShadowsScreen = Find(shader, "_MAIN_LIGHT_SHADOWS_SCREEN");
                additionalLightShadows = Find(shader, "_ADDITIONAL_LIGHT_SHADOWS");
                softShadows = Find(shader, "_SHADOWS_SOFT");
                softShadowsLow = Find(shader, "_SHADOWS_SOFT_LOW");
                softShadowsMedium = Find(shader, "_SHADOWS_SOFT_MEDIUM");
                softShadowsHigh = Find(shader, "_SHADOWS_SOFT_HIGH");
                reflectionProbeBlending = Find(shader, "_REFLECTION_PROBE_BLENDING");
                reflectionProbeBoxProjection = Find(shader, "_REFLECTION_PROBE_BOX_PROJECTION");
                reflectionProbeAtlas = Find(shader, "_REFLECTION_PROBE_ATLAS");
                reflectionProbeRotation = Find(shader, "REFLECTION_PROBE_ROTATION");
            }

            static LocalKeyword Find(ComputeShader shader, string name) => shader.keywordSpace.FindKeyword(name);
        }
    }
}
