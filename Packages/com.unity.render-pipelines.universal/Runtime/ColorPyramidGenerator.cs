using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;

namespace UnityEngine.Rendering.Universal
{
    internal sealed class ColorPyramidGenerator
    {
        const int k_KernelSize = 16;
        const int k_MinimumResolutionGaussian = 8;

        static readonly int k_Source = Shader.PropertyToID("_Source");
        static readonly int k_Destination = Shader.PropertyToID("_Destination");
        static readonly int k_NextSource = Shader.PropertyToID("_NextSource");
        static readonly int k_Size = Shader.PropertyToID("_Size");
        static readonly int k_SourceSize = Shader.PropertyToID("_SourceSize");
        static readonly int k_SourceMip = Shader.PropertyToID("_SourceMip");
        static readonly int k_SrcScaleBias = Shader.PropertyToID("_SrcScaleBias");
        static readonly int k_SrcUvLimits = Shader.PropertyToID("_SrcUvLimits");
        static readonly int k_BlitTexture = Shader.PropertyToID("_BlitTexture");
        static readonly int k_BlitScaleBias = Shader.PropertyToID("_BlitScaleBias");
        static readonly int k_BlitMipLevel = Shader.PropertyToID("_BlitMipLevel");

        readonly ComputeShader m_ColorPyramidCS;
        readonly int m_ColorDownsampleGaussianKernel;
        readonly int m_ColorDownsampleGaussianFastKernel;
        readonly Material m_ColorPyramidPSMat;
        readonly MaterialPropertyBlock m_PropertyBlock;
        RTHandle m_FilterPing;
        RTHandle m_FilterPong;

        internal bool isValid =>
            (m_ColorPyramidCS && m_ColorDownsampleGaussianKernel >= 0) || m_ColorPyramidPSMat != null;

        internal ColorPyramidGenerator(ComputeShader colorPyramidCS, Shader colorPyramidPS)
        {
            m_ColorPyramidCS = colorPyramidCS;
            m_ColorDownsampleGaussianKernel = m_ColorPyramidCS && m_ColorPyramidCS.HasKernel("KColorDownsampleGaussian")
                ? m_ColorPyramidCS.FindKernel("KColorDownsampleGaussian") : -1;
            m_ColorDownsampleGaussianFastKernel = m_ColorPyramidCS && m_ColorPyramidCS.HasKernel("KColorDownsampleGaussianFast")
                ? m_ColorPyramidCS.FindKernel("KColorDownsampleGaussianFast") : -1;
            if (colorPyramidPS)
            {
                m_ColorPyramidPSMat = CoreUtils.CreateEngineMaterial(colorPyramidPS);
            }

            m_PropertyBlock = new MaterialPropertyBlock();
        }

        internal void Dispose()
        {
            CoreUtils.Destroy(m_ColorPyramidPSMat);
            RTHandles.Release(m_FilterPing);
            RTHandles.Release(m_FilterPong);
            m_FilterPing = null;
            m_FilterPong = null;
        }

