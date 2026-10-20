using System;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering.RenderGraphModule;

namespace UnityEngine.Rendering.Universal
{
    struct ReflectionProbeManager : IDisposable
    {
        int2 m_Resolution;
        RenderTexture m_AtlasTexture0;
        RenderTexture m_AtlasTexture1;
        RTHandle m_AtlasTexture0Handle;
        BuddyAllocator m_AtlasAllocator;
        Dictionary<int, CachedProbe> m_Cache;
        Dictionary<int, int> m_WarningCache;
        List<int> m_NeedsUpdate;
        List<int> m_NeedsRemove;
        AtlasUpdate[] m_AtlasUpdates;
        Material m_AtlasBlitMaterial;
        MaterialPropertyBlock m_AtlasBlitProperties;
        int m_AtlasBlitPass;

        // Pre-allocated arrays for filling constant buffers
        Vector4[] m_BoxMax;
        Vector4[] m_BoxMin;
        Vector4[] m_ProbePosition;
        Vector4[] m_MipScaleOffset;
        Vector4[] m_Rotations;

        // There is a global max of 7 mips in Unity.
        const int k_MaxMipCount = 7;
        const string k_ReflectionProbeAtlasName = "URP Reflection Probe Atlas";
        static readonly ProfilingSampler s_UpdateAtlasSampler = new ProfilingSampler("Update Reflection Probe Atlas");

        internal static int runtimeMipBias { get; set; }

        unsafe struct CachedProbe
        {
            public uint updateCount;
            public Hash128 imageContentsHash;
            public int size;
            public int mipCount;
            // One for each mip.
            public fixed int dataIndices[k_MaxMipCount];
            public fixed int levels[k_MaxMipCount];
            public Texture texture;
            public int lastUsed;
            public Vector4 hdrData;
        }

        static class ShaderProperties
        {
            public static readonly int BoxMin = Shader.PropertyToID("urp_ReflProbes_BoxMin");
            public static readonly int BoxMax = Shader.PropertyToID("urp_ReflProbes_BoxMax");
            public static readonly int ProbePosition = Shader.PropertyToID("urp_ReflProbes_ProbePosition");
            public static readonly int MipScaleOffset = Shader.PropertyToID("urp_ReflProbes_MipScaleOffset");
            public static readonly int Count = Shader.PropertyToID("urp_ReflProbes_Count");
            public static readonly int Atlas = Shader.PropertyToID("urp_ReflProbes_Atlas");
            public static readonly int Rotation = Shader.PropertyToID("urp_ReflProbes_Rotation");
        }


        static class AtlasBlitShaderProperties
        {
            public static readonly int CubeTexture = Shader.PropertyToID("_BlitCubeTexture");
            public static readonly int MipLevel = Shader.PropertyToID("_BlitMipLevel");
            public static readonly int ScaleBias = Shader.PropertyToID("_BlitScaleBias");
            public static readonly int ScaleBiasRt = Shader.PropertyToID("_BlitScaleBiasRt");
            public static readonly int TextureSize = Shader.PropertyToID("_BlitTextureSize");
            public static readonly int PaddingSize = Shader.PropertyToID("_BlitPaddingSize");
            public static readonly int DecodeInstructions = Shader.PropertyToID("_BlitDecodeInstructions");
        }

        public RenderTexture atlasRT => m_AtlasTexture0;
        public RTHandle atlasRTHandle => m_AtlasTexture0Handle;

        public static ReflectionProbeManager Create()
        {
            var instance = new ReflectionProbeManager();
            instance.Init();
            return instance;
        }

