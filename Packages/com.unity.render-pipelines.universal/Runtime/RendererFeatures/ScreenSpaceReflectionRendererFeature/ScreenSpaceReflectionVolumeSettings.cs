using System;

namespace UnityEngine.Rendering.Universal
{
    /// <summary>
    /// A volume component that holds settings for the Screen Space Reflections Renderer Feature.
    /// </summary>
    [Serializable, VolumeComponentMenu("Lighting/Screen Space Reflection"), SupportedOnRenderPipeline(typeof(UniversalRenderPipelineAsset))]
    [DisplayInfo(name = "Screen Space Reflection")]
    public class ScreenSpaceReflectionVolumeSettings : VolumeComponent
    {
        /// <summary>
        /// An enum specifying which resolution to render Screen Space Reflections at.
        /// </summary>
        public enum Resolution
        {
            Full = 1,
            Half = 2,
            Quarter = 4,
        }

        /// <summary>
        /// An enum specifying which technique to use for upscaling Screen Space Reflections.
        /// </summary>
        public enum UpscalingMethod
        {
            /// <summary>Upscale Screen Space Reflections using bilinear filtering. This is cheaper, but produces more blurry reflections.</summary>
            Bilinear,
            /// <summary>Upscale Screen Space Reflections using depth, normal and smoothness guided hit reconstruction.</summary>
            Bilateral,
        }

        /// <summary>
        /// An enum specifying which quality to use for Screen Space Reflections.
        /// </summary>
        public enum RoughReflectionsQuality
        {
            Disabled,
            Pyramid,
            TrilinearPyramid,
        }

        /// <summary>
        /// An enum specifying which objects to reflect using Screen Space Reflections.
        /// </summary>
        public enum ReflectionMode
        {
            Disabled,
            OpaquesOnly,
            OpaquesAndTransparents,
        }

        internal enum PerformancePreset
        {
            Fast,
            Balanced,
            HighQuality,
            BestQuality,
            Custom
        }

        internal struct PerformancePresetValues
        {
            public Resolution resolution;
            public UpscalingMethod upscalingMethod;
            public float maxRayLength;
            public int maxRaySteps;
            public float objectThickness;
        }

        internal static ref readonly PerformancePresetValues DefaultPreset => ref k_PerformancePresets[(int)PerformancePreset.HighQuality];
        internal static readonly PerformancePresetValues[] k_PerformancePresets =
        {
            // Fastest
            new()
            {
                resolution = Resolution.Quarter,
                upscalingMethod = UpscalingMethod.Bilinear,
                maxRayLength = 20f,
                maxRaySteps = 16,
                objectThickness = 0.325f
            },
            // Balanced
            new()
            {
                resolution = Resolution.Half,
                upscalingMethod = UpscalingMethod.Bilinear,
                maxRayLength = 30f,
                maxRaySteps = 32,
                objectThickness = 0.325f
            },
            // High Quality
            new()
            {
                resolution = Resolution.Half,
                upscalingMethod = UpscalingMethod.Bilateral,
                maxRayLength = 30f,
                maxRaySteps = 64,
                objectThickness = 0.01f
            },
            // Best Quality
            new()
            {
                resolution = Resolution.Full,
                upscalingMethod = UpscalingMethod.Bilateral,
                maxRayLength = 30f,
                maxRaySteps = 64,
                objectThickness = 0.01f
            }
        };

        /// <summary>The mode determining which objects to reflect using Screen Space Reflections.</summary>
        [Tooltip("The mode determining which objects to reflect using Screen Space Reflections. 'Opaques Only' will only render opaque objects in reflections, while 'Opaques And Transparents' will also render transparent objects in reflections.")]
        public EnumParameter<ReflectionMode> mode = new(ReflectionMode.OpaquesOnly);

        /// <summary>Scales the overall contribution. A value of 0 disables the effect, 1 is full reflection strength.</summary>
        [Tooltip("Scales the overall contribution. A value of 0 disables the effect, 1 is full reflection strength.")]
        public ClampedFloatParameter reflectionStrength = new(1.0f, 0.0f, 1.0f);

        /// <summary>Whether to clamp the reflected color to avoid very bright values causing issues.</summary>
        [Tooltip("Whether to clamp the reflected color to avoid very bright values causing issues.")]
        public BoolParameter clampReflectedColor = new(false);

        /// <summary>The maximum color value of reflected colors when clamping is enabled.</summary>
        [Tooltip("The maximum color value of reflected colors when clamping is enabled.")]
        public MinFloatParameter maxColorValue = new(1.0f, 0f);

