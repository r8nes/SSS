namespace UnityEngine.Rendering.Universal
{
    /// <summary>
    /// Base class for URP volume components that resolve performance-sensitive parameters from built-in quality tiers.
    /// </summary>
    public abstract class VolumeComponentWithQuality : VolumeComponent
    {
        /// <summary>Quality tier used for performance-sensitive parameters, or Custom when overridden.</summary>
        [Tooltip("Specifies the quality level to use for performance relevant parameters.")]
        [InspectorName("Tier")]
        public ScalableSettingLevelParameter quality = new ScalableSettingLevelParameter(
            (int)ScalableSettingLevelParameter.Level.Medium, false);

        /// <summary>Returns true when this component resolves settings from its selected quality tier.</summary>
        protected bool UsesQualitySettings()
        {
            return !quality.levelAndOverride.useOverride;
        }
    }
}
