using System;

namespace UnityEngine.Rendering.Universal
{
    /// <summary>
    /// A resource container for textures used for <see cref="UniversalRenderPipeline"/>.
    /// </summary>
    /// <remarks>
    /// You cannot edit these resources through the editor's UI; use the API for advanced changes.
    /// Changing this through the API is only allowed in the Editor. In the Player, this raises an error.
    /// </remarks>
    /// <seealso cref="IRenderPipelineResources"/>
    /// <example>
    /// <para> Here is an example of how to get the baked blue noise texture used by URP. </para>
    /// <code>
    /// using UnityEngine.Rendering;
    /// using UnityEngine.Rendering.Universal;
    ///
    /// public static class URPUniversalRendererRuntimeTexturesHelper
    /// {
    ///     public static Texture blueNoise
    ///     {
    ///         get
    ///         {
    ///             var gs = GraphicsSettings.GetRenderPipelineSettings&lt;UniversalRenderPipelineRuntimeTextures&gt;();
    ///             if (gs == null) //not in URP
    ///                 return null;
    ///             return gs.blueNoise64LTex;
    ///         }
    ///     }
    /// }
    /// </code>
    /// </example>
    [Serializable]
    [SupportedOnRenderPipeline(typeof(UniversalRenderPipelineAsset))]
    [Categorization.CategoryInfo(Name = "R: Runtime Textures", Order = 1000), HideInInspector]
    public class UniversalRenderPipelineRuntimeTextures : IRenderPipelineResources
    {
        [SerializeField][HideInInspector] private int m_Version = 1;

        /// <summary>
        /// Current version of the resource container. Used only for upgrading a project.
        /// </summary>
        public int version => m_Version;

        bool IRenderPipelineGraphicsSettings.isAvailableInPlayerBuild => true;

        [SerializeField]
        [ResourcePath("Textures/BlueNoise64/L/LDR_LLL1_0.png")]
        private Texture2D m_BlueNoise64LTex;

        /// <summary>
        /// Pre-baked blue noise textures.
        /// </summary>
        public Texture2D blueNoise64LTex
        {
            get => m_BlueNoise64LTex;
            set => this.SetValueAndNotify(ref m_BlueNoise64LTex, value, nameof(m_BlueNoise64LTex));
        }

        [SerializeField]
        [ResourcePath("Textures/CoherentNoise/OwenScrambledNoise4.png")]
        private Texture2D m_OwenScrambledRGBATex;

        /// <summary>
        /// Owen-scrambled RGBA texture used by diffuse-denoiser point-distribution initialization.
        /// </summary>
        public Texture2D owenScrambledRGBATex
        {
            get => m_OwenScrambledRGBATex;
            set => this.SetValueAndNotify(ref m_OwenScrambledRGBATex, value, nameof(m_OwenScrambledRGBATex));
        }

        [SerializeField]
        [ResourcePath("Textures/CoherentNoise/RankingTile1SPP.png")]
        private Texture2D m_RankingTile1SPP;

        public Texture2D rankingTile1SPP
        {
            get => m_RankingTile1SPP;
            set => this.SetValueAndNotify(ref m_RankingTile1SPP, value, nameof(m_RankingTile1SPP));
        }

        [SerializeField]
        [ResourcePath("Textures/CoherentNoise/RankingTile8SPP.png")]
        private Texture2D m_RankingTile8SPP;

        /// <summary>
        /// BND ranking tile for 8 samples per pixel.
        /// </summary>
        public Texture2D rankingTile8SPP
        {
            get => m_RankingTile8SPP;
            set => this.SetValueAndNotify(ref m_RankingTile8SPP, value, nameof(m_RankingTile8SPP));
        }

        [SerializeField]
        [ResourcePath("Textures/CoherentNoise/RankingTile256SPP.png")]
        private Texture2D m_RankingTile256SPP;

        public Texture2D rankingTile256SPP
        {
            get => m_RankingTile256SPP;
            set => this.SetValueAndNotify(ref m_RankingTile256SPP, value, nameof(m_RankingTile256SPP));
        }

        [SerializeField]
        [ResourcePath("Textures/CoherentNoise/ScramblingTile1SPP.png")]
        private Texture2D m_ScramblingTile1SPP;

        public Texture2D scramblingTile1SPP
        {
            get => m_ScramblingTile1SPP;
            set => this.SetValueAndNotify(ref m_ScramblingTile1SPP, value, nameof(m_ScramblingTile1SPP));
        }

        [SerializeField]
        [ResourcePath("Textures/CoherentNoise/ScramblingTile8SPP.png")]
        private Texture2D m_ScramblingTile8SPP;

        /// <summary>
        /// BND scrambling tile for 8 samples per pixel.
        /// </summary>
        public Texture2D scramblingTile8SPP
        {
            get => m_ScramblingTile8SPP;
            set => this.SetValueAndNotify(ref m_ScramblingTile8SPP, value, nameof(m_ScramblingTile8SPP));
        }

        [SerializeField]
        [ResourcePath("Textures/CoherentNoise/ScramblingTile256SPP.png")]
        private Texture2D m_ScramblingTile256SPP;

        public Texture2D scramblingTile256SPP
        {
            get => m_ScramblingTile256SPP;
            set => this.SetValueAndNotify(ref m_ScramblingTile256SPP, value, nameof(m_ScramblingTile256SPP));
        }

        [SerializeField]
        [ResourcePath("Textures/CoherentNoise/ScrambleNoise.png")]
        private Texture2D m_ScramblingTex;

        /// <summary>
        /// Pixel scrambling texture used by BND sampling.
        /// </summary>
        public Texture2D scramblingTex
        {
            get => m_ScramblingTex;
            set => this.SetValueAndNotify(ref m_ScramblingTex, value, nameof(m_ScramblingTex));
        }
        [SerializeField]
        [ResourcePath("Textures/BayerMatrix.png")]
        private Texture2D m_BayerMatrixTex;

        /// <summary>
        /// Bayer matrix texture.
        /// </summary>
        public Texture2D bayerMatrixTex
        {
            get => m_BayerMatrixTex;
            set => this.SetValueAndNotify(ref m_BayerMatrixTex, value, nameof(m_BayerMatrixTex));
        }

        [SerializeField]
        [ResourcePath("Textures/DebugFont.tga")]
        private Texture2D m_DebugFontTex;

        /// <summary>
        /// Debug font texture.
        /// </summary>
        public Texture2D debugFontTexture
        {
            get => m_DebugFontTex;
            set => this.SetValueAndNotify(ref m_DebugFontTex, value, nameof(m_DebugFontTex));
        }

        private Texture2D m_StencilDitherTex = null;

        /// <summary>
        /// stencil-based lod texture.
        /// </summary>
        public Texture2D stencilDitherTex
        {
            get
            {
                if (!m_StencilDitherTex)
                {
                    m_StencilDitherTex = new Texture2D(width: 2, height: 2, textureFormat: TextureFormat.Alpha8, mipChain: false, linear: true);

                    // keep in sync with StencilDitherMaskSeed.shader
                    m_StencilDitherTex.SetPixel(0, 0, Color.red * 0.25f);
                    m_StencilDitherTex.SetPixel(1, 1, Color.red * 0.5f);
                    m_StencilDitherTex.SetPixel(0, 1, Color.red * 0.75f);
                    m_StencilDitherTex.SetPixel(1, 0, Color.red * 1.0f);
                    m_StencilDitherTex.Apply();
                }
                return m_StencilDitherTex;
            }
        }


    }
}
