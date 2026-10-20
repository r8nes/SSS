using System;
using System.Collections.Generic;
using UnityEngine.Rendering.RenderGraphModule;

namespace UnityEngine.Rendering.Universal.Internal
{
    /// <summary>
    /// Draw  objects into the given color and depth target
    ///
    /// You can use this pass to render objects that have a material and/or shader
    /// with the pass names UniversalForward or SRPDefaultUnlit.
    /// </summary>
    public partial class DrawObjectsPass : ScriptableRenderPass
    {
        FilteringSettings m_FilteringSettings;
        RenderStateBlock m_RenderStateBlock;
        List<ShaderTagId> m_ShaderTagIdList = new List<ShaderTagId>();

        bool m_IsOpaque;
        internal bool drawErrorObjects = true;

#if URP_COMPATIBILITY_MODE
        /// <summary>
        /// Used to indicate if the active target of the pass is the back buffer
        /// </summary>
        [Obsolete(DeprecationMessage.CompatibilityScriptingAPIObsolete + " #from(6000.3)")]
        public bool m_IsActiveTargetBackBuffer; // TODO: Remove this when we remove non-RG path
#endif

        /// <summary>
        /// Used to indicate whether transparent objects should receive shadows or not.
        /// </summary>
        public bool m_ShouldTransparentsReceiveShadows;
        internal bool applyTransparentShadowSettings = true;

        /// <summary>Whether transparent materials in this pass may sample screen-space reflections.</summary>
        public bool shouldTransparentsReceiveSSR { get; set; }

        static readonly int s_DrawObjectPassDataPropID = Shader.PropertyToID("_DrawObjectPassData");
        static readonly int s_CameraDepthTexturePropID = Shader.PropertyToID("_CameraDepthTexture");

#if URP_COMPATIBILITY_MODE
        PassData m_PassData;
#endif

        /// <summary>
        /// Creates a new <c>DrawObjectsPass</c> instance.
        /// </summary>
        /// <param name="profilerTag">The profiler tag used with the pass.</param>
        /// <param name="shaderTagIds"></param>
        /// <param name="opaque">Marks whether the objects are opaque or transparent.</param>
        /// <param name="evt">The <c>RenderPassEvent</c> to use.</param>
        /// <param name="renderQueueRange">The <c>RenderQueueRange</c> to use for creating filtering settings that control what objects get rendered.</param>
        /// <param name="layerMask">The layer mask to use for creating filtering settings that control what objects get rendered.</param>
        /// <param name="stencilState">The stencil settings to use with this poss.</param>
        /// <param name="stencilReference">The stencil reference value to use with this pass.</param>
        /// <seealso cref="ShaderTagId"/>
        /// <seealso cref="RenderPassEvent"/>
        /// <seealso cref="RenderQueueRange"/>
        /// <seealso cref="LayerMask"/>
        /// <seealso cref="StencilState"/>
        public DrawObjectsPass(string profilerTag, ShaderTagId[] shaderTagIds, bool opaque, RenderPassEvent evt, RenderQueueRange renderQueueRange, LayerMask layerMask, StencilState stencilState, int stencilReference)
        {
            Init(opaque, evt, renderQueueRange, layerMask, stencilState, stencilReference, shaderTagIds);

            profilingSampler = new ProfilingSampler(profilerTag);
        }

        /// <summary>
        /// Creates a new <c>DrawObjectsPass</c> instance.
        /// </summary>
        /// <param name="profilerTag">The profiler tag used with the pass.</param>
        /// <param name="opaque">Marks whether the objects are opaque or transparent.</param>
        /// <param name="evt">The <c>RenderPassEvent</c> to use.</param>
        /// <param name="renderQueueRange">The <c>RenderQueueRange</c> to use for creating filtering settings that control what objects get rendered.</param>
        /// <param name="layerMask">The layer mask to use for creating filtering settings that control what objects get rendered.</param>
        /// <param name="stencilState">The stencil settings to use with this poss.</param>
        /// <param name="stencilReference">The stencil reference value to use with this pass.</param>
        /// <seealso cref="RenderPassEvent"/>
        /// <seealso cref="RenderQueueRange"/>
        /// <seealso cref="LayerMask"/>
        /// <seealso cref="StencilState"/>
        public DrawObjectsPass(string profilerTag, bool opaque, RenderPassEvent evt, RenderQueueRange renderQueueRange, LayerMask layerMask, StencilState stencilState, int stencilReference)
            : this(profilerTag, null, opaque, evt, renderQueueRange, layerMask, stencilState, stencilReference)
        { }

