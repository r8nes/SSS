using System;

namespace UnityEngine.Rendering.Universal
{
    internal struct DepthPyramidPackedMipChainInfo
    {
        public const int MaxMipLevels = 15;

        public Vector2Int TextureSize;

        public int MipLevelCount;
        public int MipLevelCountCheckerboard;

        public Vector2Int[] MipLevelSizes;
        public Vector2Int[] MipLevelOffsets;
        public Vector2Int[] MipLevelOffsetsCheckerboard;

        private enum PackDirection
        {
            Right,
            Down
        }

        public void AllocateIfNeeded()
        {
            if (MipLevelSizes is { Length: MaxMipLevels } &&
                MipLevelOffsets is { Length: MaxMipLevels } &&
                MipLevelOffsetsCheckerboard is { Length: MaxMipLevels })
                return;

            MipLevelSizes = new Vector2Int[MaxMipLevels];
            MipLevelOffsets = new Vector2Int[MaxMipLevels];
            MipLevelOffsetsCheckerboard = new Vector2Int[MaxMipLevels];
        }

        private static Vector2Int NextMipBegin(Vector2Int prevBegin, Vector2Int prevSize, PackDirection dir)
        {
            if (dir == PackDirection.Right)
                prevBegin.x += prevSize.x;
            else
                prevBegin.y += prevSize.y;
            return prevBegin;
        }

        public void ComputePackedMipChainInfo(Vector2Int viewportSize, int checkerboardMipCount,
            int maxMipLevelCount = MaxMipLevels)
        {
            AllocateIfNeeded();

            maxMipLevelCount = Mathf.Clamp(maxMipLevelCount, 1, MaxMipLevels);
            checkerboardMipCount = Mathf.Clamp(checkerboardMipCount, 0, 2);

            var atlasSize = viewportSize;
            MipLevelSizes[0] = viewportSize;
            MipLevelOffsets[0] = Vector2Int.zero;
            MipLevelOffsetsCheckerboard[0] = Vector2Int.zero;

            bool hasCheckerboard = checkerboardMipCount != 0;

            int mipLevel = 0;
            var mipSize = viewportSize;

            while ((mipSize.x > 1 || mipSize.y > 1) && mipLevel + 1 < maxMipLevelCount)
            {
                mipLevel++;

                mipSize.x = Math.Max(1, (mipSize.x + 1) >> 1);
                mipSize.y = Math.Max(1, (mipSize.y + 1) >> 1);

                MipLevelSizes[mipLevel] = mipSize;

                var prevMipSize = MipLevelSizes[mipLevel - 1];
                var prevMipBegin = MipLevelOffsets[mipLevel - 1];
                var prevMipBeginCb = MipLevelOffsetsCheckerboard[mipLevel - 1];

                Vector2Int mipBegin, mipBeginCb;

                if (mipLevel == 1)
                {
                    mipBegin = NextMipBegin(prevMipBegin, prevMipSize, PackDirection.Down);
                    mipBeginCb = hasCheckerboard
                        ? NextMipBegin(mipBegin, mipSize, PackDirection.Right)
                        : mipBegin;
                }
                else
                {
                    bool isOdd = (mipLevel & 1) != 0;
                    var dir = isOdd ^ hasCheckerboard ? PackDirection.Down : PackDirection.Right;
                    mipBegin = NextMipBegin(prevMipBegin, prevMipSize, dir);
                    mipBeginCb = NextMipBegin(prevMipBeginCb, prevMipSize, dir);
                }

                MipLevelOffsets[mipLevel] = mipBegin;
                MipLevelOffsetsCheckerboard[mipLevel] = mipBeginCb;

                atlasSize.x = Math.Max(atlasSize.x, mipBegin.x + mipSize.x);
                atlasSize.y = Math.Max(atlasSize.y, mipBegin.y + mipSize.y);
                atlasSize.x = Math.Max(atlasSize.x, mipBeginCb.x + mipSize.x);
                atlasSize.y = Math.Max(atlasSize.y, mipBeginCb.y + mipSize.y);

            }

            TextureSize = atlasSize;
            MipLevelCount = Mathf.Clamp(mipLevel + 1, 1, MaxMipLevels);
            MipLevelCountCheckerboard = hasCheckerboard ? Mathf.Clamp(1 + checkerboardMipCount, 0, MipLevelCount) : 0;
        }
    }
}