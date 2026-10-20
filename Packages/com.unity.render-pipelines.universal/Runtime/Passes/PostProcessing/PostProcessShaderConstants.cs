namespace UnityEngine.Rendering.Universal
{
    internal static class PostProcessShaderConstants
    {
        public static readonly int _CameraDepthTextureID = Shader.PropertyToID("_CameraDepthTexture");

        public static readonly int _StencilRef = Shader.PropertyToID("_StencilRef");
        public static readonly int _StencilMask = Shader.PropertyToID("_StencilMask");

        public static readonly int _ColorTexture = Shader.PropertyToID("_ColorTexture");
        public static readonly int _Params = Shader.PropertyToID("_Params");
        public static readonly int _Params2 = Shader.PropertyToID("_Params2");

        public static readonly int _FullscreenProjMat = Shader.PropertyToID("_FullscreenProjMat");

        // DoF
        public static readonly int _FullCoCTexture = Shader.PropertyToID("_FullCoCTexture");
        public static readonly int _HalfCoCTexture = Shader.PropertyToID("_HalfCoCTexture");
        public static readonly int _DofTexture = Shader.PropertyToID("_DofTexture");
        public static readonly int _CoCParams = Shader.PropertyToID("_CoCParams");
        public static readonly int _BokehKernel = Shader.PropertyToID("_BokehKernel");
        public static readonly int _BokehConstants = Shader.PropertyToID("_BokehConstants");
        public static readonly int _DownSampleScaleFactor = Shader.PropertyToID("_DownSampleScaleFactor");

        // SMAA
        public static readonly int _Metrics = Shader.PropertyToID("_Metrics");
        public static readonly int _AreaTexture = Shader.PropertyToID("_AreaTexture");
        public static readonly int _SearchTexture = Shader.PropertyToID("_SearchTexture");
        public static readonly int _BlendTexture = Shader.PropertyToID("_BlendTexture");
        //public static readonly int _EdgeTexture = Shader.PropertyToID("_EdgeTexture");

        // Bloom
        public static readonly int _Bloom_Params = Shader.PropertyToID("_Bloom_Params");
        public static readonly int _BloomTint = Shader.PropertyToID("_BloomTint");
        public static readonly int _Bloom_Texture = Shader.PropertyToID("_Bloom_Texture");
        public static readonly int _BloomBicubicParams = Shader.PropertyToID("_BloomBicubicParams");
        public static readonly int _Bloom_TextureScaleBias = Shader.PropertyToID("_Bloom_TextureScaleBias");
        public static readonly int _BloomTexture_TexelSize = Shader.PropertyToID("_BloomTexture_TexelSize");
        public static readonly int _InputTexture = Shader.PropertyToID("_InputTexture");
        public static readonly int _InputLowTexture = Shader.PropertyToID("_InputLowTexture");
        public static readonly int _InputHighTexture = Shader.PropertyToID("_InputHighTexture");
        public static readonly int _InputHighOutputTexture = Shader.PropertyToID("_InputHighOutputTexture");
        public static readonly int _OutputTexture = Shader.PropertyToID("_OutputTexture");
        public static readonly int _BloomTailOutputTexture = Shader.PropertyToID("_BloomTailOutputTexture");
        public static readonly int _BloomTailHighOutputTexture = Shader.PropertyToID("_BloomTailHighOutputTexture");
        public static readonly int _BloomTailSize = Shader.PropertyToID("_BloomTailSize");
        public static readonly int _EnergyTexture = Shader.PropertyToID("_EnergyTexture");
        public static readonly int _TexelSize = Shader.PropertyToID("_TexelSize");
        public static readonly int _SourceSize = Shader.PropertyToID("_SourceSize");
        public static readonly int _BloomThreshold = Shader.PropertyToID("_BloomThreshold");
        public static readonly int _AutoExposureTexture = Shader.PropertyToID("_AutoExposureTexture");
        public static readonly int _LensDirt_Texture = Shader.PropertyToID("_LensDirt_Texture");
        public static readonly int _LensDirt_Params = Shader.PropertyToID("_LensDirt_Params");
        public static readonly int _ScreenSpaceLensFlareTexture = Shader.PropertyToID("_ScreenSpaceLensFlareTexture");
        public static readonly int _ScreenSpaceLensFlareTexture_TexelSize = Shader.PropertyToID("_ScreenSpaceLensFlareTexture_TexelSize");
        public static readonly int _ScreenSpaceLensFlareTextureScaleBias = Shader.PropertyToID("_ScreenSpaceLensFlareTextureScaleBias");

        // Uber
        public static readonly int _PaniniParams = Shader.PropertyToID("_PaniniParams");
        public static readonly int _PaniniEnabled = Shader.PropertyToID("_PaniniEnabled");
        public static readonly int _Distortion_Params1 = Shader.PropertyToID("_Distortion_Params1");
        public static readonly int _Distortion_Params2 = Shader.PropertyToID("_Distortion_Params2");
        public static readonly int _Chroma_Params = Shader.PropertyToID("_Chroma_Params");
        public static readonly int _Chroma_RadialCoefficients = Shader.PropertyToID("_Chroma_RadialCoefficients");
        public static readonly int _Chroma_TangentialCoefficients = Shader.PropertyToID("_Chroma_TangentialCoefficients");
        public static readonly int _Chroma_TapPositions0 = Shader.PropertyToID("_Chroma_TapPositions0");
        public static readonly int _Chroma_TapPositions1 = Shader.PropertyToID("_Chroma_TapPositions1");
        public static readonly int _Vignette_Params1 = Shader.PropertyToID("_Vignette_Params1");
        public static readonly int _Vignette_Params2 = Shader.PropertyToID("_Vignette_Params2");
        public static readonly int _Grain_Texture = Shader.PropertyToID("_Grain_Texture");
        public static readonly int _Grain_Params = Shader.PropertyToID("_Grain_Params");
        public static readonly int _Grain_TilingParams = Shader.PropertyToID("_Grain_TilingParams");
        public static readonly int _BlueNoise_Texture = Shader.PropertyToID("_BlueNoise_Texture");
        public static readonly int _Dithering_Params = Shader.PropertyToID("_Dithering_Params");
        public static readonly int _OverlayUITexture = Shader.PropertyToID("_OverlayUITexture");
        public static readonly int _OffscreenUIViewportParams = Shader.PropertyToID("_OffscreenUIViewportParams");
        public static readonly int _UberPostFeatureFlags = Shader.PropertyToID("_UberPostFeatureFlags");

        // Color Lookup-table
        public static readonly int _InternalLut = Shader.PropertyToID("_InternalLut");
        public static readonly int _Lut_Params = Shader.PropertyToID("_Lut_Params");
        public static readonly int _UserLut = Shader.PropertyToID("_UserLut");
        public static readonly int _UserLut_Params = Shader.PropertyToID("_UserLut_Params");
    }
}
