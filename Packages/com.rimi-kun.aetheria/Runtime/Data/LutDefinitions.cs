using Aetheria.Runtime.Rendering;
using Aetheria.Runtime.Utils;
using UnityEngine;
using UnityEngine.Experimental.Rendering;

namespace Aetheria.Runtime.Data
{
    internal static class LutDefinitions
    {
        public static readonly int TransmittanceId = ShaderProperties.Textures.TransmittanceId;
        public static readonly int MultiScatteringId = ShaderProperties.Textures.MultiScatteringId;
        public static readonly int GroundIrradianceId = ShaderProperties.Textures.GroundIrradianceId;
        public static readonly int SkyViewId = ShaderProperties.Textures.SkyViewId;
        public static readonly int AtmosphericScatteringId =
            ShaderProperties.Textures.AtmosphericScatteringId;

        public const int TransmittanceWidth = (int)AtmosphereShaderConfig.TransmittanceLutWidth;
        public const int TransmittanceHeight = (int)AtmosphereShaderConfig.TransmittanceLutHeight;
        public const int ThreadGroupSize = (int)AtmosphereShaderConfig.LutThreadGroupSize;
        public const int MultiScatteringWidth = (int)AtmosphereShaderConfig.MultiScatteringLutWidth;
        public const int MultiScatteringHeight = (int)AtmosphereShaderConfig.MultiScatteringLutHeight;
        public const int MultiScatteringSqrtSampleCount =
            (int)AtmosphereShaderConfig.MultiScatteringSqrtSampleCount;
        public const int MultiScatteringSampleCount =
            MultiScatteringSqrtSampleCount * MultiScatteringSqrtSampleCount;
        public const int MultiScatteringRaymarchSampleCount =
            (int)AtmosphereShaderConfig.MultiScatteringRaymarchSampleCount;
        public const int GroundIrradianceWidth = (int)AtmosphereShaderConfig.GroundIrradianceLutWidth;
        public const int GroundIrradianceHeight = (int)AtmosphereShaderConfig.GroundIrradianceLutHeight;
        public const int GroundIrradianceSampleCount = (int)AtmosphereShaderConfig.GroundIrradianceSampleCount;
        public const int SkyViewWidth = (int)AtmosphereShaderConfig.SkyViewLutWidth;
        public const int SkyViewHeight = (int)AtmosphereShaderConfig.SkyViewLutHeight;
        public const int AtmosphericScatteringWidth =
            (int)AtmosphereShaderConfig.AtmosphericScatteringLutWidth;
        public const int AtmosphericScatteringHeight =
            (int)AtmosphereShaderConfig.AtmosphericScatteringLutHeight;
        public const int AtmosphericScatteringDepth =
            (int)AtmosphereShaderConfig.AtmosphericScatteringLutDepth;

        private const GraphicsFormat CompactHdrFormat = GraphicsFormat.B10G11R11_UFloatPack32;
        private const GraphicsFormat HalfHdrFormat = GraphicsFormat.R16G16B16A16_SFloat;
        private const GraphicsFormat FullHdrFormat = GraphicsFormat.R32G32B32A32_SFloat;

        public static AtmosphereTools.RenderTextureSpec Transmittance =>
            AtmosphereTools.RenderTextureSpec.Lut2D(
                ShaderProperties.Textures.Transmittance,
                TransmittanceWidth,
                TransmittanceHeight,
                SelectStorageFormat(CompactHdrFormat),
                enableRandomWrite: true);

        public static AtmosphereTools.RenderTextureSpec MultiScattering =>
            AtmosphereTools.RenderTextureSpec.Lut2D(
                ShaderProperties.Textures.MultiScattering,
                MultiScatteringWidth,
                MultiScatteringHeight,
                SelectStorageFormat(CompactHdrFormat),
                enableRandomWrite: true);

        public static AtmosphereTools.RenderTextureSpec GroundIrradiance =>
            AtmosphereTools.RenderTextureSpec.Lut2D(
                ShaderProperties.Textures.GroundIrradiance,
                GroundIrradianceWidth,
                GroundIrradianceHeight,
                SelectStorageFormat(CompactHdrFormat),
                enableRandomWrite: true);

        public static AtmosphereTools.RenderTextureSpec SkyView =>
            AtmosphereTools.RenderTextureSpec.Lut2D(
                ShaderProperties.Textures.SkyView,
                SkyViewWidth,
                SkyViewHeight,
                SelectStorageFormat(HalfHdrFormat),
                enableRandomWrite: true,
                wrapU: TextureWrapMode.Repeat);

        public static AtmosphereTools.RenderTextureSpec AtmosphericScattering =>
            AtmosphereTools.RenderTextureSpec.Lut3D(
                ShaderProperties.Textures.AtmosphericScattering,
                AtmosphericScatteringWidth,
                AtmosphericScatteringHeight,
                AtmosphericScatteringDepth,
                SelectStorageFormat(HalfHdrFormat));

        private static GraphicsFormat SelectStorageFormat(GraphicsFormat preferred)
        {
            if (SupportsStorage(preferred))
                return preferred;

            if (preferred != HalfHdrFormat && SupportsStorage(HalfHdrFormat))
                return HalfHdrFormat;

            return FullHdrFormat;
        }

        private static bool SupportsStorage(GraphicsFormat format) =>
            SystemInfo.IsFormatSupported(format, GraphicsFormatUsage.Sample) &&
            SystemInfo.IsFormatSupported(format, GraphicsFormatUsage.LoadStore);
    }
}