        /// <summary>The resolution to render Screen Space Reflections at.</summary>
        [Tooltip("The resolution to render Screen Space Reflections at. Lower values will yield better performance, but lower quality.")]
        public EnumParameter<Resolution> resolution = new(DefaultPreset.resolution, true);

        /// <summary>The technique to use for upscaling Screen Space Reflections.</summary>
        [Tooltip("The method to use for upscaling the low resolution reflection texture. 'Bilinear' filters premultiplied radiance and confidence; 'Bilateral' also preserves depth, normal and material boundaries.")]
        public EnumParameter<UpscalingMethod> upscalingMethod = new(DefaultPreset.upscalingMethod, true);

        /// <summary>Which technique to use for rendering rough/glossy reflections.</summary>
        [Tooltip("Which technique to use for rendering rough/glossy reflections. Disabled uses mirror radiance. Pyramid selects a discrete reflection mip; Trilinear Pyramid interpolates reflection mips using a roughness-controlled reflection footprint.")]
        public EnumParameter<RoughReflectionsQuality> roughnessFilter = new(RoughReflectionsQuality.TrilinearPyramid);

        /// <summary>Scales the reflection blur radius on a logarithmic scale. A value of 0 is neutral, negative values reduce blurriness, positive values increase it.</summary>
        [Tooltip("Scales the reflection blur radius on a logarithmic scale. A value of 0 is neutral, negative values reduce blurriness, positive values increase it.")]
        public ClampedFloatParameter roughnessScale = new(0.0f, -10.0f, 10.0f);

        /// <summary>The minimum amount of surface smoothness at which Screen Space Reflections are used.</summary>
        [Tooltip("The minimum amount of surface smoothness at which Screen Space Reflections are used. Higher values will result in less objects receiving Screen Space Reflections.")]
        public ClampedFloatParameter minimumSmoothness = new(0.05f, 0.0f, 1.0f);

        /// <summary>The smoothness value at which the smoothness-controlled fade out starts.</summary>
        [Tooltip("The smoothness value at which the smoothness-controlled fade out starts. The fade is in the range [Min Smoothness, Smoothness Fade Start].")]
        public ClampedFloatParameter smoothnessFadeStart = new(0.1f, 0.0f, 1.0f);

        /// <summary>How much to fade reflections based on the reflection normal.</summary>
        [Tooltip("How much to fade reflections based on the reflection normal.")]
        public ClampedFloatParameter normalFade = new(0.0f, 0.0f, 1.0f);

        /// <summary>The distance at which the reflection fades out near the edge of the screen.</summary>
        [Tooltip("The distance at which the reflection fades out near the edge of the screen.")]
        public ClampedFloatParameter screenEdgeFadeDistance = new(0.2f, 0.0f, 1.0f);

        /// <summary>Whether to use Screen Space Reflections to handle reflections of the sky.</summary>
        [Tooltip("Whether to use SSR to handle sky reflection. If you disable this property, pixels that reflect the sky will sample from nearby reflection probes, or the skybox.")]
        public BoolParameter reflectSky = new(false);

        /// <summary>The maximum distance in world space units a ray can travel.</summary>
        [Tooltip("The maximum distance in world space units a ray can travel.")]
        public MinFloatParameter maxRayLength = new(DefaultPreset.maxRayLength, 0f, true);

        /// <summary>The fade distance in world space units before the maximum ray length.</summary>
        [Tooltip("The fade distance in world space units before the maximum ray length.")]
        public MinFloatParameter rayLengthFade = new(1f, 0f);

        /// <summary>The maximum amount of steps to take when tracing rays.</summary>
        [Tooltip("The maximum amount of steps to take when tracing rays.")]
        public MinIntParameter maxRaySteps = new(DefaultPreset.maxRaySteps, 1, true);


        /// <summary>How close to the depth buffer a ray must be to be considered a hit.</summary>
        [Tooltip("How close to the depth buffer a ray must be to be considered a hit. Higher values will result in less accurate reflections, but may help mitigate shimmering artifacts.")]
        public ClampedFloatParameter objectThickness = new(DefaultPreset.objectThickness, 0f, 1f, true);


        // Helpers
        internal bool IsActive() => mode.value != ReflectionMode.Disabled && reflectionStrength.value > 0.0f;
        internal bool ShouldRenderTransparents() => mode.value == ReflectionMode.OpaquesAndTransparents;

        // Allow listening for property changes, to support presets in the presence of undo and changing values from script etc.
#if UNITY_EDITOR
        internal event Action propertyChanged;
        private void OnValidate() => propertyChanged?.Invoke();
#endif
    }
}
