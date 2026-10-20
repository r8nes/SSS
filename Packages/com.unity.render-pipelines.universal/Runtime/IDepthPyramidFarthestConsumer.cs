namespace UnityEngine.Rendering.Universal
{
    public interface IDepthPyramidFarthestConsumer
    {
        /// <summary>True when this pass requires the farthest-depth pyramid this frame.</summary>
        bool requiresFarthestDepthPyramid { get; }
    }
}