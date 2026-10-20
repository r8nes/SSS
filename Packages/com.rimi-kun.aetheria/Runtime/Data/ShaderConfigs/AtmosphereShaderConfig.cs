using UnityEngine.Rendering;

namespace Aetheria.Runtime.Data
{
    [GenerateHLSL]
    public enum AtmosphereShaderConfig
    {
        DistanceUnitMeters = 1000,
        MaxCelestialLights = 16,
        LutThreadGroupSize = 8,

        TransmittanceLutWidth = 256,
        TransmittanceLutHeight = 64,

        MultiScatteringLutWidth = 32,
        MultiScatteringLutHeight = 32,
        MultiScatteringSqrtSampleCount = 8,
        MultiScatteringRaymarchSampleCount = 20,

        GroundIrradianceLutWidth = 64,
        GroundIrradianceLutHeight = 16,
        GroundIrradianceSampleCount = 16,

        SkyViewLutWidth = 192,
        SkyViewLutHeight = 108,

        AtmosphericScatteringLutWidth = 32,
        AtmosphericScatteringLutHeight = 32,
        AtmosphericScatteringLutDepth = 32,
    }
}
