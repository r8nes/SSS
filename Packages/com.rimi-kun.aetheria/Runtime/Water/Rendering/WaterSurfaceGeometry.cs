using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using System.Collections.Generic;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using static Unity.Mathematics.math;

namespace Aetheria.Runtime.Water
{
    internal struct WaterLodEvaluation
    {
        internal Vector2 center;
        internal float baseScale;
        internal float viewerAltitudeLevelAlpha;
        internal bool scaleCouldIncrease;
        internal bool scaleCouldDecrease;
        internal float lodAlphaBlackPointFade;
        internal float lodAlphaBlackPointWhitePointFade;
    }

    internal sealed class WaterSurfaceGeometry
    {
        private const int k_MaximumPatchCount = 16 + 12 * (WaterLimits.MaxLodCount - 1);

        private enum PatchType
        {
            Interior,
            FatX,
            FatXSlimZ,
            FatXOuter,
            FatXZ,
            FatXZOuter,
            SlimX,
            SlimXZ,
            SlimXFatZ,
            Count,
        }

        private struct LodPatchData
        {
            public Vector4 positionAxisX;
            public Vector4 axisScaleGrid;
            public Vector4 morphNormalLod;
            public Vector4 edgeTopology;
        }

        private sealed class LodResources
        {
            internal readonly Bounds[] patchBounds = new Bounds[(int)PatchType.Count];
            internal readonly LodPatchData[] lodPatches = new LodPatchData[k_MaximumPatchCount];
            internal GraphicsBuffer lodPatchBuffer;
            internal Mesh patchMesh;
            internal int patchResolution = -1;
            internal int boundsLodCount = -1;
            internal float boundsExtentsMultiplier = float.NaN;
        }

        private sealed class SharedPatchMesh
        {
            internal Mesh mesh;
            internal int referenceCount;
        }

        private readonly LodResources[] m_LodResources = new LodResources[WaterLimits.MaxSurfaceCount];
        private readonly Dictionary<int, SharedPatchMesh> m_SharedPatchMeshes = new Dictionary<int, SharedPatchMesh>();
        private readonly Plane[] m_FrustumPlanes = new Plane[6];
        private readonly WaterCameraShaderVariables[] m_CameraVariables = new WaterCameraShaderVariables[WaterLimits.MaxSurfaceCount];
        private GraphicsBuffer m_CameraVariablesBuffer;
        private Mesh m_GridMesh;

        private static readonly Vector2[] s_InnerOffsets =
        {
            new(-1.5f, 1.5f), new(-0.5f, 1.5f), new(0.5f, 1.5f), new(1.5f, 1.5f),
            new(-1.5f, 0.5f), new(-0.5f, 0.5f), new(0.5f, 0.5f), new(1.5f, 0.5f),
            new(-1.5f,-0.5f), new(-0.5f,-0.5f), new(0.5f,-0.5f), new(1.5f,-0.5f),
            new(-1.5f,-1.5f), new(-0.5f,-1.5f), new(0.5f,-1.5f), new(1.5f,-1.5f),
        };

        private static readonly Vector2[] s_RingOffsets =
        {
            new(-1.5f, 1.5f), new(-0.5f, 1.5f), new(0.5f, 1.5f), new(1.5f, 1.5f),
            new(-1.5f, 0.5f),                                         new(1.5f, 0.5f),
            new(-1.5f,-0.5f),                                         new(1.5f,-0.5f),
            new(-1.5f,-1.5f), new(-0.5f,-1.5f), new(0.5f,-1.5f), new(1.5f,-1.5f),
        };

        internal GraphicsBuffer cameraVariablesBuffer => m_CameraVariablesBuffer;
        internal Mesh gridMesh => m_GridMesh;

        internal bool Initialize()
        {
            m_CameraVariablesBuffer = new GraphicsBuffer(
                GraphicsBuffer.Target.Structured,
                WaterLimits.MaxSurfaceCount,
                UnsafeUtility.SizeOf<WaterCameraShaderVariables>());
            return true;
        }

        internal void Cleanup()
        {
            CoreUtils.SafeRelease(m_CameraVariablesBuffer);
            m_CameraVariablesBuffer = null;

            for (int surfaceIndex = 0; surfaceIndex < m_LodResources.Length; ++surfaceIndex)
                ReleaseLodResources(surfaceIndex);

            foreach (SharedPatchMesh shared in m_SharedPatchMeshes.Values)
                CoreUtils.Destroy(shared.mesh);
            m_SharedPatchMeshes.Clear();

            CoreUtils.Destroy(m_GridMesh);
            m_GridMesh = null;
        }

