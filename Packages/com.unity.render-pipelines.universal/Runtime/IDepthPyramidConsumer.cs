namespace UnityEngine.Rendering.Universal
{
    public interface IDepthPyramidConsumer
    {
        /// <summary>True when this pass requires the shared camera-frame depth pyramid.</summary>
        bool requiresDepthPyramid { get; }

        /// <summary>Minimum number of regular mip levels required, including mip0.</summary>
        int requiredDepthPyramidMipCount { get; }

        /// <summary>Minimum number of checkerboard mip levels required. Use 0 for HDRP-style single-channel nearest-depth consumers.</summary>
        int requiredDepthPyramidCheckerboardMipCount { get; }
    }
}
