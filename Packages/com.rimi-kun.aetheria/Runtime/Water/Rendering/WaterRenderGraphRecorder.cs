using Aetheria.Runtime.Rendering;
using Aetheria.Runtime.Overrides;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.RenderGraphModule;

namespace Aetheria.Runtime.Water
{
    internal static class WaterRenderGraphRecorder
    {
        internal static void RecordScreenSpaceShadowReceiver(
            WaterRenderSystem system,
            RenderGraph renderGraph,
            ContextContainer frameData)
        {
            if (!EnsureGBuffer(system, renderGraph, frameData, false, out WaterFrameResources frame))
                return;

            ScreenSpaceShadowReceiverData receiverData = frameData.GetOrCreate<ScreenSpaceShadowReceiverData>();
            receiverData.depth = frame.waterDepth;
            receiverData.normals = frame.surfaceGBuffer;
            receiverData.normalEncoding = ScreenSpaceShadowReceiverNormalEncoding.Octahedral888;
            receiverData.isValid = frame.waterDepth.IsValid() && frame.surfaceGBuffer.IsValid();
            frame.screenSpaceShadowsEnabled = receiverData.isValid;
        }

        internal static void RecordTransparentPrepass(
            WaterRenderSystem system,
            RenderGraph renderGraph,
            ContextContainer frameData,
            bool publishScreenSpaceReflectionReceiver)
        {
            if (!EnsureGBuffer(system, renderGraph, frameData, true, out WaterFrameResources frame))
                return;

            if (!frameData.Contains<TransparentRefractionFrameData>())
                return;

            TransparentRefractionFrameData refractionData = frameData.Get<TransparentRefractionFrameData>();
            if (!refractionData.depthBuffer.IsValid()
                || !refractionData.depthBufferPreRefraction.IsValid()
                || !refractionData.beforeRefraction.IsValid())
                return;

            frame.opaqueDepth = refractionData.depthBufferPreRefraction;
            if (!refractionData.waterSurfaceDepth.IsValid())
            {
                refractionData.waterSurfaceDepth = frame.waterDepth;
                refractionData.isValid = frame.waterDepth.IsValid();
            }

            if (publishScreenSpaceReflectionReceiver)
            {
                ScreenSpaceReflectionReceiverData receiverData = frameData.GetOrCreate<ScreenSpaceReflectionReceiverData>();
                receiverData.depth = frame.waterDepth;
                receiverData.normalSmoothness = frame.metadataGBuffer;
                receiverData.isValid = frame.waterDepth.IsValid() && frame.metadataGBuffer.IsValid();
            }
        }

        private static bool EnsureGBuffer(
            WaterRenderSystem system,
            RenderGraph renderGraph,
            ContextContainer frameData,
            bool usePreRefractionDepth,
            out WaterFrameResources frame)
        {
            frame = null;
            if (!PrepareFrame(system, renderGraph, frameData, out WaterFrameResources preparedFrame, out WaterSimulationFrame simulationFrame, out WaterCameraContext cameraContext))
                return false;

            UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();
            TextureHandle opaqueDepth = resourceData.cameraDepthTexture;
            if (usePreRefractionDepth && frameData.Contains<TransparentRefractionFrameData>())
            {
                TransparentRefractionFrameData refractionData = frameData.Get<TransparentRefractionFrameData>();
                if (refractionData.depthBufferPreRefraction.IsValid())
                    opaqueDepth = refractionData.depthBufferPreRefraction;
            }

            TextureHandle depthAttachment = resourceData.activeDepthTexture;
            if (!opaqueDepth.IsValid() || !depthAttachment.IsValid())
                return false;

            WaterFrameResourceAllocator.AllocateGBuffer(
                renderGraph,
                resourceData,
                opaqueDepth,
                depthAttachment,
                preparedFrame);

            if (!preparedFrame.gBufferRecorded)
            {
                preparedFrame.opaqueDepth = opaqueDepth;
                WaterGBufferPass.Record(system, renderGraph, preparedFrame, preparedFrame.cameraRenderData, simulationFrame);
                preparedFrame.gBufferRecorded = true;
            }

            frame = preparedFrame;
            return true;
        }