        internal void PrepareCamera(
            WaterCameraContext cameraContext,
            WaterSurface[] surfaces,
            int surfaceCount)
        {
            GeometryUtility.CalculateFrustumPlanes(cameraContext.camera, m_FrustumPlanes);

            bool gridRequired = false;
            for (int surfaceIndex = 0; surfaceIndex < m_LodResources.Length; ++surfaceIndex)
            {
                WaterSurface surface = surfaceIndex < surfaceCount ? surfaces[surfaceIndex] : null;
                if (surface == null || !surface.IsInstancedQuads())
                    ReleaseLodResources(surfaceIndex);
                if (surface != null && UsesGridMesh(surface))
                    gridRequired = true;
            }

            if (!gridRequired && m_GridMesh != null)
            {
                CoreUtils.Destroy(m_GridMesh);
                m_GridMesh = null;
            }
        }

        private LodResources EnsureLodResources(int surfaceIndex)
        {
            LodResources resources = m_LodResources[surfaceIndex];
            if (resources != null)
                return resources;

            resources = new LodResources
            {
                lodPatchBuffer = new GraphicsBuffer(
                    GraphicsBuffer.Target.Structured,
                    k_MaximumPatchCount,
                    UnsafeUtility.SizeOf<LodPatchData>())
            };
            m_LodResources[surfaceIndex] = resources;
            return resources;
        }

        private void ReleaseLodResources(int surfaceIndex)
        {
            LodResources resources = m_LodResources[surfaceIndex];
            if (resources == null)
                return;

            ReleasePatchMesh(resources);
            CoreUtils.SafeRelease(resources.lodPatchBuffer);
            resources.lodPatchBuffer = null;
            m_LodResources[surfaceIndex] = null;
        }

        private Mesh AcquirePatchMesh(int patchResolution)
        {
            if (!m_SharedPatchMeshes.TryGetValue(patchResolution, out SharedPatchMesh shared))
            {
                shared = new SharedPatchMesh
                {
                    mesh = BuildPatchMesh(patchResolution)
                };
                m_SharedPatchMeshes.Add(patchResolution, shared);
            }

            ++shared.referenceCount;
            return shared.mesh;
        }

        private void ReleasePatchMesh(LodResources resources)
        {
            int patchResolution = resources.patchResolution;
            if (patchResolution >= 0
                && m_SharedPatchMeshes.TryGetValue(patchResolution, out SharedPatchMesh shared))
            {
                --shared.referenceCount;
                if (shared.referenceCount <= 0)
                {
                    CoreUtils.Destroy(shared.mesh);
                    m_SharedPatchMeshes.Remove(patchResolution);
                }
            }

            resources.patchResolution = -1;
            resources.patchMesh = null;
            resources.boundsLodCount = -1;
            resources.boundsExtentsMultiplier = float.NaN;
        }

        private void EnsureGridMesh()
        {
            if (m_GridMesh == null)
                BuildGridMesh(ref m_GridMesh);
        }

        internal void PopulateCameraRenderData(WaterCameraRenderData renderData)
        {
            renderData.cameraVariablesBuffer = m_CameraVariablesBuffer;
            renderData.gridMesh = m_GridMesh;
        }

        internal void UploadCameraData(int surfaceCount)
        {
            if (surfaceCount > 0)
                m_CameraVariablesBuffer.SetData(m_CameraVariables, 0, 0, surfaceCount);
        }

