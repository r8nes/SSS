using UnityEngine.Rendering;

namespace Aetheria.Runtime.Data
{
    [GenerateHLSL]
    public enum CloudShaderConfig
    {
        TransferLutResolution = 64,
        CoverageNumerator = 9,
        CoverageDenominator = 10,
        DensityThresholdNumerator = 1,
        DensityThresholdDenominator = 1000,
        DropletDiameterMicrometers = 10,
    }
}
