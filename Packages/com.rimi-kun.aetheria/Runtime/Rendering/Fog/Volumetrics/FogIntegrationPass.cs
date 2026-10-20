using Aetheria.Runtime.Utils;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;

namespace Aetheria.Runtime.Rendering.Fog.Volumetrics
{
    internal sealed class FogIntegrationPass : System.IDisposable
    {
        private static readonly AetheriaProfiler.Marker ClusterClearProfile =
            AetheriaProfiler.CreateMarker("Fog/Volumetrics", "Clear Media Clusters");

        private static readonly AetheriaProfiler.Marker ClusterBinProfile =
            AetheriaProfiler.CreateMarker("Fog/Volumetrics", "Bin Local Media");

        private static readonly AetheriaProfiler.Marker ClusterClassifyProfile =
            AetheriaProfiler.CreateMarker("Fog/Volumetrics", "Classify Media Columns");

        private static readonly AetheriaProfiler.Marker IntegrationProfile =
            AetheriaProfiler.CreateMarker("Fog/Volumetrics", "Integrate");

        private static readonly AetheriaProfiler.Marker FilteringProfile =
            AetheriaProfiler.CreateMarker("Fog/Volumetrics", "Gaussian Filter");

        private const int ClusterThreadGroupSize = 64;
        private const int BinThreadGroupSize = 8;
        private const int MediaTileStrideInUInts = 6;
        private const int DispatchCategoryCount = 3;
        private const int DispatchArgsStrideInUInts = 3;

        private readonly ComputeShader _clusterShader;
        private readonly int _clearClustersKernel;
        private readonly int _binMediaKernel;
        private readonly int _classifyColumnsKernel;
        private readonly ComputeShader _shader;
        private readonly int _globalOnlyKernel;
        private readonly int _emptyTilesKernel;
        private readonly int _localMediaKernel;
        private readonly int _texturedLocalMediaKernel;
        private readonly ComputeShader _filterShader;
        private readonly int _filterKernel;
        private readonly RenderGraphDefaultTextures _defaultTextures = new();
        private GraphicsBuffer _fallbackAmbientProbeBuffer;

        private readonly struct ClusterResources
        {
            public readonly BufferHandle MediaTiles;
            public readonly BufferHandle DispatchTiles;
            public readonly BufferHandle IndirectArgs;
            public readonly Vector4 GridParams;
            public readonly int TileCountXY;

            public ClusterResources(
                BufferHandle mediaTiles,
                BufferHandle dispatchTiles,
                BufferHandle indirectArgs,
                Vector4 gridParams,
                int tileCountXY)
            {
                MediaTiles = mediaTiles;
                DispatchTiles = dispatchTiles;
                IndirectArgs = indirectArgs;
                GridParams = gridParams;
                TileCountXY = tileCountXY;
            }
        }

        private sealed class ClearClustersPassData
        {
            public ComputeShader Shader;
            public int Kernel;
            public BufferHandle MediaTiles;
            public BufferHandle IndirectArgs;
            public Vector4 GridParams;
            public int Groups;
        }

        private sealed class BinMediaPassData
        {
            public ComputeShader Shader;
            public int Kernel;
            public VolumetricContext Context;
            public BufferHandle MediaTiles;
            public BufferHandle VolumeBounds;
            public BufferHandle FogMaskBounds;
            public Vector4 GridParams;
            public int GroupsX;
            public int GroupsY;
            public int VolumeCount;
            public int FogMaskCount;
        }

        private sealed class ClassifyColumnsPassData
        {
            public ComputeShader Shader;
            public int Kernel;
            public BufferHandle MediaTiles;
            public BufferHandle DispatchTiles;
            public BufferHandle IndirectArgs;
            public Vector4 GridParams;
            public int Groups;
        }