        void Init()
        {
            var maxProbes = UniversalRenderPipeline.maxVisibleReflectionProbes;

            // m_Resolution = math.min((int)reflectionProbeResolution, SystemInfo.maxTextureSize);
            m_Resolution = 1;
            var format = GraphicsFormat.B10G11R11_UFloatPack32;
            if (!SystemInfo.IsFormatSupported(format, GraphicsFormatUsage.Render)) { format = GraphicsFormat.R16G16B16A16_SFloat; }
            m_AtlasTexture0 = new RenderTexture(new RenderTextureDescriptor
            {
                width = m_Resolution.x,
                height = m_Resolution.y,
                volumeDepth = 1,
                dimension = TextureDimension.Tex2D,
                graphicsFormat = format,
                useMipMap = false,
                msaaSamples = 1
            });
            m_AtlasTexture0.name = k_ReflectionProbeAtlasName;
            m_AtlasTexture0.filterMode = FilterMode.Bilinear;
            m_AtlasTexture0.hideFlags = HideFlags.HideAndDontSave;
            m_AtlasTexture0.Create();
            m_AtlasTexture0Handle = RTHandles.Alloc(m_AtlasTexture0, transferOwnership: false);

            m_AtlasTexture1 = new RenderTexture(m_AtlasTexture0.descriptor);
            m_AtlasTexture1.name = k_ReflectionProbeAtlasName;
            m_AtlasTexture1.filterMode = FilterMode.Bilinear;
            m_AtlasTexture1.hideFlags = HideFlags.HideAndDontSave;

            // The smallest allocatable resolution we want is 4x4. We calculate the number of levels as:
            // log2(max) - log2(4) = log2(max) - 2
            m_AtlasAllocator = new BuddyAllocator(math.floorlog2(SystemInfo.maxTextureSize) - 2, 2);
            m_Cache = new Dictionary<int, CachedProbe>(maxProbes);
            m_WarningCache = new Dictionary<int, int>(maxProbes);
            m_NeedsUpdate = new List<int>(maxProbes);
            m_NeedsRemove = new List<int>(maxProbes);
            m_AtlasUpdates = new AtlasUpdate[maxProbes * k_MaxMipCount];
            m_AtlasBlitProperties = new MaterialPropertyBlock();
            m_AtlasBlitPass = -1;

            m_BoxMax = new Vector4[maxProbes];
            m_BoxMin = new Vector4[maxProbes];
            m_ProbePosition = new Vector4[maxProbes];
            m_MipScaleOffset = new Vector4[maxProbes * 7];
            m_Rotations = new Vector4[maxProbes];
        }

        private struct AtlasUpdate
        {
            public Texture texture;
            public Vector2 textureSize;
            public Vector4 scaleBias;
            public int sourceMip;
            public Vector4 hdrData;
        }

        private class AtlasUpdatePassData
        {
            public AtlasUpdate[] updates;
            public int updateCount;
            public Material material;
            public MaterialPropertyBlock properties;
            public int passIndex;
        }

        private int m_VisibleProbeCount;

