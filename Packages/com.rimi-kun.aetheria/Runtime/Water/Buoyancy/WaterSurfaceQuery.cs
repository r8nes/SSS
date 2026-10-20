using System;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace Aetheria.Runtime.Water
{
    public sealed class WaterSurfaceQuery : IDisposable
    {
        private const int k_InitialCapacity = 1;
        private const float k_Tolerance = 0.01f;
        private const uint k_MaxIterations = 8u;

        private WaterSurface m_Surface;
        private Vector3[] m_TargetPositions = new Vector3[k_InitialCapacity];
        private Vector3[] m_SearchPositions = new Vector3[k_InitialCapacity];
        private bool[] m_HasSearchPosition = new bool[k_InitialCapacity];
        private int m_QueryCapacity = k_InitialCapacity;
        private int m_SubmissionFrame = -1;
        private double m_SubmissionFixedTime = double.NaN;
        private bool m_SubmissionUsesFixedTime;
        private int m_SubmissionCount;
        private int m_LastDispatchFrame = -1;
        private int m_GpuQueryOffset = -1;
        private int m_GpuQueryCount;
        private int m_GpuLayoutVersion = -1;
        private int m_GpuLayoutCapacity;
        private WaterSurface m_GpuLayoutSurface;
        private bool m_Registered;
        private bool m_Disposed;

        internal WaterSurface activeQuerySurface => !m_Disposed && m_Surface != null && m_Surface.isActiveAndEnabled ? m_Surface : null;
        internal int queryCount => activeQuerySurface != null ? m_QueryCapacity : 0;

        public bool TrySample(Vector3 point, out WaterSurface surface, out Vector3 position, out Vector3 normal)
        {
            surface = WaterSurfaceRegistry.FindClosestSurface(point);
            return TrySample(surface, point, out position, out normal);
        }

        public bool TrySample(WaterSurface surface, Vector3 point, out Vector3 position, out Vector3 normal)
        {
            position = default;
            normal = Vector3.up;
            if (m_Disposed || surface == null || !surface.isActiveAndEnabled)
                return false;

            SetSurface(surface);
            EnsureRegistered();
            BeginSubmissionBatch();

            int slot = m_SubmissionCount++;
            EnsureCapacity(slot + 1);

            bool hasResult = WaterBuoyancyScheduler.TryGetResult(this, slot, out WaterBuoyancyGpuResult result);
            if (hasResult && result.success != 0u)
            {
                m_SearchPositions[slot] = ToVector3(result.searchPositionWS);
                m_HasSearchPosition[slot] = true;
                position = ToVector3(result.projectedPositionWS);
                normal = ToVector3(result.normalWS);
                if (normal.sqrMagnitude <= 1e-8f)
                    normal = surface.transform.up;
            }
            else if (hasResult)
            {
                m_HasSearchPosition[slot] = false;
            }

            m_TargetPositions[slot] = point;
            return hasResult && result.success != 0u;
        }

        public void Dispose()
        {
            if (m_Disposed)
                return;

            m_Disposed = true;
            if (m_Registered)
            {
                WaterBuoyancyScheduler.Unregister(this);
                m_Registered = false;
            }

            m_Surface = null;
            m_SubmissionFrame = -1;
            m_SubmissionFixedTime = double.NaN;
            m_SubmissionUsesFixedTime = false;
            m_SubmissionCount = 0;
            m_GpuQueryOffset = -1;
            m_GpuQueryCount = 0;
            ClearProjectionHistory();
        }

        internal bool MatchesGpuLayout()
        {
            return m_GpuLayoutCapacity == queryCount
                && ReferenceEquals(m_GpuLayoutSurface, activeQuerySurface);
        }

        internal void SetGpuQueryRange(int offset, int count, int layoutVersion)
        {
            m_GpuQueryOffset = offset;
            m_GpuQueryCount = count;
            m_GpuLayoutVersion = layoutVersion;
            m_GpuLayoutCapacity = queryCount;
            m_GpuLayoutSurface = activeQuerySurface;
        }

        internal bool HasPendingQueries(int frameIndex)
        {
            return m_SubmissionFrame == frameIndex
                && m_SubmissionCount > 0
                && m_LastDispatchFrame != frameIndex;
        }

        internal void MarkDispatched(int frameIndex)
        {
            if (m_SubmissionFrame == frameIndex && m_SubmissionCount > 0)
                m_LastDispatchFrame = frameIndex;
        }

        internal void WriteGpuQueries(
            NativeArray<WaterBuoyancyGpuQuery> queries,
            int totalQueryCount,
            int layoutVersion,
            int frameIndex)
        {
            if (layoutVersion != m_GpuLayoutVersion
                || m_GpuQueryOffset < 0
                || m_GpuQueryCount <= 0
                || m_GpuQueryOffset + m_GpuQueryCount > totalQueryCount
                || m_TargetPositions == null
                || m_SearchPositions == null
                || m_HasSearchPosition == null)
                return;

            int activeCount = m_SubmissionFrame == frameIndex ? Mathf.Min(m_SubmissionCount, m_GpuQueryCount) : 0;
            for (int slot = 0; slot < m_GpuQueryCount; ++slot)
            {
                if (slot >= activeCount)
                {
                    queries[m_GpuQueryOffset + slot] = new WaterBuoyancyGpuQuery
                    {
                        maxIterations = 0u
                    };
                    continue;
                }

                Vector3 target = m_TargetPositions[slot];
                Vector3 start = m_HasSearchPosition[slot] ? m_SearchPositions[slot] : target;
                queries[m_GpuQueryOffset + slot] = new WaterBuoyancyGpuQuery
                {
                    targetPositionWS = ToFloat3(target),
                    tolerance = k_Tolerance,
                    startPositionWS = ToFloat3(start),
                    maxIterations = k_MaxIterations
                };
            }
        }

        internal bool TryReadGpuResult(
            NativeArray<WaterBuoyancyGpuResult> results,
            int totalResultCount,
            int resultLayoutVersion,
            int slot,
            out WaterBuoyancyGpuResult result)
        {
            result = default;
            if (resultLayoutVersion != m_GpuLayoutVersion
                || !ReferenceEquals(m_GpuLayoutSurface, activeQuerySurface)
                || m_GpuQueryOffset < 0
                || slot < 0
                || slot >= m_GpuQueryCount
                || m_GpuQueryOffset + slot >= totalResultCount)
                return false;

            result = results[m_GpuQueryOffset + slot];
            return true;
        }

        private void EnsureRegistered()
        {
            if (m_Registered)
                return;

            m_Registered = true;
            WaterBuoyancyScheduler.Register(this);
        }

        private void BeginSubmissionBatch()
        {
            int frameIndex = Time.frameCount;
            if (Time.inFixedTimeStep)
            {
                double fixedTime = Time.fixedTimeAsDouble;
                if (m_SubmissionUsesFixedTime && fixedTime == m_SubmissionFixedTime)
                    return;

                m_SubmissionUsesFixedTime = true;
                m_SubmissionFixedTime = fixedTime;
                m_SubmissionFrame = frameIndex;
                m_SubmissionCount = 0;
                return;
            }

            if (!m_SubmissionUsesFixedTime && m_SubmissionFrame == frameIndex)
                return;

            m_SubmissionUsesFixedTime = false;
            m_SubmissionFixedTime = double.NaN;
            m_SubmissionFrame = frameIndex;
            m_SubmissionCount = 0;
        }

        private void EnsureCapacity(int requiredCount)
        {
            if (requiredCount <= m_QueryCapacity)
                return;

            int capacity = m_QueryCapacity > 0 ? m_QueryCapacity : k_InitialCapacity;
            while (capacity < requiredCount)
                capacity <<= 1;

            Array.Resize(ref m_TargetPositions, capacity);
            Array.Resize(ref m_SearchPositions, capacity);
            Array.Resize(ref m_HasSearchPosition, capacity);
            m_QueryCapacity = capacity;
            WaterBuoyancyScheduler.InvalidateLayout();
        }

        private void SetSurface(WaterSurface surface)
        {
            if (ReferenceEquals(m_Surface, surface))
                return;

            m_Surface = surface;
            m_SubmissionFrame = -1;
            m_SubmissionFixedTime = double.NaN;
            m_SubmissionUsesFixedTime = false;
            m_SubmissionCount = 0;
            m_LastDispatchFrame = -1;
            ClearProjectionHistory();
            WaterBuoyancyScheduler.InvalidateLayout();
        }

        private void ClearProjectionHistory()
        {
            if (m_HasSearchPosition != null)
                Array.Clear(m_HasSearchPosition, 0, m_HasSearchPosition.Length);
        }

        private static float3 ToFloat3(Vector3 value) => new(value.x, value.y, value.z);
        private static Vector3 ToVector3(float3 value) => new(value.x, value.y, value.z);
    }
}