        internal int RenderColorPyramid(CommandBuffer cmd, Vector2Int size, Texture source, RenderTexture destination, int requestedMipCount = -1)
        {
            if (cmd == null || source == null || destination == null || size.x <= 0 || size.y <= 0 ||
                m_ColorPyramidCS == null || m_ColorDownsampleGaussianKernel < 0 ||
                !SystemInfo.supportsComputeShaders || !SystemInfo.IsFormatSupported(destination.graphicsFormat, GraphicsFormatUsage.LoadStore) ||
                source.dimension != TextureDimension.Tex2D || destination.dimension != TextureDimension.Tex2D)
                return 0;

            int width = Mathf.Clamp(size.x, 1, destination.width);
            int height = Mathf.Clamp(size.y, 1, destination.height);
            int targetMipCount = requestedMipCount <= 0
                ? GetFullMipCount(new Vector2Int(width, height))
                : Mathf.Clamp(requestedMipCount, 1, GetFullMipCount(new Vector2Int(width, height)));
            targetMipCount = Mathf.Min(targetMipCount, Mathf.Max(1, destination.mipmapCount));

            Vector2Int sourcePhysicalSize = new(Mathf.Max(1, source.width), Mathf.Max(1, source.height));
            if (DynamicResolutionHandler.instance.HardwareDynamicResIsEnabled())
                sourcePhysicalSize = DynamicResolutionHandler.instance.ApplyScalesOnSize(sourcePhysicalSize);

            m_PropertyBlock.SetTexture(k_BlitTexture, source);
            m_PropertyBlock.SetVector(k_BlitScaleBias, new Vector4(
                width / (float)Mathf.Max(1, sourcePhysicalSize.x),
                height / (float)Mathf.Max(1, sourcePhysicalSize.y),
                0.0f, 0.0f));
            m_PropertyBlock.SetFloat(k_BlitMipLevel, 0.0f);
            cmd.SetRenderTarget(destination, 0, CubemapFace.Unknown, -1);
            cmd.SetViewport(new Rect(0, 0, width, height));
            cmd.DrawProcedural(Matrix4x4.identity, Blitter.GetBlitMaterial(TextureDimension.Tex2D), 0, MeshTopology.Triangles, 3, 1, m_PropertyBlock);

            if (targetMipCount <= 1)
                return 1;

            int firstMipWidth = Mathf.Max(1, width >> 1);
            int firstMipHeight = Mathf.Max(1, height >> 1);
            EnsureFilterTextures(firstMipWidth, firstMipHeight, destination.graphicsFormat);
            Texture currentSource = source;
            RTHandle nextSource = m_FilterPing;
            int mip = 0;

            while ((width >= k_MinimumResolutionGaussian || height >= k_MinimumResolutionGaussian) && mip + 1 < targetMipCount)
            {
                int nextWidth = Mathf.Max(1, width >> 1);
                int nextHeight = Mathf.Max(1, height >> 1);
                int groupsX = DivUp(nextWidth, k_KernelSize);
                int groupsY = DivUp(nextHeight, k_KernelSize);

                int kernel = mip < 2 && m_ColorDownsampleGaussianFastKernel >= 0
                    ? m_ColorDownsampleGaussianFastKernel
                    : m_ColorDownsampleGaussianKernel;
                int sourceWidth = Mathf.Max(1, currentSource.width);
                int sourceHeight = Mathf.Max(1, currentSource.height);
                cmd.SetComputeVectorParam(m_ColorPyramidCS, k_Size, new Vector4(width, height, nextWidth, nextHeight));
                cmd.SetComputeVectorParam(m_ColorPyramidCS, k_SourceSize, new Vector4(sourceWidth, sourceHeight, 1.0f / sourceWidth, 1.0f / sourceHeight));
                cmd.SetComputeTextureParam(m_ColorPyramidCS, kernel, k_Source, currentSource);
                cmd.SetComputeTextureParam(m_ColorPyramidCS, kernel, k_Destination, destination, mip + 1);
                cmd.SetComputeTextureParam(m_ColorPyramidCS, kernel, k_NextSource, nextSource);
                cmd.DispatchCompute(m_ColorPyramidCS, kernel, groupsX, groupsY, 1);

                currentSource = nextSource.rt;
                nextSource = nextSource == m_FilterPing ? m_FilterPong : m_FilterPing;
                mip++;
                width = nextWidth;
                height = nextHeight;
            }

            return mip + 1;
        }