        internal void PrepareSurface(
            WaterSurface surface,
            WaterCameraContext cameraContext,
            int surfaceIndex,
            float maximumHeight,
            float maximumDisplacement,
            in WaterLodEvaluation lodEvaluation,
            ref WaterSurfaceRenderData renderData)
        {
            renderData.lodPatchBuffer = null;
            renderData.lodPatchMesh = null;
            renderData.lodPatchCount = 0;

            ResolveGeometryMode(surface, out renderData.usesInstancedQuads, out renderData.isInfinite, out renderData.usesCustomMesh, out renderData.meshRenderers);
            UpdateCameraData(
                surface,
                cameraContext,
                renderData.usesInstancedQuads,
                renderData.isInfinite,
                surfaceIndex,
                lodEvaluation);

            if (!renderData.usesInstancedQuads)
            {
                if (!renderData.usesCustomMesh)
                    EnsureGridMesh();
                return;
            }

            LodResources resources = EnsureLodResources(surfaceIndex);
            EnsurePatchMesh(surface, resources);
            renderData.lodPatchBuffer = resources.lodPatchBuffer;
            renderData.lodPatchMesh = resources.patchMesh;
            renderData.lodPatchCount = FillLodPatchData(
                surface,
                maximumHeight,
                maximumDisplacement,
                m_CameraVariables[surfaceIndex],
                lodEvaluation,
                resources);
        }

        private static void BuildGridMesh(ref Mesh grid)
        {
            int meshResolution = WaterConstants.k_GridMeshResolution;
            Vector3[] vertices = new Vector3[(meshResolution + 1) * (meshResolution + 1)];
            Vector3[] normals = new Vector3[vertices.Length];
            for (int i = 0, y = 0; y <= meshResolution; ++y)
            {
                for (int x = 0; x <= meshResolution; ++x, ++i)
                {
                    vertices[i] = new Vector3(x / (float)meshResolution - 0.5f, 0.0f, y / (float)meshResolution - 0.5f);
                    normals[i] = Vector3.up;
                }
            }

            int[] triangles = new int[meshResolution * meshResolution * 6];
            for (int ti = 0, vi = 0, y = 0; y < meshResolution; ++y, ++vi)
            {
                for (int x = 0; x < meshResolution; ++x, ti += 6, ++vi)
                {
                    triangles[ti] = vi;
                    triangles[ti + 3] = triangles[ti + 2] = vi + 1;
                    triangles[ti + 4] = triangles[ti + 1] = vi + meshResolution + 1;
                    triangles[ti + 5] = vi + meshResolution + 2;
                }
            }

            grid = new Mesh
            {
                name = "Water Grid",
                vertices = vertices,
                normals = normals,
                triangles = triangles,
            };
        }

        private static int EvaluatePatchResolution(int resolution, int downsampleFactor)
        {
            float requestedDensity = 0.25f * Mathf.Max(resolution, 1) / Mathf.Max(downsampleFactor, 1);
            int density = Mathf.Max(8, Mathf.RoundToInt(requestedDensity * 0.5f) * 2);
            return density;
        }

        private void EnsurePatchMesh(WaterSurface surface, LodResources resources)
        {
            int lodCount = Mathf.Clamp(surface.lodLevels, 2, WaterLimits.MaxLodCount);
            int resolution = Mathf.Max(surface.lodResolution, 1);
            int downsampleFactor = Mathf.Max(surface.lodGeometryDownsampleFactor, 1);
            int patchResolution = EvaluatePatchResolution(resolution, downsampleFactor);
            float extentsMultiplier = surface.lodHorizonExtensionMultiplier
                * (WaterLimits.MaxLodCount + 1 - lodCount);

            if (resources.patchResolution != patchResolution || resources.patchMesh == null)
            {
                ReleasePatchMesh(resources);
                resources.patchMesh = AcquirePatchMesh(patchResolution);
                resources.patchResolution = patchResolution;
            }

            if (resources.boundsLodCount == lodCount
                && resources.boundsExtentsMultiplier.Equals(extentsMultiplier))
                return;

            for (int i = 0; i < (int)PatchType.Count; ++i)
                resources.patchBounds[i] = EvaluatePatchBounds((PatchType)i, patchResolution, extentsMultiplier);
            resources.boundsLodCount = lodCount;
            resources.boundsExtentsMultiplier = extentsMultiplier;
        }

