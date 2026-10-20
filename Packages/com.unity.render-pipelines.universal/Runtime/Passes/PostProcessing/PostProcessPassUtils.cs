using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using System.Runtime.CompilerServices;

namespace UnityEngine.Rendering.Universal
{
    internal static class PostProcessPassUtils
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static bool IsHDRFormat(GraphicsFormat format)
        {
            return format == GraphicsFormat.B10G11R11_UFloatPack32 ||
                   GraphicsFormatUtility.IsHalfFormat(format) ||
                   GraphicsFormatUtility.IsFloatFormat(format);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static bool IsAlphaFormat(GraphicsFormat format)
        {
            return GraphicsFormatUtility.HasAlphaChannel(format);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]

        internal static Vector2 GetViewportScale(RTHandle sourceTexture)
        {
            return sourceTexture.useScaling
                ? new Vector2(sourceTexture.rtHandleProperties.rtHandleScale.x, sourceTexture.rtHandleProperties.rtHandleScale.y)
                : Vector2.one;
        }

        internal static bool RequireSRGBConversionBlitToBackBuffer(bool requireSrgbConversion, bool enableColorEncodingIfNeeded)
        {
            return requireSrgbConversion && enableColorEncodingIfNeeded;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static bool RequireHDROutput(UniversalCameraData cameraData)
        {
            // If capturing, don't convert to HDR.
            // If not last in the stack, don't convert to HDR.
            return cameraData.isHDROutputActive && cameraData.captureActions == null;
        }

        // Updates render target descriptors to reflect a new render size.
        // Post-processing passes should bind size-dependent shader constants explicitly, not through a global RenderGraph pass.
        internal static void UpdateCameraResolution(RenderGraph renderGraph, UniversalCameraData cameraData, Vector2Int newCameraTargetSize)
        {
            cameraData.cameraTargetDescriptor.width = newCameraTargetSize.x;
            cameraData.cameraTargetDescriptor.height = newCameraTargetSize.y;
        }


        internal static void SetMaterialSourceSize(Material material, RTHandle source)
        {
            if (material == null || source == null || source.rt == null)
                return;

            float width = source.rt.width;
            float height = source.rt.height;
            if (source.rt.useDynamicScale)
            {
                width *= ScalableBufferManager.widthScaleFactor;
                height *= ScalableBufferManager.heightScaleFactor;
            }

            material.SetVector(PostProcessShaderConstants._SourceSize, new Vector4(width, height, 1.0f / width, 1.0f / height));
        }

        internal static void SetMaterialSourceSize(Material material, TextureHandle source)
        {
            SetMaterialSourceSize(material, (RTHandle)source);
        }

        internal static TextureHandle CreateCompatibleTexture(RenderGraph renderGraph, in TextureHandle source, string name, bool clear, FilterMode filterMode)
        {
            var desc = source.GetDescriptor(renderGraph);
            MakeCompatible(ref desc);
            desc.name = name;
            desc.clearBuffer = clear;
            desc.filterMode = filterMode;
            return renderGraph.CreateTexture(desc);
        }

        internal static TextureHandle CreateCompatibleTexture(RenderGraph renderGraph, in TextureDesc desc, string name, bool clear, FilterMode filterMode)
        {
            var descCompatible = GetCompatibleDescriptor(desc);
            descCompatible.name = name;
            descCompatible.clearBuffer = clear;
            descCompatible.filterMode = filterMode;
            return renderGraph.CreateTexture(descCompatible);
        }

        internal static TextureHandle CreateCompatibleDepthStencilTexture(RenderGraph renderGraph, in TextureDesc desc, string name, GraphicsFormat depthStencilFormat, bool clear, FilterMode filterMode)
        {
            var depthDesc = desc;
            depthDesc.name = name;
            depthDesc.format = depthStencilFormat;
            depthDesc.clearBuffer = clear;
            depthDesc.filterMode = filterMode;
            depthDesc.msaaSamples = MSAASamples.None;
            depthDesc.bindTextureMS = false;
            depthDesc.enableRandomWrite = false;
            return renderGraph.CreateTexture(depthDesc);
        }

        internal static TextureDesc GetCompatibleDescriptor(TextureDesc desc, int width, int height, GraphicsFormat format)
        {
            desc.width = width;
            desc.height = height;
            desc.format = format;

            MakeCompatible(ref desc);

            return desc;
        }

        internal static TextureDesc GetCompatibleDescriptor(TextureDesc desc)
        {
            MakeCompatible(ref desc);

            return desc;
        }

        internal static void MakeCompatible(ref TextureDesc desc)
        {
            desc.msaaSamples = MSAASamples.None;
            desc.clearBuffer = false;
            desc.clearColor = Color.black;
            desc.bindTextureMS = false;
            desc.depthBufferBits = DepthBits.None;
        }

        internal static void MakeComputeCompatible(ref TextureDesc desc)
        {
            MakeCompatible(ref desc);
            desc.enableRandomWrite = true;

            if (!SystemInfo.IsFormatSupported(desc.format, GraphicsFormatUsage.LoadStore))
                desc.format = GraphicsFormat.R16G16B16A16_SFloat;
        }

        internal static RenderTextureDescriptor GetCompatibleDescriptor(RenderTextureDescriptor desc, int width, int height, GraphicsFormat format, GraphicsFormat depthStencilFormat = GraphicsFormat.None)
        {
            desc.width = width;
            desc.height = height;
            desc.graphicsFormat = format;
            desc.depthStencilFormat = depthStencilFormat;
            desc.msaaSamples = 1;
            desc.depthBufferBits = (int)DepthBits.None;
            return desc;
        }
    }

    internal static class PostProcessBlitUtils
    {
        static private void ScaleViewport(RasterCommandBuffer cmd, RTHandle dest, UniversalCameraData cameraData, bool isActiveTargetBackBuffer)
        {
            RenderTargetIdentifier cameraTarget = BuiltinRenderTextureType.CameraTarget;
            if (dest.nameID == cameraTarget || cameraData.targetTexture != null)
            {
                if (!isActiveTargetBackBuffer)
                {
                    // Inside the camera stack the target is the shared intermediate target, which can be scaled with render scale.
                    // camera.pixelRect is the viewport of the final target in pixels, so it cannot be used for the intermediate target.
                    // On intermediate target allocation the viewport size is baked into the target size.
                    // Which means the intermediate target does not have a viewport rect. Its offset is always 0 and its size matches viewport size.
                    // The overlay cameras inherit the base viewport, so they cannot have a different viewport,
                    // a necessary limitation since the target covers only the base viewport area.
                    // The offsetting is finally done by the final output viewport-rect to the final target.
                    // Note: effectively this is setting a fullscreen viewport for the intermediate target.
                    var targetWidth = cameraData.cameraTargetDescriptor.width;
                    var targetHeight = cameraData.cameraTargetDescriptor.height;
                    var targetViewportInPixels = new Rect(
                        0,
                        0,
                        targetWidth,
                        targetHeight);
                    cmd.SetViewport(targetViewportInPixels);
                }
                else
                    cmd.SetViewport(cameraData.pixelRect);
            }
        }

        internal static void ScaleViewportAndBlit(in RasterGraphContext context, in TextureHandle source, in TextureHandle destination, UniversalCameraData cameraData, Material material, bool isActiveTargetBackBuffer, int shaderPass = 0)
        {
            var scaleBias = RenderingUtils.GetFinalBlitScaleBias(in context, in source, in destination);
            ScaleViewport(context.cmd, destination, cameraData, isActiveTargetBackBuffer);

            Blitter.BlitTexture(context.cmd, source, scaleBias, material, shaderPass);
        }



    }

}
