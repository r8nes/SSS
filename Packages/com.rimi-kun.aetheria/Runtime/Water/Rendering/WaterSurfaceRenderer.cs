using Aetheria.Runtime.Rendering;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using System.Collections.Generic;


namespace Aetheria.Runtime.Water
{
    internal enum WaterMaterialRole
    {
        Invalid,
        Surface,
    }

    internal static class WaterMaterialResolver
    {
        internal const string GBufferPassName = "Water GBuffer";
        internal const string GBufferWithCurrentPassName = "Water GBuffer With Current";
        internal const string DebugGBufferPassName = "Water GBuffer Debug";
        internal const string DebugGBufferWithCurrentPassName = "Water GBuffer Debug With Current";

        internal static bool TryResolve(
            Material material,
            bool debugEnabled,
            bool usesCurrentMap,
            out WaterMaterialRole role,
            out int passIndex)
        {
            role = WaterMaterialRole.Invalid;
            passIndex = -1;
            if (material == null || material.shader == null)
                return false;

            int physicalPass = material.FindPass(GBufferPassName);
            if (physicalPass < 0)
                return false;

            role = WaterMaterialRole.Surface;
            if (debugEnabled)
                passIndex = material.FindPass(usesCurrentMap ? DebugGBufferWithCurrentPassName : DebugGBufferPassName);
            else
                passIndex = usesCurrentMap ? material.FindPass(GBufferWithCurrentPassName) : physicalPass;
            return passIndex >= 0;
        }
    }

    internal sealed class WaterSurfaceRenderer
    {
        private readonly Vector3[] m_AmbientDirections = new Vector3[1];
        private readonly Color[] m_AmbientLighting = new Color[1];
        private WaterSimulationResolution m_Resolution;
        private Texture m_FoamTexture;
        private Texture m_PhysicalFoamTexture;
        private Material m_DefaultSurfaceMaterial;
        private readonly WaterSurfaceShaderData m_ShaderData = new WaterSurfaceShaderData();
        private readonly WaterSurfaceGeometry m_Geometry = new WaterSurfaceGeometry();
        private readonly WaterLodEvaluation[] m_LodEvaluations = new WaterLodEvaluation[WaterLimits.MaxSurfaceCount];
        private readonly bool[] m_LodEvaluationValid = new bool[WaterLimits.MaxSurfaceCount];
        private bool m_IsInitialized;

        internal bool Initialize(WaterResources resources, WaterSimulationResolution resolution)
        {
            m_Resolution = resolution;
            m_FoamTexture = resources.Foam;
            m_PhysicalFoamTexture = resources.PhysicalFoam;
            if (!m_ShaderData.Initialize(resolution) || !m_Geometry.Initialize())
                return false;

            m_DefaultSurfaceMaterial = resources.Surface != null
                ? CoreUtils.CreateEngineMaterial(resources.Surface)
                : null;
            if (m_DefaultSurfaceMaterial != null)
            {
                m_DefaultSurfaceMaterial.enableInstancing = true;
            }

            m_IsInitialized = true;
            return true;
        }

        internal void Cleanup()
        {
            m_IsInitialized = false;
            CoreUtils.Destroy(m_DefaultSurfaceMaterial);
            m_DefaultSurfaceMaterial = null;
            m_ShaderData.Cleanup();
            m_Geometry.Cleanup();
            m_FoamTexture = null;
            m_PhysicalFoamTexture = null;
        }

        internal void UpdateSurfacesForCamera(WaterSurface[] surfaces, int surfaceCount, WaterCameraContext cameraContext)
        {
            if (!m_IsInitialized || surfaceCount == 0)
                return;

            for (int surfaceIndex = 0; surfaceIndex < surfaceCount; ++surfaceIndex)
            {
                WaterSurface surface = surfaces[surfaceIndex];
                surface.surfaceIndex = surfaceIndex;
                surface.PrepareSimulationFrame(
                    (int)m_Resolution,
                    m_FoamTexture,
                    m_PhysicalFoamTexture);

                WaterSimulationState simulation = surface.simulation;
                WaterLodEvaluation lodEvaluation = default;
                bool lodEvaluationValid = simulation != null && cameraContext.camera != null;
                if (lodEvaluationValid)
                {
                    lodEvaluation = WaterSurfaceGeometry.EvaluateLod(
                        surface,
                        cameraContext,
                        simulation.renderingParameters.maxWaveHeight);
                    simulation.foam.UpdateCascades(surface, cameraContext, lodEvaluation);
                    simulation.gpuResources.UploadFoamCascades(simulation.foam.cascadeParameters);
                }
                m_LodEvaluations[surfaceIndex] = lodEvaluation;
                m_LodEvaluationValid[surfaceIndex] = lodEvaluationValid;
                m_ShaderData.UpdateSimulation(surface);
            }
        }