        private sealed class IntegrationPassData
        {
            public ComputeShader Shader;
            public int GlobalOnlyKernel;
            public int EmptyTilesKernel;
            public int LocalMediaKernel;
            public int TexturedLocalMediaKernel;
            public VolumetricContext Context;
            public VolumetricFogVolumeData FogData;
            public TextureHandle CameraDepth;
            public TextureHandle FarthestDepthPyramid;
            public Vector4 FarthestDepthMipParams;
            public int FarthestDepthDilationWidth;
            public TextureHandle IntegratedFog;
            public BufferHandle PreviousAmbientProbe;
            public BufferHandle TargetAmbientProbe;
            public Vector4 AmbientProbeL0;
            public float AmbientProbeBlendFactor;
            public Vector4 MainLightDirection;
            public Vector4 MainLightColor;
            public bool UseGpuAmbientProbe;
            public TextureHandle MainLightShadowmap;
            public bool MainLightShadowsActive;
            public BufferHandle DispatchTiles;
            public BufferHandle IndirectArgs;
            public BufferHandle MediaTiles;
            public BufferHandle Volumes;
            public BufferHandle FogMasks;
            public Vector4 MediaTileGridParams;
            public int TileCountXY;
            public VolumetricAtlasBinding VolumeMaskAtlas;
            public VolumetricAtlasBinding FogMaskAtlas;
            public bool HasLocalMedia;
            public int GroupsX;
            public int GroupsY;
        }

        private sealed class FilterPassData
        {
            public ComputeShader Shader;
            public int Kernel;
            public TextureHandle Input;
            public TextureHandle Output;
            public Vector4 GridParams;
            public int GroupsX;
            public int GroupsY;
            public int GroupsZ;
        }

        public FogIntegrationPass(
            ComputeShader clusterShader,
            ComputeShader integrationShader,
            ComputeShader filteringShader)
        {
            _clusterShader = clusterShader;
            _clearClustersKernel = clusterShader.FindKernel("ClearVolumetricMediaClusters");
            _binMediaKernel = clusterShader.FindKernel("BinVolumetricMedia");
            _classifyColumnsKernel = clusterShader.FindKernel("ClassifyVolumetricMediaColumns");
            _shader = integrationShader;
            _globalOnlyKernel = integrationShader.FindKernel("IntegrateFogGlobalOnly");
            _emptyTilesKernel = integrationShader.FindKernel("IntegrateFogEmptyTiles");
            _localMediaKernel =
                integrationShader.FindKernel("IntegrateFogLocalMedia");
            _texturedLocalMediaKernel =
                integrationShader.FindKernel("IntegrateFogTexturedLocalMedia");
            _filterShader = filteringShader;
            _filterKernel = filteringShader.FindKernel("FilterVolumetricFog");
        }

