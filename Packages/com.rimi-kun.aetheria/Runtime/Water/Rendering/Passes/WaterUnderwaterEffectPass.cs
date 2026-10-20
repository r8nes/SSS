using Aetheria.Runtime.Rendering;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.RenderGraphModule;

namespace Aetheria.Runtime.Water
{
    internal static class WaterUnderwaterEffectPass
    {
        private sealed class PassData
        {
            public Material material;
            public MaterialPropertyBlock properties;
            public TextureHandle sourceColor;
            public TextureHandle sceneDepth;
            public TextureHandle beforeRefraction;
            public TextureHandle waterDepth;
            public TextureHandle screenSpaceShadows;
            public bool hasScreenSpaceShadows;
            public TextureHandle mainShadows;
            public TextureHandle waveField0;
            public TextureHandle waveField1;
            public TextureHandle waveField2;
            public BufferHandle simulationConstantBufferHandle;
            public BufferHandle surfaceConstantBufferHandle;
            public BufferHandle waterLine;
            public BufferHandle cameraHeight;
        }

        internal static void Record(WaterRenderSystem system, RenderGraph renderGraph, ContextContainer frameData)
        {
            if (system == null || !system.isInitialized || system.underwater == null || !system.underwater.IsValid)
                return;

            WaterFrameResources frame = frameData.Get<WaterFrameResources>();
            if (frame == null || !frame.gBufferRecorded || !frame.waterDepth.IsValid()
                || !frame.underwaterLineRecorded || !frame.underwaterLine.IsValid()
                || !frame.underwaterCameraHeight.IsValid())
                return;

            int surfaceIndex = frame.underwaterSurfaceIndex;
            if (surfaceIndex < 0 || surfaceIndex >= frame.cameraRenderData.surfaceCount
                || surfaceIndex >= system.activeSurfaceCount)
                return;

            ref WaterSurfaceRenderData surfaceData = ref frame.cameraRenderData.surfaces[surfaceIndex];
            WaterSurface surface = system.activeSurfaces[surfaceIndex];
            if (!surfaceData.shouldRender || surface == null)
                return;

            WaterSimulationFrame simulationFrame = frameData.Get<WaterSimulationFrame>();
            UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();
            Camera camera = cameraData.camera;
            UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();
            TransparentRefractionFrameData refractionData = frameData.Contains<TransparentRefractionFrameData>()
                ? frameData.Get<TransparentRefractionFrameData>()
                : null;
            if (refractionData is not { isValid: true } || !refractionData.beforeRefraction.IsValid())
                return;

            TextureHandle sourceColor = resourceData.activeColorTexture;
            TextureHandle sceneDepth = frame.opaqueDepth.IsValid()
                ? frame.opaqueDepth
                : resourceData.cameraDepthTexture;
            if (!sourceColor.IsValid() || !sceneDepth.IsValid())
                return;

            bool hasScreenSpaceShadows = frame.screenSpaceShadowsEnabled;
            if (hasScreenSpaceShadows && !resourceData.screenSpaceShadowsTexture.IsValid())
                return;

            TextureDesc colorDesc = renderGraph.GetTextureDesc(sourceColor);
            var outputDesc = colorDesc;
            outputDesc.name = "Water Underwater Color";
            outputDesc.clearBuffer = false;
            outputDesc.enableRandomWrite = false;
            TextureHandle outputColor = renderGraph.CreateTexture(outputDesc);

            using (var profiledPass = WaterProfiling.UnderwaterEffect.AddRasterPass<PassData>(
                renderGraph, out var passData))
            {
                var builder = profiledPass.Builder;
                passData.material = system.underwater.effectMaterial;
                passData.properties = system.underwater.BuildEffectProperties(
                    camera, surface, frame.underwaterLineParameters);
                passData.properties.SetConstantBuffer(
                    WaterShaderIDs._WaterSimulationShaderVariables,
                    surfaceData.simulationConstantBuffer,
                    0,
                    surfaceData.simulationConstantBuffer.stride);
                passData.properties.SetConstantBuffer(
                    WaterShaderIDs._WaterSurfaceShaderVariables,
                    surfaceData.surfaceConstantBuffer,
                    0,
                    surfaceData.surfaceConstantBuffer.stride);
                passData.properties.SetTexture(
                    WaterShaderIDs._WaterWaveFieldCascade0,
                    surface.simulation.gpuResources.waveCascades[0].waveField);
                passData.properties.SetTexture(
                    WaterShaderIDs._WaterWaveFieldCascade1,
                    surface.simulation.gpuResources.waveCascades[1].waveField);
                passData.properties.SetTexture(
                    WaterShaderIDs._WaterWaveFieldCascade2,
                    surface.simulation.gpuResources.waveCascades[2].waveField);
                passData.sourceColor = sourceColor;
                passData.sceneDepth = sceneDepth;
                passData.beforeRefraction = refractionData.beforeRefraction;
                passData.waterDepth = frame.waterDepth;
                passData.hasScreenSpaceShadows = hasScreenSpaceShadows;
                passData.screenSpaceShadows = passData.hasScreenSpaceShadows
                    ? resourceData.screenSpaceShadowsTexture
                    : renderGraph.defaultResources.whiteTexture;
                passData.mainShadows = passData.hasScreenSpaceShadows
                    ? TextureHandle.nullHandle
                    : resourceData.mainShadowsTexture;
                passData.simulationConstantBufferHandle = renderGraph.ImportBuffer(surfaceData.simulationConstantBuffer);
                passData.surfaceConstantBufferHandle = renderGraph.ImportBuffer(surfaceData.surfaceConstantBuffer);
                passData.waveField0 = simulationFrame != null && surfaceIndex < simulationFrame.surfaceCount
                    ? simulationFrame.WaveField(surfaceIndex, 0)
                    : TextureHandle.nullHandle;
                passData.waveField1 = simulationFrame != null && surfaceIndex < simulationFrame.surfaceCount
                    ? simulationFrame.WaveField(surfaceIndex, 1)
                    : TextureHandle.nullHandle;
                passData.waveField2 = simulationFrame != null && surfaceIndex < simulationFrame.surfaceCount
                    ? simulationFrame.WaveField(surfaceIndex, 2)
                    : TextureHandle.nullHandle;
                passData.waterLine = frame.underwaterLine;
                passData.cameraHeight = frame.underwaterCameraHeight;

                builder.UseTexture(passData.sourceColor, AccessFlags.Read);
                builder.UseTexture(passData.sceneDepth, AccessFlags.Read);
                builder.UseTexture(passData.beforeRefraction, AccessFlags.Read);
                builder.UseTexture(passData.waterDepth, AccessFlags.Read);
                builder.UseBuffer(passData.simulationConstantBufferHandle, AccessFlags.Read);
                builder.UseBuffer(passData.surfaceConstantBufferHandle, AccessFlags.Read);
                if (passData.waveField0.IsValid())
                    builder.UseTexture(passData.waveField0, AccessFlags.Read);
                if (passData.waveField1.IsValid())
                    builder.UseTexture(passData.waveField1, AccessFlags.Read);
                if (passData.waveField2.IsValid())
                    builder.UseTexture(passData.waveField2, AccessFlags.Read);
                builder.UseBuffer(passData.waterLine, AccessFlags.Read);
                builder.UseBuffer(passData.cameraHeight, AccessFlags.Read);
                builder.UseTexture(passData.screenSpaceShadows, AccessFlags.Read);
                if (passData.mainShadows.IsValid())
                    builder.UseTexture(passData.mainShadows, AccessFlags.Read);
                builder.SetRenderAttachment(outputColor, 0, AccessFlags.Write);
                builder.AllowGlobalStateModification(true);

                builder.SetRenderFunc(static (PassData data, RasterGraphContext context) =>
                {
                    context.cmd.SetGlobalTexture(WaterShaderIDs._WaterUnderwaterCameraColorTexture, data.sourceColor);
                    context.cmd.SetGlobalTexture(WaterShaderIDs._WaterUnderwaterSceneDepthTexture, data.sceneDepth);
                    context.cmd.SetGlobalTexture(ShaderProperties.Composite.BeforeRefraction, data.beforeRefraction);
                    context.cmd.SetGlobalTexture(WaterShaderIDs._WaterDepthTexture, data.waterDepth);
                    context.cmd.SetGlobalTexture(WaterShaderIDs._ScreenSpaceShadowmapTexture, data.screenSpaceShadows);
                    context.cmd.SetGlobalInt(WaterShaderIDs._WaterHasScreenSpaceShadows, data.hasScreenSpaceShadows ? 1 : 0);
                    if (data.mainShadows.IsValid())
                    {
                        context.cmd.SetGlobalTexture(WaterShaderIDs._MainLightShadowmapTexture, data.mainShadows);
                        context.cmd.SetGlobalTexture(WaterShaderIDs._MainLightShadowmapTextureRaw, data.mainShadows);
                    }
                    context.cmd.SetGlobalBuffer(WaterShaderIDs._WaterUnderwaterLine, data.waterLine);
                    context.cmd.SetGlobalBuffer(WaterShaderIDs._WaterCameraHeightBuffer, data.cameraHeight);
                    context.cmd.DrawProcedural(
                        Matrix4x4.identity,
                        data.material,
                        0,
                        MeshTopology.Triangles,
                        3,
                        1,
                        data.properties);
                });
            }

            resourceData.cameraColor = outputColor;
        }
    }
}