        internal void PrepareSurfacesForRender(
            WaterSurface[] surfaces,
            int surfaceCount,
            WaterCameraContext cameraContext,
            GraphicsBuffer currentSectorSwizzleBuffer,
            float ambientProbeDimmer,
            float environmentReflectionIntensity,
            WaterCameraRenderData data)
        {
            data.surfaceCount = surfaceCount;
            data.drawCount = 0;
            data.surfaceProfileBuffer = m_ShaderData.profileBuffer;
            data.ambientProbeDimmer = ambientProbeDimmer;
            data.environmentReflectionIntensity = environmentReflectionIntensity;

            LightProbes.GetInterpolatedProbe(
                cameraContext.camera.transform.position,
                null,
                out SphericalHarmonicsL2 ambientProbe);

            m_Geometry.PrepareCamera(cameraContext, surfaces, surfaceCount);

            for (int surfaceIndex = 0; surfaceIndex < surfaceCount; ++surfaceIndex)
            {
                WaterSurface surface = surfaces[surfaceIndex];
                ref WaterSurfaceRenderData surfaceData = ref data.surfaces[surfaceIndex];
                surfaceData.shouldRender = m_Geometry.IsVisible(cameraContext, surface);
                if (!surfaceData.shouldRender)
                    continue;

                PrepareSurfaceRenderData(
                    cameraContext,
                    surface,
                    surfaceIndex,
                    ref surfaceData);
                if (!surfaceData.shouldRender)
                    continue;

                int surfaceDrawCount = PrepareDraws(surface, ref surfaceData);
                if (surfaceDrawCount == 0)
                {
                    surfaceData.shouldRender = false;
                    continue;
                }
                data.drawCount += surfaceDrawCount;

                m_ShaderData.UpdateAppearance(surface);
                PrepareSurfaceBindings(surface, currentSectorSwizzleBuffer, ref surfaceData);

                WaterRenderMode renderMode = surfaceData.debugEnabled
                    ? WaterRenderMode.Debug
                    : WaterRenderMode.Physical;
                m_AmbientDirections[0] = cameraContext.GetSurfaceUp(surface);
                ambientProbe.Evaluate(m_AmbientDirections, m_AmbientLighting);
                m_ShaderData.FillProfile(
                    surfaceIndex,
                    renderMode,
                    surface,
                    m_AmbientLighting[0]);
                surfaceData.propertyBlock.SetFloat(WaterShaderIDs._WaterAmbientProbeDimmer, data.ambientProbeDimmer);
            }

            if (data.drawCount > 0)
            {
                m_Geometry.PopulateCameraRenderData(data);
                m_Geometry.UploadCameraData(surfaceCount);
                m_ShaderData.UploadProfiles(surfaceCount);
            }
        }

        private int PrepareDraws(WaterSurface surface, ref WaterSurfaceRenderData surfaceData)
        {
            surfaceData.preparedMeshDraws?.Clear();

            if (surfaceData.usesInstancedQuads)
            {
                return surfaceData.lodPatchBuffer != null
                    && surfaceData.lodPatchMesh != null
                    && surfaceData.lodPatchCount > 0
                    ? 1
                    : 0;
            }

            if (!surfaceData.usesCustomMesh)
                return m_Geometry.gridMesh != null ? 1 : 0;

            if (surfaceData.meshRenderers == null)
                return 0;

            surfaceData.preparedMeshDraws ??= new List<WaterPreparedMeshDraw>(surfaceData.meshRenderers.Count);
            int drawCount = 0;
            for (int rendererIndex = 0; rendererIndex < surfaceData.meshRenderers.Count; ++rendererIndex)
            {
                MeshRenderer renderer = surfaceData.meshRenderers[rendererIndex];
                if (renderer == null
                    || !m_Geometry.IsCustomMeshVisible(renderer, surface.simulation.renderingParameters)
                    || !renderer.TryGetComponent(out MeshFilter filter))
                    continue;

                Mesh mesh = filter.sharedMesh;
                if (mesh == null || mesh.subMeshCount == 0)
                    continue;

                surfaceData.preparedMeshDraws.Add(new WaterPreparedMeshDraw
                {
                    mesh = mesh,
                    localToWorld = renderer.transform.localToWorldMatrix,
                    subMeshCount = mesh.subMeshCount
                });
                drawCount += mesh.subMeshCount;
            }
            return drawCount;
        }

