using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;

namespace Aetheria.Runtime.Rendering.Fog.Volumetrics
{
    internal readonly struct VolumetricAtlasBinding
    {
        private readonly TextureHandle _texture0;
        private readonly TextureHandle _texture1;
        private readonly TextureHandle _texture2;
        private readonly TextureHandle _texture3;
        private readonly Vector4 _parameters0;
        private readonly Vector4 _parameters1;
        private readonly Vector4 _parameters2;
        private readonly Vector4 _parameters3;

        private VolumetricAtlasBinding(
            TextureHandle texture0,
            TextureHandle texture1,
            TextureHandle texture2,
            TextureHandle texture3,
            Vector4 parameters0,
            Vector4 parameters1,
            Vector4 parameters2,
            Vector4 parameters3)
        {
            _texture0 = texture0;
            _texture1 = texture1;
            _texture2 = texture2;
            _texture3 = texture3;
            _parameters0 = parameters0;
            _parameters1 = parameters1;
            _parameters2 = parameters2;
            _parameters3 = parameters3;
        }

        public static VolumetricAtlasBinding Import(
            RenderGraph renderGraph,
            IVolumetricTextureAtlas atlas,
            TextureHandle fallback,
            bool useAtlas = true)
        {
            return new VolumetricAtlasBinding(
                ImportGroup(renderGraph, atlas, fallback, useAtlas, 0),
                ImportGroup(renderGraph, atlas, fallback, useAtlas, 1),
                ImportGroup(renderGraph, atlas, fallback, useAtlas, 2),
                ImportGroup(renderGraph, atlas, fallback, useAtlas, 3),
                atlas.GetAtlasParams(0),
                atlas.GetAtlasParams(1),
                atlas.GetAtlasParams(2),
                atlas.GetAtlasParams(3));
        }

        public TextureHandle GetTexture(int groupIndex) => groupIndex switch
        {
            1 => _texture1,
            2 => _texture2,
            3 => _texture3,
            _ => _texture0,
        };

        private Vector4 GetParameters(int groupIndex) => groupIndex switch
        {
            1 => _parameters1,
            2 => _parameters2,
            3 => _parameters3,
            _ => _parameters0,
        };

        public void Bind(
            ComputeCommandBuffer command,
            ComputeShader shader,
            int kernel,
            int[] textureIds,
            int[] parameterIds = null)
        {
            for (int groupIndex = 0; groupIndex < VolumetricGridConfig.MaxAtlasGroups; ++groupIndex)
            {
                command.SetComputeTextureParam(
                    shader,
                    kernel,
                    textureIds[groupIndex],
                    GetTexture(groupIndex));

                if (parameterIds != null)
                {
                    command.SetComputeVectorParam(
                        shader,
                        parameterIds[groupIndex],
                        GetParameters(groupIndex));
                }
            }
        }

        private static TextureHandle ImportGroup(
            RenderGraph renderGraph,
            IVolumetricTextureAtlas atlas,
            TextureHandle fallback,
            bool useAtlas,
            int groupIndex) =>
            useAtlas && atlas.HasAtlasGroup(groupIndex)
                ? atlas.Import(renderGraph, groupIndex)
                : fallback;
    }
}