using UnityEngine.Rendering.RenderGraphModule;

namespace UnityEngine.Rendering.Universal
{
    /// <summary>
    /// Renderer-wide Gaussian color pyramid generated immediately before a requesting pass.
    /// </summary>
    public sealed class ColorPyramidFrameData : ContextItem
    {
        public TextureHandle Texture;
        public int MipCount;
        public Vector2Int ViewportSize;
        public Vector2Int BufferSize;
        public Vector4 UvScaleAndLimit;

        public bool isValid => Texture.IsValid() && MipCount > 0;

        public override void Reset()
        {
            Texture = TextureHandle.nullHandle;
            MipCount = 0;
            ViewportSize = Vector2Int.zero;
            BufferSize = Vector2Int.zero;
            UvScaleAndLimit = Vector4.zero;
        }
    }
}
