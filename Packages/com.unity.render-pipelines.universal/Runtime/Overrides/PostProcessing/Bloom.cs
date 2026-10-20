using System;

namespace UnityEngine.Rendering.Universal
{
    /// <summary>
    /// The resolution at which URP processes the bloom effect.
    /// </summary>
    public enum BloomResolution
    {
        /// <summary>
        /// Quarter resolution. Faster than <see cref="Half"/>, but can introduce aliasing.
        /// </summary>
        Quarter = 4,

        /// <summary>
        /// Half resolution.
        /// </summary>
        Half = 2,
    }

    /// <summary>
    /// A volume component that holds settings for the Bloom effect.
    /// </summary>
    [Serializable, VolumeComponentMenu("Post-processing/Bloom")]
    [SupportedOnRenderPipeline(typeof(UniversalRenderPipelineAsset))]
    [URPHelpURL("post-processing-bloom")]
    public sealed class Bloom : VolumeComponent, IPostProcessComponent
    {
        /// <summary>
        /// Filters out pixels below this gamma-space brightness level.
        /// </summary>
        [Header("Bloom")]
        [Tooltip("Set the level of brightness to filter out pixels under this level. This value is expressed in gamma-space. A value above 0 disregards energy conservation rules.")]
        public MinFloatParameter threshold = new(0f, 0f);

        /// <summary>
        /// Controls the strength of the bloom filter.
        /// </summary>
        [Tooltip("Controls the strength of the bloom filter.")]
        public ClampedFloatParameter intensity = new(0f, 0f, 1f);

        /// <summary>
        /// Controls the extent of the veiling effect.
        /// </summary>
        [Tooltip("Set the radius of the bloom effect.")]
        public ClampedFloatParameter scatter = new(0.7f, 0f, 1f);

        /// <summary>
        /// Specifies the tint of the bloom filter.
        /// </summary>
        [Tooltip("Use the color picker to select a color for the Bloom effect to tint to.")]
        public ColorParameter tint = new(Color.white, false, false, true);

        /// <summary>
        /// Specifies a texture to add smudges or dust to the bloom effect.
        /// </summary>
        [Header("Lens Dirt")]
        [Tooltip("Specifies a Texture to add smudges or dust to the bloom effect.")]
        public Texture2DParameter dirtTexture = new(null);

        /// <summary>
        /// Controls the strength of the lens dirt.
        /// </summary>
        [Tooltip("Controls the strength of the lens dirt.")]
        public MinFloatParameter dirtIntensity = new(0f, 0f);

        /// <summary>
        /// Stretches bloom using the physical Camera anamorphism value.
        /// </summary>
        [AdditionalProperty]
        [Tooltip("When enabled, bloom stretches horizontally depending on the current physical Camera's Anamorphism property value.")]
        public BoolParameter anamorphic = new(true);

        /// <summary>
        /// Specifies the resolution at which URP processes bloom.
        /// </summary>
        public BloomResolution resolution
        {
            get => m_Resolution.value;
            set => m_Resolution.value = value;
        }

        /// <summary>
        /// Enables the HDRP multi-sample prefilter.
        /// </summary>
        public bool highQualityPrefiltering
        {
            get => m_HighQualityPrefiltering.value;
            set => m_HighQualityPrefiltering.value = value;
        }

        /// <summary>
        /// Enables bicubic filtering during pyramid upsampling.
        /// </summary>
        public bool highQualityFiltering
        {
            get => m_HighQualityFiltering.value;
            set => m_HighQualityFiltering.value = value;
        }

        [Header("Advanced Tweaks")]
        [AdditionalProperty]
        [Tooltip("Specifies the resolution at which URP processes the effect. Quarter resolution is less resource intensive but can result in aliasing artifacts.")]
        [SerializeField]
        private BloomResolutionParameter m_Resolution = new(BloomResolution.Half);

        [AdditionalProperty]
        [Tooltip("When enabled, bloom uses multiple bilinear samples for the prefiltering pass.")]
        [SerializeField]
        private BoolParameter m_HighQualityPrefiltering = new(false);

        [AdditionalProperty]
        [Tooltip("When enabled, bloom uses bicubic sampling instead of bilinear sampling for the upsampling passes.")]
        [SerializeField]
        private BoolParameter m_HighQualityFiltering = new(true);

        /// <inheritdoc/>
        public bool IsActive() => intensity.value > 0f;

    }

    /// <summary>
    /// A <see cref="VolumeParameter"/> that holds a <see cref="BloomResolution"/> value.
    /// </summary>
    [Serializable]
    public sealed class BloomResolutionParameter : VolumeParameter<BloomResolution>
    {
        /// <summary>
        /// Creates a new <see cref="BloomResolutionParameter"/> instance.
        /// </summary>
        public BloomResolutionParameter(BloomResolution value, bool overrideState = false)
            : base(value, overrideState)
        {
        }
    }
}
