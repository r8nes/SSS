using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace Aetheria.Runtime.Rendering.Fog.Volumetrics
{
    internal readonly struct VolumetricResources
    {
        public readonly TextureHandle IntegratedFog;

        public VolumetricResources(TextureHandle integratedFog)
        {
            IntegratedFog = integratedFog;
        }

        public static RenderTextureDescriptor CreateFroxelDescriptor(
            VolumetricContext context,
            GraphicsFormat graphicsFormat)
        {
            return new RenderTextureDescriptor(context.FroxelWidth, context.FroxelHeight)
            {
                dimension = TextureDimension.Tex3D,
                volumeDepth = context.FroxelDepth,
                graphicsFormat = graphicsFormat,
                enableRandomWrite = true,
                msaaSamples = 1,
                depthBufferBits = 0,
                depthStencilFormat = GraphicsFormat.None,
                useMipMap = false,
                autoGenerateMips = false,
                sRGB = false
            };
        }

        public static VolumetricResources Create(
            RenderGraph renderGraph,
            VolumetricContext context,
            GraphicsFormat vBufferGraphicsFormat)
        {
            var descriptor = CreateFroxelDescriptor(context, vBufferGraphicsFormat);
            var integratedFog = UniversalRenderer.CreateRenderGraphTexture(
                renderGraph,
                descriptor,
                "VolumetricIntegratedFog",
                false,
                FilterMode.Bilinear);

            return new VolumetricResources(integratedFog);
        }
    }
}
