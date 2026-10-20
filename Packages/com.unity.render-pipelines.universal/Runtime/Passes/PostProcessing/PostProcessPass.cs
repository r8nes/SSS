using System;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering.RenderGraphModule;

namespace UnityEngine.Rendering.Universal
{
    internal abstract class PostProcessPass : IDisposable
    {
        protected PostProcessMaterialLibrary Materials { get; }

        protected PostProcessPass(PostProcessMaterialLibrary materials = null)
        {
            Materials = materials;
        }

        public virtual void Dispose() { }

        protected static TextureHandle CreateCompatibleTexture(RenderGraph renderGraph, in TextureHandle source, string name, bool clear, FilterMode filterMode)
        {
            return PostProcessPassUtils.CreateCompatibleTexture(renderGraph, in source, name, clear, filterMode);
        }

        protected static TextureHandle CreateCompatibleTexture(RenderGraph renderGraph, in TextureDesc desc, string name, bool clear, FilterMode filterMode)
        {
            return PostProcessPassUtils.CreateCompatibleTexture(renderGraph, in desc, name, clear, filterMode);
        }

        protected static TextureHandle CreateCompatibleDepthStencilTexture(RenderGraph renderGraph, in TextureDesc desc, string name, GraphicsFormat depthStencilFormat, bool clear, FilterMode filterMode)
        {
            return PostProcessPassUtils.CreateCompatibleDepthStencilTexture(renderGraph, in desc, name, depthStencilFormat, clear, filterMode);
        }

        protected static TextureDesc GetCompatibleDescriptor(TextureDesc desc, int width, int height, GraphicsFormat format)
        {
            return PostProcessPassUtils.GetCompatibleDescriptor(desc, width, height, format);
        }

        protected static TextureDesc GetCompatibleDescriptor(TextureDesc desc)
        {
            return PostProcessPassUtils.GetCompatibleDescriptor(desc);
        }

        protected static void MakeCompatible(ref TextureDesc desc)
        {
            PostProcessPassUtils.MakeCompatible(ref desc);
        }

        protected static void MakeComputeCompatible(ref TextureDesc desc)
        {
            PostProcessPassUtils.MakeComputeCompatible(ref desc);
        }

        protected static RenderTextureDescriptor GetCompatibleDescriptor(RenderTextureDescriptor desc, int width, int height, GraphicsFormat format, GraphicsFormat depthStencilFormat = GraphicsFormat.None)
        {
            return PostProcessPassUtils.GetCompatibleDescriptor(desc, width, height, format, depthStencilFormat);
        }


        protected static void UpdateCameraResolution(RenderGraph renderGraph, UniversalCameraData cameraData, Vector2Int resolution)
        {
            PostProcessPassUtils.UpdateCameraResolution(renderGraph, cameraData, resolution);
        }

        protected static Vector2 GetViewportScale(RTHandle sourceTexture)
        {
            return PostProcessPassUtils.GetViewportScale(sourceTexture);
        }
    }

    internal abstract class PostProcessPass<TSettings> : PostProcessPass where TSettings : VolumeComponent
    {
        protected TSettings Settings { get; private set; }

        protected PostProcessPass(PostProcessMaterialLibrary materials)
            : base(materials)
        {
        }

        public virtual void SetSettings(TSettings settings)
        {
            Settings = settings;
        }
    }
}