        public TextureHandle Record(
            RenderGraph renderGraph,
            VolumetricContext context,
            VolumetricResources resources,
            TextureHandle cameraDepth,
            TextureHandle farthestDepthPyramid,
            Vector2Int farthestDepthMipOffset,
            Vector2Int farthestDepthMipSize,
            int farthestDepthDilationWidth,
            VolumetricFogVolumeData fogData,
            FogMainLightData mainLight,
            Vector4 ambientProbeL0,
            BufferHandle previousAmbientProbeBuffer,
            BufferHandle targetAmbientProbeBuffer,
            float ambientProbeBlendFactor,
            TextureHandle mainLightShadowmap,
            bool mainLightShadowsActive,
            VolumetricVolumeCollector volumes,
            VolumetricFogMaskCollector fogMasks)
        {
            bool useGpuAmbientProbe =
                previousAmbientProbeBuffer.IsValid() &&
                targetAmbientProbeBuffer.IsValid();
            
            if (_fallbackAmbientProbeBuffer == null)
            {
                _fallbackAmbientProbeBuffer =
                    new GraphicsBuffer(GraphicsBuffer.Target.Structured, 27, sizeof(float));
                _fallbackAmbientProbeBuffer.SetData(new float[27]);
            }

            float probeBlendFactor = Mathf.Clamp01(ambientProbeBlendFactor);
            var fallbackAmbientProbe = renderGraph.ImportBuffer(_fallbackAmbientProbeBuffer);
            var boundPreviousAmbientProbe = useGpuAmbientProbe
                ? previousAmbientProbeBuffer
                : fallbackAmbientProbe;
            var boundTargetAmbientProbe = useGpuAmbientProbe
                ? targetAmbientProbeBuffer
                : fallbackAmbientProbe;

            if (useGpuAmbientProbe && probeBlendFactor <= 1.0e-5f)
            {
                boundTargetAmbientProbe = boundPreviousAmbientProbe;
                probeBlendFactor = 0.0f;
            }
            else if (useGpuAmbientProbe && probeBlendFactor >= 0.99999f)
            {
                boundPreviousAmbientProbe = boundTargetAmbientProbe;
                probeBlendFactor = 1.0f;
            }
            bool hasLocalMedia = volumes.Count > 0 || fogMasks.Count > 0;
            var clusters = hasLocalMedia
                ? RecordMediaClustering(renderGraph, context, volumes, fogMasks)
                : default;

            RecordFogIntegration(
                renderGraph,
                context,
                resources,
                cameraDepth,
                farthestDepthPyramid,
                farthestDepthMipOffset,
                farthestDepthMipSize,
                farthestDepthDilationWidth,
                fogData,
                mainLight,
                ambientProbeL0,
                boundPreviousAmbientProbe,
                boundTargetAmbientProbe,
                probeBlendFactor,
                useGpuAmbientProbe,
                mainLightShadowmap,
                mainLightShadowsActive,
                clusters,
                volumes,
                fogMasks,
                hasLocalMedia);

            return context.GaussianFilterEnabled
                ? RecordGaussianFilter(renderGraph, context, resources.IntegratedFog)
                : resources.IntegratedFog;
        }

        private ClusterResources RecordMediaClustering(
            RenderGraph renderGraph,
            VolumetricContext context,
            VolumetricVolumeCollector volumes,
            VolumetricFogMaskCollector fogMasks)
        {
            int tileCountX = CoreUtils.DivRoundUp(
                context.FroxelWidth, VolumetricGridConfig.MediaTileSizeXY);
            int tileCountY = CoreUtils.DivRoundUp(
                context.FroxelHeight, VolumetricGridConfig.MediaTileSizeXY);
            int tileCountZ = CoreUtils.DivRoundUp(
                context.FroxelDepth, VolumetricGridConfig.MediaTileSizeZ);
            
            int tileCountXY = tileCountX * tileCountY;
            int tileCount = tileCountXY * tileCountZ;
            
            var gridParams = new Vector4(tileCountX, tileCountY, tileCountZ, tileCountXY);

            var mediaTiles = renderGraph.CreateBuffer(new BufferDesc(
                tileCount * MediaTileStrideInUInts,
                sizeof(uint),
                GraphicsBuffer.Target.Raw)
            {
                name = "Volumetric Media Tile Masks"
            });
            
            var dispatchTiles = renderGraph.CreateBuffer(new BufferDesc(
                tileCountXY * DispatchCategoryCount,
                sizeof(uint),
                GraphicsBuffer.Target.Structured)
            {
                name = "Volumetric Media Dispatch Tiles"
            });
            
            var indirectArgs = renderGraph.CreateBuffer(new BufferDesc(
                DispatchCategoryCount * DispatchArgsStrideInUInts,
                sizeof(uint),
                GraphicsBuffer.Target.Structured | GraphicsBuffer.Target.IndirectArguments)
            {
                name = "Volumetric Media Indirect Arguments"
            });

            RecordClusterClear(renderGraph, mediaTiles, indirectArgs, gridParams, tileCount);
            RecordMediaBin(
                renderGraph,
                context,
                mediaTiles,
                gridParams,
                tileCountX,
                tileCountY,
                volumes,
                fogMasks);
            RecordColumnClassification(
                renderGraph,
                mediaTiles,
                dispatchTiles,
                indirectArgs,
                gridParams,
                tileCountXY);

            return new ClusterResources(
                mediaTiles,
                dispatchTiles,
                indirectArgs,
                gridParams,
                tileCountXY);
        }

