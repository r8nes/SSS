using System;
using System.Collections.Generic;

namespace UnityEngine.Rendering.Universal
{
    internal enum PostProcessMaterialFamily
    {
        SMAA,
        DepthOfField,
        ScalingSetup,
        EASU,
        FinalPost,
        UberPost
    }

    [Flags]
    internal enum PostProcessMaterialFlags : uint
    {
        None = 0u,
        AlphaOutput = 1u << 0,
        HighQualitySampling = 1u << 1,
        FastSRGBLinearConversion = 1u << 2,
        FXAA = 1u << 3,
        Gamma20 = 1u << 4,
        Gamma20AndHDRInput = 1u << 5,
        PointSampling = 1u << 6,
        RCAS = 1u << 7,
        EasuRcasAndHDRInput = 1u << 8,
        LinearToSRGBConversion = 1u << 9,
        HDRInput = 1u << 10,
        HDREncoding = 1u << 11,
        HDROverlay = 1u << 12,
        FilmGrain = 1u << 13,
        Dithering = 1u << 14,
        Distortion = 1u << 15,
        ChromaticAberration = 1u << 16,
        Bloom = 1u << 17,
        BloomDirt = 1u << 18,
        AutoExposure = 1u << 19,
        ColorGrading = 1u << 20,
        ChromaticAberrationCustomLens = 1u << 21,
        ChromaticAberrationTangential = 1u << 22,
        DistortionPositive = 1u << 23,
        PaniniProjection = 1u << 24,
        Vignette = 1u << 25,
        ScreenSpaceLensFlare = 1u << 26,
        HDRGrading = 1u << 27,
    }

