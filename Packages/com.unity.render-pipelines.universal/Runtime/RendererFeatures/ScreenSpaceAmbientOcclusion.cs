using System;
using UnityEngine.Serialization;
#if UNITY_EDITOR
using ShaderKeywordFilter = UnityEditor.ShaderKeywordFilter;
#endif

namespace UnityEngine.Rendering.Universal
{
    /// <summary>
    /// Shader keyword constants for Screen Space Ambient Occlusion.
    /// </summary>
    internal static class ScreenSpaceAmbientOcclusionKeywords
    {
        internal const string k_AOInterleavedGradientKeyword = "_INTERLEAVED_GRADIENT";
        internal const string k_AOBlueNoiseKeyword = "_BLUE_NOISE";
        internal const string k_OrthographicCameraKeyword = "_ORTHOGRAPHIC";
        internal const string k_SourceDepthLowKeyword = "_SOURCE_DEPTH_LOW";
        internal const string k_SourceDepthMediumKeyword = "_SOURCE_DEPTH_MEDIUM";
        internal const string k_SourceDepthHighKeyword = "_SOURCE_DEPTH_HIGH";
        internal const string k_SourceDepthNormalsKeyword = "_SOURCE_DEPTH_NORMALS";
        internal const string k_SampleCountLowKeyword = "_SAMPLE_COUNT_LOW";
        internal const string k_SampleCountMediumKeyword = "_SAMPLE_COUNT_MEDIUM";
        internal const string k_SampleCountHighKeyword = "_SAMPLE_COUNT_HIGH";
        internal const string k_TemporalFilteringKeyword = "_TEMPORAL_FILTERING";
    }

    [Serializable]
    internal class ScreenSpaceAmbientOcclusionSettings
    {
        internal AOMethodOptions AOMethod = AOMethodOptions.BlueNoise;
        internal bool Downsample;
        internal bool AfterOpaque;
        internal DepthSource Source = DepthSource.DepthNormals;
        internal NormalQuality NormalSamples = NormalQuality.Medium;
        internal float Intensity;
        internal float DirectLightingStrength;
        internal float Radius;
        internal AOSampleOption Samples = AOSampleOption.Medium;
        internal BlurQualityOptions BlurQuality = BlurQualityOptions.High;
        internal float Falloff;
        internal ScreenSpaceAmbientOcclusionMode Mode;
        internal int GTAOMinimumRadiusInPixels;
        internal int GTAOMaximumRadiusInPixels;
        internal bool GTAOTemporalFilterEnabled;
        internal float GTAOGhostingMitigation;
        internal float GTAOHistoryLength;
        internal int GTAODirectionCount;
        internal int GTAOStepCount;
        internal ScreenSpaceAmbientOcclusionSpatialFilter GTAOSpatialFilter;

        internal bool IsTemporalFilterActive => Mode == ScreenSpaceAmbientOcclusionMode.GTAO && GTAOTemporalFilterEnabled;

        // Enums
        internal enum DepthSource
        {
            Depth = 0,
            DepthNormals = 1
        }

        internal enum NormalQuality
        {
            Low,
            Medium,
            High
        }

        internal enum AOSampleOption
        {
            High,   // 12 Samples
            Medium, // 8 Samples
            Low,    // 4 Samples
        }

        internal enum AOMethodOptions
        {
            BlueNoise,
            InterleavedGradient,
        }

        internal enum BlurQualityOptions
        {
            High,   // Bilateral
            Medium, // Gaussian
            Low,    // Kawase
        }
    }

    [Serializable]
    [Scripting.APIUpdating.MovedFrom(false, sourceClassName: "ScreenSpaceAmbientOcclusionPersistentResources")]
    [SupportedOnRenderPipeline(typeof(UniversalRenderPipelineAsset))]
    [Categorization.CategoryInfo(Name = "R: SSAO Shader", Order = 1000)]
    [Categorization.ElementInfo(Order = 0), HideInInspector]
    class ScreenSpaceAmbientOcclusionCoreResources : IRenderPipelineResources
    {
        [SerializeField]
        [ResourcePath("Shaders/Utils/ScreenSpaceAmbientOcclusion.shader"), FormerlySerializedAs("m_Shader")]
        Shader m_RasterizationShader;

        public Shader RasterizationShader
        {
            get => m_RasterizationShader;
            set => this.SetValueAndNotify(ref m_RasterizationShader, value);
        }

        [SerializeField]
        [ResourcePath("Shaders/Utils/GTAO.compute")]
        ComputeShader m_GTAOComputeShader;

        public ComputeShader GTAOComputeShader
        {
            get => m_GTAOComputeShader;
            set => this.SetValueAndNotify(ref m_GTAOComputeShader, value);
        }

        public bool isAvailableInPlayerBuild => true;

        [SerializeField][HideInInspector] private int m_Version = 0;

        /// <summary>Current version of the resource container. Used only for upgrading a project.</summary>
        public int version => m_Version;
    }

    [Serializable]
    [Scripting.APIUpdating.MovedFrom(false, sourceClassName: "ScreenSpaceAmbientOcclusionDynamicResources")]
    [SupportedOnRenderPipeline(typeof(UniversalRenderPipelineAsset))]
    [Categorization.CategoryInfo(Name = "R: SSAO Noise Textures", Order = 1000)]
    [Categorization.ElementInfo(Order = 0), HideInInspector]
    class ScreenSpaceAmbientOcclusionBlueNoiseResources : IRenderPipelineResources
    {
        [SerializeField]
        [ResourceFormattedPaths("Textures/BlueNoise256/LDR_LLL1_{0}.png", 0, 7)]
        Texture2D[] m_BlueNoise256Textures;