        private static Mesh BuildPatchMesh(float vertexDensity)
        {
            var verts = new List<Vector3>();
            var indices = new List<int>();
            float dx = 1.0f / vertexDensity;

            int sideLengthVerts = Mathf.RoundToInt(vertexDensity) + 2;
            float start = -0.5f;
            float end = 0.5f + dx;

            for (int j = 0; j < sideLengthVerts; ++j)
            {
                float z = Mathf.Lerp(start, end, j / (float)(sideLengthVerts - 1));
                for (int i = 0; i < sideLengthVerts; ++i)
                {
                    float x = Mathf.Lerp(start, end, i / (float)(sideLengthVerts - 1));
                    verts.Add(new Vector3(x, 0.0f, z));
                }
            }

            int sideLengthSquares = sideLengthVerts - 1;
            for (int j = 0; j < sideLengthSquares; ++j)
            {
                for (int i = 0; i < sideLengthSquares; ++i)
                {
                    bool flipEdge = ((i + j) & 1) != 0;
                    int i0 = i + j * sideLengthVerts;
                    int i1 = i0 + 1;
                    int i2 = i0 + sideLengthVerts;
                    int i3 = i2 + 1;

                    if (!flipEdge)
                    {
                        indices.Add(i3); indices.Add(i1); indices.Add(i0);
                        indices.Add(i0); indices.Add(i2); indices.Add(i3);
                    }
                    else
                    {
                        indices.Add(i3); indices.Add(i1); indices.Add(i2);
                        indices.Add(i0); indices.Add(i2); indices.Add(i1);
                    }
                }
            }

            Mesh mesh = new Mesh
            {
                name = "Water LOD Patch",
            };
            if (verts.Count > 65535)
                mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.vertices = verts.ToArray();
            mesh.normals = null;
            mesh.SetIndices(indices.ToArray(), MeshTopology.Triangles, 0);
            mesh.RecalculateBounds();
            Bounds bounds = mesh.bounds;
            dx *= 3.0f;
            bounds.extents = new Vector3(bounds.extents.x + dx, bounds.extents.y, bounds.extents.z + dx);
            mesh.bounds = bounds;
            return mesh;
        }

        private static void GetPatchTopology(PatchType patchType, out float xEdgeMode, out float zEdgeMode)
        {
            xEdgeMode = 0.0f;
            zEdgeMode = 0.0f;
            switch (patchType)
            {
                case PatchType.FatX:
                    xEdgeMode = 1.0f;
                    break;
                case PatchType.FatXSlimZ:
                    xEdgeMode = 1.0f;
                    zEdgeMode = -1.0f;
                    break;
                case PatchType.FatXOuter:
                    xEdgeMode = 2.0f;
                    break;
                case PatchType.FatXZ:
                    xEdgeMode = 1.0f;
                    zEdgeMode = 1.0f;
                    break;
                case PatchType.FatXZOuter:
                    xEdgeMode = 2.0f;
                    zEdgeMode = 2.0f;
                    break;
                case PatchType.SlimX:
                    xEdgeMode = -1.0f;
                    break;
                case PatchType.SlimXZ:
                    xEdgeMode = -1.0f;
                    zEdgeMode = -1.0f;
                    break;
                case PatchType.SlimXFatZ:
                    xEdgeMode = -1.0f;
                    zEdgeMode = 1.0f;
                    break;
            }
        }

        private static Bounds EvaluatePatchBounds(PatchType patchType, float vertexDensity, float extentsMultiplier)
        {
            float dx = 1.0f / vertexDensity;
            GetPatchTopology(patchType, out float xEdgeMode, out float zEdgeMode);

            float minX = -0.5f;
            float minZ = -0.5f;
            float maxX = xEdgeMode < -0.5f ? 0.5f - dx : xEdgeMode < 0.5f ? 0.5f : 0.5f + dx;
            float maxZ = zEdgeMode < -0.5f ? 0.5f - dx : zEdgeMode < 0.5f ? 0.5f : 0.5f + dx;
            if (xEdgeMode > 1.5f)
                maxX *= extentsMultiplier;
            if (zEdgeMode > 1.5f)
                maxZ *= extentsMultiplier;

            Bounds bounds = new Bounds();
            bounds.SetMinMax(new Vector3(minX, 0.0f, minZ), new Vector3(maxX, 0.0f, maxZ));
            float snappingAllowance = 3.0f * dx;
            bounds.extents = new Vector3(
                bounds.extents.x + snappingAllowance,
                bounds.extents.y,
                bounds.extents.z + snappingAllowance);
            return bounds;
        }

