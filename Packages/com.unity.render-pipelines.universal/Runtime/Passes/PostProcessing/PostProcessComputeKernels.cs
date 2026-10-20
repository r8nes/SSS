namespace UnityEngine.Rendering.Universal
{
    /// <summary>
    /// Shared post-processing compute kernel variant naming helpers.
    ///
    /// RenderGraph execution commands must not mutate shader keyword state. Post-processing
    /// compute shaders should expose runtime choices as explicit kernels (compiled with
    /// #pragma kernel defines) and select the kernel during RecordRenderGraph instead.
    /// </summary>
    internal static class PostProcessComputeKernels
    {
        public const string KMain = "KMain";
        public const string AlphaSuffix = "Alpha";
        public const string LowQualitySuffix = "LowQuality";
        public const string HighQualitySuffix = "HighQuality";

        public static int FindAlphaVariant(ComputeShader shader, string baseKernel, bool alphaEnabled)
            => shader.FindKernel(alphaEnabled ? baseKernel + AlphaSuffix : baseKernel);

        public static int FindQualityVariant(ComputeShader shader, string baseKernel, bool highQuality)
            => shader.FindKernel(baseKernel + (highQuality ? HighQualitySuffix : LowQualitySuffix));

        public static int FindQualityAlphaVariant(ComputeShader shader, string baseKernel, bool highQuality,
            bool alphaEnabled)
            => shader.FindKernel(baseKernel + (highQuality ? HighQualitySuffix : LowQualitySuffix) +
                                 (alphaEnabled ? AlphaSuffix : string.Empty));
    }
}
