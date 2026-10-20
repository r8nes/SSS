using Aetheria.Runtime.Data;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;

namespace Aetheria.Runtime.Rendering.Passes
{
    internal static class AtmosphereRenderGraphResources
    {
        internal static void UseCoreTextures(
            IRasterRenderGraphBuilder builder,
            AtmosphereFrameData atmosphere,
            bool includeAerialPerspective)
        {
            Use(builder, atmosphere.Transmittance);
            Use(builder, atmosphere.MultiScatter);
            Use(builder, atmosphere.GroundIrradiance);
            Use(builder, atmosphere.SkyView);

            if (includeAerialPerspective)
                Use(builder, atmosphere.AerialPerspective);
            Use(builder, atmosphere.SpaceCubemap);
            Use(builder, atmosphere.CelestialData.CookieAtlas);
        }

        internal static void BindCoreTextures(
            RasterCommandBuffer cmd,
            AtmosphereFrameData atmosphere,
            bool includeAerialPerspective)
        {
            Bind(cmd, atmosphere.Transmittance, LutDefinitions.TransmittanceId);
            Bind(cmd, atmosphere.MultiScatter, LutDefinitions.MultiScatteringId);
            Bind(cmd, atmosphere.GroundIrradiance, LutDefinitions.GroundIrradianceId);
            Bind(cmd, atmosphere.SkyView, LutDefinitions.SkyViewId);

            if (includeAerialPerspective)
                Bind(cmd, atmosphere.AerialPerspective, LutDefinitions.AtmosphericScatteringId);
            Bind(cmd, atmosphere.SpaceCubemap, ShaderProperties.Textures.SpaceCubemapId);
            Bind(cmd, atmosphere.CelestialData.CookieAtlas, ShaderProperties.Celestial.CookieAtlasId);
        }

        internal static void UseAndPublishCoreTextures(
            IRasterRenderGraphBuilder builder,
            AtmosphereFrameData atmosphere,
            bool includeAerialPerspective)
        {
            UseAndPublish(builder, atmosphere.Transmittance, LutDefinitions.TransmittanceId);
            UseAndPublish(builder, atmosphere.MultiScatter, LutDefinitions.MultiScatteringId);
            UseAndPublish(builder, atmosphere.GroundIrradiance, LutDefinitions.GroundIrradianceId);
            UseAndPublish(builder, atmosphere.SkyView, LutDefinitions.SkyViewId);

            if (includeAerialPerspective && atmosphere.AerialPerspective.IsValid())
                UseAndPublish(builder, atmosphere.AerialPerspective, LutDefinitions.AtmosphericScatteringId);
            if (atmosphere.SpaceCubemap.IsValid())
                UseAndPublish(builder, atmosphere.SpaceCubemap, ShaderProperties.Textures.SpaceCubemapId);
            if (atmosphere.CelestialData.CookieAtlas.IsValid())
                UseAndPublish(builder, atmosphere.CelestialData.CookieAtlas, ShaderProperties.Celestial.CookieAtlasId);
        }

        private static void Use(IRasterRenderGraphBuilder builder, TextureHandle texture)
        {
            if (texture.IsValid())
                builder.UseTexture(texture);
        }

        private static void Bind(RasterCommandBuffer cmd, TextureHandle texture, int globalPropertyId)
        {
            if (texture.IsValid())
                cmd.SetGlobalTexture(globalPropertyId, texture);
        }

        private static void UseAndPublish(
            IRasterRenderGraphBuilder builder,
            TextureHandle texture,
            int globalPropertyId)
        {
            builder.UseTexture(texture);
            builder.SetGlobalTextureAfterPass(texture, globalPropertyId);
        }
    }
}