        private void UpdateCameraData(
            WaterSurface surface,
            WaterCameraContext cameraContext,
            bool usesInstancedQuads,
            bool isInfinite,
            int surfaceIndex,
            in WaterLodEvaluation lodEvaluation)
        {
            ref WaterCameraShaderVariables cb = ref m_CameraVariables[surfaceIndex];
            Vector2 extent = surface.IsProceduralGeometry()
                ? new Vector2(Mathf.Abs(surface.transform.lossyScale.x), Mathf.Abs(surface.transform.lossyScale.z))
                : Vector2.one;

            cb._PatchOffset = 0.0f;
            cb._GridSize = extent;
            cb._RegionExtent = usesInstancedQuads && !isInfinite
                ? extent * 0.5f
                : new float2(float.MaxValue, float.MaxValue);
            cb._LodCenter = 0.0f;
            cb._LodCameraPosition = 0.0f;
            cb._LodAlphaBlackPointFade = 0.0f;
            cb._LodAlphaBlackPointWhitePointFade = 1.0f;

            if (!usesInstancedQuads)
                return;

            cb._LodCenter = lodEvaluation.center;
            Vector3 cameraWorld = cameraContext.camera.transform.position;
            cb._LodCameraPosition = new float2(cameraWorld.x, cameraWorld.z);
            cb._LodAlphaBlackPointFade = lodEvaluation.lodAlphaBlackPointFade;
            cb._LodAlphaBlackPointWhitePointFade = lodEvaluation.lodAlphaBlackPointWhitePointFade;
        }

        internal static WaterLodEvaluation EvaluateLod(
            WaterSurface surface,
            WaterCameraContext cameraContext,
            float maximumHeight)
        {
            WaterLodEvaluation lodEvaluation = new WaterLodEvaluation
            {
                center = EvaluateLodCenter(surface, cameraContext)
            };
            EvaluateLodScale(
                surface,
                cameraContext,
                maximumHeight,
                out lodEvaluation.baseScale,
                out lodEvaluation.viewerAltitudeLevelAlpha,
                out lodEvaluation.scaleCouldIncrease,
                out lodEvaluation.scaleCouldDecrease);
            EvaluateLodAlphaRange(
                surface,
                out lodEvaluation.lodAlphaBlackPointFade,
                out lodEvaluation.lodAlphaBlackPointWhitePointFade);
            return lodEvaluation;
        }

        internal static Vector2 EvaluateLodCenter(WaterSurface surface, WaterCameraContext cameraContext)
        {
            Matrix4x4 worldToWater = surface.simulation.renderingParameters.worldToWaterMatrix;
            Vector3 viewpointLocal = worldToWater.MultiplyPoint3x4(cameraContext.camera.transform.position);

            Vector2 center = new Vector2(viewpointLocal.x, viewpointLocal.z);
            if (Mathf.Abs(center.x * 60.0f - Mathf.Round(center.x * 60.0f)) < 0.001f)
                center.x += 0.002f;
            if (Mathf.Abs(center.y * 60.0f - Mathf.Round(center.y * 60.0f)) < 0.001f)
                center.y += 0.002f;
            return center;
        }

        internal static void EvaluateLodAlphaRange(
            WaterSurface surface,
            out float blackPointFade,
            out float blackPointWhitePointFade)
        {
            int resolution = Mathf.Max(surface.lodResolution, 1);
            int downsampleFactor = Mathf.Max(surface.lodGeometryDownsampleFactor, 1);
            float baseMeshDensity = EvaluatePatchResolution(resolution, downsampleFactor);
            blackPointFade = 0.4f / (baseMeshDensity / 8.0f);
            blackPointWhitePointFade = 1.0f - blackPointFade - blackPointFade;
        }

        internal static void EvaluateLodScale(
            WaterSurface surface,
            WaterCameraContext cameraContext,
            float maximumHeight,
            out float scale,
            out float viewerAltitudeLevelAlpha,
            out bool scaleCouldIncrease,
            out bool scaleCouldDecrease)
        {
            Matrix4x4 worldToWater = surface.simulation.renderingParameters.worldToWaterMatrix;
            Vector3 viewpointLocal = worldToWater.MultiplyPoint3x4(cameraContext.camera.transform.position);
            float viewerHeight = viewpointLocal.y;

            viewerHeight += maximumHeight * Mathf.Clamp01(surface.lodWaveHeightDetailBias);
            float camDistance = Mathf.Abs(viewerHeight);
            camDistance = Mathf.Max(camDistance - 4.0f, 0.0f);

            float minimumScale = Mathf.Max(surface.lodScaleRange.x, 0.25f);
            float maximumScale = surface.lodScaleRange.y;
            if (float.IsNaN(maximumScale) || maximumScale <= 0.0f)
                maximumScale = minimumScale;
            if (!float.IsPositiveInfinity(maximumScale))
                maximumScale = Mathf.Max(maximumScale, minimumScale);

            float level = Mathf.Max(camDistance, minimumScale);
            if (maximumScale < Mathf.Infinity)
                level = Mathf.Min(level, 1.99f * maximumScale);

            float log2Level = Mathf.Log(level) / Mathf.Log(2.0f);
            float floorLog2Level = Mathf.Floor(log2Level);
            viewerAltitudeLevelAlpha = log2Level - floorLog2Level;
            scale = Mathf.Pow(2.0f, floorLog2Level);

            scaleCouldIncrease = maximumScale < Mathf.Infinity || scale < maximumScale * 0.99f;
            scaleCouldDecrease = scale > minimumScale * 1.01f;
        }