        void EnsureFilterTextures(int width, int height, GraphicsFormat format)
        {
            bool valid = m_FilterPing != null && m_FilterPing.rt != null &&
                         m_FilterPong != null && m_FilterPong.rt != null &&
                         m_FilterPing.rt.graphicsFormat == format && m_FilterPong.rt.graphicsFormat == format &&
                         m_FilterPing.rt.width >= width && m_FilterPing.rt.height >= height &&
                         m_FilterPong.rt.width >= width && m_FilterPong.rt.height >= height;
            if (valid)
                return;

            int allocationWidth = width;
            int allocationHeight = height;
            if (m_FilterPing != null && m_FilterPing.rt != null && m_FilterPing.rt.graphicsFormat == format)
            {
                allocationWidth = Mathf.Max(allocationWidth, m_FilterPing.rt.width);
                allocationHeight = Mathf.Max(allocationHeight, m_FilterPing.rt.height);
            }
            if (m_FilterPong != null && m_FilterPong.rt != null && m_FilterPong.rt.graphicsFormat == format)
            {
                allocationWidth = Mathf.Max(allocationWidth, m_FilterPong.rt.width);
                allocationHeight = Mathf.Max(allocationHeight, m_FilterPong.rt.height);
            }

            RTHandles.Release(m_FilterPing);
            RTHandles.Release(m_FilterPong);
            m_FilterPing = AllocateFilterTexture(allocationWidth, allocationHeight, format, "Color Pyramid Filter Ping");
            m_FilterPong = AllocateFilterTexture(allocationWidth, allocationHeight, format, "Color Pyramid Filter Pong");
        }

        static RTHandle AllocateFilterTexture(int width, int height, GraphicsFormat format, string name)
        {
            return RTHandles.Alloc(
                width, height,
                dimension: TextureDimension.Tex2D,
                filterMode: FilterMode.Bilinear,
                wrapMode: TextureWrapMode.Clamp,
                colorFormat: format,
                enableRandomWrite: true,
                useMipMap: false,
                autoGenerateMips: false,
                useDynamicScale: false,
                name: name);
        }

        internal static int GetFullMipCount(Vector2Int size)
        {
            int width = Mathf.Max(1, size.x);
            int height = Mathf.Max(1, size.y);
            int mipCount = 1;
            while (width >= k_MinimumResolutionGaussian || height >= k_MinimumResolutionGaussian)
            {
                width = Mathf.Max(1, width >> 1);
                height = Mathf.Max(1, height >> 1);
                mipCount++;
            }
            return mipCount;
        }

        sealed class CopyPassData
        {
            internal TextureHandle source;
            internal float scaleX;
            internal float scaleY;
            internal int width;
            internal int height;
        }

        sealed class ComputePassData
        {
            internal ComputeShader cs;
            internal TextureHandle source;
            internal TextureHandle destination;
            internal TextureHandle nextSource;
            internal int kernel;
            internal int sourceMip;
            internal int destinationMip;
            internal int srcWidth;
            internal int srcHeight;
            internal int sourcePhysicalWidth;
            internal int sourcePhysicalHeight;
            internal int dstWidth;
            internal int dstHeight;
            internal int groupsX;
            internal int groupsY;
        }

        sealed class RasterPassData
        {
            internal Material material;
            internal MaterialPropertyBlock propertyBlock;
            internal TextureHandle source;
            internal int sourceMip;
            internal int dstWidth;
            internal int dstHeight;
            internal float scaleX;
            internal float scaleY;
            internal float sourceWidth;
            internal float sourceHeight;
            internal bool vertical;
        }