        private unsafe void PrepareGpuData(ref CullingResults cullResults)
        {
            var probes = cullResults.visibleReflectionProbes;
            var probeCount = math.min(probes.Length, UniversalRenderPipeline.maxVisibleReflectionProbes);
            var frameIndex = Time.renderedFrameCount;

            foreach (var (id, cachedProbe) in m_Cache)
            {
                int expectedSize = cachedProbe.texture
                    ? math.max(4, cachedProbe.texture.width >> runtimeMipBias)
                    : 0;
                if (Math.Abs(cachedProbe.lastUsed - frameIndex) > 1 ||
                    !cachedProbe.texture ||
                    cachedProbe.size != expectedSize)
                {
                    m_NeedsRemove.Add(id);
                    for (var i = 0; i < k_MaxMipCount; i++)
                    {
                        if (cachedProbe.dataIndices[i] != -1)
                            m_AtlasAllocator.Free(new BuddyAllocation(cachedProbe.levels[i], cachedProbe.dataIndices[i]));
                    }
                }
            }

            foreach (var probeIndex in m_NeedsRemove)
                m_Cache.Remove(probeIndex);
            m_NeedsRemove.Clear();

            foreach (var (id, lastUsed) in m_WarningCache)
            {
                if (Math.Abs(lastUsed - frameIndex) > 1)
                    m_NeedsRemove.Add(id);
            }

            foreach (var probeIndex in m_NeedsRemove)
                m_WarningCache.Remove(probeIndex);
            m_NeedsRemove.Clear();

            var showFullWarning = false;
            var requiredAtlasSize = math.int2(0, 0);

            for (var probeIndex = 0; probeIndex < probeCount; probeIndex++)
            {
                var probe = probes[probeIndex];
                var texture = probe.texture;
                var id = probe.reflectionProbe.GetInstanceID();
                var wasCached = m_Cache.TryGetValue(id, out var cachedProbe);

                if (!texture)
                    continue;

                if (!wasCached)
                {
                    cachedProbe.size = math.max(4, texture.width >> runtimeMipBias);
                    var mipCount = math.ceillog2(cachedProbe.size * 4) + 1;
                    var level = m_AtlasAllocator.levelCount + 2 - mipCount;
                    cachedProbe.mipCount = math.min(mipCount, k_MaxMipCount);
                    cachedProbe.texture = texture;

                    var mip = 0;
                    for (; mip < cachedProbe.mipCount; mip++)
                    {
                        var mipLevel = math.min(level + mip, m_AtlasAllocator.levelCount - 1);
                        if (!m_AtlasAllocator.TryAllocate(mipLevel, out var allocation))
                            break;

                        cachedProbe.levels[mip] = allocation.level;
                        cachedProbe.dataIndices[mip] = allocation.index;
                        var scaleOffset = (int4)(GetScaleOffset(mipLevel, allocation.index, true, false) * m_Resolution.xyxy);
                        requiredAtlasSize = math.max(requiredAtlasSize, scaleOffset.zw + scaleOffset.xy);
                    }

                    if (mip < cachedProbe.mipCount)
                    {
                        if (!m_WarningCache.ContainsKey(id))
                            showFullWarning = true;
                        m_WarningCache[id] = frameIndex;

                        for (var i = 0; i < mip; i++)
                            m_AtlasAllocator.Free(new BuddyAllocation(cachedProbe.levels[i], cachedProbe.dataIndices[i]));
                        for (var i = 0; i < k_MaxMipCount; i++)
                            cachedProbe.dataIndices[i] = -1;
                        continue;
                    }

                    for (; mip < k_MaxMipCount; mip++)
                        cachedProbe.dataIndices[mip] = -1;
                }

                var needsUpdate = !wasCached || cachedProbe.updateCount != texture.updateCount;
#if UNITY_EDITOR
                needsUpdate |= cachedProbe.imageContentsHash != texture.imageContentsHash;
#endif
                needsUpdate |= cachedProbe.hdrData != probe.hdrData;

                if (needsUpdate)
                {
                    cachedProbe.updateCount = texture.updateCount;
#if UNITY_EDITOR
                    cachedProbe.imageContentsHash = texture.imageContentsHash;
#endif
                    m_NeedsUpdate.Add(id);
                }

                if (probe.reflectionProbe.mode == ReflectionProbeMode.Realtime && probe.reflectionProbe.refreshMode == ReflectionProbeRefreshMode.EveryFrame)
                    cachedProbe.lastUsed = -1;
                else
                    cachedProbe.lastUsed = frameIndex;

                cachedProbe.hdrData = probe.hdrData;
                m_Cache[id] = cachedProbe;
            }

            if (math.any(m_Resolution < requiredAtlasSize))
                GrowAtlas(math.max(m_Resolution, math.ceilpow2(requiredAtlasSize)));

            var skipCount = 0;
            for (var probeIndex = 0; probeIndex < probeCount; probeIndex++)
            {
                var probe = probes[probeIndex];
                var id = probe.reflectionProbe.GetInstanceID();
                var dataIndex = probeIndex - skipCount;
                if (!m_Cache.TryGetValue(id, out var cachedProbe) || !probe.texture)
                {
                    skipCount++;
                    continue;
                }

                m_BoxMax[dataIndex] = new Vector4(probe.bounds.max.x, probe.bounds.max.y, probe.bounds.max.z, probe.blendDistance);
                m_BoxMin[dataIndex] = new Vector4(probe.bounds.min.x, probe.bounds.min.y, probe.bounds.min.z, probe.importance);
                m_ProbePosition[dataIndex] = new Vector4(
                    probe.localToWorldMatrix.m03,
                    probe.localToWorldMatrix.m13,
                    probe.localToWorldMatrix.m23,
                    (probe.isBoxProjection ? 1 : -1) * cachedProbe.mipCount);

                for (var i = 0; i < cachedProbe.mipCount; i++)
                    m_MipScaleOffset[dataIndex * k_MaxMipCount + i] = GetScaleOffset(cachedProbe.levels[i], cachedProbe.dataIndices[i], false, false);

                var rot = Quaternion.Inverse(probe.reflectionProbe.transform.rotation);
                m_Rotations[dataIndex] = new Vector4(rot.x, rot.y, rot.z, rot.w);
            }

            m_VisibleProbeCount = probeCount - skipCount;

            if (showFullWarning)
                Debug.LogWarning("A number of reflection probes have been skipped due to the reflection probe atlas being full.\nTo fix this, you can decrease the number or resolution of probes.");
        }

