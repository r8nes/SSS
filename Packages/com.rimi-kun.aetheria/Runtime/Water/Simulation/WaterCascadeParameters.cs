using System;
using UnityEngine;

namespace Aetheria.Runtime.Water
{
    [Serializable]
    public sealed class WaterCascadeParameters
    {
        public Vector2 tileLength = new(50.0f, 50.0f);
        [Range(0.0f, 2.0f)] public float displacementScale = 1.0f;
        [Range(0.0f, 2.0f)] public float normalScale = 1.0f;
        [Min(0.0001f)] public float windSpeed = 10.0f;
        [Range(-360.0f, 360.0f)] public float windDirection;
        [Min(0.0001f)] public float fetchLength = 150.0f;
        [Range(0.0f, 2.0f)] public float swell = 0.8f;
        [Range(0.0f, 1.0f)] public float spread = 0.2f;
        [Range(0.0f, 1.0f)] public float detail = 1.0f;
        [Range(0.0f, 2.0f)] public float whitecap = 0.5f;
        [Range(0.0f, 10.0f)] public float foamAmount = 5.0f;

        internal static WaterCascadeParameters Create(
            Vector2 tileLength,
            float displacementScale,
            float normalScale,
            float windSpeed,
            float windDirection,
            float fetchLength,
            float swell,
            float spread,
            float detail,
            float whitecap,
            float foamAmount)
        {
            return new WaterCascadeParameters
            {
                tileLength = tileLength,
                displacementScale = displacementScale,
                normalScale = normalScale,
                windSpeed = windSpeed,
                windDirection = windDirection,
                fetchLength = fetchLength,
                swell = swell,
                spread = spread,
                detail = detail,
                whitecap = whitecap,
                foamAmount = foamAmount
            };
        }
    }
}
