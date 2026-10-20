using System;

namespace UnityEngine.Rendering.Universal
{
    /// <summary>Scalable Contact Shadow quality level parameter.</summary>
    [Serializable]
    public sealed class ScalableSettingLevelParameter : NoInterpIntParameter
    {
        /// <summary>Number of quality levels.</summary>
        public const int LevelCount = 3;

        /// <summary>Quality levels.</summary>
        public enum Level
        {
            /// <summary>Low quality.</summary>
            Low,
            /// <summary>Medium quality.</summary>
            Medium,
            /// <summary>High quality.</summary>
            High
        }

        /// <summary>Creates a scalable quality level parameter.</summary>
        public ScalableSettingLevelParameter(int level, bool useOverride, bool overrideState = false)
            : base(useOverride ? LevelCount : level, overrideState)
        {
        }

        internal static int GetScalableSettingLevelParameterValue(int level, bool useOverride) =>
            useOverride ? LevelCount : level;

        /// <summary>Gets or sets the selected level and whether the custom value is used.</summary>
        public (int level, bool useOverride) levelAndOverride
        {
            get => value == LevelCount ? ((int)Level.Low, true) : (value, false);
            set
            {
                var (level, useOverride) = value;
                this.value = GetScalableSettingLevelParameterValue(level, useOverride);
            }
        }
    }

}