        private void PrepareSurfaceRenderData(
            WaterCameraContext cameraContext,
            WaterSurface surface,
            int surfaceIndex,
            ref WaterSurfaceRenderData data)
        {
            data.activeCascadeCount = surface.simulationEnabled ? surface.simulation.activeCascadeCount : 0;
            data.debugEnabled = surface.debugMode != WaterDebugMode.None;
            data.usesCurrentMap = surface.currentMap != null && surface.currentMapInfluence > 1e-4f;
            Material requestedMaterial = data.debugEnabled ? m_DefaultSurfaceMaterial : surface.materialOverride;
            bool materialResolved = WaterMaterialResolver.TryResolve(
                requestedMaterial, data.debugEnabled, data.usesCurrentMap, out WaterMaterialRole materialRole, out int passIndex);
            if (!materialResolved)
            {
                requestedMaterial = m_DefaultSurfaceMaterial;
                materialResolved = WaterMaterialResolver.TryResolve(
                    requestedMaterial, data.debugEnabled, data.usesCurrentMap, out materialRole, out passIndex);
            }

            data.surfaceMaterial = requestedMaterial;
            if (!materialResolved || data.surfaceMaterial == null)
            {
                data.shouldRender = false;
                return;
            }

            if (!data.surfaceMaterial.enableInstancing)
                data.surfaceMaterial.enableInstancing = true;
            data.gBufferPassIndex = passIndex;

            if (surface.IsInstancedQuads() && !m_LodEvaluationValid[surfaceIndex])
            {
                m_LodEvaluations[surfaceIndex] = WaterSurfaceGeometry.EvaluateLod(
                    surface,
                    cameraContext,
                    surface.simulation.renderingParameters.maxWaveHeight);
                m_LodEvaluationValid[surfaceIndex] = true;
            }

            m_Geometry.PrepareSurface(
                surface,
                cameraContext,
                surfaceIndex,
                surface.simulation.renderingParameters.maxWaveHeight,
                surface.simulation.renderingParameters.maxWaveDisplacement,
                m_LodEvaluations[surfaceIndex],
                ref data);
        }

        private void PrepareSurfaceBindings(
            WaterSurface surface,
            GraphicsBuffer currentSectorSwizzleBuffer,
            ref WaterSurfaceRenderData data)
        {
            data.simulationConstantBuffer = surface.simulationConstantBuffer;
            data.surfaceConstantBuffer = surface.surfaceConstantBuffer;
            data.foamCascadesBuffer = surface.simulation.gpuResources.foamCascadesBuffer;
            surface.FillMaterialPropertyBlock(m_FoamTexture, m_PhysicalFoamTexture);
            data.propertyBlock = surface.propertyBlock;
            data.propertyBlock.SetBuffer(WaterShaderIDs._WaterCameraVariables, m_Geometry.cameraVariablesBuffer);
            if (currentSectorSwizzleBuffer != null)
                data.propertyBlock.SetBuffer(WaterShaderIDs._WaterCurrentSectorSwizzles, currentSectorSwizzleBuffer);
        }

        private static void DrawInstancedQuads(
            RasterCommandBuffer cmd,
            ref WaterSurfaceRenderData surfaceData,
            int passIndex)
        {
            if (surfaceData.lodPatchBuffer == null || surfaceData.lodPatchMesh == null || surfaceData.lodPatchCount <= 0)
                return;

            surfaceData.propertyBlock.SetBuffer(WaterShaderIDs._WaterLodPatches, surfaceData.lodPatchBuffer);
            cmd.DrawMeshInstancedProcedural(
                surfaceData.lodPatchMesh,
                0,
                surfaceData.surfaceMaterial,
                passIndex,
                surfaceData.lodPatchCount,
                surfaceData.propertyBlock);
        }

        private static void DrawMeshRenderers(RasterCommandBuffer cmd, ref WaterSurfaceRenderData surfaceData, int passIndex)
        {
            if (surfaceData.preparedMeshDraws == null)
                return;

            for (int drawIndex = 0; drawIndex < surfaceData.preparedMeshDraws.Count; ++drawIndex)
            {
                WaterPreparedMeshDraw draw = surfaceData.preparedMeshDraws[drawIndex];
                for (int subMeshIndex = 0; subMeshIndex < draw.subMeshCount; ++subMeshIndex)
                    cmd.DrawMesh(
                        draw.mesh,
                        draw.localToWorld,
                        surfaceData.surfaceMaterial,
                        subMeshIndex,
                        passIndex,
                        surfaceData.propertyBlock);
            }
        }

        internal static void RenderGBuffer(
            RasterCommandBuffer cmd,
            WaterCameraRenderData parameters,
            ref WaterSurfaceRenderData surfaceData)
        {
            if (surfaceData.usesInstancedQuads)
            {
                DrawInstancedQuads(cmd, ref surfaceData, surfaceData.gBufferPassIndex);
                return;
            }

            if (!surfaceData.usesCustomMesh)
            {
                if (parameters.gridMesh != null)
                    cmd.DrawMesh(
                        parameters.gridMesh,
                        Matrix4x4.identity,
                        surfaceData.surfaceMaterial,
                        0,
                        surfaceData.gBufferPassIndex,
                        surfaceData.propertyBlock);
                return;
            }

            DrawMeshRenderers(cmd, ref surfaceData, surfaceData.gBufferPassIndex);
        }


    }
}
