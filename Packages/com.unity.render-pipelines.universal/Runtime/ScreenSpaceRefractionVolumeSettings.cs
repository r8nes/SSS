using System;

namespace UnityEngine.Rendering.Universal
{
    /// <summary>
    /// Global settings for material-driven Screen Space Refraction.
    /// The effect itself lives in transparent material shaders while the volume
    /// controls the screen-border hierarchy fade.
    /// </summary>
    [Serializable, VolumeComponentMenu("Lighting/Screen Space Refraction"), SupportedOnRenderPipeline(typeof(UniversalRenderPipelineAsset))]
    [DisplayInfo(name = "Screen Space Refraction")]
    public class ScreenSpaceRefractionVolumeSettings : VolumeComponent
    {
        /// <summary>
        /// Controls how far from the screen border Screen Space Refraction fades to
        /// reflection probes / sky.
        /// </summary>
        [Tooltip("Controls the distance at which Screen Space Refraction fades near the edge of the screen.")]
        public ClampedFloatParameter screenFadeDistance = new ClampedFloatParameter(0.1f, 0.001f, 1.0f);
    }
}