        private void RecordClusterClear(
            RenderGraph renderGraph,
            BufferHandle mediaTiles,
            BufferHandle indirectArgs,
            Vector4 gridParams,
            int tileCount)
        {
            using var profiledPass = ClusterClearProfile.AddComputePass<ClearClustersPassData>(
                renderGraph,
                out var passData);
            
            var builder = profiledPass.Builder;
            passData.Shader = _clusterShader;
            passData.Kernel = _clearClustersKernel;
            passData.MediaTiles = mediaTiles;
            passData.IndirectArgs = indirectArgs;
            passData.GridParams = gridParams;
            passData.Groups = CoreUtils.DivRoundUp(
                Mathf.Max(tileCount, DispatchCategoryCount * DispatchArgsStrideInUInts),
                ClusterThreadGroupSize);
            
            builder.UseBuffer(passData.MediaTiles, AccessFlags.Write);
            builder.UseBuffer(passData.IndirectArgs, AccessFlags.Write);
            
            builder.SetRenderFunc(static (ClearClustersPassData data, ComputeGraphContext context) =>
            {
                context.cmd.SetComputeVectorParam(
                    data.Shader, ShaderProperties.VolumetricFog.MediaTileGridParams, data.GridParams);
                context.cmd.SetComputeBufferParam(
                    data.Shader, data.Kernel, ShaderProperties.VolumetricFog.MediaTileBuffer, data.MediaTiles);
                context.cmd.SetComputeBufferParam(
                    data.Shader, data.Kernel, ShaderProperties.VolumetricFog.MediaIndirectArgs, data.IndirectArgs);
                context.cmd.DispatchCompute(data.Shader, data.Kernel, data.Groups, 1, 1);
            });
        }

        private void RecordMediaBin(
            RenderGraph renderGraph,
            VolumetricContext context,
            BufferHandle mediaTiles,
            Vector4 gridParams,
            int tileCountX,
            int tileCountY,
            VolumetricVolumeCollector volumes,
            VolumetricFogMaskCollector fogMasks)
        {
            using var profiledPass = ClusterBinProfile.AddComputePass<BinMediaPassData>(
                renderGraph,
                out var passData);
            
            var builder = profiledPass.Builder;
            passData.Shader = _clusterShader;
            passData.Kernel = _binMediaKernel;
            passData.Context = context;
            passData.MediaTiles = mediaTiles;
            passData.VolumeBounds = renderGraph.ImportBuffer(volumes.BoundsBuffer);
            passData.FogMaskBounds = renderGraph.ImportBuffer(fogMasks.BoundsBuffer);
            passData.GridParams = gridParams;
            passData.GroupsX = CoreUtils.DivRoundUp(tileCountX, BinThreadGroupSize);
            passData.GroupsY = CoreUtils.DivRoundUp(tileCountY, BinThreadGroupSize);
            passData.VolumeCount = volumes.Count;
            passData.FogMaskCount = fogMasks.Count;
            
            builder.UseBuffer(passData.MediaTiles, AccessFlags.ReadWrite);
            builder.UseBuffer(passData.VolumeBounds);
            builder.UseBuffer(passData.FogMaskBounds);
            
            builder.SetRenderFunc(static (BinMediaPassData data, ComputeGraphContext context) =>
            {
                var writer = ShaderValueWriter.ForCompute(context.cmd, data.Shader);
                VolumetricPassCommon.WriteClusteringContext(ref writer, data.Context);
                
                context.cmd.SetComputeVectorParam(
                    data.Shader, ShaderProperties.VolumetricFog.MediaTileGridParams, data.GridParams);
                context.cmd.SetComputeBufferParam(
                    data.Shader, data.Kernel, ShaderProperties.VolumetricFog.MediaTileBuffer, data.MediaTiles);
                
                DispatchMediaCategory(data, context, data.VolumeBounds, data.VolumeCount, 0);
                DispatchMediaCategory(data, context, data.FogMaskBounds, data.FogMaskCount, 1);
            });
        }

