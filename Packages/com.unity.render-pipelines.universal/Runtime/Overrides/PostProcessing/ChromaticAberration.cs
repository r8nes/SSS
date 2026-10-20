using System;

namespace UnityEngine.Rendering.Universal
{
    /// <summary>
    /// A volume component that holds settings for the Chromatic Aberration effect.
    /// </summary>
    /// <remarks>
    /// The effect models lateral chromatic aberration as wavelength-dependent lens distortion.
    /// Spectral sampling adapts to the projected dispersion while preserving the full lens model
    /// for visibly separated wavelengths.
    /// </remarks>
    [Serializable, VolumeComponentMenu("Post-processing/Chromatic Aberration")]
    [SupportedOnRenderPipeline(typeof(UniversalRenderPipelineAsset))]
    [URPHelpURL("post-processing-chromatic-aberration")]
    public sealed class ChromaticAberration : VolumeComponent, IPostProcessComponent
    {
        /// <summary>
        /// Controls the strength of the wavelength-dependent lens dispersion.
        /// </summary>
        [Tooltip("Controls the strength of the wavelength-dependent lens dispersion.")]
        public ClampedFloatParameter intensity = new ClampedFloatParameter(0f, 0f, 1f);

        /// <summary>
        /// Sets the optical center used by the chromatic lens warp.
        /// </summary>
        [Tooltip("Sets the optical center used by the chromatic lens warp.")]
        public Vector2Parameter center = new Vector2Parameter(new Vector2(0.5f, 0.5f));

        /// <summary>
        /// Scales how far apart the spectral wavelength taps spread from the reference wavelength.
        /// </summary>
        [Tooltip("Scales how far apart the spectral wavelength taps spread from the reference wavelength.")]
        public ClampedFloatParameter spectralSpread = new ClampedFloatParameter(1f, 0f, 2f);

        /// <summary>
        /// Controls the normalized radial lens polynomial used by the wavelength-dependent warp.
        /// </summary>
        [Tooltip("Controls the normalized radial lens polynomial used by the wavelength-dependent warp.")]
        public Vector3Parameter radialCoefficients = new Vector3Parameter(new Vector3(1f, 0f, 0f));

        /// <summary>
        /// Controls the wavelength-dependent decentering component of the lens warp.
        /// </summary>
        [Tooltip("Controls the wavelength-dependent decentering component of the lens warp.")]
        public Vector2Parameter tangentialCoefficients = new Vector2Parameter(Vector2.zero);

        /// <summary>
        /// Tells if the post process needs to be rendered or not.
        /// </summary>
        /// <returns><c>true</c> if the effect should be rendered, <c>false</c> otherwise.</returns>
        public bool IsActive() => intensity.value > 0f;

        /// <summary>
        /// Tells if the post process can run the effect on-tile or if it needs a full pass.
        /// </summary>
        /// <returns><c>true</c> if it can run on-tile, <c>false</c> otherwise.</returns>
        [Obsolete("Unused. #from(2023.1)")]
        public bool IsTileCompatible() => false;
    }
}
