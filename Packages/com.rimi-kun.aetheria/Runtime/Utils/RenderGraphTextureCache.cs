using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;

namespace Aetheria.Runtime.Utils
{
    internal struct RenderGraphTextureHandleCache : System.IDisposable
    {
        private RTHandle _handle;
        private Texture _cachedTexture;

        public TextureHandle Import(RenderGraph renderGraph, Texture texture) =>
            RenderGraphTextureCache.ImportExternalTexture(
                renderGraph,
                texture,
                ref _handle,
                ref _cachedTexture);

        public void Dispose() =>
            RenderGraphTextureCache.ReleaseHandle(ref _handle, ref _cachedTexture);
    }

    internal static class RenderGraphTextureCache
    {
        public static TextureHandle ImportExternalTexture(
            RenderGraph renderGraph,
            Texture texture,
            ref RTHandle handle,
            ref Texture cachedTexture)
        {
            if (!texture)
                return TextureHandle.nullHandle;

            if (handle != null && cachedTexture == texture)
                return ImportExternalTexture(renderGraph, texture, handle);

            ReleaseHandle(ref handle, ref cachedTexture);
            handle = AllocateHandle(texture);
            cachedTexture = texture;
            return ImportExternalTexture(renderGraph, texture, handle);
        }

        public static RTHandle AllocateHandle(Texture texture) =>
            texture ? RTHandles.Alloc(texture) : null;

        public static TextureHandle ImportExternalTexture(
            RenderGraph renderGraph,
            Texture texture,
            RTHandle handle)
        {
            if (!texture || handle == null)
                return TextureHandle.nullHandle;

            return handle.rt != null
                ? renderGraph.ImportTexture(handle)
                : renderGraph.ImportTexture(handle, GetTextureInfo(texture));
        }

        public static void ReleaseHandle(ref RTHandle handle)
        {
            if (handle != null)
                RTHandles.Release(handle);

            handle = null;
        }

        public static void ReleaseHandle(ref RTHandle handle, ref Texture cachedTexture)
        {
            ReleaseHandle(ref handle);
            cachedTexture = null;
        }

        private static RenderTargetInfo GetTextureInfo(Texture texture)
        {
            int volumeDepth = texture switch
            {
                Texture2DArray textureArray => textureArray.depth,
                Texture3D texture3D => texture3D.depth,
                _ => 1
            };
            int msaaSamples = texture is RenderTexture renderTexture
                ? Mathf.Max(renderTexture.antiAliasing, 1)
                : 1;

            return new RenderTargetInfo
            {
                width = texture.width,
                height = texture.height,
                volumeDepth = volumeDepth,
                msaaSamples = msaaSamples,
                format = texture.graphicsFormat,
                bindMS = texture is RenderTexture rt && rt.bindTextureMS
            };
        }
    }
}
