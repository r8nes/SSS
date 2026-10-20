using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;

namespace Aetheria.Runtime.Utils
{
    internal sealed class RenderGraphDefaultTextures : IDisposable
    {
        private Texture3D _white3D;
        private Texture3D _black3D;
        private Texture2DArray _black2DArray;
        private Cubemap _blackCubemap;
        private RenderGraphTextureHandleCache _white3DHandleCache;
        private RenderGraphTextureHandleCache _black3DHandleCache;
        private RenderGraphTextureHandleCache _black2DArrayHandleCache;
        private RenderGraphTextureHandleCache _blackCubemapHandleCache;

        public TextureHandle ImportWhite3D(RenderGraph renderGraph)
        {
            _white3D ??= CreateTexture3D(Color.white, "Aetheria White 3D");
            return _white3DHandleCache.Import(renderGraph, _white3D);
        }

        public TextureHandle ImportBlack3D(RenderGraph renderGraph)
        {
            _black3D ??= CreateTexture3D(Color.black, "Aetheria Black 3D");
            return _black3DHandleCache.Import(renderGraph, _black3D);
        }

        public TextureHandle ImportBlack2DArray(RenderGraph renderGraph)
        {
            _black2DArray ??= CreateTexture2DArray(Color.black, "Aetheria Black 2D Array");
            return _black2DArrayHandleCache.Import(renderGraph, _black2DArray);
        }

        public TextureHandle ImportBlackCubemap(RenderGraph renderGraph)
        {
            _blackCubemap ??= CreateCubemap(Color.black, "Aetheria Black Cubemap");
            return _blackCubemapHandleCache.Import(renderGraph, _blackCubemap);
        }

        private static Texture3D CreateTexture3D(Color color, string name)
        {
            var texture = new Texture3D(1, 1, 1, TextureFormat.RGBA32, false)
            {
                name = name,
                hideFlags = HideFlags.HideAndDontSave,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Point,
                anisoLevel = 0
            };
            
            texture.SetPixel(0, 0, 0, color);
            texture.Apply(false, true);
            return texture;
        }

        private static Texture2DArray CreateTexture2DArray(Color color, string name)
        {
            var texture = new Texture2DArray(1, 1, 1, TextureFormat.RGBA32, false)
            {
                name = name,
                hideFlags = HideFlags.HideAndDontSave,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Point,
                anisoLevel = 0
            };

            texture.SetPixels(new[] { color }, 0);
            texture.Apply(false, true);
            return texture;
        }

        private static Cubemap CreateCubemap(Color color, string name)
        {
            var texture = new Cubemap(1, TextureFormat.RGBA32, false)
            {
                name = name,
                hideFlags = HideFlags.HideAndDontSave,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Point,
                anisoLevel = 0
            };

            var faceColor = new[] { color };
            for (int face = 0; face < 6; ++face)
                texture.SetPixels(faceColor, (CubemapFace)face);

            texture.Apply(false, true);
            return texture;
        }

        public void Dispose()
        {
            _white3DHandleCache.Dispose();
            _black3DHandleCache.Dispose();
            _black2DArrayHandleCache.Dispose();
            _blackCubemapHandleCache.Dispose();
            
            DestroyTexture(ref _white3D);
            DestroyTexture(ref _black3D);
            DestroyTexture(ref _black2DArray);
            DestroyTexture(ref _blackCubemap);
        }

        private static void DestroyTexture<T>(ref T texture) where T : Texture
        {
            if (texture)
                CoreUtils.Destroy(texture);
            
            texture = null;
        }
    }
}
