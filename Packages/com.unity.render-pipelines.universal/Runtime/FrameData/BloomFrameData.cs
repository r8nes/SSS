using UnityEngine.Rendering.RenderGraphModule;

namespace UnityEngine.Rendering.Universal
{
    /// <summary>
    /// Frame-scoped bloom resources and parameters produced during post-processing.
    /// </summary>
    public sealed class BloomFrameData : ContextItem
    {
        internal TextureHandle texture = TextureHandle.nullHandle;
        internal bool textureActive;
        internal int mipCount;
        internal Vector4 bicubicParams;
        internal Vector4 resolveInfo;
        internal TextureHandle screenSpaceLensFlareTexture = TextureHandle.nullHandle;
        internal bool screenSpaceLensFlareActive;
        internal BloomPostProcessPass.BloomUberPostParameters uberPostParameters;
        internal readonly TextureHandle[] mipDown = new TextureHandle[PostProcessConstants.k_MaxPyramidSize];
        internal readonly Vector4[] mipInfo = new Vector4[PostProcessConstants.k_MaxPyramidSize];

        internal TextureHandle GetReconstructedMip(int mipIndex) => mipDown[Mathf.Max(0, mipIndex)];

        /// <inheritdoc/>
        public override void Reset()
        {
            texture = TextureHandle.nullHandle;
            textureActive = false;
            mipCount = 0;
            bicubicParams = Vector4.zero;
            resolveInfo = Vector4.zero;
            screenSpaceLensFlareTexture = TextureHandle.nullHandle;
            screenSpaceLensFlareActive = false;
            uberPostParameters = default;

            for (int i = 0; i < PostProcessConstants.k_MaxPyramidSize; i++)
            {
                mipDown[i] = TextureHandle.nullHandle;
                mipInfo[i] = Vector4.zero;
            }
        }
    }
}