        private static PatchType GetPatchType(int lodIndex, int patchIndex, bool generateSkirt)
        {
            PatchType leadSideType = generateSkirt ? PatchType.FatXOuter : PatchType.SlimX;
            PatchType trailSideType = generateSkirt ? PatchType.FatXOuter : PatchType.FatX;
            PatchType leadCornerType = generateSkirt ? PatchType.FatXZOuter : PatchType.SlimXZ;
            PatchType trailCornerType = generateSkirt ? PatchType.FatXZOuter : PatchType.FatXZ;
            PatchType tlCornerType = generateSkirt ? PatchType.FatXZOuter : PatchType.SlimXFatZ;
            PatchType brCornerType = generateSkirt ? PatchType.FatXZOuter : PatchType.FatXSlimZ;

            if (lodIndex != 0)
            {
                return patchIndex switch
                {
                    0 => tlCornerType,
                    1 or 2 => leadSideType,
                    3 => leadCornerType,
                    4 => trailSideType,
                    5 => leadSideType,
                    6 => trailSideType,
                    7 => leadSideType,
                    8 => trailCornerType,
                    9 or 10 => trailSideType,
                    11 => brCornerType,
                    _ => PatchType.Interior,
                };
            }

            return patchIndex switch
            {
                0 => tlCornerType,
                1 or 2 => leadSideType,
                3 => leadCornerType,
                4 => trailSideType,
                5 or 6 or 9 or 10 => PatchType.Interior,
                7 => leadSideType,
                8 => trailSideType,
                11 => leadSideType,
                12 => trailCornerType,
                13 or 14 => trailSideType,
                15 => brCornerType,
                _ => PatchType.Interior,
            };
        }

        private static Quaternion GetPatchRotation(PatchType patchType, Vector2 position)
        {
            bool rotateXOutwards = patchType is PatchType.FatX or PatchType.FatXOuter or PatchType.SlimX or PatchType.SlimXFatZ;
            if (rotateXOutwards)
            {
                float angle = Mathf.Abs(position.y) >= Mathf.Abs(position.x)
                    ? -90.0f * Mathf.Sign(position.y)
                    : position.x < 0.0f ? 180.0f : 0.0f;
                return Quaternion.Euler(0.0f, angle, 0.0f);
            }

            bool rotateXZOutwards = patchType is PatchType.FatXZ or PatchType.SlimXZ or PatchType.FatXSlimZ or PatchType.FatXZOuter;
            if (!rotateXZOutwards)
                return Quaternion.identity;

            Vector3 from = new Vector3(1.0f, 0.0f, 1.0f).normalized;
            Vector3 to = new Vector3(position.x, 0.0f, position.y).normalized;
            if (Vector3.Dot(from, to) < -0.99f)
                return Quaternion.Euler(0.0f, 180.0f, 0.0f);
            return Quaternion.FromToRotation(from, to);
        }

