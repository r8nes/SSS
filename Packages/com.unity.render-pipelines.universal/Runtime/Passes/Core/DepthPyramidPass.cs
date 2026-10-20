using System;
using System.Collections.Generic;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering.RenderGraphModule;

namespace UnityEngine.Rendering.Universal
{
    internal sealed class DepthPyramidPass : ScriptableRenderPass
    {
        private const int k_Threads = 8;
        private const GraphicsFormat k_PyramidFormat = GraphicsFormat.R32_SFloat;
        private static readonly ProfilingSampler s_DepthPyramidProfilingSampler = new("DepthPyramid");

        private static readonly int k_DepthAtlasId = Shader.PropertyToID("_DepthAtlas");
        private static readonly int k_DepthAtlasFarthestId = Shader.PropertyToID("_DepthAtlasFarthest");
        private static readonly int k_MinDstCountId = Shader.PropertyToID("_MinDstCount");
        private static readonly int k_CbDstCountId = Shader.PropertyToID("_CbDstCount");
        private static readonly int k_SrcOffsetId = Shader.PropertyToID("_SrcOffset");
        private static readonly int k_SrcLimitId = Shader.PropertyToID("_SrcLimit");
        private static readonly int k_SrcSizeId = Shader.PropertyToID("_SrcSize");
        private static readonly int k_DstSize0Id = Shader.PropertyToID("_DstSize0");
        private static readonly int k_DstSize1Id = Shader.PropertyToID("_DstSize1");
        private static readonly int k_DstSize2Id = Shader.PropertyToID("_DstSize2");
        private static readonly int k_DstSize3Id = Shader.PropertyToID("_DstSize3");
        private static readonly int k_MinDstOffset0Id = Shader.PropertyToID("_MinDstOffset0");
        private static readonly int k_MinDstOffset1Id = Shader.PropertyToID("_MinDstOffset1");
        private static readonly int k_MinDstOffset2Id = Shader.PropertyToID("_MinDstOffset2");
        private static readonly int k_MinDstOffset3Id = Shader.PropertyToID("_MinDstOffset3");
        private static readonly int k_CbDstOffset0Id = Shader.PropertyToID("_CbDstOffset0");
        private static readonly int k_CbDstOffset1Id = Shader.PropertyToID("_CbDstOffset1");
        private readonly int[] m_TmpInt2 = new int[2];

        private readonly ComputeShader m_PyramidCompute;
        private readonly int m_CopyFromSourceKernel;
        private readonly int m_CopyFromSourceFarthestKernel;
        private readonly int m_BuildFromSourceKernel;
        private readonly int m_BuildFromSourceCheckerboardKernel;
        private readonly int m_BuildFromSourceFarthestKernel;
        private readonly int m_BuildKernel;
        private readonly int m_BuildCheckerboardKernel;
        private readonly int m_BuildFarthestKernel;

        // Layout data is immutable once recorded into RenderGraph. Cache it by physical source size and
        // requested chain shape so all consumers of the camera-frame pyramid share the same metadata.
        private readonly Dictionary<LayoutKey, LayoutResources> m_Layouts = new();

        private readonly struct LayoutKey : IEquatable<LayoutKey>
        {
            private readonly int m_Width;
            private readonly int m_Height;
            private readonly int m_CheckerboardMipCount;
            private readonly int m_MaxMipLevelCount;

            public LayoutKey(int width, int height, int checkerboardMipCount, int maxMipLevelCount)
            {
                m_Width = width;
                m_Height = height;
                m_CheckerboardMipCount = checkerboardMipCount;
                m_MaxMipLevelCount = maxMipLevelCount;
            }

            public bool Equals(LayoutKey other) =>
                m_Width == other.m_Width &&
                m_Height == other.m_Height &&
                m_CheckerboardMipCount == other.m_CheckerboardMipCount &&
                m_MaxMipLevelCount == other.m_MaxMipLevelCount;

            public override bool Equals(object obj) => obj is LayoutKey other && Equals(other);

            public override int GetHashCode()
            {
                unchecked
                {
                    int hash = 17;
                    hash = hash * 31 + m_Width;
                    hash = hash * 31 + m_Height;
                    hash = hash * 31 + m_CheckerboardMipCount;
                    hash = hash * 31 + m_MaxMipLevelCount;
                    return hash;
                }
            }
        }