        internal DrawObjectsPass(URPProfileId profileId, bool opaque, RenderPassEvent evt, RenderQueueRange renderQueueRange, LayerMask layerMask, StencilState stencilState, int stencilReference)
        {
            Init(opaque, evt, renderQueueRange, layerMask, stencilState, stencilReference);

            profilingSampler = ProfilingSampler.Get(profileId);
        }

        internal void Init(bool opaque, RenderPassEvent evt, RenderQueueRange renderQueueRange, LayerMask layerMask, StencilState stencilState, int stencilReference, ShaderTagId[] shaderTagIds = null)
        {
            if (shaderTagIds == null)
                shaderTagIds = new ShaderTagId[] { new ShaderTagId("SRPDefaultUnlit"), new ShaderTagId("UniversalForward"), new ShaderTagId("UniversalForwardOnly") };

            foreach (ShaderTagId sid in shaderTagIds)
                m_ShaderTagIdList.Add(sid);
            renderPassEvent = evt;
            m_FilteringSettings = new FilteringSettings(renderQueueRange, layerMask);
            m_RenderStateBlock = new RenderStateBlock(RenderStateMask.Nothing);
            m_IsOpaque = opaque;
            m_ShouldTransparentsReceiveShadows = false;
            shouldTransparentsReceiveSSR = false;

            if (stencilState.enabled)
            {
                m_RenderStateBlock.stencilReference = stencilReference;
                m_RenderStateBlock.mask = RenderStateMask.Stencil;
                m_RenderStateBlock.stencilState = stencilState;
            }

#if URP_COMPATIBILITY_MODE
#pragma warning disable CS0618
            m_IsActiveTargetBackBuffer = false;
#pragma warning restore CS0618
            m_PassData = new PassData();
#endif
        }

#if URP_COMPATIBILITY_MODE
        /// <inheritdoc/>
        [Obsolete(DeprecationMessage.CompatibilityScriptingAPIObsoleteFrom2023_3)]
        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            ContextContainer frameData = renderingData.frameData;
            UniversalRenderingData universalRenderingData = frameData.Get<UniversalRenderingData>();
            UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();
            UniversalLightData lightData = frameData.Get<UniversalLightData>();

            bool disableZWrite = CanDisableZWrite(cameraData, m_IsOpaque);

            InitPassData(cameraData, ref m_PassData, uint.MaxValue, m_IsActiveTargetBackBuffer);
            InitRendererLists(universalRenderingData, cameraData, lightData, ref m_PassData, context, default(RenderGraph), false, disableZWrite);

            using (new ProfilingScope(renderingData.commandBuffer, profilingSampler))
            {
                ExecutePass(CommandBufferHelpers.GetRasterCommandBuffer(renderingData.commandBuffer), m_PassData, m_PassData.rendererList, m_PassData.objectsWithErrorRendererList, m_PassData.cameraData.IsCameraProjectionMatrixFlipped());
            }
        }
#endif

        internal static void ExecutePass(RasterCommandBuffer cmd, PassData data, RendererList rendererList, RendererList objectsWithErrorRendererList, bool yFlip)
        {
            // Global render pass data containing various settings.
            // x,y,z are currently unused
            // w is used for knowing whether the object is opaque(1) or alpha blended(0)
            Vector4 drawObjectPassData = new Vector4(0.0f, 0.0f, 0.0f, (data.isOpaque) ? 1.0f : 0.0f);
            cmd.SetGlobalVector(s_DrawObjectPassDataPropID, drawObjectPassData);

            if (data.cameraData.xr.enabled && data.isActiveTargetBackBuffer)
            {
                cmd.SetViewport(data.cameraData.xr.GetViewport());
            }

            bool useScreenSpaceIrradiance = data.screenSpaceIrradianceHdl.IsValid();
            cmd.SetKeyword(ShaderGlobalKeywords.ScreenSpaceIrradiance, useScreenSpaceIrradiance);
            if (useScreenSpaceIrradiance)
                cmd.SetGlobalTexture(ShaderPropertyId.screenSpaceIrradiance, data.screenSpaceIrradianceHdl);

            bool useSSR = data.screenSpaceReflectionHdl.IsValid() &&
                          (data.isOpaque || data.shouldTransparentsReceiveSSR);
            cmd.SetKeyword(ShaderGlobalKeywords.ScreenSpaceReflection, useSSR);
            if (useSSR)
                cmd.SetGlobalTexture(ShaderPropertyId.screenSpaceReflection, data.screenSpaceReflectionHdl);

            // scaleBias.x = flipSign
            // scaleBias.y = scale
            // scaleBias.z = bias
            // scaleBias.w = unused
            float flipSign = yFlip ? -1.0f : 1.0f;
            Vector4 scaleBias = (flipSign < 0.0f)
                ? new Vector4(flipSign, 1.0f, -1.0f, 1.0f)
                : new Vector4(flipSign, 0.0f, 1.0f, 1.0f);
            cmd.SetGlobalVector(ShaderPropertyId.scaleBiasRt, scaleBias);

            // Set a value that can be used by shaders to identify when AlphaToMask functionality may be active
            // The material shader alpha clipping logic requires this value in order to function correctly in all cases.
            float alphaToMaskAvailable = ((data.cameraData.cameraTargetDescriptor.msaaSamples > 1) && data.isOpaque) ? 1.0f : 0.0f;
            cmd.SetGlobalFloat(ShaderPropertyId.alphaToMaskAvailable, alphaToMaskAvailable);

            var activeDebugHandler = GetActiveDebugHandler(data.cameraData);
            if (activeDebugHandler != null)
            {
                data.debugRendererLists.DrawWithRendererList(cmd);
            }
            else
            {
                cmd.DrawRendererList(rendererList);
                // Render objects that did not match any shader pass with error shader
                if (data.drawErrorObjects)
                    RenderingUtils.DrawRendererListObjectsWithError(cmd, ref objectsWithErrorRendererList);
            }
        }

