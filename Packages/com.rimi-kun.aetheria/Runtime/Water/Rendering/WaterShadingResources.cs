using Aetheria.Runtime.Rendering;
using Aetheria.Runtime.Utils;
using UnityEngine;
using UnityEngine.Rendering.RenderGraphModule;
using System;

namespace Aetheria.Runtime.Water
{
    internal sealed class WaterShadingResources
    {
        private RenderGraphTextureHandleCache _causticsTextureCache;
        private RenderGraphTextureHandleCache _causticsDistortionTextureCache;
        private readonly RenderGraphDefaultTextures _defaultTextures = new();

        internal ComputeShader deferredShader { get; private set; }
        internal Texture2D causticsTexture { get; private set; }
        internal Texture2D causticsDistortionTexture { get; private set; }
        internal int deferredLightingKernel { get; private set; } = -1;
        internal int deferredLightingRefractionKernel { get; private set; } = -1;

        internal bool Initialize(WaterResources resources)
        {
            deferredShader = resources.DeferredLighting;
            causticsTexture = resources.Caustics;
            causticsDistortionTexture = resources.CausticsDistortion;
            deferredLightingKernel = FindKernel(deferredShader, "WaterDeferredLighting");
            deferredLightingRefractionKernel = FindKernel(deferredShader, "WaterDeferredLightingRefraction");
            return IsValid();
        }

        internal TextureHandle ImportCaustics(RenderGraph renderGraph) =>
            _causticsTextureCache.Import(renderGraph, causticsTexture);

        internal TextureHandle ImportCausticsDistortion(RenderGraph renderGraph) =>
            _causticsDistortionTextureCache.Import(renderGraph, causticsDistortionTexture);

        internal TextureHandle ImportBlack3D(RenderGraph renderGraph) =>
            _defaultTextures.ImportBlack3D(renderGraph);

        internal TextureHandle ImportBlackCubemap(RenderGraph renderGraph) =>
            _defaultTextures.ImportBlackCubemap(renderGraph);

        internal bool IsValid() =>
            deferredShader != null
            && causticsTexture && causticsDistortionTexture
            && deferredLightingKernel >= 0
            && deferredLightingRefractionKernel >= 0;

        internal void Cleanup()
        {
            deferredShader = null;
            _causticsTextureCache.Dispose();
            _causticsDistortionTextureCache.Dispose();
            _defaultTextures.Dispose();
            causticsTexture = null;
            causticsDistortionTexture = null;
            deferredLightingKernel = -1;
            deferredLightingRefractionKernel = -1;
        }

        private static int FindKernel(ComputeShader computeShader, string kernelName)
        {
            if (computeShader == null)
                return -1;
            try
            {
                return computeShader.FindKernel(kernelName);
            }
            catch (ArgumentException)
            {
                return -1;
            }
        }
    }
}
