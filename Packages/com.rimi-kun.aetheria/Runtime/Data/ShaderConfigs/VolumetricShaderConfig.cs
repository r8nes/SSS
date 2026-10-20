using UnityEngine.Rendering;

namespace Aetheria.Runtime.Data
{
    [GenerateHLSL]
    public enum VolumetricShaderConfig
    {
        FroxelThreadGroupSize = 8,
        MediaTileSizeXY = 8,
        MediaTileSizeZ = 4,
        MaxMediaPerTile = 8,
        FilterThreadGroupSizeX = 16,
        FilterThreadGroupSizeY = 8,
        AtlasGroupCount = 4,
        AtlasSlicesPerGroup = 64,
    }
}