        /// <summary>
        /// Shared pass data
        /// </summary>
        internal class PassData
        {
            internal TextureHandle albedoHdl;
            internal TextureHandle depthHdl;
            internal TextureHandle screenSpaceIrradianceHdl;
            internal TextureHandle screenSpaceReflectionHdl;
            internal TextureHandle beforeRefractionHdl;
            internal TextureHandle waterSurfaceDepthHdl;
            internal TextureHandle sampledDepthHdl;
            internal TextureHandle restoreDepthHdl;
            internal TextureHandle depthMergeHdl;
            internal Material depthMergeMaterial;

            internal UniversalCameraData cameraData;
            internal bool isOpaque;
            internal bool drawErrorObjects;
            internal bool shouldTransparentsReceiveShadows;
            internal bool applyTransparentShadowSettings;
            internal bool shouldTransparentsReceiveSSR;
            internal bool waterRefractionSort;
            internal uint batchLayerMask;
            internal bool isActiveTargetBackBuffer;
			internal RendererListHandle rendererListHdl;
            internal RendererListHandle objectsWithErrorRendererListHdl;
            internal DebugRendererLists debugRendererLists;

            // Required for code sharing purpose between RG and non-RG.
            internal RendererList rendererList;
            internal RendererList objectsWithErrorRendererList;
            internal ContextContainer frameData;
            internal List<ScriptableRendererFeature> rendererFeatures;
            internal bool hasTransparentExtensions;
            internal bool hasProceduralExtensions;
        }

        /// <summary>
        /// Initialize the shared pass data.
        /// </summary>
        /// <param name="passData"></param>
        internal void InitPassData(UniversalCameraData cameraData, ref PassData passData, uint batchLayerMask, bool isActiveTargetBackBuffer = false)
        {
            passData.cameraData = cameraData;
            passData.isOpaque = m_IsOpaque;
            passData.drawErrorObjects = drawErrorObjects;
            passData.shouldTransparentsReceiveShadows = m_ShouldTransparentsReceiveShadows;
            passData.applyTransparentShadowSettings = applyTransparentShadowSettings;
            passData.shouldTransparentsReceiveSSR = shouldTransparentsReceiveSSR;
            passData.batchLayerMask = batchLayerMask;
            passData.isActiveTargetBackBuffer = isActiveTargetBackBuffer;
        }

