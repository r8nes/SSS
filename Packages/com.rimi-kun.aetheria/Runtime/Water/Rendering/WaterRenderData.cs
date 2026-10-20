using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using System.Collections.Generic;
using UnityEngine.Rendering.RenderGraphModule;

namespace Aetheria.Runtime.Water
{
    internal struct WaterPreparedMeshDraw
    {
        internal Mesh mesh;
        internal Matrix4x4 localToWorld;
        internal int subMeshCount;
    }

    internal struct WaterSurfaceRenderData
    {
        internal bool shouldRender;
        internal GraphicsBuffer lodPatchBuffer;
        internal Mesh lodPatchMesh;
        internal int lodPatchCount;
        internal int activeCascadeCount;
        internal bool usesInstancedQuads;
        internal bool isInfinite;
        internal bool usesCustomMesh;
        internal bool debugEnabled;
        internal bool usesCurrentMap;
        internal List<MeshRenderer> meshRenderers;
        internal List<WaterPreparedMeshDraw> preparedMeshDraws;
        internal Material surfaceMaterial;
        internal int gBufferPassIndex;
        internal GraphicsBuffer simulationConstantBuffer;
        internal GraphicsBuffer surfaceConstantBuffer;
        internal GraphicsBuffer foamCascadesBuffer;
        internal MaterialPropertyBlock propertyBlock;
    }

    internal sealed class WaterCameraRenderData
    {
        internal int surfaceCount;
        internal int drawCount;
        internal readonly WaterSurfaceRenderData[] surfaces = new WaterSurfaceRenderData[WaterLimits.MaxSurfaceCount];
        internal GraphicsBuffer surfaceProfileBuffer;
        internal GraphicsBuffer cameraVariablesBuffer;
        internal Mesh gridMesh;
        internal float ambientProbeDimmer;
        internal float environmentReflectionIntensity;
    }

    internal sealed class WaterFrameResources : ContextItem
    {
        internal readonly WaterCameraRenderData cameraRenderData = new WaterCameraRenderData();
        internal TextureHandle opaqueDepth;
        internal TextureHandle waterDepth;
        internal TextureHandle materialGBuffer;
        internal TextureHandle surfaceGBuffer;
        internal TextureHandle geometryGBuffer;
        internal TextureHandle metadataGBuffer;
        internal BufferHandle underwaterLine;
        internal BufferHandle underwaterCameraHeight;
        internal WaterUnderwaterRenderer.WaterLineParameters underwaterLineParameters;
        internal int underwaterSurfaceIndex;
        internal bool underwaterLineRecorded;
        internal bool preparedForCamera;
        internal bool gBufferResourcesAllocated;
        internal bool gBufferRecorded;
        internal bool screenSpaceShadowsEnabled;
        internal WaterCameraContext cameraContext;

        public override void Reset()
        {
            cameraRenderData.surfaceCount = 0;
            cameraRenderData.drawCount = 0;
            cameraRenderData.surfaceProfileBuffer = null;
            cameraRenderData.cameraVariablesBuffer = null;
            cameraRenderData.gridMesh = null;
            cameraRenderData.ambientProbeDimmer = 0.0f;
            cameraRenderData.environmentReflectionIntensity = 1.0f;
            opaqueDepth = TextureHandle.nullHandle;
            waterDepth = TextureHandle.nullHandle;
            materialGBuffer = TextureHandle.nullHandle;
            surfaceGBuffer = TextureHandle.nullHandle;
            geometryGBuffer = TextureHandle.nullHandle;
            metadataGBuffer = TextureHandle.nullHandle;
            underwaterLine = default;
            underwaterCameraHeight = default;
            underwaterLineParameters = default;
            underwaterSurfaceIndex = -1;
            underwaterLineRecorded = false;
            preparedForCamera = false;
            gBufferResourcesAllocated = false;
            gBufferRecorded = false;
            screenSpaceShadowsEnabled = false;
            cameraContext = default;
        }
    }
}
