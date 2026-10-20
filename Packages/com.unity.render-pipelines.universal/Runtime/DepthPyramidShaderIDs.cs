namespace UnityEngine.Rendering.Universal
{
    public static class DepthPyramidShaderIDs
    {
        public static readonly int DepthPyramid = Shader.PropertyToID("_DepthPyramid");
        public static readonly int DepthPyramidFarthest = Shader.PropertyToID("_DepthPyramidFarthest");

        public static readonly int DepthPyramidMipCount = Shader.PropertyToID("_DepthPyramidMipCount");
        public static readonly int DepthPyramidCheckerboardMipCount = Shader.PropertyToID("_DepthPyramidCheckerboardMipCount");
        public static readonly int DepthPyramidMip0Size = Shader.PropertyToID("_DepthPyramidMip0Size");
        public static readonly int DepthPyramidAtlasSize = Shader.PropertyToID("_DepthPyramidAtlasSize");
        public static readonly int DepthPyramidChannelMode = Shader.PropertyToID("_DepthPyramidChannelMode");

        public static readonly int DepthPyramidMipLevelOffsets = Shader.PropertyToID("_DepthPyramidMipLevelOffsets");
        public static readonly int DepthPyramidMipLevelOffsetsCheckerboard = Shader.PropertyToID("_DepthPyramidMipLevelOffsetsCheckerboard");

        public static readonly int SourceDepthTexture = Shader.PropertyToID("_SourceDepthTexture");
    }
}