using UnityEngine.Rendering.RenderGraphModule;

namespace UnityEngine.Rendering.Universal
{
    public enum UniversalRenderGraphExtensionStage
    {
        BeforeShadows,
        AfterShadows,
        BeforePrepasses,
        BeforeDepthCopy,
        AfterPrepasses,
        BeforeScreenSpaceShadows,
        AfterOpaques,
        BeforeSkybox,
        AfterSkybox,
        TransparentPrepass,
        AfterTransparentPrepass,
        VolumetricLighting,
        OpaqueAtmosphere,
        BeforePreRefraction,
        AfterPreRefraction,
        WaterLighting,
        BeforeRefraction,
        AfterRefraction,
        TransparentComposite,
        BeforeTransparents,
        BeforeTransparentDraw,
        AfterTransparents
    }

    public struct UniversalRenderGraphRequirements
    {
        internal bool requiresDepthTexture;
        internal bool requiresNormalsTexture;
        internal bool requiresColorTexture;
        internal bool requiresMotionVectors;
        internal bool requiresIntermediateColor;
        internal bool requiresTransparentRefraction;
        internal bool requiresColorPyramid;
        internal int colorPyramidMipCount;
        internal RenderPassEvent depthTextureEarliestEvent;
        internal RenderPassEvent depthNormalEarliestEvent;
        internal int depthPyramidMipCount;
        internal int depthPyramidCheckerboardMipCount;
        internal bool requiresFarthestDepthPyramid;
        internal UniversalRenderGraphExtensionStage depthPyramidStage;
        internal bool hasDepthPyramidRequest;

        internal static UniversalRenderGraphRequirements Create()
        {
            return new UniversalRenderGraphRequirements
            {
                depthTextureEarliestEvent = RenderPassEvent.BeforeRenderingPostProcessing,
                depthNormalEarliestEvent = RenderPassEvent.BeforeRenderingOpaques,
                depthPyramidStage = UniversalRenderGraphExtensionStage.BeforeTransparents
            };
        }

        public void RequireDepth(UniversalRenderGraphExtensionStage stage)
        {
            RenderPassEvent earliestEvent = ToRenderPassEvent(stage);
            requiresDepthTexture = true;
            if (earliestEvent < depthTextureEarliestEvent)
                depthTextureEarliestEvent = earliestEvent;
        }

        public void RequireNormals(UniversalRenderGraphExtensionStage stage)
        {
            RenderPassEvent earliestEvent = ToRenderPassEvent(stage);
            requiresNormalsTexture = true;
            if (earliestEvent < depthNormalEarliestEvent)
                depthNormalEarliestEvent = earliestEvent;
            RequireDepth(stage);
        }

        public void RequireColorTexture() => requiresColorTexture = true;
        public void RequireIntermediateColor() => requiresIntermediateColor = true;

        public void RequireTransparentRefraction()
        {
            requiresTransparentRefraction = true;
            RequireDepth(UniversalRenderGraphExtensionStage.TransparentPrepass);
            RequireIntermediateColor();
        }

        public void RequireColorPyramid(int mipCount = 1)
        {
            requiresColorPyramid = true;
            colorPyramidMipCount = Mathf.Max(colorPyramidMipCount, Mathf.Max(1, mipCount));
        }

        public void RequireMotionVectors(UniversalRenderGraphExtensionStage stage)
        {
            requiresMotionVectors = true;
            RequireDepth(stage);
        }

        public void RequireDepthPyramid(
            UniversalRenderGraphExtensionStage stage,
            int mipCount,
            int checkerboardMipCount = 0,
            bool farthest = false)
        {
            mipCount = Mathf.Clamp(mipCount, 1, DepthPyramidPackedMipChainInfo.MaxMipLevels);
            checkerboardMipCount = Mathf.Clamp(checkerboardMipCount, 0, 2);

            if (!hasDepthPyramidRequest || stage < depthPyramidStage)
                depthPyramidStage = stage;

            hasDepthPyramidRequest = true;
            depthPyramidMipCount = Mathf.Max(depthPyramidMipCount, mipCount);
            depthPyramidCheckerboardMipCount = Mathf.Max(depthPyramidCheckerboardMipCount, checkerboardMipCount);
            requiresFarthestDepthPyramid |= farthest;
        }

