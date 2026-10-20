using UnityEngine.Rendering.RenderGraphModule;

namespace UnityEngine.Rendering.Universal
{
    /// <summary>
    /// Minimal common pass data for post-processing fullscreen raster passes.
    ///
    /// Keep this intentionally small. Specialized passes can derive from it and add only the
    /// constants/resources they actually need; this preserves RenderGraph's ability to reason about
    /// dependencies while removing the duplicated source/destination/material/pass-index wiring.
    /// </summary>
    internal abstract class PostProcessFullscreenPassData
    {
        internal TextureHandle sourceTexture;
        internal TextureHandle destinationTexture;
        internal Material material;
        internal int passIndex;
    }

    internal static class PostProcessGraphPasses
    {
        internal static void SetupFullscreenPass<TData>(IRasterRenderGraphBuilder builder, TData passData,
            in TextureHandle source, in TextureHandle destination, Material material, int passIndex,
            AccessFlags sourceAccess = AccessFlags.Read, AccessFlags destinationAccess = AccessFlags.Write)
            where TData : PostProcessFullscreenPassData
        {
            passData.sourceTexture = source;
            passData.destinationTexture = destination;
            passData.material = material;
            passData.passIndex = passIndex;

            builder.UseTexture(source, sourceAccess);
            builder.SetRenderAttachment(destination, 0, destinationAccess);
        }

        internal static void DrawFullscreen<TData>(TData data, RasterGraphContext context)
            where TData : PostProcessFullscreenPassData
        {
            RTHandle source = data.sourceTexture;
            Blitter.BlitTexture(context.cmd, source, PostProcessPassUtils.GetViewportScale(source), data.material, data.passIndex);
        }

        internal static void DrawFullscreenWithSourceSize<TData>(TData data, RasterGraphContext context)
            where TData : PostProcessFullscreenPassData
        {
            PostProcessPassUtils.SetMaterialSourceSize(data.material, data.sourceTexture);
            DrawFullscreen(data, context);
        }
    }
}