        internal void InitRendererLists(UniversalRenderingData renderingData, UniversalCameraData cameraData, UniversalLightData lightData, ref PassData passData, ScriptableRenderContext context, RenderGraph renderGraph, bool useRenderGraph, bool zWriteOff)
        {
            ref Camera camera = ref cameraData.camera;
            var sortFlags = (m_IsOpaque) ? cameraData.defaultOpaqueSortFlags : SortingCriteria.CommonTransparent;
            if (cameraData.renderer.useDepthPriming && m_IsOpaque && (cameraData.renderType == CameraRenderType.Base || cameraData.clearDepth))
                sortFlags = SortingCriteria.SortingLayer | SortingCriteria.RenderQueue | SortingCriteria.OptimizeStateChanges | SortingCriteria.CanvasOrder;

            var filterSettings = m_FilteringSettings;
            filterSettings.batchLayerMask = passData.batchLayerMask;
#if UNITY_EDITOR
                // When rendering the preview camera, we want the layer mask to be forced to Everything
                if (cameraData.isPreviewCamera)
                {
                    filterSettings.layerMask = -1;
                }
#endif
            DrawingSettings drawSettings = RenderingUtils.CreateDrawingSettings(m_ShaderTagIdList, renderingData, cameraData, lightData, sortFlags);

            if (zWriteOff)
            {
                m_RenderStateBlock.depthState = new DepthState(false, CompareFunction.Equal);
                m_RenderStateBlock.mask |= RenderStateMask.Depth;
            }
            else 
            {
                m_RenderStateBlock.depthState = DepthState.defaultValue;
                m_RenderStateBlock.mask &= ~RenderStateMask.Depth;
            }

            var activeDebugHandler = GetActiveDebugHandler(cameraData);
            if (useRenderGraph)
            {
                if (activeDebugHandler != null)
                {
                    passData.debugRendererLists = activeDebugHandler.CreateRendererListsWithDebugRenderState(renderGraph, ref renderingData.cullResults, ref drawSettings, ref filterSettings, ref m_RenderStateBlock);
                }
                else
                {
                    RenderingUtils.CreateRendererListWithRenderStateBlock(renderGraph, ref renderingData.cullResults, drawSettings, filterSettings, m_RenderStateBlock, ref passData.rendererListHdl);
                    RenderingUtils.CreateRendererListObjectsWithError(renderGraph, ref renderingData.cullResults, camera, filterSettings, sortFlags, ref passData.objectsWithErrorRendererListHdl);
                }
            }
            else
            {
                if (activeDebugHandler != null)
                {
                    passData.debugRendererLists = activeDebugHandler.CreateRendererListsWithDebugRenderState(context, ref renderingData.cullResults, ref drawSettings, ref filterSettings, ref m_RenderStateBlock);
                }
                else
                {
                    RenderingUtils.CreateRendererListWithRenderStateBlock(context, ref renderingData.cullResults, drawSettings, filterSettings, m_RenderStateBlock, ref passData.rendererList);
                    RenderingUtils.CreateRendererListObjectsWithError(context, ref renderingData.cullResults, camera, filterSettings, sortFlags, ref passData.objectsWithErrorRendererList);
                }
            }
        }

        internal static bool CanDisableZWrite(UniversalCameraData cameraData, bool isOpaque)
        {
            return cameraData.renderer.useDepthPriming && isOpaque && (cameraData.renderType == CameraRenderType.Base || cameraData.clearDepth);
        }

        protected static bool ProceduralExtensionsWriteOpaqueDepth(
            List<ScriptableRendererFeature> rendererFeatures,
            ContextContainer frameData)
        {
            if (rendererFeatures == null)
                return false;

            for (int i = 0; i < rendererFeatures.Count; ++i)
            {
                ScriptableRendererFeature feature = rendererFeatures[i];
                if (feature != null && feature.isActive &&
                    feature is IUniversalProceduralRenderGraphExtension extension &&
                    extension.WritesDepthInOpaquePass(frameData))
                    return true;
            }

            return false;
        }