        private void GrowAtlas(int2 requiredAtlasSize)
        {
            m_AtlasTexture1.width = requiredAtlasSize.x;
            m_AtlasTexture1.height = requiredAtlasSize.y;
            m_AtlasTexture1.Create();

            if (m_AtlasTexture0.width != 1)
            {
                if (SystemInfo.copyTextureSupport != CopyTextureSupport.None)
                    Graphics.CopyTexture(m_AtlasTexture0, 0, 0, 0, 0, m_Resolution.x, m_Resolution.y, m_AtlasTexture1, 0, 0, 0, 0);
                else
                    Graphics.Blit(m_AtlasTexture0, m_AtlasTexture1, (float2)m_Resolution / requiredAtlasSize, Vector2.zero);
            }

            m_AtlasTexture0Handle?.Release();
            m_AtlasTexture0.Release();
            (m_AtlasTexture0, m_AtlasTexture1) = (m_AtlasTexture1, m_AtlasTexture0);
            m_AtlasTexture0Handle = RTHandles.Alloc(m_AtlasTexture0, transferOwnership: false);
            m_Resolution = requiredAtlasSize;
        }

        private unsafe int BuildAtlasUpdates()
        {
            if (m_NeedsUpdate.Count == 0)
                return 0;

            var updateIndex = 0;
            foreach (var probeId in m_NeedsUpdate)
            {
                var cachedProbe = m_Cache[probeId];
                for (var mip = 0; mip < cachedProbe.mipCount; mip++)
                {
                    var level = cachedProbe.levels[mip];
                    var dataIndex = cachedProbe.dataIndices[mip];
                    var scaleBias = GetScaleOffset(level, dataIndex, true, !SystemInfo.graphicsUVStartsAtTop);
                    var sizeWithoutPadding = (1 << (m_AtlasAllocator.levelCount + 1 - level)) - 2;
                    var sourceMip = math.min(mip + runtimeMipBias, cachedProbe.texture.mipmapCount - 1);

                    m_AtlasUpdates[updateIndex++] = new AtlasUpdate
                    {
                        texture = cachedProbe.texture,
                        textureSize = new Vector2(sizeWithoutPadding, sizeWithoutPadding),
                        scaleBias = scaleBias,
                        sourceMip = sourceMip,
                        hdrData = cachedProbe.hdrData
                    };
                }
            }

            m_NeedsUpdate.Clear();
            return updateIndex;
        }

        private void EnsureAtlasBlitMaterial()
        {
            if (m_AtlasBlitMaterial != null)
                return;

            var blitMaterial = Blitter.GetBlitMaterial(TextureDimension.Cube);
            m_AtlasBlitMaterial = new Material(blitMaterial)
            {
                hideFlags = HideFlags.HideAndDontSave
            };
            m_AtlasBlitMaterial.EnableKeyword("BLIT_DECODE_HDR");
            m_AtlasBlitPass = m_AtlasBlitMaterial.FindPass("BilinearCubeToOctahedralPadding");
        }

        private static void RenderAtlasUpdates(
            RasterCommandBuffer cmd,
            AtlasUpdate[] updates,
            int updateCount,
            Material material,
            MaterialPropertyBlock properties,
            int passIndex)
        {
            for (var i = 0; i < updateCount; i++)
            {
                ref readonly var update = ref updates[i];
                properties.Clear();
                properties.SetTexture(AtlasBlitShaderProperties.CubeTexture, update.texture);
                properties.SetFloat(AtlasBlitShaderProperties.MipLevel, update.sourceMip);
                properties.SetVector(AtlasBlitShaderProperties.ScaleBias, new Vector4(1, 1, 0, 0));
                properties.SetVector(AtlasBlitShaderProperties.ScaleBiasRt, update.scaleBias);
                properties.SetVector(AtlasBlitShaderProperties.TextureSize, update.textureSize);
                properties.SetInt(AtlasBlitShaderProperties.PaddingSize, 2);
                properties.SetVector(AtlasBlitShaderProperties.DecodeInstructions, update.hdrData);
                cmd.DrawProcedural(Matrix4x4.identity, material, passIndex, MeshTopology.Quads, 4, 1, properties);
            }
        }

        private static void RenderAtlasUpdates(CommandBuffer cmd, AtlasUpdate[] updates, int updateCount)
        {
            for (var i = 0; i < updateCount; i++)
            {
                ref readonly var update = ref updates[i];
                Blitter.BlitCubeToOctahedral2DQuadWithPadding(
                    cmd,
                    update.texture,
                    update.textureSize,
                    update.scaleBias,
                    update.sourceMip,
                    true,
                    2,
                    update.hdrData);
            }
        }