        internal int RenderColorGaussianPyramid(RenderGraph renderGraph, Vector2Int size, TextureHandle source,
            TextureHandle destination, int requestedMipCount = -1, bool copyMip0 = true, string passPrefix = "Color Pyramid",
            string[] mipPassNames = null, string filterPingName = null, string filterPongName = null)
        {
            if (!isValid || !source.IsValid() || !destination.IsValid() || size.x <= 0 || size.y <= 0)
                return 0;

            TextureDesc sourceDesc = source.GetDescriptor(renderGraph);
            TextureDesc destinationDesc = destination.GetDescriptor(renderGraph);
            if (sourceDesc.dimension != TextureDimension.Tex2D || destinationDesc.dimension != TextureDimension.Tex2D)
                return 0;
            if (sourceDesc.dimension != destinationDesc.dimension)
                return 0;

            bool hardwareDynamicResolution = DynamicResolutionHandler.instance.HardwareDynamicResIsEnabled();
            Vector2Int sourcePhysicalSize = new(Mathf.Max(1, sourceDesc.width), Mathf.Max(1, sourceDesc.height));
            if (sourceDesc.useDynamicScale && hardwareDynamicResolution)
                sourcePhysicalSize = DynamicResolutionHandler.instance.ApplyScalesOnSize(sourcePhysicalSize);
            Vector2Int destinationPhysicalSize = new(Mathf.Max(1, destinationDesc.width), Mathf.Max(1, destinationDesc.height));
            if (destinationDesc.useDynamicScale && hardwareDynamicResolution)
                destinationPhysicalSize = DynamicResolutionHandler.instance.ApplyScalesOnSize(destinationPhysicalSize);

            int srcWidth = Mathf.Clamp(size.x, 1, destinationPhysicalSize.x);
            int srcHeight = Mathf.Clamp(size.y, 1, destinationPhysicalSize.y);
            int targetMipCount = requestedMipCount <= 0
                ? GetFullMipCount(new Vector2Int(srcWidth, srcHeight))
                : Mathf.Clamp(requestedMipCount, 1, GetFullMipCount(new Vector2Int(srcWidth, srcHeight)));

            if (copyMip0)
                RecordMip0Copy(renderGraph, source, destination, srcWidth, srcHeight, sourcePhysicalSize, $"{passPrefix} - Copy Mip 0");

            if (targetMipCount <= 1)
                return 1;

            bool useCompute = destinationDesc.enableRandomWrite &&
                              m_ColorPyramidCS && m_ColorDownsampleGaussianKernel >= 0 &&
                              SystemInfo.supportsComputeShaders && SystemInfo.IsFormatSupported(destinationDesc.format, GraphicsFormatUsage.LoadStore);
            if (!useCompute && m_ColorPyramidPSMat == null)
                return copyMip0 ? 1 : 0;

            int srcMip = 0;

            if (useCompute)
            {
                int firstMipWidth = Mathf.Max(1, srcWidth >> 1);
                int firstMipHeight = Mathf.Max(1, srcHeight >> 1);
                TextureDesc scratchDesc = new(firstMipWidth, firstMipHeight)
                {
                    name = filterPingName ?? $"{passPrefix} Filter Ping",
                    dimension = TextureDimension.Tex2D,
                    colorFormat = destinationDesc.format,
                    enableRandomWrite = true,
                    useMipMap = false,
                    autoGenerateMips = false,
                    filterMode = FilterMode.Bilinear,
                    wrapMode = TextureWrapMode.Clamp,
                    clearBuffer = false
                };
                TextureHandle filterPing = renderGraph.CreateTexture(scratchDesc);
                scratchDesc.name = filterPongName ?? $"{passPrefix} Filter Pong";
                TextureHandle filterPong = renderGraph.CreateTexture(scratchDesc);
                TextureHandle currentSource = source;
                int currentSourceMip = 0;
                int currentSourcePhysicalWidth = sourcePhysicalSize.x;
                int currentSourcePhysicalHeight = sourcePhysicalSize.y;

                while ((srcWidth >= k_MinimumResolutionGaussian || srcHeight >= k_MinimumResolutionGaussian) && srcMip + 1 < targetMipCount)
                {
                    int dstWidth = Mathf.Max(1, srcWidth >> 1);
                    int dstHeight = Mathf.Max(1, srcHeight >> 1);
                    TextureHandle nextSource = (srcMip & 1) == 0 ? filterPing : filterPong;

                    string mipPassName = mipPassNames != null && srcMip < mipPassNames.Length
                        ? mipPassNames[srcMip]
                        : $"{passPrefix} - Mip {srcMip + 1}";
                    using var builder = renderGraph.AddComputePass<ComputePassData>(mipPassName, out var data);
                    data.cs = m_ColorPyramidCS;
                    data.source = currentSource;
                    data.destination = destination;
                    data.nextSource = nextSource;
                    data.kernel = srcMip < 2 && m_ColorDownsampleGaussianFastKernel >= 0
                        ? m_ColorDownsampleGaussianFastKernel
                        : m_ColorDownsampleGaussianKernel;
                    data.sourceMip = currentSourceMip;
                    data.destinationMip = srcMip + 1;
                    data.srcWidth = srcWidth;
                    data.srcHeight = srcHeight;
                    data.sourcePhysicalWidth = currentSourcePhysicalWidth;
                    data.sourcePhysicalHeight = currentSourcePhysicalHeight;
                    data.dstWidth = dstWidth;
                    data.dstHeight = dstHeight;
                    data.groupsX = DivUp(dstWidth, k_KernelSize);
                    data.groupsY = DivUp(dstHeight, k_KernelSize);

                    if (currentSource.Equals(destination))
                        builder.UseTexture(destination, AccessFlags.ReadWrite);
                    else
                    {
                        builder.UseTexture(currentSource, AccessFlags.Read);
                        builder.UseTexture(destination, AccessFlags.Write);
                    }
                    builder.UseTexture(nextSource, AccessFlags.Write);
                    builder.AllowGlobalStateModification(true);
                    builder.SetRenderFunc(static (ComputePassData data, ComputeGraphContext context) =>
                    {
                        context.cmd.SetComputeVectorParam(data.cs, k_Size,
                            new Vector4(data.srcWidth, data.srcHeight, data.dstWidth, data.dstHeight));
                        context.cmd.SetComputeVectorParam(data.cs, k_SourceSize,
                            new Vector4(data.sourcePhysicalWidth, data.sourcePhysicalHeight,
                                1.0f / data.sourcePhysicalWidth, 1.0f / data.sourcePhysicalHeight));
                        context.cmd.SetComputeTextureParam(data.cs, data.kernel, k_Source, data.source, data.sourceMip);
                        context.cmd.SetComputeTextureParam(data.cs, data.kernel, k_Destination, data.destination, data.destinationMip);
                        context.cmd.SetComputeTextureParam(data.cs, data.kernel, k_NextSource, data.nextSource);
                        context.cmd.DispatchCompute(data.cs, data.kernel, data.groupsX, data.groupsY, 1);
                    });

                    currentSource = nextSource;
                    currentSourceMip = 0;
                    currentSourcePhysicalWidth = firstMipWidth;
                    currentSourcePhysicalHeight = firstMipHeight;
                    srcMip++;
                    srcWidth = dstWidth;
                    srcHeight = dstHeight;
                }

                return srcMip + 1;
            }

            while ((srcWidth >= k_MinimumResolutionGaussian || srcHeight >= k_MinimumResolutionGaussian) && srcMip + 1 < targetMipCount)
            {
                int dstWidth = Mathf.Max(1, srcWidth >> 1);
                int dstHeight = Mathf.Max(1, srcHeight >> 1);
                string mipPassName = mipPassNames != null && srcMip < mipPassNames.Length
                    ? mipPassNames[srcMip]
                    : $"{passPrefix} - Mip {srcMip + 1}";
                TextureDesc downsampleDesc = new(dstWidth, dstHeight)
                {
                    name = filterPingName ?? $"{passPrefix} Downsample {srcMip + 1}",
                    dimension = TextureDimension.Tex2D,
                    colorFormat = destinationDesc.format,
                    enableRandomWrite = false,
                    useMipMap = false,
                    autoGenerateMips = false,
                    filterMode = FilterMode.Bilinear,
                    wrapMode = TextureWrapMode.Clamp,
                    clearBuffer = false
                };
                TextureHandle downsample = renderGraph.CreateTexture(downsampleDesc);
                int sourceMipPhysicalWidth = Mathf.Max(1, destinationPhysicalSize.x >> srcMip);
                int sourceMipPhysicalHeight = Mathf.Max(1, destinationPhysicalSize.y >> srcMip);
                renderGraph.AddBlitPass(destination, downsample,
                    new Vector2(srcWidth / (float)sourceMipPhysicalWidth, srcHeight / (float)sourceMipPhysicalHeight),
                    Vector2.zero, sourceMip: srcMip, passName: mipPassName);

                TextureDesc blurDesc = downsampleDesc;
                blurDesc.name = filterPongName ?? $"{passPrefix} Blur {srcMip + 1}";
                TextureHandle blur = renderGraph.CreateTexture(blurDesc);
                RecordRasterBlur(renderGraph, downsample, blur, 0, dstWidth, dstHeight, false, mipPassName);
                RecordRasterBlur(renderGraph, blur, destination, srcMip + 1, dstWidth, dstHeight, true, mipPassName);

                srcMip++;
                srcWidth = dstWidth;
                srcHeight = dstHeight;
            }

            return srcMip + 1;
        }