        private sealed class LayoutResources : IDisposable
        {
            public readonly DepthPyramidPackedMipChainInfo PackedInfo;
            public readonly GraphicsBuffer MipLevelOffsetsBuffer;
            public readonly GraphicsBuffer MipLevelOffsetsCheckerboardBuffer;

            public LayoutResources(int width, int height, int checkerboardMipCount, int maxMipLevelCount)
            {
                var packedInfo = new DepthPyramidPackedMipChainInfo();
                packedInfo.AllocateIfNeeded();
                packedInfo.ComputePackedMipChainInfo(new Vector2Int(width, height), checkerboardMipCount,
                    maxMipLevelCount);
                PackedInfo = packedInfo;

                const int count = DepthPyramidPackedMipChainInfo.MaxMipLevels;
                MipLevelOffsetsBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, count, sizeof(int) * 2);
                MipLevelOffsetsCheckerboardBuffer =
                    new GraphicsBuffer(GraphicsBuffer.Target.Structured, count, sizeof(int) * 2);

                var offsets = new Vector2Int[count];
                var checkerboardOffsets = new Vector2Int[count];
                for (int i = 0; i < count; i++)
                {
                    offsets[i] = i < packedInfo.MipLevelCount
                        ? packedInfo.MipLevelOffsets[i]
                        : Vector2Int.zero;
                    checkerboardOffsets[i] = i < packedInfo.MipLevelCountCheckerboard
                        ? packedInfo.MipLevelOffsetsCheckerboard[i]
                        : Vector2Int.zero;
                }

                MipLevelOffsetsBuffer.SetData(offsets);
                MipLevelOffsetsCheckerboardBuffer.SetData(checkerboardOffsets);
            }

            public void Dispose()
            {
                MipLevelOffsetsBuffer?.Release();
                MipLevelOffsetsCheckerboardBuffer?.Release();
            }
        }

        private sealed class BuildPassData
        {
            public DepthPyramidPass Owner;
            public ComputeShader ComputeShader;
            public int CopyKernel;
            public int CopyFarthestKernel;
            public int BuildFromSourceKernel;
            public int BuildFromSourceCheckerboardKernel;
            public int BuildFromSourceFarthestKernel;
            public int Kernel;
            public int CheckerboardKernel;
            public int FarthestKernel;
            public TextureHandle Pyramid;
            public TextureHandle PyramidFarthest;
            public TextureHandle SourceDepth;
            public DepthPyramidPackedMipChainInfo PackedInfo;
            public DepthPyramidHistoryItem HistoryItem;
            public GraphicsBuffer MipLevelOffsetsBuffer;
            public int MipCount;
            public int Width;
            public int Height;
        }

        public DepthPyramidPass(ComputeShader pyramidCompute)
        {
            m_PyramidCompute = pyramidCompute;
            m_CopyFromSourceKernel = pyramidCompute ? pyramidCompute.FindKernel("KDepthCopyFromSource") : -1;
            m_CopyFromSourceFarthestKernel = pyramidCompute ? pyramidCompute.FindKernel("KDepthCopyFromSourceFarthest") : -1;
            m_BuildFromSourceKernel = pyramidCompute ? pyramidCompute.FindKernel("KDepthDownsampleFromSource") : -1;
            m_BuildFromSourceCheckerboardKernel = pyramidCompute ? pyramidCompute.FindKernel("KDepthDownsampleFromSourceCheckerboard") : -1;
            m_BuildFromSourceFarthestKernel = pyramidCompute ? pyramidCompute.FindKernel("KDepthDownsampleFromSourceFarthest") : -1;
            m_BuildKernel = pyramidCompute ? pyramidCompute.FindKernel("KDepthDownsampleDualUav") : -1;
            m_BuildCheckerboardKernel = pyramidCompute ? pyramidCompute.FindKernel("KDepthDownsampleDualUavCheckerboard") : -1;
            m_BuildFarthestKernel = pyramidCompute ? pyramidCompute.FindKernel("KDepthDownsampleDualUavFarthest") : -1;

            ConfigureInput(ScriptableRenderPassInput.Depth);
        }