        internal void Render(
            RenderGraph renderGraph,
            ContextContainer frameData,
            TextureHandle colorTarget,
            TextureHandle depthTarget,
            TextureHandle mainShadowsTexture,
            TextureHandle additionalShadowsTexture,
            uint batchLayerMask = uint.MaxValue,
            bool isMainOpaquePass = false,
            List<ScriptableRendererFeature> rendererFeatures = null,
            TextureHandle beforeRefraction = default,
            TextureHandle waterSurfaceDepth = default,
            bool waterRefractionSort = false,
            TextureHandle sampledDepth = default,
            TextureHandle depthMergeSource = default,
            Material depthMergeMaterial = null)
        {
            UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();
            UniversalRenderingData renderingData = frameData.Get<UniversalRenderingData>();
            UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();
            UniversalLightData lightData = frameData.Get<UniversalLightData>();

            bool disableZWrite = CanDisableZWrite(cameraData, m_IsOpaque);
            bool proceduralWritesDepth = m_IsOpaque &&
                ProceduralExtensionsWriteOpaqueDepth(rendererFeatures, frameData);

            using (var builder = renderGraph.AddRasterRenderPass<PassData>(passName, out var passData, profilingSampler))
            {
                builder.UseAllGlobalTextures(true);

                InitPassData(cameraData, ref passData, batchLayerMask, resourceData.isActiveTargetBackBuffer);

                if (colorTarget.IsValid())
                {
                    passData.albedoHdl = colorTarget;
                    builder.SetRenderAttachment(colorTarget, 0, AccessFlags.Write);
                }

                passData.sampledDepthHdl = sampledDepth;
                passData.restoreDepthHdl = resourceData.cameraDepthTexture;
                if (passData.sampledDepthHdl.IsValid())
                    builder.UseTexture(passData.sampledDepthHdl, AccessFlags.Read);
                if (passData.restoreDepthHdl.IsValid() && passData.restoreDepthHdl != passData.sampledDepthHdl)
                    builder.UseTexture(passData.restoreDepthHdl, AccessFlags.Read);

                passData.waterSurfaceDepthHdl = waterSurfaceDepth;
                if (passData.waterSurfaceDepthHdl.IsValid())
                    builder.UseTexture(passData.waterSurfaceDepthHdl, AccessFlags.Read);

                passData.waterRefractionSort = waterRefractionSort && beforeRefraction.IsValid() && passData.waterSurfaceDepthHdl.IsValid();
                if (passData.waterRefractionSort)
                {
                    passData.beforeRefractionHdl = beforeRefraction;
                    builder.SetRenderAttachment(beforeRefraction, 1, AccessFlags.ReadWrite);
                }

                passData.depthMergeHdl = depthMergeSource;
                passData.depthMergeMaterial = depthMergeMaterial;
                if (passData.depthMergeHdl.IsValid() && passData.depthMergeMaterial != null)
                    builder.UseTexture(passData.depthMergeHdl, AccessFlags.Read);

                if (depthTarget.IsValid())
                {
                    var depthAccessFlags = passData.depthMergeHdl.IsValid() && passData.depthMergeMaterial != null
                        ? AccessFlags.ReadWrite
                        : disableZWrite && !proceduralWritesDepth
                            ? AccessFlags.Read
                            : AccessFlags.ReadWrite;
                    passData.depthHdl = depthTarget;
                    builder.SetRenderAttachmentDepth(depthTarget, depthAccessFlags);
                }

                if (mainShadowsTexture.IsValid())
                    builder.UseTexture(mainShadowsTexture, AccessFlags.Read);
                if (additionalShadowsTexture.IsValid())
                    builder.UseTexture(additionalShadowsTexture, AccessFlags.Read);

                TextureHandle ssaoTexture = resourceData.ssaoTexture;
                if (ssaoTexture.IsValid())
                    builder.UseTexture(ssaoTexture, AccessFlags.Read);

                TextureHandle contactShadowsTexture = resourceData.contactShadowsTexture;
                if (contactShadowsTexture.IsValid())
                    builder.UseTexture(contactShadowsTexture, AccessFlags.Read);

                TextureHandle irradianceTexture = resourceData.irradianceTexture;
                if (irradianceTexture.IsValid())
                {
                    passData.screenSpaceIrradianceHdl = irradianceTexture;
                    builder.UseTexture(irradianceTexture, AccessFlags.Read);
                }

                TextureHandle ssrTexture = resourceData.ssrTexture;
                passData.screenSpaceReflectionHdl = ssrTexture;
                if (ssrTexture.IsValid())
                {
                    builder.UseTexture(ssrTexture, AccessFlags.Read);
                    if (m_IsOpaque)
                        builder.SetGlobalTextureAfterPass(ssrTexture, ShaderPropertyId.screenSpaceReflection);
                }

                RenderGraphUtils.UseDBufferIfValid(builder, resourceData);

                if (rendererFeatures != null)
                {
                    passData.frameData = frameData;
                    passData.rendererFeatures = rendererFeatures;

                    for (int i = 0; i < rendererFeatures.Count; ++i)
                    {
                        ScriptableRendererFeature feature = rendererFeatures[i];
                        if (feature == null || !feature.isActive)
                            continue;

                        if (!m_IsOpaque && feature is IUniversalTransparentRenderGraphExtension transparentExtension)
                            passData.hasTransparentExtensions |=
                                transparentExtension.PrepareTransparentPass(builder, frameData);
                        else if (m_IsOpaque && feature is IUniversalProceduralRenderGraphExtension proceduralExtension)
                            passData.hasProceduralExtensions |=
                                proceduralExtension.PrepareOpaquePass(builder, frameData, false);
                    }

                    if (passData.hasProceduralExtensions)
                        builder.AllowPassCulling(false);
                }

                InitRendererLists(renderingData, cameraData, lightData, ref passData, default(ScriptableRenderContext), renderGraph, true, disableZWrite);

                var activeDebugHandler = GetActiveDebugHandler(cameraData);
                if (activeDebugHandler != null)
                {
                    passData.debugRendererLists.PrepareRendererListForRasterPass(builder);
                }
                else
                {
                    builder.UseRendererList(passData.rendererListHdl);
                    if (passData.drawErrorObjects)
                        builder.UseRendererList(passData.objectsWithErrorRendererListHdl);
                }

                builder.AllowGlobalStateModification(true);
                if (cameraData.xr.enabled)
                {
                    bool passSupportsFoveation = cameraData.xrUniversal.canFoveateIntermediatePasses || resourceData.isActiveTargetBackBuffer;
                    builder.EnableFoveatedRasterization(cameraData.xr.supportsFoveatedRendering && passSupportsFoveation);
                    builder.SetExtendedFeatureFlags(ExtendedFeatureFlags.MultiviewRenderRegionsCompatible);
#if ENABLE_VR && ENABLE_XR_MODULE && PLATFORM_ANDROID
                    if (isMainOpaquePass)
                    {
                        builder.SetExtendedFeatureFlags(ExtendedFeatureFlags.TileProperties);
                    }
#endif
                }

                builder.SetRenderFunc((PassData data, RasterGraphContext context) =>
                {
                    if (data.depthMergeHdl.IsValid() && data.depthMergeMaterial != null)
                    {
                        context.cmd.SetGlobalTexture(ShaderPropertyId.refractionBoundaryDepthTexture, data.depthMergeHdl, RenderTextureSubElement.Depth);
                        context.cmd.DrawProcedural(Matrix4x4.identity, data.depthMergeMaterial, 0, MeshTopology.Triangles, 3, 1);
                    }

                    // Currently we only need to call this additional pass when the user
                    // doesn't want transparent objects to receive shadows
                    if (data.applyTransparentShadowSettings && !data.isOpaque && !data.shouldTransparentsReceiveShadows)
                        TransparentSettingsPass.ExecutePass(context.cmd);

                    if (data.hasTransparentExtensions && data.rendererFeatures != null)
                    {
                        for (int i = 0; i < data.rendererFeatures.Count; ++i)
                        {
                            ScriptableRendererFeature feature = data.rendererFeatures[i];
                            if (feature != null && feature.isActive &&
                                feature is IUniversalTransparentRenderGraphExtension extension)
                                extension.ExecuteBeforeTransparents(context.cmd, data.frameData);
                        }
                    }

                    bool yFlip = RenderingUtils.IsHandleYFlipped(context, in (data.albedoHdl.IsValid() ? ref data.albedoHdl : ref data.depthHdl));

                    bool useScreenSpaceIrradiance = data.screenSpaceIrradianceHdl.IsValid();
                    context.cmd.SetKeyword(ShaderGlobalKeywords.ScreenSpaceIrradiance, useScreenSpaceIrradiance);
                    if (useScreenSpaceIrradiance)
                    {
                        context.cmd.SetGlobalTexture(ShaderPropertyId.screenSpaceIrradiance, data.screenSpaceIrradianceHdl);
                    }

                    if (data.sampledDepthHdl.IsValid())
                    {
                        context.cmd.SetGlobalTexture(s_CameraDepthTexturePropID, data.sampledDepthHdl);
                    }

                    context.cmd.SetKeyword(ShaderGlobalKeywords.TransparentRefractiveSort, data.waterRefractionSort);
                    if (data.waterSurfaceDepthHdl.IsValid())
                    {
                        context.cmd.SetGlobalTexture(ShaderPropertyId.waterSurfaceDepthTexture, data.waterSurfaceDepthHdl, RenderTextureSubElement.Depth);
                        context.cmd.SetGlobalTexture(ShaderPropertyId.waterStencilTexture, data.waterSurfaceDepthHdl, RenderTextureSubElement.Stencil);
                    }

                    ExecutePass(context.cmd, data, data.rendererListHdl, data.objectsWithErrorRendererListHdl, yFlip);
                    context.cmd.SetKeyword(ShaderGlobalKeywords.TransparentRefractiveSort, false);

                    if (data.sampledDepthHdl.IsValid() && data.restoreDepthHdl.IsValid())
                        context.cmd.SetGlobalTexture(s_CameraDepthTexturePropID, data.restoreDepthHdl);

                    if (data.hasProceduralExtensions && data.rendererFeatures != null)
                    {
                        for (int i = 0; i < data.rendererFeatures.Count; ++i)
                        {
                            ScriptableRendererFeature feature = data.rendererFeatures[i];
                            if (feature != null && feature.isActive &&
                                feature is IUniversalProceduralRenderGraphExtension extension)
                                extension.ExecuteOpaquePass(context.cmd, data.frameData, false);
                        }
                    }
                });
            }
        }
    }

    /// <summary>
    /// Extension of DrawObjectPass that also output Rendering Layers Texture as second render target.
    /// </summary>
    internal class DrawObjectsWithRenderingLayersPass : DrawObjectsPass
    {
#if URP_COMPATIBILITY_MODE
        RTHandle[] m_ColorTargetIndentifiers;
        RTHandle m_DepthTargetIndentifiers;
#endif

        /// <summary>
        /// Creates a new <c>DrawObjectsWithRenderingLayersPass</c> instance.
        /// </summary>
        /// <param name="profilerTag">The profiler tag used with the pass.</param>
        /// <param name="opaque">Marks whether the objects are opaque or transparent.</param>
        /// <param name="evt">The <c>RenderPassEvent</c> to use.</param>
        /// <param name="renderQueueRange">The <c>RenderQueueRange</c> to use for creating filtering settings that control what objects get rendered.</param>
        /// <param name="layerMask">The layer mask to use for creating filtering settings that control what objects get rendered.</param>
        /// <param name="stencilState">The stencil settings to use with this poss.</param>
        /// <param name="stencilReference">The stencil reference value to use with this pass.</param>
        public DrawObjectsWithRenderingLayersPass(URPProfileId profilerTag, bool opaque, RenderPassEvent evt, RenderQueueRange renderQueueRange, LayerMask layerMask, StencilState stencilState, int stencilReference) :
            base(profilerTag, opaque, evt, renderQueueRange, layerMask, stencilState, stencilReference)
        {
#if URP_COMPATIBILITY_MODE
            m_ColorTargetIndentifiers = new RTHandle[2];
#endif
        }

#if URP_COMPATIBILITY_MODE
        /// <summary>
        /// Sets up the pass.
        /// </summary>
        /// <param name="colorAttachment">Color attachment handle.</param>
        /// <param name="renderingLayersTexture">Texture used with rendering layers.</param>
        /// <param name="depthAttachment">Depth attachment handle.</param>
        /// <exception cref="ArgumentException"></exception>
        public void Setup(RTHandle colorAttachment, RTHandle renderingLayersTexture, RTHandle depthAttachment)
        {
            if (colorAttachment == null)
                throw new ArgumentException("Color attachment can not be null", "colorAttachment");
            if (renderingLayersTexture == null)
                throw new ArgumentException("Rendering layers attachment can not be null", "renderingLayersTexture");
            if (depthAttachment == null)
                throw new ArgumentException("Depth attachment can not be null", "depthAttachment");

            m_ColorTargetIndentifiers[0] = colorAttachment;
            m_ColorTargetIndentifiers[1] = renderingLayersTexture;
            m_DepthTargetIndentifiers = depthAttachment;
        }

        /// <inheritdoc/>
        [Obsolete(DeprecationMessage.CompatibilityScriptingAPIObsoleteFrom2023_3)]
        public override void Configure(CommandBuffer cmd, RenderTextureDescriptor cameraTextureDescriptor)
        {
            // Disable obsolete warning for internal usage
            #pragma warning disable CS0618
            ConfigureTarget(m_ColorTargetIndentifiers, m_DepthTargetIndentifiers);
            #pragma warning restore CS0618
        }

        /// <inheritdoc/>
        [Obsolete(DeprecationMessage.CompatibilityScriptingAPIObsoleteFrom2023_3)]
        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            CommandBuffer cmd = renderingData.commandBuffer;

            // Enable Rendering Layers
            cmd.SetKeyword(ShaderGlobalKeywords.WriteRenderingLayers, true);

            // Execute
            base.Execute(context, ref renderingData);

            // Clean up
            cmd.SetKeyword(ShaderGlobalKeywords.WriteRenderingLayers, false);
        }