        private static void DispatchMediaCategory(
            BinMediaPassData data,
            ComputeGraphContext context,
            BufferHandle bounds,
            int count,
            int category)
        {
            if (count <= 0) return;

            context.cmd.SetComputeBufferParam(
                data.Shader, data.Kernel, ShaderProperties.VolumetricFog.MediaBoundsBuffer, bounds);
            context.cmd.SetComputeIntParam(
                data.Shader, ShaderProperties.VolumetricFog.MediaCount, count);
            context.cmd.SetComputeIntParam(
                data.Shader, ShaderProperties.VolumetricFog.MediaCategory, category);
            context.cmd.DispatchCompute(
                data.Shader,
                data.Kernel,
                data.GroupsX,
                data.GroupsY,
                count);
        }

        private void RecordColumnClassification(
            RenderGraph renderGraph,
            BufferHandle mediaTiles,
            BufferHandle dispatchTiles,
            BufferHandle indirectArgs,
            Vector4 gridParams,
            int tileCountXY)
        {
            using var profiledPass = ClusterClassifyProfile.AddComputePass<ClassifyColumnsPassData>(
                renderGraph,
                out var passData);
            
            var builder = profiledPass.Builder;
            passData.Shader = _clusterShader;
            passData.Kernel = _classifyColumnsKernel;
            passData.MediaTiles = mediaTiles;
            passData.DispatchTiles = dispatchTiles;
            passData.IndirectArgs = indirectArgs;
            passData.GridParams = gridParams;
            passData.Groups = CoreUtils.DivRoundUp(tileCountXY, ClusterThreadGroupSize);
            
            builder.UseBuffer(passData.MediaTiles);
            builder.UseBuffer(passData.DispatchTiles, AccessFlags.Write);
            builder.UseBuffer(passData.IndirectArgs, AccessFlags.ReadWrite);
            
            builder.SetRenderFunc(static (ClassifyColumnsPassData data, ComputeGraphContext context) =>
            {
                context.cmd.SetComputeVectorParam(
                    data.Shader, ShaderProperties.VolumetricFog.MediaTileGridParams, data.GridParams);
                context.cmd.SetComputeBufferParam(
                    data.Shader, data.Kernel, ShaderProperties.VolumetricFog.MediaTileBuffer, data.MediaTiles);
                context.cmd.SetComputeBufferParam(
                    data.Shader, data.Kernel, ShaderProperties.VolumetricFog.MediaDispatchTileBuffer,
                    data.DispatchTiles);
                context.cmd.SetComputeBufferParam(
                    data.Shader, data.Kernel, ShaderProperties.VolumetricFog.MediaIndirectArgs,
                    data.IndirectArgs);
                context.cmd.DispatchCompute(data.Shader, data.Kernel, data.Groups, 1, 1);
            });
        }