    internal readonly struct PostProcessMaterialKey : IEquatable<PostProcessMaterialKey>
    {
        private readonly int m_SourceMaterialId;
        private readonly PostProcessMaterialFamily m_Family;
        private readonly PostProcessMaterialFlags m_Flags;
        private readonly TonemappingMode m_Tonemapping;
        private readonly AntialiasingQuality m_SmaaQuality;

        internal PostProcessMaterialFamily family => m_Family;
        internal PostProcessMaterialFlags flags => m_Flags;
        internal TonemappingMode tonemapping => m_Tonemapping;
        internal AntialiasingQuality smaaQuality => m_SmaaQuality;

        private PostProcessMaterialKey(int sourceMaterialId, PostProcessMaterialFamily family,
            PostProcessMaterialFlags flags, TonemappingMode tonemapping, AntialiasingQuality smaaQuality)
        {
            m_SourceMaterialId = sourceMaterialId;
            m_Family = family;
            m_Flags = flags;
            m_Tonemapping = tonemapping;
            m_SmaaQuality = smaaQuality;
        }

        internal PostProcessMaterialKey WithSource(Material source) =>
            new PostProcessMaterialKey(source ? source.GetInstanceID() : 0, m_Family, m_Flags, m_Tonemapping, m_SmaaQuality);

        internal static PostProcessMaterialKey SMAA(AntialiasingQuality quality) =>
            new PostProcessMaterialKey(0, PostProcessMaterialFamily.SMAA, PostProcessMaterialFlags.None, default, quality);

        internal static PostProcessMaterialKey DepthOfField(bool alphaOutput, bool highQualitySampling, bool fastSRGBLinearConversion) =>
            new PostProcessMaterialKey(0, PostProcessMaterialFamily.DepthOfField,
                (alphaOutput ? PostProcessMaterialFlags.AlphaOutput : PostProcessMaterialFlags.None) |
                (highQualitySampling ? PostProcessMaterialFlags.HighQualitySampling : PostProcessMaterialFlags.None) |
                (fastSRGBLinearConversion ? PostProcessMaterialFlags.FastSRGBLinearConversion : PostProcessMaterialFlags.None),
                default, default);

        internal static PostProcessMaterialKey ScalingSetup(bool fxaa, bool fsr, HDROutputUtils.Operation hdrOperations, bool alphaOutput) =>
            new PostProcessMaterialKey(0, PostProcessMaterialFamily.ScalingSetup,
                (fxaa ? PostProcessMaterialFlags.FXAA : PostProcessMaterialFlags.None) |
                (fsr ? (hdrOperations.HasFlag(HDROutputUtils.Operation.ColorEncoding) ? PostProcessMaterialFlags.Gamma20AndHDRInput : PostProcessMaterialFlags.Gamma20) : PostProcessMaterialFlags.None) |
                (alphaOutput ? PostProcessMaterialFlags.AlphaOutput : PostProcessMaterialFlags.None),
                default, default);

        internal static PostProcessMaterialKey EASU(bool alphaOutput) =>
            new PostProcessMaterialKey(0, PostProcessMaterialFamily.EASU,
                alphaOutput ? PostProcessMaterialFlags.AlphaOutput : PostProcessMaterialFlags.None,
                default, default);

        internal static PostProcessMaterialKey FinalPost(PostProcessMaterialFlags flags) =>
            new PostProcessMaterialKey(0, PostProcessMaterialFamily.FinalPost, flags, default, default);

        internal static PostProcessMaterialKey UberPost(PostProcessMaterialFlags flags, TonemappingMode tonemapping) =>
            new PostProcessMaterialKey(0, PostProcessMaterialFamily.UberPost, flags, tonemapping, default);

        public bool Equals(PostProcessMaterialKey other) =>
            m_SourceMaterialId == other.m_SourceMaterialId &&
            m_Family == other.m_Family &&
            m_Flags == other.m_Flags &&
            m_Tonemapping == other.m_Tonemapping &&
            m_SmaaQuality == other.m_SmaaQuality;

        public override bool Equals(object obj) => obj is PostProcessMaterialKey other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = m_SourceMaterialId;
                hash = (hash * 397) ^ (int)m_Family;
                hash = (hash * 397) ^ (int)m_Flags;
                hash = (hash * 397) ^ (int)m_Tonemapping;
                hash = (hash * 397) ^ (int)m_SmaaQuality;
                return hash;
            }
        }
    }

    /// <summary>
    /// Bounded cache for immutable post-processing materials with local keyword state baked at creation time.
    ///
    /// The key is intentionally typed, not an arbitrary string[]: every keyword combination must be represented
    /// by a known post-processing family and a small bit field. This prevents accidental unbounded material growth
    /// and makes variant count auditable in the PP stack.
    /// </summary>
    internal sealed class PostProcessMaterialKeywordCache
    {
        private const int k_MaxCachedMaterials = 96;

        private readonly Dictionary<PostProcessMaterialKey, Material> m_Materials = new Dictionary<PostProcessMaterialKey, Material>();
        private readonly List<Material> m_CreatedMaterials = new List<Material>();
        private readonly List<string> m_Keywords = new List<string>(16);

        public Material Get(Material source, PostProcessMaterialKey key)
        {
            if (source == null)
                return null;

            key = key.WithSource(source);
            if (m_Materials.TryGetValue(key, out var material))
                return material;

            if (m_Materials.Count >= k_MaxCachedMaterials)
            {
                Debug.LogError($"Post-processing material keyword cache exceeded {k_MaxCachedMaterials} entries. " +
                    "This indicates a variant explosion in the post-processing stack. Falling back to the source material for this frame.");
                return source;
            }

            material = new Material(source)
            {
                hideFlags = HideFlags.HideAndDontSave,
                name = source.name + " (PostFX Keywords)"
            };

            material.shaderKeywords = Array.Empty<string>();
            BuildKeywords(key, m_Keywords);
            for (int i = 0; i < m_Keywords.Count; i++)
                material.EnableKeyword(m_Keywords[i]);

            m_Materials.Add(key, material);
            m_CreatedMaterials.Add(material);
            return material;
        }

        public void Cleanup()
        {
            for (int i = 0; i < m_CreatedMaterials.Count; i++)
                CoreUtils.Destroy(m_CreatedMaterials[i]);

            m_CreatedMaterials.Clear();
            m_Materials.Clear();
        }

        private static string TonemappingKeyword(TonemappingMode mode)
        {
            switch (mode)
            {
                case TonemappingMode.Neutral: return ShaderKeywordStrings.TonemapNeutral;
                case TonemappingMode.ACES: return ShaderKeywordStrings.TonemapACES;
                case TonemappingMode.ACESFilmic: return ShaderKeywordStrings.TonemapACESFilmic;
                case TonemappingMode.GTSport: return ShaderKeywordStrings.TonemapGTSport;
                case TonemappingMode.GT7: return ShaderKeywordStrings.TonemapGT7;
                default: return null;
            }
        }

        private static void AddFlagKeyword(List<string> keywords, PostProcessMaterialFlags flags,
            PostProcessMaterialFlags flag, string keyword)
        {
            if ((flags & flag) != 0 && !string.IsNullOrEmpty(keyword))
                keywords.Add(keyword);
        }

        private static void BuildKeywords(PostProcessMaterialKey key, List<string> keywords)
        {
            keywords.Clear();
            var flags = key.flags;

            switch (key.family)
            {
                case PostProcessMaterialFamily.SMAA:
                    switch (key.smaaQuality)
                    {
                        case AntialiasingQuality.Low:
                            keywords.Add(ShaderKeywordStrings.SmaaLow);
                            break;
                        case AntialiasingQuality.Medium:
                            keywords.Add(ShaderKeywordStrings.SmaaMedium);
                            break;
                        case AntialiasingQuality.High:
                            keywords.Add(ShaderKeywordStrings.SmaaHigh);
                            break;
                    }
                    break;
                case PostProcessMaterialFamily.DepthOfField:
                    AddFlagKeyword(keywords, flags, PostProcessMaterialFlags.AlphaOutput, ShaderKeywordStrings._ENABLE_ALPHA_OUTPUT);
                    AddFlagKeyword(keywords, flags, PostProcessMaterialFlags.HighQualitySampling, ShaderKeywordStrings.HighQualitySampling);
                    AddFlagKeyword(keywords, flags, PostProcessMaterialFlags.FastSRGBLinearConversion, ShaderKeywordStrings.UseFastSRGBLinearConversion);
                    break;
                case PostProcessMaterialFamily.EASU:
                    AddFlagKeyword(keywords, flags, PostProcessMaterialFlags.AlphaOutput, ShaderKeywordStrings._ENABLE_ALPHA_OUTPUT);
                    break;
                case PostProcessMaterialFamily.ScalingSetup:
                    AddFlagKeyword(keywords, flags, PostProcessMaterialFlags.FXAA, ShaderKeywordStrings.Fxaa);
                    AddFlagKeyword(keywords, flags, PostProcessMaterialFlags.Gamma20, ShaderKeywordStrings.Gamma20);
                    AddFlagKeyword(keywords, flags, PostProcessMaterialFlags.Gamma20AndHDRInput, ShaderKeywordStrings.Gamma20AndHDRInput);
                    AddFlagKeyword(keywords, flags, PostProcessMaterialFlags.AlphaOutput, ShaderKeywordStrings._ENABLE_ALPHA_OUTPUT);
                    break;
                case PostProcessMaterialFamily.FinalPost:
                    AddCommonPresentationKeywords(keywords, flags);
                    AddFlagKeyword(keywords, flags, PostProcessMaterialFlags.PointSampling, ShaderKeywordStrings.PointSampling);
                    AddFlagKeyword(keywords, flags, PostProcessMaterialFlags.RCAS, ShaderKeywordStrings.Rcas);
                    AddFlagKeyword(keywords, flags, PostProcessMaterialFlags.EasuRcasAndHDRInput, ShaderKeywordStrings.EasuRcasAndHDRInput);
                    break;
                case PostProcessMaterialFamily.UberPost:
                    if ((flags & PostProcessMaterialFlags.Distortion) != 0)
                    {
                        keywords.Add((flags & PostProcessMaterialFlags.DistortionPositive) != 0
                            ? ShaderKeywordStrings.DistortionPositive
                            : ShaderKeywordStrings.DistortionNegative);
                    }

                    if ((flags & PostProcessMaterialFlags.ChromaticAberration) != 0)
                    {
                        if ((flags & PostProcessMaterialFlags.ChromaticAberrationTangential) != 0)
                            keywords.Add(ShaderKeywordStrings.ChromaticAberrationTangential);
                        else if ((flags & PostProcessMaterialFlags.ChromaticAberrationCustomLens) != 0)
                            keywords.Add(ShaderKeywordStrings.ChromaticAberrationCustomLens);
                        else
                            keywords.Add(ShaderKeywordStrings.ChromaticAberration);
                    }

                    AddFlagKeyword(keywords, flags, PostProcessMaterialFlags.PaniniProjection, ShaderKeywordStrings.PaniniProjection);
                    AddFlagKeyword(keywords, flags, PostProcessMaterialFlags.Vignette, ShaderKeywordStrings.Vignette);
                    AddFlagKeyword(keywords, flags, PostProcessMaterialFlags.Bloom, ShaderKeywordStrings.Bloom);
                    AddFlagKeyword(keywords, flags, PostProcessMaterialFlags.BloomDirt, ShaderKeywordStrings.BloomDirt);
                    AddFlagKeyword(keywords, flags, PostProcessMaterialFlags.ScreenSpaceLensFlare, "_SCREEN_SPACE_LENS_FLARE");
                    AddFlagKeyword(keywords, flags, PostProcessMaterialFlags.AutoExposure, "_AUTO_EXPOSURE");

                    bool colorGrading = (flags & PostProcessMaterialFlags.ColorGrading) != 0;
                    if (colorGrading)
                    {
                        if ((flags & PostProcessMaterialFlags.HDRGrading) != 0)
                        {
                            keywords.Add(ShaderKeywordStrings.HDRGrading);
                        }
                        else
                        {
                            var tonemap = TonemappingKeyword(key.tonemapping);
                            keywords.Add(string.IsNullOrEmpty(tonemap) ? "_COLOR_GRADING" : tonemap);
                        }
                    }

                    AddCommonPresentationKeywords(keywords, flags);
                    AddFlagKeyword(keywords, flags, PostProcessMaterialFlags.FastSRGBLinearConversion, ShaderKeywordStrings.UseFastSRGBLinearConversion);
                    break;
            }
        }

        private static void AddCommonPresentationKeywords(List<string> keywords, PostProcessMaterialFlags flags)
        {
            AddFlagKeyword(keywords, flags, PostProcessMaterialFlags.FXAA, ShaderKeywordStrings.Fxaa);
            AddFlagKeyword(keywords, flags, PostProcessMaterialFlags.AlphaOutput, ShaderKeywordStrings._ENABLE_ALPHA_OUTPUT);
            AddFlagKeyword(keywords, flags, PostProcessMaterialFlags.LinearToSRGBConversion, ShaderKeywordStrings.LinearToSRGBConversion);
            AddFlagKeyword(keywords, flags, PostProcessMaterialFlags.HDRInput, "HDR_INPUT");
            AddFlagKeyword(keywords, flags, PostProcessMaterialFlags.HDREncoding, "HDR_ENCODING");
            AddFlagKeyword(keywords, flags, PostProcessMaterialFlags.HDROverlay, ShaderKeywordStrings.HDROverlay);
            AddFlagKeyword(keywords, flags, PostProcessMaterialFlags.FilmGrain, ShaderKeywordStrings.FilmGrain);
            AddFlagKeyword(keywords, flags, PostProcessMaterialFlags.Dithering, ShaderKeywordStrings.Dithering);
        }
    }
}
