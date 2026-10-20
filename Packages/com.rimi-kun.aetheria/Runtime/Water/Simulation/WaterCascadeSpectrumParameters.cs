using UnityEngine;
using Unity.Mathematics;

namespace Aetheria.Runtime.Water
{
    internal struct WaterCascadeSpectrumParameters
    {
        internal float2 tileLength;
        internal float2 deltaK;
        internal float alpha;
        internal float omegaPeak;
        internal float windSpeed;
        internal float windDirection;
        internal float swell;
        internal float spread;
        internal float detail;
        internal uint seed;

        private static uint EvaluateSeed(int cascadeIndex)
        {
            return cascadeIndex switch
            {
                0 => 1337u,
                1 => 7331u,
                _ => 31337u
            };
        }

        internal static WaterCascadeSpectrumParameters Create(WaterCascadeParameters source, int cascadeIndex)
        {
            float2 tileLength = new float2(
                Mathf.Max(source.tileLength.x, 0.001f),
                Mathf.Max(source.tileLength.y, 0.001f));
            float windSpeed = Mathf.Max(source.windSpeed, 0.0001f);
            float fetchMeters = Mathf.Max(source.fetchLength, 0.0001f) * 1000.0f;
            float dimensionlessFetch = Mathf.Max(
                WaterConstants.k_GravityConstant * fetchMeters / (windSpeed * windSpeed), 1e-6f);

            return new WaterCascadeSpectrumParameters
            {
                tileLength = tileLength,
                deltaK = 2.0f * Mathf.PI / tileLength,
                alpha = 0.076f * Mathf.Pow(dimensionlessFetch, -0.22f),
                omegaPeak = 22.0f * Mathf.Pow(
                    WaterConstants.k_GravityConstant * WaterConstants.k_GravityConstant / (windSpeed * fetchMeters),
                    1.0f / 3.0f),
                windSpeed = windSpeed,
                windDirection = WaterSimulationLayout.NormalizeAngle(source.windDirection) * Mathf.Deg2Rad,
                swell = Mathf.Clamp(source.swell, 0.0f, 2.0f),
                spread = Mathf.Clamp01(source.spread),
                detail = Mathf.Clamp01(source.detail),
                seed = EvaluateSeed(cascadeIndex)
            };
        }

        public static bool operator ==(WaterCascadeSpectrumParameters a, WaterCascadeSpectrumParameters b)
        {
            return a.tileLength.Equals(b.tileLength)
                && a.deltaK.Equals(b.deltaK)
                && a.alpha == b.alpha
                && a.omegaPeak == b.omegaPeak
                && a.windSpeed == b.windSpeed
                && a.windDirection == b.windDirection
                && a.swell == b.swell
                && a.spread == b.spread
                && a.detail == b.detail
                && a.seed == b.seed;
        }

        public static bool operator !=(WaterCascadeSpectrumParameters a, WaterCascadeSpectrumParameters b) => !(a == b);
        public override bool Equals(object obj) => obj is WaterCascadeSpectrumParameters other && this == other;
        public override int GetHashCode()
        {
            unchecked
            {
                int hash = 17;
                hash = hash * 31 + tileLength.x.GetHashCode();
                hash = hash * 31 + tileLength.y.GetHashCode();
                hash = hash * 31 + deltaK.x.GetHashCode();
                hash = hash * 31 + deltaK.y.GetHashCode();
                hash = hash * 31 + alpha.GetHashCode();
                hash = hash * 31 + omegaPeak.GetHashCode();
                hash = hash * 31 + windSpeed.GetHashCode();
                hash = hash * 31 + windDirection.GetHashCode();
                hash = hash * 31 + swell.GetHashCode();
                hash = hash * 31 + spread.GetHashCode();
                hash = hash * 31 + detail.GetHashCode();
                return hash * 31 + (int)seed;
            }
        }
    }
}