        private void RecordFogIntegration(
            RenderGraph renderGraph,
            VolumetricContext context,
            VolumetricResources resources,
            TextureHandle cameraDepth,
            TextureHandle farthestDepthPyramid,
            Vector2Int farthestDepthMipOffset,
            Vector2Int farthestDepthMipSize,
            int farthestDepthDilationWidth,
            VolumetricFogVolumeData fogData,
            FogMainLightData mainLight,
            Vector4 ambientProbeL0,
            BufferHandle previousAmbientProbeBuffer,
            BufferHandle targetAmbientProbeBuffer,
            float ambientProbeBlendFactor,
            bool useGpuAmbientProbe,
            TextureHandle mainLightShadowmap,
            bool mainLightShadowsActive,
            ClusterResources clusters,
            VolumetricVolumeCollector volumes,
            VolumetricFogMaskCollector fogMasks,
            bool hasLocalMedia)
        {
            using var profiledPass = IntegrationProfile.AddComputePass<IntegrationPassData>(
                renderGraph,
                out var passData);
            
            var builder = profiledPass.Builder;
            passData.Shader = _shader;
            passData.GlobalOnlyKernel = _globalOnlyKernel;
            passData.EmptyTilesKernel = _emptyTilesKernel;
            passData.LocalMediaKernel = _localMediaKernel;
            passData.TexturedLocalMediaKernel = _texturedLocalMediaKernel;
            passData.Context = context;
            passData.FogData = fogData;
            passData.CameraDepth = cameraDepth;
            passData.FarthestDepthPyramid = farthestDepthPyramid;
            passData.FarthestDepthMipParams = new Vector4(
                farthestDepthMipOffset.x,
                farthestDepthMipOffset.y,
                farthestDepthMipSize.x,
                farthestDepthMipSize.y);
            passData.FarthestDepthDilationWidth = farthestDepthDilationWidth;
            passData.IntegratedFog = resources.IntegratedFog;
            passData.PreviousAmbientProbe = previousAmbientProbeBuffer;
            passData.TargetAmbientProbe = targetAmbientProbeBuffer;
            passData.AmbientProbeL0 = ambientProbeL0;
            passData.AmbientProbeBlendFactor = Mathf.Clamp01(ambientProbeBlendFactor);
            passData.MainLightDirection = mainLight.Direction;
            passData.MainLightColor = mainLight.Color;
            passData.UseGpuAmbientProbe = useGpuAmbientProbe;
            passData.MainLightShadowmap = mainLightShadowsActive
                ? mainLightShadowmap
                : TextureHandle.nullHandle;
            passData.MainLightShadowsActive = mainLightShadowsActive;
            passData.HasLocalMedia = hasLocalMedia;
            passData.GroupsX = CoreUtils.DivRoundUp(
                context.FroxelWidth, VolumetricGridConfig.ThreadGroupSizeXY);
            passData.GroupsY = CoreUtils.DivRoundUp(
                context.FroxelHeight, VolumetricGridConfig.ThreadGroupSizeXY);

            if (hasLocalMedia)
            {
                passData.DispatchTiles = clusters.DispatchTiles;
                passData.IndirectArgs = clusters.IndirectArgs;
                passData.MediaTiles = clusters.MediaTiles;
                passData.MediaTileGridParams = clusters.GridParams;
                passData.TileCountXY = clusters.TileCountXY;
                passData.Volumes = renderGraph.ImportBuffer(volumes.Buffer);
                passData.FogMasks = renderGraph.ImportBuffer(fogMasks.Buffer);

                var neutralWhite3D = _defaultTextures.ImportWhite3D(renderGraph);
                var neutralBlack3D = _defaultTextures.ImportBlack3D(renderGraph);
                passData.VolumeMaskAtlas = VolumetricAtlasBinding.Import(
                    renderGraph, volumes.MaskAtlas, neutralWhite3D, volumes.Count > 0);
                passData.FogMaskAtlas = VolumetricAtlasBinding.Import(
                    renderGraph, fogMasks.MaskAtlas, neutralBlack3D, fogMasks.Count > 0);
            }

            builder.UseTexture(passData.CameraDepth);
            builder.UseTexture(passData.FarthestDepthPyramid);
            builder.UseTexture(passData.IntegratedFog, AccessFlags.Write);
            builder.UseBuffer(passData.PreviousAmbientProbe);
            builder.UseBuffer(passData.TargetAmbientProbe);
            if (passData.HasLocalMedia)
            {
                builder.UseBuffer(passData.DispatchTiles);
                builder.UseBuffer(passData.IndirectArgs);
                builder.UseBuffer(passData.MediaTiles);
                builder.UseBuffer(passData.Volumes);
                builder.UseBuffer(passData.FogMasks);
            }
            
            if (passData.MainLightShadowsActive)
                builder.UseTexture(passData.MainLightShadowmap, AccessFlags.Read);
            
            if (passData.HasLocalMedia)
            {
                for (int groupIndex = 0;
                     groupIndex < VolumetricGridConfig.MaxAtlasGroups;
                     ++groupIndex)
                {
                    builder.UseTexture(passData.VolumeMaskAtlas.GetTexture(groupIndex));
                    builder.UseTexture(passData.FogMaskAtlas.GetTexture(groupIndex));
                }
            }

            builder.SetRenderFunc(static (IntegrationPassData data, ComputeGraphContext context) =>
            {
                var writer = ShaderValueWriter.ForCompute(context.cmd, data.Shader);
                
                VolumetricPassCommon.WriteIntegrationContext(ref writer, data.Context);
                VolumetricPassCommon.WriteFogData(
                    ref writer, data.FogData, data.MainLightShadowsActive);
                context.cmd.SetComputeVectorParam(
                    data.Shader, ShaderProperties.VolumetricFog.MainLightDirection, data.MainLightDirection);
                context.cmd.SetComputeVectorParam(
                    data.Shader, ShaderProperties.VolumetricFog.MainLightColor, data.MainLightColor);
                context.cmd.SetComputeVectorParam(
                    data.Shader, ShaderProperties.VolumetricFog.AmbientProbeL0, data.AmbientProbeL0);
                context.cmd.SetComputeIntParam(
                    data.Shader, ShaderProperties.VolumetricFog.UseGpuAmbientProbe,
                    data.UseGpuAmbientProbe ? 1 : 0);
                context.cmd.SetComputeFloatParam(
                    data.Shader, ShaderProperties.VolumetricFog.AmbientProbeBlendFactor,
                    data.AmbientProbeBlendFactor);

                if (!data.HasLocalMedia)
                {
                    BindIntegrationKernel(data, context, data.GlobalOnlyKernel, 0, false);
                    context.cmd.DispatchCompute(
                        data.Shader, data.GlobalOnlyKernel, data.GroupsX, data.GroupsY, 1);
                    return;
                }

                BindIntegrationKernel(data, context, data.EmptyTilesKernel, 0, true);
                context.cmd.DispatchCompute(
                    data.Shader, data.EmptyTilesKernel, data.IndirectArgs, 0u);

                BindIntegrationKernel(
                    data, context, data.LocalMediaKernel, data.TileCountXY, true);
                BindLocalMediaBuffers(data, context.cmd, data.LocalMediaKernel, false);
                context.cmd.DispatchCompute(
                    data.Shader,
                    data.LocalMediaKernel,
                    data.IndirectArgs,
                    12u);

                BindIntegrationKernel(
                    data, context, data.TexturedLocalMediaKernel, data.TileCountXY * 2, true);
                BindLocalMediaBuffers(data, context.cmd, data.TexturedLocalMediaKernel, true);
                context.cmd.DispatchCompute(
                    data.Shader,
                    data.TexturedLocalMediaKernel,
                    data.IndirectArgs,
                    24u);
            });
        }