        void RecordMip0Copy(RenderGraph renderGraph, TextureHandle source, TextureHandle destination, int width, int height,
            Vector2Int sourcePhysicalSize, string passName)
        {
            using var builder = renderGraph.AddRasterRenderPass<CopyPassData>(passName, out var data);
            data.source = source;
            data.scaleX = width / (float)Mathf.Max(1, sourcePhysicalSize.x);
            data.scaleY = height / (float)Mathf.Max(1, sourcePhysicalSize.y);
            data.width = width;
            data.height = height;
            builder.UseTexture(source, AccessFlags.Read);
            builder.SetRenderAttachment(destination, 0, AccessFlags.Write, 0, -1);
            builder.SetRenderFunc(static (CopyPassData data, RasterGraphContext context) =>
            {
                context.cmd.SetViewport(new Rect(0, 0, data.width, data.height));
                Blitter.BlitTexture(context.cmd, data.source, new Vector4(data.scaleX, data.scaleY, 0.0f, 0.0f), 0, false);
            });
        }

        void RecordRasterBlur(RenderGraph renderGraph, TextureHandle source, TextureHandle destination, int destinationMip,
            int width, int height, bool vertical, string passName)
        {
            using var builder = renderGraph.AddRasterRenderPass<RasterPassData>(passName, out var data);
            data.material = m_ColorPyramidPSMat;
            data.propertyBlock = m_PropertyBlock;
            data.source = source;
            data.sourceMip = 0;
            data.dstWidth = width;
            data.dstHeight = height;
            data.sourceWidth = width;
            data.sourceHeight = height;
            data.scaleX = 1.0f;
            data.scaleY = 1.0f;
            data.vertical = vertical;
            builder.UseTexture(source, AccessFlags.Read);
            builder.SetRenderAttachment(destination, 0, AccessFlags.Write, destinationMip, -1);
            builder.SetRenderFunc(static (RasterPassData data, RasterGraphContext context) =>
            {
                data.propertyBlock.SetTexture(k_Source, data.source);
                data.propertyBlock.SetVector(k_SrcScaleBias, new Vector4(data.scaleX, data.scaleY, 0.0f, 0.0f));
                data.propertyBlock.SetVector(k_SrcUvLimits, new Vector4(
                    (data.dstWidth - 0.5f) / data.sourceWidth,
                    (data.dstHeight - 0.5f) / data.sourceHeight,
                    data.vertical ? 0.0f : 1.0f / data.sourceWidth,
                    data.vertical ? 1.0f / data.sourceHeight : 0.0f));
                data.propertyBlock.SetFloat(k_SourceMip, data.sourceMip);
                context.cmd.SetViewport(new Rect(0, 0, data.dstWidth, data.dstHeight));
                context.cmd.DrawProcedural(Matrix4x4.identity, data.material, 0, MeshTopology.Triangles, 3, 1, data.propertyBlock);
            });
        }

        static int DivUp(int value, int divisor) => (value + divisor - 1) / divisor;
    }
}