        private static bool IsPatchVisible(
            Bounds meshBounds,
            Vector2 center,
            Vector3 axisX,
            Vector3 axisZ,
            float scale,
            float maximumHeight,
            float maximumDisplacement,
            float2 regionExtent,
            bool isInfinite,
            Matrix4x4 waterToWorld,
            Plane[] frustumPlanes)
        {
            Vector3 boundsCenter = meshBounds.center;
            Vector3 boundsExtents = meshBounds.extents;

            float minX = float.MaxValue;
            float maxX = float.MinValue;
            float minZ = float.MaxValue;
            float maxZ = float.MinValue;

            for (int corner = 0; corner < 4; ++corner)
            {
                float x = boundsCenter.x + ((corner & 1) == 0 ? -boundsExtents.x : boundsExtents.x);
                float z = boundsCenter.z + ((corner & 2) == 0 ? -boundsExtents.z : boundsExtents.z);
                Vector3 rotated = axisX * (x * scale) + axisZ * (z * scale);
                float px = center.x + rotated.x;
                float pz = center.y + rotated.z;
                minX = Mathf.Min(minX, px);
                maxX = Mathf.Max(maxX, px);
                minZ = Mathf.Min(minZ, pz);
                maxZ = Mathf.Max(maxZ, pz);
            }

            minX -= maximumDisplacement;
            maxX += maximumDisplacement;
            minZ -= maximumDisplacement;
            maxZ += maximumDisplacement;

            if (!isInfinite && (maxX < -regionExtent.x || minX > regionExtent.x || maxZ < -regionExtent.y || minZ > regionExtent.y))
                return false;

            float halfHeight = Mathf.Max(maximumHeight, 0.1f);
            Bounds worldBounds = default;
            bool initialized = false;

            for (int corner = 0; corner < 8; ++corner)
            {
                float x = (corner & 1) == 0 ? minX : maxX;
                float y = (corner & 2) == 0 ? -halfHeight : halfHeight;
                float z = (corner & 4) == 0 ? minZ : maxZ;
                Vector3 world = waterToWorld.MultiplyPoint3x4(new Vector3(x, y, z));
                if (!initialized)
                {
                    worldBounds = new Bounds(world, Vector3.zero);
                    initialized = true;
                }
                else
                    worldBounds.Encapsulate(world);
            }

            return GeometryUtility.TestPlanesAABB(frustumPlanes, worldBounds);
        }

        private int FillLodPatchData(
            WaterSurface surface,
            float maximumHeight,
            float maximumDisplacement,
            WaterCameraShaderVariables cameraData,
            in WaterLodEvaluation lodEvaluation,
            LodResources resources)
        {
            float baseScale = lodEvaluation.baseScale;
            float viewerAltitudeLevelAlpha = lodEvaluation.viewerAltitudeLevelAlpha;
            bool scaleCouldIncrease = lodEvaluation.scaleCouldIncrease;
            bool scaleCouldDecrease = lodEvaluation.scaleCouldDecrease;

            int lodCount = Mathf.Clamp(surface.lodLevels, 2, WaterLimits.MaxLodCount);
            int resolution = Mathf.Max(surface.lodResolution, 1);
            int downsampleFactor = Mathf.Max(surface.lodGeometryDownsampleFactor, 1);
            int tileResolution = EvaluatePatchResolution(resolution, downsampleFactor);
            float extentsMultiplier = surface.lodHorizonExtensionMultiplier * (WaterLimits.MaxLodCount + 1 - lodCount);
            int patchCount = 0;

            void AppendLod(int lodIndex, bool interiorsOnly = false, bool excludeInteriors = false)
            {
                float horizScale = Mathf.Pow(2.0f, lodIndex);
                float lodScale = baseScale * horizScale;
                float geometryGridWidth = lodScale / tileResolution;
                float meshScaleAlpha = lodIndex == 0 && scaleCouldIncrease ? viewerAltitudeLevelAlpha : 0.0f;

                const float normalScrollMul = 1.875f;
                const float normalScrollPow = 1.4f;
                float texelWidth = geometryGridWidth / downsampleFactor;
                float normalScrollLow = Mathf.Pow(Mathf.Log(1.0f + 2.0f * texelWidth) * normalScrollMul, normalScrollPow);
                float normalScrollHigh = Mathf.Pow(Mathf.Log(1.0f + 4.0f * texelWidth) * normalScrollMul, normalScrollPow);
                float farNormalsWeight = lodIndex == lodCount - 1 && scaleCouldDecrease
                    ? viewerAltitudeLevelAlpha
                    : 1.0f;

                bool generateSkirt = lodIndex == lodCount - 1;
                Vector2[] offsets = lodIndex == 0 ? s_InnerOffsets : s_RingOffsets;

                for (int patchIndex = 0; patchIndex < offsets.Length; ++patchIndex)
                {
                    PatchType patchType = GetPatchType(lodIndex, patchIndex, generateSkirt);
                    bool interior = patchType == PatchType.Interior;
                    if (interiorsOnly && !interior)
                        continue;
                    if (excludeInteriors && interior)
                        continue;

                    Vector2 offset = offsets[patchIndex];
                    Vector2 center = new Vector2(cameraData._LodCenter.x, cameraData._LodCenter.y) + lodScale * offset;
                    Quaternion rotation = GetPatchRotation(patchType, offset);
                    Vector3 axisX = rotation * Vector3.right;
                    Vector3 axisZ = rotation * Vector3.forward;

                    if (!IsPatchVisible(
                            resources.patchBounds[(int)patchType],
                            center,
                            axisX,
                            axisZ,
                            lodScale,
                            maximumHeight,
                            maximumDisplacement,
                            cameraData._RegionExtent,
                            surface.IsInfinite(),
                            surface.simulation.renderingParameters.waterToWorldMatrix,
                            m_FrustumPlanes))
                        continue;

                    GetPatchTopology(patchType, out float xEdgeMode, out float zEdgeMode);
                    resources.lodPatches[patchCount] = new LodPatchData
                    {
                        positionAxisX = new Vector4(center.x, center.y, axisX.x, axisX.z),
                        axisScaleGrid = new Vector4(axisZ.x, axisZ.z, lodScale, geometryGridWidth),
                        morphNormalLod = new Vector4(meshScaleAlpha, normalScrollLow, normalScrollHigh, farNormalsWeight),
                        edgeTopology = new Vector4(xEdgeMode, zEdgeMode, extentsMultiplier, 0.0f),
                    };
                    ++patchCount;
                }
            }

            AppendLod(0, interiorsOnly: true);
            AppendLod(0, excludeInteriors: true);
            for (int lodIndex = 1; lodIndex < lodCount; ++lodIndex)
                AppendLod(lodIndex);

            if (patchCount > 0)
                resources.lodPatchBuffer.SetData(resources.lodPatches, 0, 0, patchCount);

            return patchCount;
        }