#endif

        private class RenderingLayersPassData
        {
            internal PassData basePassData;
            internal RenderingLayerUtils.MaskSize maskSize;

            public RenderingLayersPassData()
            {
                basePassData = new PassData();
            }
        }

        internal void Render(
            RenderGraph renderGraph,
            ContextContainer frameData,
            TextureHandle colorTarget,
            TextureHandle renderingLayersTexture,
            TextureHandle depthTarget,
            TextureHandle mainShadowsTexture,
            TextureHandle additionalShadowsTexture,
            RenderingLayerUtils.MaskSize maskSize,
            uint batchLayerMask = uint.MaxValue,
            List<ScriptableRendererFeature> rendererFeatures = null)
        {
            using (var builder = renderGraph.AddRasterRenderPass<RenderingLayersPassData>(passName, out var passData, profilingSampler))
            {
                UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();
                UniversalRenderingData renderingData = frameData.Get<UniversalRenderingData>();
                UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();
                UniversalLightData lightData = frameData.Get<UniversalLightData>();                

                InitPassData(cameraData, ref passData.basePassData, batchLayerMask);

                passData.maskSize = maskSize;

                passData.basePassData.albedoHdl = colorTarget;
                builder.SetRenderAttachment(colorTarget, 0, AccessFlags.Write);
                builder.SetRenderAttachment(renderingLayersTexture, 1, AccessFlags.Write);

                bool disableZWrite = CanDisableZWrite(cameraData, passData.basePassData.isOpaque);
                bool proceduralWritesDepth = passData.basePassData.isOpaque &&
                    ProceduralExtensionsWriteOpaqueDepth(rendererFeatures, frameData);
                var depthAccessFlags = disableZWrite && !proceduralWritesDepth
                    ? AccessFlags.Read
                    : AccessFlags.ReadWrite;
                passData.basePassData.depthHdl = depthTarget;
                builder.SetRenderAttachmentDepth(depthTarget, depthAccessFlags);

                if (mainShadowsTexture.IsValid())
                    builder.UseTexture(mainShadowsTexture, AccessFlags.Read);
                if (additionalShadowsTexture.IsValid())
                    builder.UseTexture(additionalShadowsTexture, AccessFlags.Read);

                UniversalRenderer renderer = cameraData.renderer as UniversalRenderer;
                if (renderer != null)
                {
                    TextureHandle ssaoTexture = resourceData.ssaoTexture;
                    if (ssaoTexture.IsValid())
                        builder.UseTexture(ssaoTexture, AccessFlags.Read);

                    TextureHandle screenSpaceShadowsTexture = resourceData.screenSpaceShadowsTexture;
                    if (screenSpaceShadowsTexture.IsValid())
                        builder.UseTexture(screenSpaceShadowsTexture, AccessFlags.Read);

                    TextureHandle contactShadowsTexture = resourceData.contactShadowsTexture;
                    if (contactShadowsTexture.IsValid())
                        builder.UseTexture(contactShadowsTexture, AccessFlags.Read);

                    RenderGraphUtils.UseDBufferIfValid(builder, resourceData);
                }

                if (rendererFeatures != null)
                {
                    passData.basePassData.frameData = frameData;
                    passData.basePassData.rendererFeatures = rendererFeatures;
                    for (int i = 0; i < rendererFeatures.Count; ++i)
                    {
                        ScriptableRendererFeature feature = rendererFeatures[i];
                        if (feature == null || !feature.isActive ||
                            feature is not IUniversalProceduralRenderGraphExtension extension)
                            continue;
                        passData.basePassData.hasProceduralExtensions |=
                            extension.PrepareOpaquePass(builder, frameData, true);
                    }
                    if (passData.basePassData.hasProceduralExtensions)
                        builder.AllowPassCulling(false);
                }

                InitRendererLists(renderingData, cameraData, lightData, ref passData.basePassData, default(ScriptableRenderContext), renderGraph, true, disableZWrite);

                var activeDebugHandler = GetActiveDebugHandler(cameraData);
                if (activeDebugHandler != null)
                {
                    passData.basePassData.debugRendererLists.PrepareRendererListForRasterPass(builder);
                }
                else
                {
                    builder.UseRendererList(passData.basePassData.rendererListHdl);
                    builder.UseRendererList(passData.basePassData.objectsWithErrorRendererListHdl);
                }

                // Required here because of RenderingLayerUtils.SetupProperties
                builder.AllowGlobalStateModification(true);

                if (cameraData.xr.enabled)
                {
                    bool passSupportsFoveation = cameraData.xrUniversal.canFoveateIntermediatePasses || resourceData.isActiveTargetBackBuffer;
                    builder.EnableFoveatedRasterization(cameraData.xr.supportsFoveatedRendering && passSupportsFoveation);
                    builder.SetExtendedFeatureFlags(ExtendedFeatureFlags.MultiviewRenderRegionsCompatible);
                }

                builder.SetRenderFunc((RenderingLayersPassData data, RasterGraphContext context) =>
                {
                    // Enable Rendering Layers
                    context.cmd.SetKeyword(ShaderGlobalKeywords.WriteRenderingLayers, true);

                    RenderingLayerUtils.SetupProperties(context.cmd, data.maskSize);

                    // Currently we only need to call this additional pass when the user
                    // doesn't want transparent objects to receive shadows
                    if (data.basePassData.applyTransparentShadowSettings && !data.basePassData.isOpaque && !data.basePassData.shouldTransparentsReceiveShadows)
                        TransparentSettingsPass.ExecutePass(context.cmd);

                    bool yFlip = RenderingUtils.IsHandleYFlipped(context, in (data.basePassData.albedoHdl.IsValid() ? ref data.basePassData.albedoHdl : ref data.basePassData.depthHdl));

                    // Execute
                    ExecutePass(context.cmd, data.basePassData, data.basePassData.rendererListHdl, data.basePassData.objectsWithErrorRendererListHdl, yFlip);

                    if (data.basePassData.hasProceduralExtensions && data.basePassData.rendererFeatures != null)
                    {
                        for (int i = 0; i < data.basePassData.rendererFeatures.Count; ++i)
                        {
                            ScriptableRendererFeature feature = data.basePassData.rendererFeatures[i];
                            if (feature != null && feature.isActive &&
                                feature is IUniversalProceduralRenderGraphExtension extension)
                                extension.ExecuteOpaquePass(
                                    context.cmd, data.basePassData.frameData, true);
                        }
                    }

                    // Clean up
                    context.cmd.SetKeyword(ShaderGlobalKeywords.WriteRenderingLayers, false);
                });
            }
        }
    }
}