        private static void BindLocalMediaBuffers(
            IntegrationPassData data,
            ComputeCommandBuffer command,
            int kernel,
            bool textured)
        {
            command.SetComputeBufferParam(
                data.Shader, kernel, ShaderProperties.VolumetricFog.MediaTileBuffer, data.MediaTiles);
            command.SetComputeBufferParam(
                data.Shader, kernel, ShaderProperties.VolumetricFog.VolumeBuffer, data.Volumes);
            command.SetComputeBufferParam(
                data.Shader, kernel, ShaderProperties.VolumetricFog.FogMaskBuffer, data.FogMasks);
            
            if (!textured) return;

            data.VolumeMaskAtlas.Bind(
                command,
                data.Shader,
                kernel,
                ShaderProperties.VolumetricFog.VolumeMaskAtlasTextures,
                ShaderProperties.VolumetricFog.VolumeMaskAtlasParameters);
            data.FogMaskAtlas.Bind(
                command,
                data.Shader,
                kernel,
                ShaderProperties.VolumetricFog.FogMaskAtlasTextures,
                ShaderProperties.VolumetricFog.FogMaskAtlasParameters);
        }

        private static void BindIntegrationKernel(
            IntegrationPassData data,
            ComputeGraphContext context,
            int kernel,
            int dispatchTileOffset,
            bool bindDispatchTiles)
        {
            context.cmd.SetComputeTextureParam(
                data.Shader, kernel, ShaderProperties.VolumetricFog.CameraDepthTexture, data.CameraDepth);
            context.cmd.SetComputeTextureParam(
                data.Shader,
                kernel,
                ShaderProperties.VolumetricFog.FarthestDepthPyramidTexture,
                data.FarthestDepthPyramid);
            if (data.MainLightShadowsActive)
            {
                context.cmd.SetComputeTextureParam(
                    data.Shader,
                    kernel,
                    ShaderProperties.VolumetricFog.MainLightShadowmapTexture,
                    data.MainLightShadowmap);
            }
            context.cmd.SetComputeVectorParam(
                data.Shader,
                ShaderProperties.VolumetricFog.FarthestDepthMipParams,
                data.FarthestDepthMipParams);
            context.cmd.SetComputeIntParam(
                data.Shader,
                ShaderProperties.VolumetricFog.FarthestDepthDilationWidth,
                data.FarthestDepthDilationWidth);
            context.cmd.SetComputeTextureParam(
                data.Shader, kernel, ShaderProperties.VolumetricFog.IntegratedFogTextureRW, data.IntegratedFog);
            context.cmd.SetComputeBufferParam(
                data.Shader, kernel, ShaderProperties.VolumetricFog.PreviousAmbientProbeRawBuffer,
                data.PreviousAmbientProbe);
            context.cmd.SetComputeBufferParam(
                data.Shader, kernel, ShaderProperties.VolumetricFog.TargetAmbientProbeRawBuffer,
                data.TargetAmbientProbe);
            if (bindDispatchTiles)
            {
                context.cmd.SetComputeBufferParam(
                    data.Shader, kernel, ShaderProperties.VolumetricFog.MediaDispatchTileBuffer, data.DispatchTiles);
                context.cmd.SetComputeIntParam(
                    data.Shader, ShaderProperties.VolumetricFog.MediaDispatchTileOffset, dispatchTileOffset);
                context.cmd.SetComputeVectorParam(
                    data.Shader, ShaderProperties.VolumetricFog.MediaTileGridParams, data.MediaTileGridParams);
            }
        }