        public TextureHandle RecordRenderGraphAtlasUpdate(RenderGraph renderGraph, ref CullingResults cullResults)
        {
            PrepareGpuData(ref cullResults);
            var updateCount = BuildAtlasUpdates();
            if (updateCount == 0)
                return TextureHandle.nullHandle;

            EnsureAtlasBlitMaterial();

            var atlas = renderGraph.ImportTexture(m_AtlasTexture0Handle);
            using (var builder = renderGraph.AddRasterRenderPass<AtlasUpdatePassData>(
                       s_UpdateAtlasSampler.name,
                       out var passData,
                       s_UpdateAtlasSampler))
            {
                passData.updates = m_AtlasUpdates;
                passData.updateCount = updateCount;
                passData.material = m_AtlasBlitMaterial;
                passData.properties = m_AtlasBlitProperties;
                passData.passIndex = m_AtlasBlitPass;
                builder.SetRenderAttachment(atlas, 0, AccessFlags.ReadWrite);
                builder.SetRenderFunc(static (AtlasUpdatePassData data, RasterGraphContext context) =>
                {
                    RenderAtlasUpdates(context.cmd, data.updates, data.updateCount, data.material, data.properties, data.passIndex);
                });
            }

            return atlas;
        }

        public void BindGlobalData(UnsafeCommandBuffer cmd)
        {
            cmd.SetGlobalFloat(ShaderProperties.Count, m_VisibleProbeCount);
            cmd.SetGlobalTexture(ShaderProperties.Atlas, m_AtlasTexture0);
            if (m_VisibleProbeCount == 0)
                return;

            cmd.SetGlobalVectorArray(ShaderProperties.BoxMin, m_BoxMin);
            cmd.SetGlobalVectorArray(ShaderProperties.BoxMax, m_BoxMax);
            cmd.SetGlobalVectorArray(ShaderProperties.ProbePosition, m_ProbePosition);
            cmd.SetGlobalVectorArray(ShaderProperties.MipScaleOffset, m_MipScaleOffset);
            cmd.SetGlobalVectorArray(ShaderProperties.Rotation, m_Rotations);
        }

        public unsafe void UpdateGpuData(CommandBuffer cmd, ref CullingResults cullResults)
        {
            PrepareGpuData(ref cullResults);
            var updateCount = BuildAtlasUpdates();

            if (updateCount != 0)
            {
                using (new ProfilingScope(cmd, s_UpdateAtlasSampler))
                {
                    cmd.SetRenderTarget(m_AtlasTexture0);
                    RenderAtlasUpdates(cmd, m_AtlasUpdates, updateCount);
                }
            }

            cmd.SetGlobalFloat(ShaderProperties.Count, m_VisibleProbeCount);
            cmd.SetGlobalTexture(ShaderProperties.Atlas, m_AtlasTexture0);
            if (m_VisibleProbeCount == 0)
                return;

            cmd.SetGlobalVectorArray(ShaderProperties.BoxMin, m_BoxMin);
            cmd.SetGlobalVectorArray(ShaderProperties.BoxMax, m_BoxMax);
            cmd.SetGlobalVectorArray(ShaderProperties.ProbePosition, m_ProbePosition);
            cmd.SetGlobalVectorArray(ShaderProperties.MipScaleOffset, m_MipScaleOffset);
            cmd.SetGlobalVectorArray(ShaderProperties.Rotation, m_Rotations);
        }

        float4 GetScaleOffset(int level, int dataIndex, bool includePadding, bool yflip)
        {
            // level = m_AtlasAllocator.levelCount + 2 - (log2(size) + 1) <=>
            // log2(size) + 1 = m_AtlasAllocator.levelCount + 2 - level <=>
            // log2(size) = m_AtlasAllocator.levelCount + 1 - level <=>
            // size = 2^(m_AtlasAllocator.levelCount + 1 - level)
            var size = (1 << (m_AtlasAllocator.levelCount + 1 - level));
            var coordinate = SpaceFillingCurves.DecodeMorton2D((uint)dataIndex);
            var scale = (size - (includePadding ? 0 : 2)) / ((float2)m_Resolution);
            var bias = ((float2) coordinate * size + (includePadding ? 0 : 1)) / (m_Resolution);
            if (yflip) bias.y = 1.0f - bias.y - scale.y;
            return math.float4(scale, bias);
        }

        public void Dispose()
        {
            m_AtlasTexture0Handle?.Release();
            if (m_AtlasTexture0)
                m_AtlasTexture0.Release();
            if (m_AtlasTexture1)
                m_AtlasTexture1.Release();

            m_AtlasAllocator.Dispose();

            CoreUtils.Destroy(m_AtlasBlitMaterial);
            m_AtlasBlitMaterial = null;
            m_AtlasBlitProperties = null;

            Object.DestroyImmediate(m_AtlasTexture0);
            Object.DestroyImmediate(m_AtlasTexture1);

            this = default;
        }
    }
}