        internal static void RecordDeferredLighting(
            WaterRenderSystem system,
            RenderGraph renderGraph,
            ContextContainer frameData)
        {
            if (system == null || !system.isInitialized || system.shading == null || !system.shading.IsValid())
                return;

            WaterFrameResources frame = frameData.GetOrCreate<WaterFrameResources>();
            if (!frame.gBufferRecorded || !frame.gBufferResourcesAllocated)
                return;

            UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();
            TextureHandle sceneColor = resourceData.activeColorTexture;
            if (!sceneColor.IsValid())
                return;

            bool hasWaterScreenSpaceShadows = frame.screenSpaceShadowsEnabled;
            if (hasWaterScreenSpaceShadows && !resourceData.screenSpaceShadowsTexture.IsValid())
                return;

            TextureDesc targetDesc = renderGraph.GetTextureDesc(sceneColor);
            targetDesc.name = "Water Lighting Color";
            targetDesc.depthBufferBits = DepthBits.None;
            targetDesc.msaaSamples = MSAASamples.None;
            targetDesc.useMipMap = false;
            targetDesc.autoGenerateMips = false;
            targetDesc.enableRandomWrite = true;
            targetDesc.clearBuffer = false;
            TextureHandle target = renderGraph.CreateTexture(targetDesc);

            TransparentRefractionFrameData refractionData = frameData.Contains<TransparentRefractionFrameData>()
                ? frameData.Get<TransparentRefractionFrameData>()
                : null;
            bool compositeBeforeRefraction = !frame.underwaterLineRecorded
                && refractionData is { isValid: true }
                && refractionData.beforeRefraction.IsValid();
            TextureHandle beforeRefraction = compositeBeforeRefraction
                ? refractionData.beforeRefraction
                : TextureHandle.nullHandle;

            AtmosphereFrameData atmosphereFrame = frameData.Contains<AtmosphereFrameData>()
                ? frameData.Get<AtmosphereFrameData>()
                : null;
            bool atmosphereEnabled = atmosphereFrame is { IsActive: true }
                                     && atmosphereFrame.Volume
                                     && atmosphereFrame.AerialPerspective.IsValid();

            FogFrameData fogFrame = frameData.Contains<FogFrameData>()
                ? frameData.Get<FogFrameData>()
                : null;
            bool fogEnabled = fogFrame is { IsActive: true, CompositeOpaque: true };
            bool fogSkyColorEnabled = fogEnabled
                                      && Mathf.Approximately(
                                          fogFrame.Parameters.ColorMode,
                                          (float)FogColorMode.SkyColor)
                                      && fogFrame.FilteredSky.IsValid();
            bool volumetricFogEnabled = fogEnabled
                                        && fogFrame.UseVolumetricFog
                                        && fogFrame.IntegratedFog.IsValid();

            WaterDeferredLightingPass.Record(
                system.shading,
                renderGraph,
                resourceData,
                sceneColor,
                frame.cameraContext,
                frame,
                target,
                atmosphereFrame,
                fogFrame,
                atmosphereEnabled,
                fogEnabled,
                fogSkyColorEnabled,
                volumetricFogEnabled,
                hasWaterScreenSpaceShadows,
                beforeRefraction,
                compositeBeforeRefraction);

            resourceData.cameraColor = target;
        }


        internal static void RecordUnderwaterResources(
            WaterRenderSystem system,
            RenderGraph renderGraph,
            ContextContainer frameData)
        {
            if (!PrepareFrame(system, renderGraph, frameData, out WaterFrameResources frame, out _, out _)
                || !frame.gBufferRecorded
                || !frame.gBufferResourcesAllocated)
                return;

            WaterUnderwaterLinePass.Record(system, renderGraph, frameData);
        }

        private static bool PrepareFrame(
            WaterRenderSystem system,
            RenderGraph renderGraph,
            ContextContainer frameData,
            out WaterFrameResources frame,
            out WaterSimulationFrame simulationFrame,
            out WaterCameraContext cameraContext)
        {
            frame = frameData.GetOrCreate<WaterFrameResources>();
            simulationFrame = frameData.GetOrCreate<WaterSimulationFrame>();

            UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();
            if (frame.preparedForCamera)
            {
                cameraContext = frame.cameraContext;
                return frame.cameraRenderData.drawCount > 0;
            }

            cameraContext = WaterCameraContext.Create(cameraData);
            frame.cameraContext = cameraContext;

            if (!system.isInitialized || system.activeSurfaceCount == 0)
                return false;

            if (cameraData.camera == null
                || system.simulationPipeline == null
                || system.surfaceRenderer == null
                || system.shading == null
                || !system.shading.IsValid())
                return false;

            system.UpdateSurfacesForCamera(cameraContext);
            system.simulationPipeline.Record(
                renderGraph,
                simulationFrame,
                system.activeSurfaces,
                system.activeSurfaceCount,
                system.currentSectorSwizzleBuffer);
            system.PrepareSurfacesForRender(cameraContext, frame.cameraRenderData);
            frame.preparedForCamera = true;
            return frame.cameraRenderData.drawCount > 0;
        }
    }
}
