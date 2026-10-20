using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Rendering;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Aetheria.Runtime.Water
{
    internal static class WaterBuoyancyScheduler
    {
        private const int k_SlotCount = 3;
        private const int k_ThreadCount = 64;
        private const string k_GpuSampleName = "Water/Buoyancy";
        private static readonly int s_QueryStride = UnsafeUtility.SizeOf<WaterBuoyancyGpuQuery>();
        private static readonly int s_ResultStride = UnsafeUtility.SizeOf<WaterBuoyancyGpuResult>();

        private struct SurfaceGroup
        {
            internal WaterSurface surface;
            internal int offset;
            internal int count;
            internal int cursor;
        }

        private sealed class GpuSlot
        {
            internal GraphicsBuffer queries;
            internal GraphicsBuffer results;
            internal AsyncGPUReadbackRequest request;
            internal bool inFlight;
            internal int layoutVersion;
            internal int queryCount;
            internal ulong sequence;
        }

        private static readonly List<WaterBuoyancy> s_ActiveBuoyancies = new();
        private static readonly List<WaterSurfaceQuery> s_ActiveSurfaceQueries = new();
        private static readonly SurfaceGroup[] s_SurfaceGroups = new SurfaceGroup[WaterLimits.MaxSurfaceCount];
        private static readonly GpuSlot[] s_GpuSlots = CreateSlots();
        private static readonly ProfilerMarker s_FixedStepMarker = new("Water/Buoyancy Fixed Step");
        private static readonly ProfilerMarker s_DispatchMarker = new("Water/Buoyancy GPU Dispatch");

        private static NativeArray<WaterBuoyancyGpuQuery> s_QueryData;
        private static NativeArray<WaterBuoyancyGpuResult> s_ResultData;
        private static int s_Capacity;
        private static int s_QueryCount;
        private static int s_SurfaceGroupCount;
        private static int s_LayoutVersion;
        private static int s_ResultLayoutVersion = -1;
        private static bool s_LayoutDirty = true;
        private static bool s_BuoyancyQueriesPending;
        private static bool s_ContextRenderingSubscribed;
        private static double s_LastFixedTime = double.NaN;
        private static int s_LastContextFrame = -1;
        private static ulong s_DispatchSequence;
        private static ulong s_ResultSequence;
        private static int s_LastQueryPollFrame = -1;

        private static ComputeShader s_EvaluationShader;
        private static GraphicsBuffer s_CurrentSectorSwizzles;
        private static CommandBuffer s_CommandBuffer;
        private static int s_FlatKernel = -1;
        private static int s_WaveKernel = -1;
        private static int s_MaskKernel = -1;
        private static int s_CurrentKernel = -1;
        private static int s_CurrentMaskKernel = -1;

        static WaterBuoyancyScheduler()
        {
            Application.quitting += ResetStatics;
#if UNITY_EDITOR
            AssemblyReloadEvents.beforeAssemblyReload += ResetStatics;
            EditorApplication.quitting += ResetStatics;
#endif
        }

        private static GpuSlot[] CreateSlots()
        {
            var slots = new GpuSlot[k_SlotCount];
            for (int i = 0; i < slots.Length; ++i)
                slots[i] = new GpuSlot();
            return slots;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            if (s_ContextRenderingSubscribed)
            {
                RenderPipelineManager.endContextRendering -= OnEndContextRendering;
                s_ContextRenderingSubscribed = false;
            }

            ReleaseGpuBuffers();
            s_CommandBuffer?.Release();
            s_CommandBuffer = null;
            s_EvaluationShader = null;
            s_CurrentSectorSwizzles = null;
            s_ActiveBuoyancies.Clear();
            s_ActiveSurfaceQueries.Clear();
            Array.Clear(s_SurfaceGroups, 0, s_SurfaceGroups.Length);
            s_QueryCount = 0;
            s_SurfaceGroupCount = 0;
            s_LayoutVersion = 0;
            s_ResultLayoutVersion = -1;
            s_LayoutDirty = true;
            s_BuoyancyQueriesPending = false;
            s_LastFixedTime = double.NaN;
            s_LastContextFrame = -1;
            s_DispatchSequence = 0;
            s_ResultSequence = 0;
            s_LastQueryPollFrame = -1;
        }

        internal static void ConfigureGpu(ComputeShader evaluationShader, GraphicsBuffer currentSectorSwizzles)
        {
            if (evaluationShader == null)
            {
                ReleaseGpuBuffers();
                s_CommandBuffer?.Release();
                s_CommandBuffer = null;
            }

            if (ReferenceEquals(s_EvaluationShader, evaluationShader)
                && ReferenceEquals(s_CurrentSectorSwizzles, currentSectorSwizzles))
                return;

            if (s_EvaluationShader != null || s_CurrentSectorSwizzles != null)
                WaitForGpuIdle();

            s_EvaluationShader = evaluationShader;
            s_CurrentSectorSwizzles = currentSectorSwizzles;
            s_FlatKernel = -1;
            s_WaveKernel = -1;
            s_MaskKernel = -1;
            s_CurrentKernel = -1;
            s_CurrentMaskKernel = -1;
            s_LayoutDirty = true;

            if (s_EvaluationShader == null)
                return;

            s_FlatKernel = s_EvaluationShader.FindKernel("BuoyancyFlat");
            s_WaveKernel = s_EvaluationShader.FindKernel("Buoyancy");
            s_MaskKernel = s_EvaluationShader.FindKernel("BuoyancyMask");
            s_CurrentKernel = s_EvaluationShader.FindKernel("BuoyancyCurrent");
            s_CurrentMaskKernel = s_EvaluationShader.FindKernel("BuoyancyCurrentMask");
            s_CommandBuffer ??= new CommandBuffer { name = "Water Buoyancy" };
        }

        internal static void Register(WaterBuoyancy buoyancy)
        {
            if (buoyancy == null || s_ActiveBuoyancies.Contains(buoyancy))
                return;

            s_ActiveBuoyancies.Add(buoyancy);
            s_LayoutDirty = true;
            if (s_ActiveBuoyancies.Count == 1)
                s_LastFixedTime = double.NaN;
            UpdateContextRenderingSubscription();
        }

        internal static void Unregister(WaterBuoyancy buoyancy)
        {
            if (buoyancy == null)
                return;

            if (s_ActiveBuoyancies.Remove(buoyancy))
                s_LayoutDirty = true;

            if (s_ActiveBuoyancies.Count == 0)
            {
                s_LastFixedTime = double.NaN;
                s_BuoyancyQueriesPending = false;
            }
            UpdateContextRenderingSubscription();
        }

        internal static void Register(WaterSurfaceQuery query)
        {
            if (query == null || s_ActiveSurfaceQueries.Contains(query))
                return;

            s_ActiveSurfaceQueries.Add(query);
            s_LayoutDirty = true;
            UpdateContextRenderingSubscription();
        }

        internal static void Unregister(WaterSurfaceQuery query)
        {
            if (query == null)
                return;

            if (s_ActiveSurfaceQueries.Remove(query))
                s_LayoutDirty = true;
            UpdateContextRenderingSubscription();
        }

        internal static void InvalidateLayout()
        {
            s_LayoutDirty = true;
        }

        internal static bool TryGetResult(WaterSurfaceQuery query, int slot, out WaterBuoyancyGpuResult result)
        {
            result = default;
            PollReadbacksForFrame();
            if (query == null
                || s_ResultLayoutVersion != s_LayoutVersion
                || !s_ResultData.IsCreated)
                return false;

            return query.TryReadGpuResult(
                s_ResultData,
                s_QueryCount,
                s_ResultLayoutVersion,
                slot,
                out result);
        }

        internal static void ExecuteFixedStep()
        {
            double fixedTime = Time.fixedTimeAsDouble;
            if (fixedTime == s_LastFixedTime)
                return;
            s_LastFixedTime = fixedTime;

            using (s_FixedStepMarker.Auto())
            {
                PollReadbacksForFrame();
                ValidateLayout();
                if (s_LayoutDirty)
                    RebuildLayout();

                if (s_ResultLayoutVersion == s_LayoutVersion && s_ResultData.IsCreated)
                {
                    for (int index = 0; index < s_ActiveBuoyancies.Count; ++index)
                    {
                        WaterBuoyancy buoyancy = s_ActiveBuoyancies[index];
                        if (buoyancy != null)
                            buoyancy.ApplyGpuResults(s_ResultData, s_QueryCount, s_LayoutVersion);
                    }
                }

                for (int index = 0; index < s_ActiveBuoyancies.Count; ++index)
                {
                    WaterBuoyancy buoyancy = s_ActiveBuoyancies[index];
                    if (buoyancy != null && buoyancy.queryCount > 0 && buoyancy.activeQuerySurface != null)
                    {
                        s_BuoyancyQueriesPending = true;
                        break;
                    }
                }
            }
        }

        private static void UpdateContextRenderingSubscription()
        {
            bool shouldSubscribe = s_ActiveBuoyancies.Count > 0 || s_ActiveSurfaceQueries.Count > 0;
            if (shouldSubscribe == s_ContextRenderingSubscribed)
                return;

            if (shouldSubscribe)
                RenderPipelineManager.endContextRendering += OnEndContextRendering;
            else
            {
                RenderPipelineManager.endContextRendering -= OnEndContextRendering;
                ReleaseGpuBuffers();
            }
            s_ContextRenderingSubscribed = shouldSubscribe;
        }

        private static void OnEndContextRendering(ScriptableRenderContext context, List<Camera> cameras)
        {
            int frameIndex = Time.frameCount;
            if (s_LastContextFrame == frameIndex)
                return;

            PollReadbacks();
            s_LastQueryPollFrame = frameIndex;
            ValidateLayout();
            if (s_LayoutDirty)
                RebuildLayout();

            bool hasPendingQueries = s_BuoyancyQueriesPending;
            if (!hasPendingQueries)
            {
                for (int index = 0; index < s_ActiveSurfaceQueries.Count; ++index)
                {
                    WaterSurfaceQuery query = s_ActiveSurfaceQueries[index];
                    if (query != null && query.HasPendingQueries(frameIndex))
                    {
                        hasPendingQueries = true;
                        break;
                    }
                }
            }

            if (!hasPendingQueries)
            {
                s_LastContextFrame = frameIndex;
                return;
            }

            if (!s_QueryData.IsCreated || s_QueryCount <= 0)
            {
                s_LastContextFrame = frameIndex;
                return;
            }

            for (int index = 0; index < s_ActiveBuoyancies.Count; ++index)
            {
                WaterBuoyancy buoyancy = s_ActiveBuoyancies[index];
                if (buoyancy != null)
                    buoyancy.WriteGpuQueries(s_QueryData, s_QueryCount, s_LayoutVersion);
            }

            for (int index = 0; index < s_ActiveSurfaceQueries.Count; ++index)
            {
                WaterSurfaceQuery query = s_ActiveSurfaceQueries[index];
                if (query != null)
                    query.WriteGpuQueries(s_QueryData, s_QueryCount, s_LayoutVersion, frameIndex);
            }

            if (!DispatchQueries())
                return;

            s_BuoyancyQueriesPending = false;
            for (int index = 0; index < s_ActiveSurfaceQueries.Count; ++index)
            {
                WaterSurfaceQuery query = s_ActiveSurfaceQueries[index];
                if (query != null)
                    query.MarkDispatched(frameIndex);
            }
            s_LastContextFrame = frameIndex;
        }

        private static void ValidateLayout()
        {
            if (s_LayoutDirty)
                return;

            for (int index = s_ActiveBuoyancies.Count - 1; index >= 0; --index)
            {
                WaterBuoyancy buoyancy = s_ActiveBuoyancies[index];
                if (buoyancy == null)
                {
                    s_ActiveBuoyancies.RemoveAt(index);
                    s_LayoutDirty = true;
                    continue;
                }

                if (!buoyancy.MatchesGpuLayout())
                    s_LayoutDirty = true;
            }

            for (int index = s_ActiveSurfaceQueries.Count - 1; index >= 0; --index)
            {
                WaterSurfaceQuery query = s_ActiveSurfaceQueries[index];
                if (query == null)
                {
                    s_ActiveSurfaceQueries.RemoveAt(index);
                    s_LayoutDirty = true;
                    continue;
                }

                if (!query.MatchesGpuLayout())
                    s_LayoutDirty = true;
            }
        }

        private static void RebuildLayout()
        {
            Array.Clear(s_SurfaceGroups, 0, s_SurfaceGroups.Length);
            s_SurfaceGroupCount = 0;
            s_QueryCount = 0;

            for (int index = s_ActiveBuoyancies.Count - 1; index >= 0; --index)
            {
                WaterBuoyancy buoyancy = s_ActiveBuoyancies[index];
                if (buoyancy == null)
                {
                    s_ActiveBuoyancies.RemoveAt(index);
                    continue;
                }

                AddSurfaceGroupQueries(buoyancy.activeQuerySurface, buoyancy.queryCount);
            }

            for (int index = s_ActiveSurfaceQueries.Count - 1; index >= 0; --index)
            {
                WaterSurfaceQuery query = s_ActiveSurfaceQueries[index];
                if (query == null)
                {
                    s_ActiveSurfaceQueries.RemoveAt(index);
                    continue;
                }

                AddSurfaceGroupQueries(query.activeQuerySurface, query.queryCount);
            }

            int offset = 0;
            for (int groupIndex = 0; groupIndex < s_SurfaceGroupCount; ++groupIndex)
            {
                SurfaceGroup group = s_SurfaceGroups[groupIndex];
                group.offset = offset;
                group.cursor = offset;
                offset += group.count;
                s_SurfaceGroups[groupIndex] = group;
            }

            int nextLayoutVersion = s_LayoutVersion + 1;
            for (int index = 0; index < s_ActiveBuoyancies.Count; ++index)
            {
                WaterBuoyancy buoyancy = s_ActiveBuoyancies[index];
                WaterSurface surface = buoyancy.activeQuerySurface;
                int count = buoyancy.queryCount;
                int groupIndex = surface != null && count > 0 ? FindSurfaceGroup(surface) : -1;
                if (groupIndex < 0)
                {
                    buoyancy.SetGpuQueryRange(-1, 0, nextLayoutVersion);
                    continue;
                }

                SurfaceGroup group = s_SurfaceGroups[groupIndex];
                buoyancy.SetGpuQueryRange(group.cursor, count, nextLayoutVersion);
                group.cursor += count;
                s_SurfaceGroups[groupIndex] = group;
            }

            for (int index = 0; index < s_ActiveSurfaceQueries.Count; ++index)
            {
                WaterSurfaceQuery query = s_ActiveSurfaceQueries[index];
                WaterSurface surface = query.activeQuerySurface;
                int count = query.queryCount;
                int groupIndex = surface != null && count > 0 ? FindSurfaceGroup(surface) : -1;
                if (groupIndex < 0)
                {
                    query.SetGpuQueryRange(-1, 0, nextLayoutVersion);
                    continue;
                }

                SurfaceGroup group = s_SurfaceGroups[groupIndex];
                query.SetGpuQueryRange(group.cursor, count, nextLayoutVersion);
                group.cursor += count;
                s_SurfaceGroups[groupIndex] = group;
            }

            s_QueryCount = offset;
            s_LayoutVersion = nextLayoutVersion;
            s_ResultLayoutVersion = -1;
            s_ResultSequence = 0;
            s_LayoutDirty = false;
            EnsureCapacity(s_QueryCount);
            UpdateContextRenderingSubscription();
        }

        private static void AddSurfaceGroupQueries(WaterSurface surface, int count)
        {
            if (surface == null || count <= 0)
                return;

            int groupIndex = FindSurfaceGroup(surface);
            if (groupIndex < 0)
            {
                if (s_SurfaceGroupCount >= s_SurfaceGroups.Length)
                    return;
                groupIndex = s_SurfaceGroupCount++;
                s_SurfaceGroups[groupIndex].surface = surface;
            }

            s_SurfaceGroups[groupIndex].count += count;
        }

        private static int FindSurfaceGroup(WaterSurface surface)
        {
            for (int groupIndex = 0; groupIndex < s_SurfaceGroupCount; ++groupIndex)
                if (ReferenceEquals(s_SurfaceGroups[groupIndex].surface, surface))
                    return groupIndex;
            return -1;
        }

        private static void EnsureCapacity(int requiredCount)
        {
            if (requiredCount <= 0 || s_EvaluationShader == null)
                return;

            if (requiredCount <= s_Capacity && s_QueryData.IsCreated && s_ResultData.IsCreated)
                return;

            int capacity = math.max(s_Capacity, 1);
            while (capacity < requiredCount)
                capacity <<= 1;

            ReleaseGpuBuffers();
            s_Capacity = capacity;
            s_QueryData = new NativeArray<WaterBuoyancyGpuQuery>(capacity, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            s_ResultData = new NativeArray<WaterBuoyancyGpuResult>(capacity, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);

            for (int i = 0; i < s_GpuSlots.Length; ++i)
            {
                GpuSlot slot = s_GpuSlots[i];
                slot.queries = new GraphicsBuffer(GraphicsBuffer.Target.Structured, capacity, s_QueryStride);
                slot.results = new GraphicsBuffer(GraphicsBuffer.Target.Structured, capacity, s_ResultStride);
            }
        }


        private static void WaitForGpuIdle()
        {
            for (int i = 0; i < s_GpuSlots.Length; ++i)
            {
                GpuSlot slot = s_GpuSlots[i];
                if (!slot.inFlight)
                    continue;

                slot.request.WaitForCompletion();
                slot.inFlight = false;
            }
        }

        private static void ReleaseGpuBuffers()
        {
            for (int i = 0; i < s_GpuSlots.Length; ++i)
            {
                GpuSlot slot = s_GpuSlots[i];
                if (slot.inFlight)
                {
                    slot.request.WaitForCompletion();
                    slot.inFlight = false;
                }
                slot.queries?.Release();
                slot.results?.Release();
                slot.queries = null;
                slot.results = null;
                slot.layoutVersion = 0;
                slot.queryCount = 0;
                slot.sequence = 0;
            }

            if (s_QueryData.IsCreated)
                s_QueryData.Dispose();
            if (s_ResultData.IsCreated)
                s_ResultData.Dispose();
            s_QueryData = default;
            s_ResultData = default;
            s_Capacity = 0;
            s_ResultLayoutVersion = -1;
            s_ResultSequence = 0;
        }

        private static void PollReadbacksForFrame()
        {
            int frameIndex = Time.frameCount;
            if (s_LastQueryPollFrame == frameIndex)
                return;

            PollReadbacks();
            s_LastQueryPollFrame = frameIndex;
        }

        private static void PollReadbacks()
        {
            for (int i = 0; i < s_GpuSlots.Length; ++i)
            {
                GpuSlot slot = s_GpuSlots[i];
                if (!slot.inFlight || !slot.request.done)
                    continue;

                slot.inFlight = false;
                if (slot.request.hasError
                    || slot.layoutVersion != s_LayoutVersion
                    || slot.queryCount != s_QueryCount
                    || slot.sequence <= s_ResultSequence
                    || !s_ResultData.IsCreated)
                    continue;

                NativeArray<WaterBuoyancyGpuResult> data = slot.request.GetData<WaterBuoyancyGpuResult>();
                if (!data.IsCreated || data.Length < slot.queryCount)
                    continue;

                NativeArray<WaterBuoyancyGpuResult>.Copy(data, 0, s_ResultData, 0, slot.queryCount);
                s_ResultLayoutVersion = slot.layoutVersion;
                s_ResultSequence = slot.sequence;
            }
        }

        private static bool DispatchQueries()
        {
            if (s_QueryCount <= 0
                || !s_QueryData.IsCreated
                || s_EvaluationShader == null
                || !SystemInfo.supportsAsyncGPUReadback)
                return false;

            GpuSlot slot = FindFreeSlot();
            if (slot == null || slot.queries == null || slot.results == null || s_CommandBuffer == null)
                return false;

            for (int groupIndex = 0; groupIndex < s_SurfaceGroupCount; ++groupIndex)
                if (!CanDispatchSurface(s_SurfaceGroups[groupIndex]))
                    return false;

            using (s_DispatchMarker.Auto())
            {
                slot.queries.SetData(s_QueryData, 0, 0, s_QueryCount);
                s_CommandBuffer.Clear();
                s_CommandBuffer.BeginSample(k_GpuSampleName);

                for (int groupIndex = 0; groupIndex < s_SurfaceGroupCount; ++groupIndex)
                    DispatchSurface(s_CommandBuffer, slot, s_SurfaceGroups[groupIndex]);

                s_CommandBuffer.EndSample(k_GpuSampleName);
                Graphics.ExecuteCommandBuffer(s_CommandBuffer);
                slot.layoutVersion = s_LayoutVersion;
                slot.queryCount = s_QueryCount;
                slot.sequence = ++s_DispatchSequence;
                slot.request = AsyncGPUReadback.Request(slot.results, s_QueryCount * s_ResultStride, 0);
                slot.inFlight = true;
            }

            return true;
        }

        private static GpuSlot FindFreeSlot()
        {
            for (int i = 0; i < s_GpuSlots.Length; ++i)
                if (!s_GpuSlots[i].inFlight)
                    return s_GpuSlots[i];
            return null;
        }

        private static bool CanDispatchSurface(SurfaceGroup group)
        {
            WaterSurface surface = group.surface;
            if (surface == null || !surface.isActiveAndEnabled || group.count <= 0)
                return false;

            WaterSimulationState simulation = surface.simulation;
            bool usesSimulation = surface.simulationEnabled
                && simulation != null
                && simulation.activeCascadeCount > 0;
            if (!usesSimulation)
                return s_FlatKernel >= 0;

            if (surface.simulationConstantBuffer == null
                || surface.surfaceConstantBuffer == null
                || simulation.gpuResources?.waveCascades == null
                || simulation.gpuResources.waveCascades.Length < WaterLimits.CascadeCount)
                return false;

            for (int cascadeIndex = 0; cascadeIndex < WaterLimits.CascadeCount; ++cascadeIndex)
                if (simulation.gpuResources.waveCascades[cascadeIndex]?.waveField?.rt == null)
                    return false;

            bool wantsCurrent = surface.currentMap != null && surface.currentMapInfluence > 1e-4f;
            if (wantsCurrent && s_CurrentSectorSwizzles == null)
                return false;

            bool usesMask = surface.simulationMask != null;
            int kernel = wantsCurrent
                ? (usesMask ? s_CurrentMaskKernel : s_CurrentKernel)
                : (usesMask ? s_MaskKernel : s_WaveKernel);
            return kernel >= 0;
        }

        private static void DispatchSurface(CommandBuffer cmd, GpuSlot slot, SurfaceGroup group)
        {
            WaterSurface surface = group.surface;
            WaterSimulationState simulation = surface.simulation;
            bool usesSimulation = surface.simulationEnabled
                && simulation != null
                && simulation.activeCascadeCount > 0;
            bool usesMask = usesSimulation && surface.simulationMask != null;
            bool usesCurrent = usesSimulation
                && surface.currentMap != null
                && surface.currentMapInfluence > 1e-4f;

            int kernel = !usesSimulation
                ? s_FlatKernel
                : usesCurrent
                    ? (usesMask ? s_CurrentMaskKernel : s_CurrentKernel)
                    : (usesMask ? s_MaskKernel : s_WaveKernel);

            cmd.SetComputeBufferParam(s_EvaluationShader, kernel, WaterShaderIDs._WaterBuoyancyQueries, slot.queries);
            cmd.SetComputeBufferParam(s_EvaluationShader, kernel, WaterShaderIDs._WaterBuoyancyResultsRW, slot.results);
            cmd.SetComputeIntParam(s_EvaluationShader, WaterShaderIDs._WaterBuoyancyQueryOffset, group.offset);
            cmd.SetComputeIntParam(s_EvaluationShader, WaterShaderIDs._WaterBuoyancyQueryCount, group.count);

            float4x4 waterToWorld = float4x4.identity;
            float4x4 worldToWater = float4x4.identity;
            WaterSurface.EvaluateTransformMatrices(
                surface.IsCustomMesh(),
                surface.transform.position,
                surface.transform.rotation,
                ref waterToWorld,
                ref worldToWater);
            cmd.SetComputeMatrixParam(s_EvaluationShader, WaterShaderIDs._WaterBuoyancyWaterToWorld, ToMatrix4x4(waterToWorld));
            cmd.SetComputeMatrixParam(s_EvaluationShader, WaterShaderIDs._WaterBuoyancyWorldToWater, ToMatrix4x4(worldToWater));
            cmd.SetComputeFloatParam(s_EvaluationShader, WaterShaderIDs._WaterBuoyancyNormalEpsilon, EvaluateNormalEpsilon(simulation));

            float angle = WaterSimulationLayout.NormalizeAngle(surface.currentDirection) * Mathf.Deg2Rad;
            float3 localDirection = new float3(Mathf.Cos(angle), 0.0f, Mathf.Sin(angle));
            float3 worldDirection = math.normalizesafe(math.mul((float3x3)waterToWorld, localDirection));
            cmd.SetComputeVectorParam(
                s_EvaluationShader,
                WaterShaderIDs._WaterBuoyancyBaseCurrentDirectionWS,
                new Vector4(worldDirection.x, worldDirection.y, worldDirection.z, 0.0f));

            if (usesSimulation)
            {
                cmd.SetComputeConstantBufferParam(
                    s_EvaluationShader,
                    WaterShaderIDs._WaterSimulationShaderVariables,
                    surface.simulationConstantBuffer,
                    0,
                    surface.simulationConstantBuffer.stride);
                cmd.SetComputeConstantBufferParam(
                    s_EvaluationShader,
                    WaterShaderIDs._WaterSurfaceShaderVariables,
                    surface.surfaceConstantBuffer,
                    0,
                    surface.surfaceConstantBuffer.stride);

                for (int cascadeIndex = 0; cascadeIndex < WaterLimits.CascadeCount; ++cascadeIndex)
                {
                    WaterWaveCascadeResources cascade = simulation.gpuResources.waveCascades[cascadeIndex];
                    int textureId = cascadeIndex == 0
                        ? WaterShaderIDs._WaterWaveFieldCascade0
                        : cascadeIndex == 1
                            ? WaterShaderIDs._WaterWaveFieldCascade1
                            : WaterShaderIDs._WaterWaveFieldCascade2;
                    cmd.SetComputeTextureParam(s_EvaluationShader, kernel, textureId, cascade.waveField.rt);
                }

                if (usesMask)
                    cmd.SetComputeTextureParam(s_EvaluationShader, kernel, WaterShaderIDs._WaterSimulationMask, surface.simulationMask);
                if (usesCurrent)
                {
                    cmd.SetComputeTextureParam(s_EvaluationShader, kernel, WaterShaderIDs._WaterCurrentMap, surface.currentMap);
                    cmd.SetComputeBufferParam(s_EvaluationShader, kernel, WaterShaderIDs._WaterCurrentSectorSwizzles, s_CurrentSectorSwizzles);
                }
            }

            cmd.DispatchCompute(s_EvaluationShader, kernel, CoreUtils.DivRoundUp(group.count, k_ThreadCount), 1, 1);
        }

        private static float EvaluateNormalEpsilon(WaterSimulationState simulation)
        {
            if (simulation == null || simulation.activeCascadeCount <= 0)
                return 0.02f;

            float minimumSpacing = float.MaxValue;
            int resolution = math.max(simulation.simulationResolution, 1);
            for (int cascadeIndex = 0; cascadeIndex < simulation.activeCascadeCount; ++cascadeIndex)
            {
                WaterCascadeSpectrumParameters cascade = simulation.spectrum.GetCascade(cascadeIndex);
                float spacing = math.min(cascade.tileLength.x, cascade.tileLength.y) / resolution;
                if (spacing > 0.0f)
                    minimumSpacing = math.min(minimumSpacing, spacing);
            }
            return math.max(math.isfinite(minimumSpacing) ? minimumSpacing : 1.0f, 0.02f);
        }

        private static Matrix4x4 ToMatrix4x4(float4x4 value)
        {
            return new Matrix4x4(
                new Vector4(value.c0.x, value.c0.y, value.c0.z, value.c0.w),
                new Vector4(value.c1.x, value.c1.y, value.c1.z, value.c1.w),
                new Vector4(value.c2.x, value.c2.y, value.c2.z, value.c2.w),
                new Vector4(value.c3.x, value.c3.y, value.c3.z, value.c3.w));
        }
    }
}
