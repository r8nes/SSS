namespace UnityEngine.Rendering.Universal
{
    internal static class PostProcessConstants
    {
        // Bloom
        public const int k_MaxPyramidSize = 16;

        // DoF
        public const int k_GaussianDoFPassDownscalePrefilter = 0;
        public const int k_GaussianDoFPassBlurH = 1;
        public const int k_GaussianDoFPassBlurV = 2;
        public const int k_GaussianDoFPassComposite = 3;

        public const int k_BokehDoFPassDownscalePrefilter = 0;
        public const int k_BokehDoFPassBlur = 1;
        public const int k_BokehDoFPassPostFilter = 2;
        public const int k_BokehDoFPassComposite = 3;
    }
}
