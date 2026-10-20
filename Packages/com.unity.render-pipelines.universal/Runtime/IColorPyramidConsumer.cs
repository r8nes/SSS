namespace UnityEngine.Rendering.Universal
{
    internal interface IColorPyramidConsumer
    {
        bool requiresHistoryColorPyramid { get; }
        int requiredHistoryColorPyramidMipCount { get; }
        bool requiresCurrentColorPyramid { get; }
        int requiredCurrentColorPyramidMipCount { get; }
    }
}