        private TextureHandle RecordGaussianFilter(
            RenderGraph renderGraph,
            VolumetricContext context,
            TextureHandle input)
        {
            var descriptor = renderGraph.GetTextureDesc(input);
            descriptor.name = "VolumetricIntegratedFogFiltered";
            descriptor.enableRandomWrite = true;
            
            var output = renderGraph.CreateTexture(descriptor);

            using var profiledPass = FilteringProfile.AddComputePass<FilterPassData>(
                renderGraph,
                out var passData);
            
            var builder = profiledPass.Builder;
            passData.Shader = _filterShader;
            passData.Kernel = _filterKernel;
            passData.Input = input;
            passData.Output = output;
            passData.GridParams = context.GridParams;
            passData.GroupsX = CoreUtils.DivRoundUp(
                context.FroxelWidth, VolumetricGridConfig.FilterThreadGroupSizeX);
            passData.GroupsY = CoreUtils.DivRoundUp(
                context.FroxelHeight, VolumetricGridConfig.FilterThreadGroupSizeY);
            passData.GroupsZ = context.FroxelDepth;

            builder.UseTexture(passData.Input);
            builder.UseTexture(passData.Output, AccessFlags.Write);
            
            builder.SetRenderFunc(static (FilterPassData data, ComputeGraphContext context) =>
            {
                context.cmd.SetComputeVectorParam(
                    data.Shader, ShaderProperties.VolumetricFog.GridParams, data.GridParams);
                context.cmd.SetComputeTextureParam(
                    data.Shader, data.Kernel, ShaderProperties.VolumetricFog.FilterInput, data.Input);
                context.cmd.SetComputeTextureParam(
                    data.Shader, data.Kernel, ShaderProperties.VolumetricFog.FilterOutput, data.Output);
                context.cmd.DispatchCompute(
                    data.Shader, data.Kernel, data.GroupsX, data.GroupsY, data.GroupsZ);
            });

            return output;
        }

        public void Dispose()
        {
            _fallbackAmbientProbeBuffer?.Dispose();
            _fallbackAmbientProbeBuffer = null;
            _defaultTextures.Dispose();
        }
    }
}
