using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering.RenderGraphModule;

namespace Aetheria.Runtime.Water
{
    internal static class WaterFrameResourceAllocator
    {
        internal static void AllocateGBuffer(
            RenderGraph renderGraph,
            UniversalResourceData resourceData,
            TextureHandle opaqueDepth,
            TextureHandle depthAttachment,
            WaterFrameResources frame)
        {
            if (frame.gBufferResourcesAllocated)
                return;

            frame.opaqueDepth = opaqueDepth;
            TextureDesc depthDesc = renderGraph.GetTextureDesc(depthAttachment);
            TextureDesc waterDepthDesc = depthDesc;
            waterDepthDesc.name = "Water Depth";
            waterDepthDesc.msaaSamples = MSAASamples.None;
            waterDepthDesc.bindTextureMS = false;
            waterDepthDesc.useMipMap = false;
            waterDepthDesc.autoGenerateMips = false;
            waterDepthDesc.enableRandomWrite = false;
            waterDepthDesc.clearBuffer = true;
            waterDepthDesc.clearColor = SystemInfo.usesReversedZBuffer ? Color.clear : Color.white;
            frame.waterDepth = renderGraph.CreateTexture(waterDepthDesc);

            var colorDesc = renderGraph.GetTextureDesc(resourceData.activeColorTexture);
            colorDesc.width = depthDesc.width;
            colorDesc.height = depthDesc.height;
            colorDesc.slices = depthDesc.slices;
            colorDesc.dimension = depthDesc.dimension;
            colorDesc.useDynamicScale = depthDesc.useDynamicScale;
            colorDesc.msaaSamples = MSAASamples.None;
            colorDesc.bindTextureMS = false;
            colorDesc.clearBuffer = true;
            colorDesc.clearColor = Color.clear;
            colorDesc.enableRandomWrite = false;
            colorDesc.depthBufferBits = DepthBits.None;

            var materialDesc = colorDesc;
            materialDesc.name = "Water Material GBuffer";
            materialDesc.clearBuffer = false;
            materialDesc.format = GraphicsFormat.R8G8B8A8_UNorm;
            frame.materialGBuffer = renderGraph.CreateTexture(materialDesc);

            var surfaceDesc = colorDesc;
            surfaceDesc.name = "Water Surface GBuffer";
            surfaceDesc.clearBuffer = false;
            surfaceDesc.format = GraphicsFormat.R8G8B8A8_UNorm;
            frame.surfaceGBuffer = renderGraph.CreateTexture(surfaceDesc);

            var geometryDesc = colorDesc;
            geometryDesc.name = "Water Geometry GBuffer";
            geometryDesc.clearBuffer = false;
            geometryDesc.format = GraphicsFormat.R8G8B8A8_UNorm;
            frame.geometryGBuffer = renderGraph.CreateTexture(geometryDesc);

            var metadataDesc = colorDesc;
            metadataDesc.name = "Water Metadata GBuffer";
            metadataDesc.clearBuffer = false;
            metadataDesc.format = GraphicsFormat.R8G8B8A8_UNorm;
            frame.metadataGBuffer = renderGraph.CreateTexture(metadataDesc);

            frame.gBufferResourcesAllocated = true;
        }

    }
}
