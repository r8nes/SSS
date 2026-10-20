using Unity.Mathematics;

namespace Aetheria.Runtime.Water
{
    internal struct WaterCascadeRenderingParameters
    {
        internal float2 inverseTileLength;
        internal float displacementScale;
        internal float normalScale;
        internal float currentSpeed;
        internal float distanceFadeScale;
        internal float distanceFadeBias;
    }

    internal struct WaterSimulationRenderingParameters
    {
        internal WaterCascadeRenderingParameters cascade0;
        internal WaterCascadeRenderingParameters cascade1;
        internal WaterCascadeRenderingParameters cascade2;
        internal float maxWaveHeight;
        internal float maxWaveDisplacement;
        internal float4x4 waterToWorldMatrix;
        internal float4x4 worldToWaterMatrix;

        internal WaterCascadeRenderingParameters GetCascade(int index)
        {
            return index == 0 ? cascade0 : index == 1 ? cascade1 : cascade2;
        }

        internal void SetCascade(int index, WaterCascadeRenderingParameters value)
        {
            if (index == 0) cascade0 = value;
            else if (index == 1) cascade1 = value;
            else cascade2 = value;
        }
    }
}