        public void Dispose()
        {
            foreach (var layout in m_Layouts.Values)
                layout.Dispose();
            m_Layouts.Clear();
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData) =>
            RecordRenderGraph(renderGraph, frameData, DepthPyramidPackedMipChainInfo.MaxMipLevels, 0, false);

        public void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData, int maxMipLevelCount,
            int checkerboardMipCount, bool buildFarthestPyramid)
        {
            if (!m_PyramidCompute ||
                m_CopyFromSourceKernel < 0 || m_CopyFromSourceFarthestKernel < 0 ||
                m_BuildFromSourceKernel < 0 || m_BuildFromSourceCheckerboardKernel < 0 || m_BuildFromSourceFarthestKernel < 0 ||
                m_BuildKernel < 0 || m_BuildCheckerboardKernel < 0 || m_BuildFarthestKernel < 0)
                return;

            var resourceData = frameData.Get<UniversalResourceData>();
            var cameraData = frameData.Get<UniversalCameraData>();
            if (cameraData.xr.enabled)
                return;

            var depthTex = resourceData.cameraDepthTexture;

            if (!depthTex.IsValid())
                return;

            // The packed chain is a property of the physical source texture, not of the active
            // viewport. This mirrors HDRP's currentRenderTargetSize rule.
            var sourceDesc = depthTex.GetDescriptor(renderGraph);
            if (sourceDesc.dimension != TextureDimension.Tex2D)
                return;

            int width = sourceDesc.width > 0 ? sourceDesc.width : cameraData.cameraTargetDescriptor.width;
            int height = sourceDesc.height > 0 ? sourceDesc.height : cameraData.cameraTargetDescriptor.height;

            if (width <= 0 || height <= 0)
                return;

            checkerboardMipCount = Mathf.Clamp(checkerboardMipCount, 0, 2);
            maxMipLevelCount = Mathf.Clamp(maxMipLevelCount, 1, DepthPyramidPackedMipChainInfo.MaxMipLevels);
            var layout = GetOrCreateLayout(width, height, checkerboardMipCount, maxMipLevelCount);
            var packedInfo = layout.PackedInfo;

            int atlasWidth = packedInfo.TextureSize.x;
            int atlasHeight = packedInfo.TextureSize.y;

            var desc = new TextureDesc(atlasWidth, atlasHeight)
            {
                colorFormat = k_PyramidFormat,
                depthBufferBits = DepthBits.None,
                msaaSamples = MSAASamples.None,
                useMipMap = false,
                autoGenerateMips = false,
                enableRandomWrite = true,
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                clearBuffer = false,
                dimension = TextureDimension.Tex2D,
                slices = 1,
                name = "DepthPyramidAtlas"
            };

            var pyramid = renderGraph.CreateTexture(desc);
            var pyramidFarthest = TextureHandle.nullHandle;
            DepthPyramidHistoryItem depthPyramidHistory = null;

            if (buildFarthestPyramid)
            {
                UniversalCameraHistory historyManager = cameraData.historyManager;
                if (historyManager != null)
                {
                    historyManager.RequestAccess<DepthPyramidHistoryItem>();
                    depthPyramidHistory = historyManager.GetHistoryForWrite<DepthPyramidHistoryItem>();
                    if (depthPyramidHistory != null)
                    {
                        depthPyramidHistory.Update(atlasWidth, atlasHeight);
                        RTHandle historyTexture = depthPyramidHistory.GetCurrentTexture();
                        if (historyTexture != null)
                            pyramidFarthest = renderGraph.ImportTexture(historyTexture);
                    }
                }

                if (!pyramidFarthest.IsValid())
                {
                    desc.name = "DepthPyramidFarthestAtlas";
                    pyramidFarthest = renderGraph.CreateTexture(desc);
                    depthPyramidHistory = null;
                }
            }

            var frame = frameData.GetOrCreate<DepthPyramidFrameData>();
            frame.DepthPyramid = pyramid;
            frame.DepthPyramidFarthest = pyramidFarthest;
            frame.HasFarthestPyramid = buildFarthestPyramid && pyramidFarthest.IsValid();
            frame.MipCount = packedInfo.MipLevelCount;
            frame.CheckerboardMipCount = packedInfo.MipLevelCountCheckerboard;
            frame.Width = width;
            frame.Height = height;
            frame.AtlasWidth = atlasWidth;
            frame.AtlasHeight = atlasHeight;
            frame.MipLevelOffsets = packedInfo.MipLevelOffsets;
            frame.MipLevelOffsetsCheckerboard = packedInfo.MipLevelOffsetsCheckerboard;
            frame.MipLevelSizes = packedInfo.MipLevelSizes;
            frame.MipLevelOffsetsBuffer = layout.MipLevelOffsetsBuffer;
            frame.MipLevelOffsetsCheckerboardBuffer = layout.MipLevelOffsetsCheckerboardBuffer;
            frame.Mip0Size = new Vector4(width, height, 1f / width, 1f / height);
            frame.AtlasSize = new Vector4(atlasWidth, atlasHeight, 1f / atlasWidth, 1f / atlasHeight);
            frame.ChannelMode = new Vector4(1f, 1f, 0f, 0f);

            using var profilingScope = new RenderGraphProfilingScope(renderGraph, s_DepthPyramidProfilingSampler);
            using (var builder =
                   renderGraph.AddComputePass<BuildPassData>("DepthPyramid.BuildMips", out var buildData))
            {
                buildData.Owner = this;
                buildData.ComputeShader = m_PyramidCompute;
                buildData.CopyKernel = m_CopyFromSourceKernel;
                buildData.CopyFarthestKernel = m_CopyFromSourceFarthestKernel;
                buildData.BuildFromSourceKernel = m_BuildFromSourceKernel;
                buildData.BuildFromSourceCheckerboardKernel = m_BuildFromSourceCheckerboardKernel;
                buildData.BuildFromSourceFarthestKernel = m_BuildFromSourceFarthestKernel;
                buildData.Kernel = m_BuildKernel;
                buildData.CheckerboardKernel = m_BuildCheckerboardKernel;
                buildData.FarthestKernel = m_BuildFarthestKernel;
                buildData.Pyramid = pyramid;
                buildData.PyramidFarthest = pyramidFarthest;
                buildData.SourceDepth = depthTex;
                buildData.PackedInfo = packedInfo;
                buildData.HistoryItem = depthPyramidHistory;
                buildData.MipLevelOffsetsBuffer = layout.MipLevelOffsetsBuffer;
                buildData.MipCount = packedInfo.MipLevelCount;
                buildData.Width = width;
                buildData.Height = height;

                builder.UseTexture(depthTex, AccessFlags.Read);
                builder.UseTexture(pyramid, packedInfo.MipLevelCount > 1 ? AccessFlags.ReadWrite : AccessFlags.Write);
                if (buildFarthestPyramid)
                    builder.UseTexture(pyramidFarthest,
                        packedInfo.MipLevelCount > 1 ? AccessFlags.ReadWrite : AccessFlags.Write);

                // The history texture is consumed outside this RenderGraph on the next camera frame.
                // Keep the producer when no current-frame RenderGraph pass reads the farthest pyramid.
                if (depthPyramidHistory != null)
                    builder.AllowPassCulling(false);

                builder.SetRenderFunc(static (BuildPassData data, ComputeGraphContext ctx) =>
                    data.Owner.ExecuteBuildPass(data, ctx));
            }
        }

        private LayoutResources GetOrCreateLayout(int width, int height, int checkerboardMipCount,
            int maxMipLevelCount)
        {
            var key = new LayoutKey(width, height, checkerboardMipCount, maxMipLevelCount);
            if (m_Layouts.TryGetValue(key, out var layout))
                return layout;

            layout = new LayoutResources(width, height, checkerboardMipCount, maxMipLevelCount);
            m_Layouts.Add(key, layout);
            return layout;
        }

        private void ExecuteBuildPass(BuildPassData data, ComputeGraphContext ctx)
        {
            var cmd = ctx.cmd;
            var info = data.PackedInfo;
            var shader = data.ComputeShader;
            int[] tmp2 = m_TmpInt2;
            bool buildFarthest = data.PyramidFarthest.IsValid();
            int dstIndex0 = 1;

            if (info.MipLevelCount == 1)
            {
                int copyKernel = buildFarthest ? data.CopyFarthestKernel : data.CopyKernel;
                cmd.SetComputeTextureParam(shader, copyKernel, DepthPyramidShaderIDs.SourceDepthTexture,
                    data.SourceDepth);
                cmd.SetComputeTextureParam(shader, copyKernel, k_DepthAtlasId, data.Pyramid);
                if (buildFarthest)
                    cmd.SetComputeTextureParam(shader, copyKernel, k_DepthAtlasFarthestId, data.PyramidFarthest);

                var sourceSize = info.MipLevelSizes[0];
                SetInt2(cmd, shader, k_SrcSizeId, tmp2, sourceSize.x, sourceSize.y);
                cmd.DispatchCompute(shader, copyKernel, DivUp(sourceSize.x, k_Threads),
                    DivUp(sourceSize.y, k_Threads), 1);

                MarkHistoryProduced(data, buildFarthest);
                return;
            }

            int firstMipCount = Mathf.Min(info.MipLevelCount - dstIndex0, 4);
            int firstCheckerboardCount = GetCheckerboardMipCount(info, dstIndex0, firstMipCount);
            int sourceKernel = buildFarthest
                ? data.BuildFromSourceFarthestKernel
                : firstCheckerboardCount != 0
                    ? data.BuildFromSourceCheckerboardKernel
                    : data.BuildFromSourceKernel;

            SetDispatchParameters(cmd, shader, info, dstIndex0, firstMipCount, firstCheckerboardCount, tmp2);
            var mip0Size = info.MipLevelSizes[0];
            SetInt2(cmd, shader, k_SrcSizeId, tmp2, mip0Size.x, mip0Size.y);
            cmd.SetComputeTextureParam(shader, sourceKernel, DepthPyramidShaderIDs.SourceDepthTexture,
                data.SourceDepth);
            cmd.SetComputeTextureParam(shader, sourceKernel, k_DepthAtlasId, data.Pyramid);
            if (buildFarthest)
                cmd.SetComputeTextureParam(shader, sourceKernel, k_DepthAtlasFarthestId, data.PyramidFarthest);

            var firstMipSize = info.MipLevelSizes[dstIndex0];
            cmd.DispatchCompute(shader, sourceKernel, DivUp(firstMipSize.x, k_Threads),
                DivUp(firstMipSize.y, k_Threads), 1);
            dstIndex0 += firstMipCount;

            while (dstIndex0 < info.MipLevelCount)
            {
                int minCount = Mathf.Min(info.MipLevelCount - dstIndex0, 4);
                int cbCount = GetCheckerboardMipCount(info, dstIndex0, minCount);
                int buildKernel = buildFarthest
                    ? data.FarthestKernel
                    : cbCount != 0
                        ? data.CheckerboardKernel
                        : data.Kernel;

                SetDispatchParameters(cmd, shader, info, dstIndex0, minCount, cbCount, tmp2);
                cmd.SetComputeTextureParam(shader, buildKernel, k_DepthAtlasId, data.Pyramid);
                if (buildFarthest)
                    cmd.SetComputeTextureParam(shader, buildKernel, k_DepthAtlasFarthestId, data.PyramidFarthest);

                var dstSize0 = info.MipLevelSizes[dstIndex0];
                cmd.DispatchCompute(shader, buildKernel, DivUp(dstSize0.x, k_Threads),
                    DivUp(dstSize0.y, k_Threads), 1);
                dstIndex0 += minCount;
            }

            MarkHistoryProduced(data, buildFarthest);
        }

        private static void MarkHistoryProduced(BuildPassData data, bool buildFarthest)
        {
            if (!buildFarthest || data.HistoryItem == null)
                return;

            data.HistoryItem.MarkProduced(
                data.MipLevelOffsetsBuffer,
                data.MipCount,
                data.Width,
                data.Height);
        }

        private static int GetCheckerboardMipCount(in DepthPyramidPackedMipChainInfo info, int dstIndex0,
            int mipCount)
        {
            if (dstIndex0 >= info.MipLevelCountCheckerboard)
                return 0;

            return Mathf.Clamp(info.MipLevelCountCheckerboard - dstIndex0, 0, Mathf.Min(2, mipCount));
        }

        private static void SetDispatchParameters(ComputeCommandBuffer cmd, ComputeShader shader,
            in DepthPyramidPackedMipChainInfo info, int dstIndex0, int minCount, int cbCount, int[] tmp2)
        {
            var srcOffset = info.MipLevelOffsets[dstIndex0 - 1];
            var srcSize = info.MipLevelSizes[dstIndex0 - 1];
            var dstSize0 = info.MipLevelSizes[dstIndex0];
            var dstSize1 = minCount >= 2 ? info.MipLevelSizes[dstIndex0 + 1] : Vector2Int.zero;
            var dstSize2 = minCount >= 3 ? info.MipLevelSizes[dstIndex0 + 2] : Vector2Int.zero;
            var dstSize3 = minCount >= 4 ? info.MipLevelSizes[dstIndex0 + 3] : Vector2Int.zero;
            var dstOff0 = info.MipLevelOffsets[dstIndex0];
            var dstOff1 = minCount >= 2 ? info.MipLevelOffsets[dstIndex0 + 1] : Vector2Int.zero;
            var dstOff2 = minCount >= 3 ? info.MipLevelOffsets[dstIndex0 + 2] : Vector2Int.zero;
            var dstOff3 = minCount >= 4 ? info.MipLevelOffsets[dstIndex0 + 3] : Vector2Int.zero;
            var cbOff0 = cbCount >= 1 ? info.MipLevelOffsetsCheckerboard[dstIndex0] : Vector2Int.zero;
            var cbOff1 = cbCount >= 2 ? info.MipLevelOffsetsCheckerboard[dstIndex0 + 1] : Vector2Int.zero;

            cmd.SetComputeIntParam(shader, k_MinDstCountId, minCount);
            cmd.SetComputeIntParam(shader, k_CbDstCountId, cbCount);

            SetInt2(cmd, shader, k_SrcOffsetId, tmp2, srcOffset.x, srcOffset.y);
            SetInt2(cmd, shader, k_SrcLimitId, tmp2, srcSize.x - 1, srcSize.y - 1);
            SetInt2(cmd, shader, k_DstSize0Id, tmp2, dstSize0.x, dstSize0.y);
            SetInt2(cmd, shader, k_DstSize1Id, tmp2, dstSize1.x, dstSize1.y);
            SetInt2(cmd, shader, k_DstSize2Id, tmp2, dstSize2.x, dstSize2.y);
            SetInt2(cmd, shader, k_DstSize3Id, tmp2, dstSize3.x, dstSize3.y);
            SetInt2(cmd, shader, k_MinDstOffset0Id, tmp2, dstOff0.x, dstOff0.y);
            SetInt2(cmd, shader, k_MinDstOffset1Id, tmp2, dstOff1.x, dstOff1.y);
            SetInt2(cmd, shader, k_MinDstOffset2Id, tmp2, dstOff2.x, dstOff2.y);
            SetInt2(cmd, shader, k_MinDstOffset3Id, tmp2, dstOff3.x, dstOff3.y);
            SetInt2(cmd, shader, k_CbDstOffset0Id, tmp2, cbOff0.x, cbOff0.y);
            SetInt2(cmd, shader, k_CbDstOffset1Id, tmp2, cbOff1.x, cbOff1.y);
        }

        private static void SetInt2(ComputeCommandBuffer cmd, ComputeShader shader, int nameId, int[] tmp, int x, int y)
        {
            tmp[0] = x;
            tmp[1] = y;
            cmd.SetComputeIntParams(shader, nameId, tmp);
        }

        private static int DivUp(int x, int d) => (x + d - 1) / d;
    }
}