        internal static RenderPassEvent ToRenderPassEvent(UniversalRenderGraphExtensionStage stage)
        {
            return stage switch
            {
                UniversalRenderGraphExtensionStage.BeforeShadows => RenderPassEvent.BeforeRenderingShadows,
                UniversalRenderGraphExtensionStage.AfterShadows => RenderPassEvent.AfterRenderingShadows,
                UniversalRenderGraphExtensionStage.BeforePrepasses => RenderPassEvent.BeforeRenderingPrePasses,
                UniversalRenderGraphExtensionStage.BeforeDepthCopy => RenderPassEvent.AfterRenderingPrePasses,
                UniversalRenderGraphExtensionStage.AfterPrepasses => RenderPassEvent.AfterRenderingPrePasses,
                UniversalRenderGraphExtensionStage.BeforeScreenSpaceShadows => RenderPassEvent.BeforeRenderingOpaques,
                UniversalRenderGraphExtensionStage.AfterOpaques => RenderPassEvent.AfterRenderingOpaques,
                UniversalRenderGraphExtensionStage.BeforeSkybox => RenderPassEvent.BeforeRenderingSkybox,
                UniversalRenderGraphExtensionStage.AfterSkybox => RenderPassEvent.AfterRenderingSkybox,
                UniversalRenderGraphExtensionStage.TransparentPrepass => RenderPassEvent.BeforeRenderingTransparents,
                UniversalRenderGraphExtensionStage.AfterTransparentPrepass => RenderPassEvent.BeforeRenderingTransparents,
                UniversalRenderGraphExtensionStage.VolumetricLighting => RenderPassEvent.BeforeRenderingTransparents,
                UniversalRenderGraphExtensionStage.OpaqueAtmosphere => RenderPassEvent.BeforeRenderingTransparents,
                UniversalRenderGraphExtensionStage.BeforePreRefraction => RenderPassEvent.BeforeRenderingTransparents,
                UniversalRenderGraphExtensionStage.AfterPreRefraction => RenderPassEvent.BeforeRenderingTransparents,
                UniversalRenderGraphExtensionStage.WaterLighting => RenderPassEvent.BeforeRenderingTransparents,
                UniversalRenderGraphExtensionStage.BeforeRefraction => RenderPassEvent.BeforeRenderingTransparents,
                UniversalRenderGraphExtensionStage.AfterRefraction => RenderPassEvent.BeforeRenderingTransparents,
                UniversalRenderGraphExtensionStage.TransparentComposite => RenderPassEvent.AfterRenderingTransparents,
                UniversalRenderGraphExtensionStage.BeforeTransparents => RenderPassEvent.BeforeRenderingTransparents,
                UniversalRenderGraphExtensionStage.BeforeTransparentDraw => RenderPassEvent.BeforeRenderingTransparents,
                UniversalRenderGraphExtensionStage.AfterTransparents => RenderPassEvent.AfterRenderingTransparents,
                _ => RenderPassEvent.BeforeRenderingPostProcessing
            };
        }
    }

    public enum ScreenSpaceShadowReceiverNormalEncoding
    {
        WorldSpace,
        Octahedral888
    }

    public sealed class ScreenSpaceShadowReceiverData : ContextItem
    {
        public TextureHandle depth;
        public TextureHandle normals;
        public ScreenSpaceShadowReceiverNormalEncoding normalEncoding;
        public bool isValid;

        public override void Reset()
        {
            depth = TextureHandle.nullHandle;
            normals = TextureHandle.nullHandle;
            normalEncoding = ScreenSpaceShadowReceiverNormalEncoding.WorldSpace;
            isValid = false;
        }
    }


    public sealed class TransparentRefractionFrameData : ContextItem
    {
        public TextureHandle depthBuffer;
        public TextureHandle depthBufferPreRefraction;
        public TextureHandle waterSurfaceDepth;
        public TextureHandle beforeRefraction;
        public bool isValid;

        public override void Reset()
        {
            depthBuffer = TextureHandle.nullHandle;
            depthBufferPreRefraction = TextureHandle.nullHandle;
            waterSurfaceDepth = TextureHandle.nullHandle;
            beforeRefraction = TextureHandle.nullHandle;
            isValid = false;
        }
    }

    public interface IUniversalRenderGraphExtension
    {
        void PrepareRenderGraph(
            RenderGraph renderGraph,
            ScriptableRenderer renderer,
            ContextContainer frameData,
            ref UniversalRenderGraphRequirements requirements);

        void RecordRenderGraph(
            RenderGraph renderGraph,
            ContextContainer frameData,
            UniversalRenderGraphExtensionStage stage);
    }



    public interface IUniversalCameraSetupRenderGraphExtension
    {
        void ExecuteCameraSetup(
            RasterCommandBuffer cmd,
            ContextContainer frameData);
    }

    public interface IUniversalSkyboxRenderGraphExtension
    {
        bool PrepareSkyboxPass(
            IRasterRenderGraphBuilder builder,
            ContextContainer frameData,
            Material skyboxMaterial);

        void ExecuteBeforeSkybox(
            RasterCommandBuffer cmd,
            ContextContainer frameData,
            Material skyboxMaterial);

        void ExecuteAfterSkybox(
            RasterCommandBuffer cmd,
            ContextContainer frameData,
            Material skyboxMaterial);
    }


    public interface IUniversalProceduralRenderGraphExtension
    {
        bool PrepareDepthOnlyPass(
            IRasterRenderGraphBuilder builder,
            ContextContainer frameData,
            bool fullRangeDepth,
            bool depthPriming);

        void ExecuteDepthOnlyPass(
            RasterCommandBuffer cmd,
            ContextContainer frameData,
            bool fullRangeDepth,
            bool depthPriming);

        bool PrepareDepthNormalPass(
            IRasterRenderGraphBuilder builder,
            ContextContainer frameData,
            bool renderingLayers,
            bool depthPriming);

        void ExecuteDepthNormalPass(
            RasterCommandBuffer cmd,
            ContextContainer frameData,
            bool renderingLayers,
            bool depthPriming);

        bool PrepareObjectMotionPass(
            IRasterRenderGraphBuilder builder,
            ContextContainer frameData);

        void ExecuteObjectMotionPass(
            RasterCommandBuffer cmd,
            ContextContainer frameData);

        bool WritesDepthInOpaquePass(ContextContainer frameData);

        bool PrepareOpaquePass(
            IRasterRenderGraphBuilder builder,
            ContextContainer frameData,
            bool renderingLayers);

        void ExecuteOpaquePass(
            RasterCommandBuffer cmd,
            ContextContainer frameData,
            bool renderingLayers);
    }

    public interface IUniversalTransparentRenderGraphExtension
    {
        bool PrepareTransparentPass(
            IRasterRenderGraphBuilder builder,
            ContextContainer frameData);

        void ExecuteBeforeTransparents(
            RasterCommandBuffer cmd,
            ContextContainer frameData);
    }
}