        internal bool IsVisible(WaterCameraContext cameraContext, WaterSurface surface)
        {
            int surfaceLayerMask = 1 << surface.gameObject.layer;
            if ((surfaceLayerMask & cameraContext.camera.cullingMask) == 0)
                return false;
            return true;
        }

        internal bool IsCustomMeshVisible(MeshRenderer renderer, in WaterSimulationRenderingParameters rendering)
        {
            if (renderer == null)
                return false;

            Bounds bounds = renderer.bounds;
            float horizontal = Mathf.Max(rendering.maxWaveDisplacement, 0.0f);
            float vertical = Mathf.Max(rendering.maxWaveHeight, 0.0f);
            float4x4 waterToWorld = rendering.waterToWorldMatrix;

            float3 worldExpansion =
                abs(waterToWorld.c0.xyz) * horizontal
                + abs(waterToWorld.c1.xyz) * vertical
                + abs(waterToWorld.c2.xyz) * horizontal;
            bounds.Expand(new Vector3(
                worldExpansion.x * 2.0f,
                worldExpansion.y * 2.0f,
                worldExpansion.z * 2.0f));

            return GeometryUtility.TestPlanesAABB(m_FrustumPlanes, bounds);
        }

        private static bool UsesGridMesh(WaterSurface surface)
        {
            if (surface.IsInstancedQuads())
                return false;
            return surface.geometryType == WaterGeometryType.Quad
                || surface.meshRenderers == null
                || surface.meshRenderers.Count == 0;
        }

        private static void ResolveGeometryMode(WaterSurface surface, out bool usesInstancedQuads, out bool isInfinite, out bool usesCustomMesh, out List<MeshRenderer> meshRenderers)
        {
            usesInstancedQuads = surface.IsInstancedQuads();
            if (usesInstancedQuads)
            {
                isInfinite = surface.IsInfinite();
                usesCustomMesh = false;
                meshRenderers = null;
            }
            else
            {
                isInfinite = false;
                if (surface.geometryType == WaterGeometryType.Quad || surface.meshRenderers == null || surface.meshRenderers.Count == 0)
                {
                    usesCustomMesh = false;
                    meshRenderers = null;
                }
                else
                {
                    usesCustomMesh = true;
                    meshRenderers = surface.meshRenderers;
                }
            }
        }
    }
}
