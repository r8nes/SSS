using UnityEngine;

namespace Aetheria.Runtime.Rendering
{
    internal static partial class ShaderProperties
    {
        internal static class EnvironmentLighting
        {
            public static readonly int IncludeSunInBaking = Shader.PropertyToID("_IncludeSunInBaking");
            public static readonly int PreviousLightingCubemap = Shader.PropertyToID("_PreviousLightingCubemap");
            public static readonly int TargetLightingCubemap = Shader.PropertyToID("_TargetLightingCubemap");
            public static readonly int LightingBlendFactor = Shader.PropertyToID("_LightingBlendFactor");
            public static readonly int LightingMipResolution = Shader.PropertyToID("_LightingMipResolution");
            public static readonly int LightingMipLevel = Shader.PropertyToID("_LightingMipLevel");
            public static readonly int Output = Shader.PropertyToID("_Output");
            public static readonly int CloudResolution = Shader.PropertyToID("_CloudResolution");
            public static readonly int OriginWs = Shader.PropertyToID("_OriginWs");
            public static readonly int OutputResolution = Shader.PropertyToID("_OutputResolution");
            public static readonly int AmbientProbeInputCubemap = Shader.PropertyToID("_AmbientProbeInputCubemap");
            public static readonly int AmbientProbeInputCubemapArray = Shader.PropertyToID("_AmbientProbeInputCubemapArray");
            public static readonly int CloudTraceOutput = Shader.PropertyToID("_CloudTraceOutput");
            public static readonly int CloudTraceDepthOutput = Shader.PropertyToID("_CloudTraceDepthOutput");
            public static readonly int ResolvedClouds = Shader.PropertyToID("_ResolvedClouds");
            public static readonly int ResolvedCloudDepth = Shader.PropertyToID("_ResolvedCloudDepth");
        }

        internal static class EnvironmentFilter
        {
            public static readonly int MainTexture = Shader.PropertyToID("_MainTex");
            public static readonly int Samples = Shader.PropertyToID("_GgxSamples");
            public static readonly int MipLevel = Shader.PropertyToID("_MipLevel");
            public static readonly int Output = Shader.PropertyToID("_Output");
            public static readonly int MipResolution = Shader.PropertyToID("_MipResolution");
        }
    }
}