        public Texture2D[] BlueNoise256Textures
        {
            get => m_BlueNoise256Textures;
            set => this.SetValueAndNotify(ref m_BlueNoise256Textures, value);
        }

        public bool isAvailableInPlayerBuild => true;

        [SerializeField][HideInInspector] private int m_Version = 0;

        /// <summary>Current version of the resource container. Used only for upgrading a project.</summary>
        public int version => m_Version;
    }


    /// <summary>
    /// The class for the SSAO renderer feature.
    /// </summary>
    [SupportedOnRenderer(typeof(UniversalRendererData))]
    [DisallowMultipleRendererFeature("Screen Space Ambient Occlusion")]
    [Tooltip("The Ambient Occlusion effect darkens creases, holes, intersections and surfaces that are close to each other.")]
    [URPHelpURL("urp/post-processing-ssao")]
    public class ScreenSpaceAmbientOcclusion : ScriptableRendererFeature
    {
        // Private Fields
        private AAOPass m_AAOPass = null;
        private GTAOPass m_GTAOPass = null;
        private Shader m_RasterizationShader;
        private Texture2D[] m_BlueNoise256Textures;

        private struct FeatureSettings
        {
            public RenderPassEvent passEvent;
            public ScriptableRenderPassInput requirements;
        }

        /// <inheritdoc/>
        public override void Create()
        {
            if (TryPrepareResources())
            {
                m_AAOPass = new AAOPass(m_RasterizationShader, m_BlueNoise256Textures);
                m_GTAOPass = new GTAOPass(m_RasterizationShader, m_BlueNoise256Textures);
            }
        }

        /// <inheritdoc/>
        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (UniversalRenderer.IsOffscreenDepthTexture(ref renderingData.cameraData))
                return;

            if (m_AAOPass == null)
                return;

            bool usesDeferred = renderer is UniversalRenderer { usesDeferredLighting: true };
            bool shouldAdd;
            FeatureSettings resolvedSettings;
            ScriptableRenderPass activePass;

            var ssaoVolume = VolumeManager.instance.stack.GetComponent<ScreenSpaceAmbientOcclusionVolumeOverride>();
            resolvedSettings = ResolveFeatureSettings(ssaoVolume, usesDeferred);

            if (!ssaoVolume.IsSSAOMode())
            {
                shouldAdd = m_GTAOPass.Setup(ssaoVolume);
                activePass = m_GTAOPass;
            }
            else
            {
                shouldAdd = m_AAOPass.Setup(ssaoVolume, usesDeferred);
                activePass = m_AAOPass;
            }

            if (!shouldAdd)
                return;

            activePass.renderPassEvent = resolvedSettings.passEvent;
            activePass.ConfigureInput(resolvedSettings.requirements);
            renderer.EnqueuePass(activePass);
        }

        private static FeatureSettings ResolveFeatureSettings(ScreenSpaceAmbientOcclusionVolumeOverride volume, bool usesDeferred)
        {
            var featureSettings = new FeatureSettings();

            if (!volume.IsActive())
                return featureSettings;

            featureSettings.passEvent = usesDeferred
                ? (volume.afterOpaque ? RenderPassEvent.AfterRenderingOpaques : RenderPassEvent.AfterRenderingPrePasses)
                : (volume.afterOpaque ? RenderPassEvent.BeforeRenderingTransparents : RenderPassEvent.AfterRenderingPrePasses + 1);

            featureSettings.requirements = ScriptableRenderPassInput.Depth | ScriptableRenderPassInput.ProceduralDepth;
            bool isGTAO = !volume.IsSSAOMode();
            if (isGTAO || usesDeferred || volume.depthSource == ScreenSpaceAmbientOcclusionDepthSource.DepthNormals)
                featureSettings.requirements |= ScriptableRenderPassInput.Normal | ScriptableRenderPassInput.ProceduralNormal;
            if (isGTAO && volume.temporalFilter)
                featureSettings.requirements |= ScriptableRenderPassInput.Motion;

            return featureSettings;
        }

        /// <inheritdoc/>
        protected override void Dispose(bool disposing)
        {
            m_AAOPass?.Dispose();
            m_AAOPass = null;
            m_GTAOPass?.Dispose();
            m_GTAOPass = null;
        }

        bool TryPrepareResources()
        {
            if (m_RasterizationShader == null)
            {
                // Create() runs even for disabled features, whose resources are stripped from the build; bail
                // out rather than dereferencing them, and only flag missing resources on an active feature.
                if (!GraphicsSettings.TryGetRenderPipelineSettings<ScreenSpaceAmbientOcclusionCoreResources>(out var ssaoCoreResources))
                {
                    if (isActive)
                        Debug.LogErrorFormat(
                            $"Couldn't find the required resources for the {nameof(ScreenSpaceAmbientOcclusion)} render feature. If this exception appears in the Player, make sure at least one {nameof(ScreenSpaceAmbientOcclusion)} render feature is enabled or adjust your stripping settings.");

                    return false;
                }

                m_RasterizationShader = ssaoCoreResources.RasterizationShader;
            }

            if (m_BlueNoise256Textures == null || m_BlueNoise256Textures.Length == 0)
            {
                if (GraphicsSettings.TryGetRenderPipelineSettings<ScreenSpaceAmbientOcclusionBlueNoiseResources>(out var ssaoBlueNoiseResources))
                    m_BlueNoise256Textures = ssaoBlueNoiseResources.BlueNoise256Textures;
            }

            return m_RasterizationShader != null;
        }
    }
}
