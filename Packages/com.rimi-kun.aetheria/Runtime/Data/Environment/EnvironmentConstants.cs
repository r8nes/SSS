namespace Aetheria.Runtime.Rendering
{
    internal static class EnvironmentConstants
    {
        public const int ConvolutionMipCount = 7;
        public const int ConvolutionMipCountMinusOne = ConvolutionMipCount - 1;
        public const int MinimumConvolutionResolution = 1 << ConvolutionMipCountMinusOne;
    }
}