using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.RenderGraphModule;
using static Unity.Mathematics.math;

namespace Aetheria.Runtime.Water
{
    internal static class WaterUnderwaterLinePass
    {
        private sealed class PassData
        {
            public ComputeShader evaluationShader;
            public int evaluationKernel;
            public GraphicsBuffer simulationConstantBuffer;
            public GraphicsBuffer surfaceConstantBuffer;
            public BufferHandle simulationConstantBufferHandle;
            public BufferHandle surfaceConstantBufferHandle;
            public GraphicsBuffer currentSectorSwizzleBuffer;
            public BufferHandle currentSectorSwizzleHandle;
            public TextureHandle waveField0;
            public TextureHandle waveField1;
            public TextureHandle waveField2;
            public TextureHandle simulationMask;
            public TextureHandle currentMap;
            public bool usesSimulation;
            public bool usesMask;
            public bool usesCurrent;
            public Vector3 targetPositionOS;
            public BufferHandle cameraHeight;

            public ComputeShader lineShader;
            public int clearKernel;
            public int lineEvaluationKernel;
            public int boundsPropagationKernel;
            public TextureHandle waterDepth;
            public BufferHandle waterLine;
            public WaterUnderwaterRenderer.WaterLineParameters parameters;
        }

        internal static void Record(WaterRenderSystem system, RenderGraph renderGraph, ContextContainer frameData)
        {
            if (system == null || !system.isInitialized || system.underwater == null || !system.underwater.IsValid)
                return;

            WaterFrameResources frame = frameData.Get<WaterFrameResources>();
            if (frame == null || !frame.gBufferRecorded)
                return;

            WaterSimulationFrame simulationFrame = frameData.Get<WaterSimulationFrame>();
            UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();
            Camera camera = cameraData.camera;
            WaterCameraContext cameraContext = frame.cameraContext;
            int surfaceIndex = system.underwater.FindSurface(
                system.activeSurfaces, system.activeSurfaceCount, cameraContext);
            if (surfaceIndex < 0 || surfaceIndex >= frame.cameraRenderData.surfaceCount
                || surfaceIndex >= simulationFrame.surfaceCount)
                return;

            ref WaterSurfaceRenderData surfaceData = ref frame.cameraRenderData.surfaces[surfaceIndex];
            WaterSurface surface = system.activeSurfaces[surfaceIndex];
            if (!surfaceData.shouldRender || surface == null || surface.simulation == null)
                return;

            int cascadeCount = simulationFrame.surfaces[surfaceIndex].cascadeCount;
            bool usesSimulation = cascadeCount > 0;
            if (usesSimulation)
            {
                if (cascadeCount != WaterLimits.CascadeCount
                    || surfaceData.simulationConstantBuffer == null
                    || surfaceData.surfaceConstantBuffer == null)
                    return;

                for (int cascadeIndex = 0; cascadeIndex < WaterLimits.CascadeCount; ++cascadeIndex)
                    if (!simulationFrame.WaveField(surfaceIndex, cascadeIndex).IsValid())
                        return;
            }

            TextureHandle waterDepth = frame.waterDepth;
            if (!waterDepth.IsValid())
                return;

            // HDRP builds the line from the camera viewport, not from the backing RTHandle size.
            // SceneView/dynamic-resolution targets can be larger than the active viewport.
            int width = Mathf.Max(cameraContext.width, 1);
            int height = Mathf.Max(cameraContext.height, 1);
            WaterUnderwaterRenderer.WaterLineParameters parameters =
                system.underwater.BuildWaterLineParameters(cameraData, cameraContext, surface, width, height);

            BufferHandle waterLine = renderGraph.CreateBuffer(new BufferDesc(
                parameters.bufferStride,
                sizeof(uint),
                GraphicsBuffer.Target.Structured)
            {
                name = "Waterline"
            });
            BufferHandle cameraHeight = renderGraph.CreateBuffer(new BufferDesc(
                4,
                sizeof(float),
                GraphicsBuffer.Target.Structured)
            {
                name = "Water Camera Height"
            });

            bool usesMask = usesSimulation && surface.simulationMask != null;
            bool usesCurrent = usesSimulation
                && surface.currentMap != null
                && surface.currentMapInfluence > 1e-4f
                && system.currentSectorSwizzleBuffer != null;
            int evaluationKernel = !usesSimulation
                ? system.underwater.evaluationFlatKernel
                : usesCurrent
                    ? (usesMask ? system.underwater.evaluationCurrentMaskKernel : system.underwater.evaluationCurrentKernel)
                    : (usesMask ? system.underwater.evaluationMaskKernel : system.underwater.evaluationKernel);

            TextureHandle simulationMask = usesMask
                ? system.simulationPipeline.m_TextureCache.Import(renderGraph, surface.simulationMask)
                : TextureHandle.nullHandle;
            TextureHandle currentMap = usesCurrent
                ? system.simulationPipeline.m_TextureCache.Import(renderGraph, surface.currentMap)
                : TextureHandle.nullHandle;

            float4 cameraPositionWS = new float4(
                camera.transform.position.x,
                camera.transform.position.y,
                camera.transform.position.z,
                1.0f);
            float3 targetPositionOS = mul(surface.simulation.renderingParameters.worldToWaterMatrix, cameraPositionWS).xyz;

            using (var profiledPass = WaterProfiling.UnderwaterLine.AddComputePass<PassData>(
                renderGraph, out var passData))
            {
                var builder = profiledPass.Builder;
                passData.evaluationShader = system.underwater.evaluationShader;
                passData.evaluationKernel = evaluationKernel;
                passData.usesSimulation = usesSimulation;
                passData.simulationConstantBuffer = usesSimulation ? surfaceData.simulationConstantBuffer : null;
                passData.surfaceConstantBuffer = usesSimulation ? surfaceData.surfaceConstantBuffer : null;
                passData.simulationConstantBufferHandle = usesSimulation
                    ? renderGraph.ImportBuffer(surfaceData.simulationConstantBuffer)
                    : default;
                passData.surfaceConstantBufferHandle = usesSimulation
                    ? renderGraph.ImportBuffer(surfaceData.surfaceConstantBuffer)
                    : default;
                passData.currentSectorSwizzleBuffer = system.currentSectorSwizzleBuffer;
                passData.currentSectorSwizzleHandle = usesCurrent && system.currentSectorSwizzleBuffer != null
                    ? renderGraph.ImportBuffer(system.currentSectorSwizzleBuffer)
                    : default;
                passData.waveField0 = usesSimulation ? simulationFrame.WaveField(surfaceIndex, 0) : TextureHandle.nullHandle;
                passData.waveField1 = usesSimulation ? simulationFrame.WaveField(surfaceIndex, 1) : TextureHandle.nullHandle;
                passData.waveField2 = usesSimulation ? simulationFrame.WaveField(surfaceIndex, 2) : TextureHandle.nullHandle;
                passData.simulationMask = simulationMask;
                passData.currentMap = currentMap;
                passData.usesMask = usesMask;
                passData.usesCurrent = usesCurrent;
                passData.targetPositionOS = targetPositionOS;
                passData.cameraHeight = cameraHeight;

                passData.lineShader = system.underwater.lineShader;
                passData.clearKernel = system.underwater.clearLineKernel;
                passData.lineEvaluationKernel = system.underwater.lineEvaluationKernel;
                passData.boundsPropagationKernel = system.underwater.boundsPropagationKernel;
                passData.waterDepth = waterDepth;
                passData.waterLine = waterLine;
                passData.parameters = parameters;

                if (passData.usesSimulation)
                {
                    builder.UseBuffer(passData.simulationConstantBufferHandle, AccessFlags.Read);
                    builder.UseBuffer(passData.surfaceConstantBufferHandle, AccessFlags.Read);
                }
                if (passData.currentSectorSwizzleHandle.IsValid())
                    builder.UseBuffer(passData.currentSectorSwizzleHandle, AccessFlags.Read);
                if (passData.waveField0.IsValid())
                    builder.UseTexture(passData.waveField0, AccessFlags.Read);
                if (passData.waveField1.IsValid())
                    builder.UseTexture(passData.waveField1, AccessFlags.Read);
                if (passData.waveField2.IsValid())
                    builder.UseTexture(passData.waveField2, AccessFlags.Read);
                if (passData.simulationMask.IsValid())
                    builder.UseTexture(passData.simulationMask, AccessFlags.Read);
                if (passData.currentMap.IsValid())
                    builder.UseTexture(passData.currentMap, AccessFlags.Read);
                builder.UseBuffer(passData.cameraHeight, AccessFlags.ReadWrite);
                builder.UseTexture(passData.waterDepth, AccessFlags.Read);
                builder.UseBuffer(passData.waterLine, AccessFlags.ReadWrite);

                builder.SetRenderFunc(static (PassData data, ComputeGraphContext context) =>
                {
                    var cmd = context.cmd;

                    if (data.usesSimulation)
                    {
                        cmd.SetComputeConstantBufferParam(
                            data.evaluationShader,
                            WaterShaderIDs._WaterSimulationShaderVariables,
                            data.simulationConstantBuffer,
                            0,
                            data.simulationConstantBuffer.stride);
                        cmd.SetComputeConstantBufferParam(
                            data.evaluationShader,
                            WaterShaderIDs._WaterSurfaceShaderVariables,
                            data.surfaceConstantBuffer,
                            0,
                            data.surfaceConstantBuffer.stride);
                    }
                    cmd.SetComputeVectorParam(
                        data.evaluationShader,
                        WaterShaderIDs._WaterEvaluationTargetPositionOS,
                        new Vector4(data.targetPositionOS.x, data.targetPositionOS.y, data.targetPositionOS.z, 0.0f));

                    if (data.waveField0.IsValid())
                        cmd.SetComputeTextureParam(data.evaluationShader, data.evaluationKernel,
                            WaterShaderIDs._WaterWaveFieldCascade0, data.waveField0);
                    if (data.waveField1.IsValid())
                        cmd.SetComputeTextureParam(data.evaluationShader, data.evaluationKernel,
                            WaterShaderIDs._WaterWaveFieldCascade1, data.waveField1);
                    if (data.waveField2.IsValid())
                        cmd.SetComputeTextureParam(data.evaluationShader, data.evaluationKernel,
                            WaterShaderIDs._WaterWaveFieldCascade2, data.waveField2);
                    if (data.usesMask)
                        cmd.SetComputeTextureParam(data.evaluationShader, data.evaluationKernel,
                            WaterShaderIDs._WaterSimulationMask, data.simulationMask);
                    if (data.usesCurrent)
                    {
                        cmd.SetComputeTextureParam(data.evaluationShader, data.evaluationKernel,
                            WaterShaderIDs._WaterCurrentMap, data.currentMap);
                        cmd.SetComputeBufferParam(data.evaluationShader, data.evaluationKernel,
                            WaterShaderIDs._WaterCurrentSectorSwizzles, data.currentSectorSwizzleBuffer);
                    }
                    cmd.SetComputeBufferParam(data.evaluationShader, data.evaluationKernel,
                        WaterShaderIDs._WaterCameraHeightBufferRW, data.cameraHeight);
                    cmd.DispatchCompute(data.evaluationShader, data.evaluationKernel, 1, 1, 1);

                    ComputeShader lineShader = data.lineShader;
                    WaterUnderwaterRenderer.WaterLineParameters p = data.parameters;
                    cmd.SetComputeVectorParam(lineShader, WaterShaderIDs._WaterUnderwaterBoundsSS, p.boundsSS);
                    cmd.SetComputeVectorParam(lineShader, WaterShaderIDs._WaterUnderwaterUpDirection,
                        new Vector4(p.upDirectionSS.x, p.upDirectionSS.y, 0.0f, 0.0f));
                    cmd.SetComputeVectorParam(lineShader, WaterShaderIDs._WaterUnderwaterScreenSize, p.screenSize);
                    cmd.SetComputeIntParam(lineShader, WaterShaderIDs._WaterUnderwaterBufferStride, p.bufferStride);

                    cmd.SetComputeBufferParam(lineShader, data.clearKernel,
                        WaterShaderIDs._WaterUnderwaterLineRW, data.waterLine);
                    cmd.DispatchCompute(lineShader, data.clearKernel, CoreUtils.DivRoundUp(p.bufferStride, 8), 1, 1);

                    cmd.SetComputeTextureParam(lineShader, data.lineEvaluationKernel,
                        WaterShaderIDs._WaterDepthTexture, data.waterDepth);
                    cmd.SetComputeTextureParam(lineShader, data.lineEvaluationKernel,
                        WaterShaderIDs._WaterStencilTexture, data.waterDepth, 0, RenderTextureSubElement.Stencil);
                    cmd.SetComputeBufferParam(lineShader, data.lineEvaluationKernel,
                        WaterShaderIDs._WaterUnderwaterLineRW, data.waterLine);
                    cmd.DispatchCompute(lineShader, data.lineEvaluationKernel,
                        CoreUtils.DivRoundUp((int)p.screenSize.x, 8),
                        CoreUtils.DivRoundUp((int)p.screenSize.y, 8),
                        1);

                    cmd.SetComputeBufferParam(lineShader, data.boundsPropagationKernel,
                        WaterShaderIDs._WaterCameraHeightBuffer, data.cameraHeight);
                    cmd.SetComputeBufferParam(lineShader, data.boundsPropagationKernel,
                        WaterShaderIDs._WaterUnderwaterLineRW, data.waterLine);
                    cmd.DispatchCompute(lineShader, data.boundsPropagationKernel, p.reductionSize, 1, 1);
                });
            }

            frame.underwaterLine = waterLine;
            frame.underwaterCameraHeight = cameraHeight;
            frame.underwaterLineParameters = parameters;
            frame.underwaterSurfaceIndex = surfaceIndex;
            frame.underwaterLineRecorded = true;
        }
    }
}
