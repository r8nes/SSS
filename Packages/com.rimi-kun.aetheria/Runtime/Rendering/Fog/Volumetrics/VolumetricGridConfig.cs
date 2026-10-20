using Aetheria.Runtime.Data;

namespace Aetheria.Runtime.Rendering.Fog.Volumetrics
{
    internal static class VolumetricGridConfig
    {
        public const int ThreadGroupSizeXY = (int)VolumetricShaderConfig.FroxelThreadGroupSize;
        public const int MediaTileSizeXY = (int)VolumetricShaderConfig.MediaTileSizeXY;
        public const int MediaTileSizeZ = (int)VolumetricShaderConfig.MediaTileSizeZ;
        public const int FilterThreadGroupSizeX = (int)VolumetricShaderConfig.FilterThreadGroupSizeX;
        public const int FilterThreadGroupSizeY = (int)VolumetricShaderConfig.FilterThreadGroupSizeY;
        public const int MaxZoneCount = 64;
        public const int MaxFogMaskCount = 64;

        public const int AtlasSlicesPerGroup = (int)VolumetricShaderConfig.AtlasSlicesPerGroup;
        public const int InitialAtlasSliceCapacity = 1;
        public const int MaxAtlasGroups = (int)VolumetricShaderConfig.AtlasGroupCount;
    }
}
