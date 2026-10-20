using UnityEngine.Rendering.RenderGraphModule;

namespace UnityEngine.Rendering.Universal
{
    public sealed class DepthPyramidFrameData : ContextItem
    {
        /// <summary>Packed atlas texture containing all nearest-depth mip levels.</summary>
        public TextureHandle DepthPyramid;

        /// <summary>Optional packed atlas texture containing conservative farthest-depth mip levels for HiZ occlusion culling.</summary>
        public TextureHandle DepthPyramidFarthest;

        /// <summary>True when <see cref="DepthPyramidFarthest"/> was built this frame.</summary>
        public bool HasFarthestPyramid;

        /// <summary>Total number of mip levels in the atlas.</summary>
        public int MipCount;

        /// <summary>Number of mip levels that have a checkerboard variant.</summary>
        public int CheckerboardMipCount;

        /// <summary>Source depth resolution (mip 0).</summary>
        public int Width;
        /// <summary>Source depth resolution (mip 0).</summary>
        public int Height;

        /// <summary>Atlas texture size.</summary>
        public int AtlasWidth;
        /// <summary>Atlas texture size.</summary>
        public int AtlasHeight;

        /// <summary>Pixel offset of each mip level inside the atlas.</summary>
        public Vector2Int[] MipLevelOffsets;
        /// <summary>Pixel offset of each checkerboard mip level inside the atlas.</summary>
        public Vector2Int[] MipLevelOffsetsCheckerboard;
        /// <summary>Pixel size of each mip level.</summary>
        public Vector2Int[] MipLevelSizes;

        /// <summary>GPU buffer containing MipLevelOffsets (int2 array).</summary>
        public GraphicsBuffer MipLevelOffsetsBuffer;
        /// <summary>GPU buffer containing MipLevelOffsetsCheckerboard (int2 array).</summary>
        public GraphicsBuffer MipLevelOffsetsCheckerboardBuffer;

        /// <summary>xy = mip0 size, zw = 1/mip0 size.</summary>
        public Vector4 Mip0Size;
        /// <summary>xy = atlas size, zw = 1/atlas size.</summary>
        public Vector4 AtlasSize;
        /// <summary>Channel swizzle metadata. URP v12 stores HDRP-style single-channel conservative depth, so x and y both resolve to R.</summary>
        public Vector4 ChannelMode;

        /// <summary>True when the pyramid was successfully built this frame.</summary>
        public bool isValid => DepthPyramid.IsValid() && MipCount > 0;

        /// <summary>Returns the pixel offset of the requested mip level inside the atlas.</summary>
        public Vector2Int GetMipOffset(int mip, bool checkerboard = false)
        {
            mip = Mathf.Clamp(mip, 0, Mathf.Max(0, MipCount - 1));
            return checkerboard ? MipLevelOffsetsCheckerboard[mip] : MipLevelOffsets[mip];
        }

        /// <summary>Returns the pixel size of the requested mip level.</summary>
        public Vector2Int GetMipSize(int mip)
        {
            mip = Mathf.Clamp(mip, 0, Mathf.Max(0, MipCount - 1));
            return MipLevelSizes[mip];
        }

        /// <inheritdoc/>
        public override void Reset()
        {
            DepthPyramid = TextureHandle.nullHandle;
            DepthPyramidFarthest = TextureHandle.nullHandle;
            HasFarthestPyramid = false;
            MipCount = 0;
            CheckerboardMipCount = 0;
            Width = Height = 0;
            AtlasWidth = AtlasHeight = 0;
            MipLevelOffsets = null;
            MipLevelOffsetsCheckerboard = null;
            MipLevelSizes = null;
            MipLevelOffsetsBuffer = null;
            MipLevelOffsetsCheckerboardBuffer = null;
            Mip0Size = default;
            AtlasSize = default;
            ChannelMode = default;
        }
    }
}
