using System;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace Aetheria.Runtime.Water
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody))]
    public sealed class WaterBuoyancy : MonoBehaviour
    {
        [SerializeField] private WaterSurface surface;
        [SerializeField] private Vector3[] probes =
        {
            new Vector3(-0.5f, 0.0f,  0.5f),
            new Vector3( 0.5f, 0.0f,  0.5f),
            new Vector3(-0.5f, 0.0f, -0.5f),
            new Vector3( 0.5f, 0.0f, -0.5f),
        };

        [SerializeField, Min(0.001f)] private float buoyancyDepth = 0.5f;
        [SerializeField, Min(0.0f)] private float buoyancyStrength = 1.15f;
        [SerializeField, Min(0.0f)] private float normalDamping = 2.0f;
        [SerializeField, Min(0.0f)] private float fluidDrag = 0.75f;
        [SerializeField, Min(0.0001f)] private float projectionTolerance = 0.01f;
        [SerializeField, Range(1, 32)] private int maxProjectionIterations = 8;

        private Rigidbody m_Rigidbody;
        private Vector3[] m_SearchPositions;
        private bool[] m_HasSearchPosition;
        private int m_GpuQueryOffset = -1;
        private int m_GpuQueryCount;
        private int m_GpuLayoutVersion;
        private int m_GpuLayoutProbeCount;
        private WaterSurface m_GpuLayoutSurface;

        public WaterSurface Surface
        {
            get => surface;
            set
            {
                if (surface == value)
                    return;
                surface = value;
                ClearProjectionHistory();
                WaterBuoyancyScheduler.InvalidateLayout();
            }
        }

        public Vector3[] Probes => probes;
        internal int queryCount => probes?.Length ?? 0;
        internal WaterSurface activeQuerySurface => isActiveAndEnabled && surface != null && surface.isActiveAndEnabled ? surface : null;

        private void Awake()
        {
            m_Rigidbody = GetComponent<Rigidbody>();
        }

        private void OnEnable()
        {
            if (m_Rigidbody == null)
                m_Rigidbody = GetComponent<Rigidbody>();
            EnsureHistory();
            WaterBuoyancyScheduler.Register(this);
        }

        private void OnDisable()
        {
            WaterBuoyancyScheduler.Unregister(this);
            ClearProjectionHistory();
        }

        private void OnDestroy()
        {
            WaterBuoyancyScheduler.Unregister(this);
        }

        private void OnValidate()
        {
            buoyancyDepth = Mathf.Max(0.001f, buoyancyDepth);
            buoyancyStrength = Mathf.Max(0.0f, buoyancyStrength);
            normalDamping = Mathf.Max(0.0f, normalDamping);
            fluidDrag = Mathf.Max(0.0f, fluidDrag);
            projectionTolerance = Mathf.Max(0.0001f, projectionTolerance);
            maxProjectionIterations = Mathf.Clamp(maxProjectionIterations, 1, 32);

            if (Application.isPlaying && isActiveAndEnabled)
                EnsureHistory();
            WaterBuoyancyScheduler.InvalidateLayout();
        }

        internal bool MatchesGpuLayout()
        {
            return m_GpuLayoutProbeCount == queryCount
                && ReferenceEquals(m_GpuLayoutSurface, activeQuerySurface);
        }

        internal void SetGpuQueryRange(int offset, int count, int layoutVersion)
        {
            m_GpuQueryOffset = offset;
            m_GpuQueryCount = count;
            m_GpuLayoutVersion = layoutVersion;
            m_GpuLayoutProbeCount = queryCount;
            m_GpuLayoutSurface = activeQuerySurface;
            EnsureHistory();
        }

        internal void WriteGpuQueries(NativeArray<WaterBuoyancyGpuQuery> queries, int totalQueryCount, int layoutVersion)
        {
            if (layoutVersion != m_GpuLayoutVersion
                || m_GpuQueryOffset < 0
                || m_GpuQueryCount <= 0
                || m_GpuQueryOffset + m_GpuQueryCount > totalQueryCount
                || probes == null
                || probes.Length != m_GpuQueryCount)
                return;

            EnsureHistory();
            for (int i = 0; i < m_GpuQueryCount; ++i)
            {
                Vector3 target = transform.TransformPoint(probes[i]);
                Vector3 start = m_HasSearchPosition[i] ? m_SearchPositions[i] : target;
                queries[m_GpuQueryOffset + i] = new WaterBuoyancyGpuQuery
                {
                    targetPositionWS = new float3(target.x, target.y, target.z),
                    tolerance = projectionTolerance,
                    startPositionWS = new float3(start.x, start.y, start.z),
                    maxIterations = (uint)maxProjectionIterations
                };
            }
        }

        internal void ApplyGpuResults(NativeArray<WaterBuoyancyGpuResult> results, int totalResultCount, int layoutVersion)
        {
            if (layoutVersion != m_GpuLayoutVersion
                || m_Rigidbody == null
                || surface == null
                || m_GpuQueryOffset < 0
                || m_GpuQueryCount <= 0
                || m_GpuQueryOffset + m_GpuQueryCount > totalResultCount
                || probes == null
                || probes.Length != m_GpuQueryCount)
                return;

            EnsureHistory();
            Vector3 gravity = Physics.gravity;
            float gravityMagnitude = gravity.magnitude;
            Vector3 buoyancyDirection = gravityMagnitude > 1e-5f ? -gravity / gravityMagnitude : surface.transform.up;
            Vector3 surfaceUp = surface.transform.up.normalized;
            float massPerProbe = m_Rigidbody.mass / m_GpuQueryCount;
            float supportForcePerProbe = massPerProbe * gravityMagnitude * buoyancyStrength;
            float currentSpeed = surface.currentSpeed * WaterConstants.k_KilometerPerHourToMeterPerSecond;
            Vector3 centerOfMass = m_Rigidbody.worldCenterOfMass;
            Vector3 linearVelocity = m_Rigidbody.linearVelocity;
            Vector3 angularVelocity = m_Rigidbody.angularVelocity;
            Vector3 totalForce = Vector3.zero;
            Vector3 totalTorque = Vector3.zero;

            for (int i = 0; i < m_GpuQueryCount; ++i)
            {
                WaterBuoyancyGpuResult result = results[m_GpuQueryOffset + i];
                if (result.success == 0u)
                {
                    m_HasSearchPosition[i] = false;
                    continue;
                }

                Vector3 searchPosition = ToVector3(result.searchPositionWS);
                m_SearchPositions[i] = searchPosition;
                m_HasSearchPosition[i] = true;

                Vector3 probePosition = ToVector3(result.targetPositionWS);
                Vector3 surfacePosition = ToVector3(result.projectedPositionWS);
                Vector3 surfaceNormal = ToVector3(result.normalWS);
                if (surfaceNormal.sqrMagnitude < 1e-8f)
                    surfaceNormal = surfaceUp;

                float depth = Vector3.Dot(surfacePosition - probePosition, surfaceUp);
                if (depth <= 0.0f)
                    continue;

                float submersion = Mathf.Clamp01(depth / buoyancyDepth);
                Vector3 currentDirection = ToVector3(result.currentDirectionWS);
                currentDirection -= surfaceNormal * Vector3.Dot(currentDirection, surfaceNormal);
                if (currentDirection.sqrMagnitude > 1e-8f)
                    currentDirection.Normalize();
                else
                    currentDirection = Vector3.zero;

                Vector3 currentVelocity = currentDirection * currentSpeed;
                Vector3 radius = probePosition - centerOfMass;
                Vector3 pointVelocity = linearVelocity + Vector3.Cross(angularVelocity, radius);
                Vector3 relativeVelocity = pointVelocity - currentVelocity;

                Vector3 buoyancyForce = buoyancyDirection * (supportForcePerProbe * submersion);
                float normalVelocity = Vector3.Dot(relativeVelocity, surfaceNormal);
                Vector3 dampingForce = -surfaceNormal * (normalVelocity * normalDamping * massPerProbe * submersion);
                Vector3 tangentVelocity = relativeVelocity - surfaceNormal * normalVelocity;
                Vector3 dragForce = -tangentVelocity * (fluidDrag * massPerProbe * submersion);
                Vector3 force = buoyancyForce + dampingForce + dragForce;
                totalForce += force;
                totalTorque += Vector3.Cross(radius, force);
            }

            if (totalForce.sqrMagnitude > 0.0f)
                m_Rigidbody.AddForce(totalForce, ForceMode.Force);
            if (totalTorque.sqrMagnitude > 0.0f)
                m_Rigidbody.AddTorque(totalTorque, ForceMode.Force);
        }

        public void ResetProjectionHistory()
        {
            ClearProjectionHistory();
        }

        private void EnsureHistory()
        {
            int count = queryCount;
            if (count <= 0)
            {
                m_SearchPositions = null;
                m_HasSearchPosition = null;
                return;
            }

            if (m_SearchPositions != null && m_SearchPositions.Length == count
                && m_HasSearchPosition != null && m_HasSearchPosition.Length == count)
                return;

            m_SearchPositions = new Vector3[count];
            m_HasSearchPosition = new bool[count];
        }

        private void ClearProjectionHistory()
        {
            if (m_HasSearchPosition != null)
                Array.Clear(m_HasSearchPosition, 0, m_HasSearchPosition.Length);
        }

        private static Vector3 ToVector3(float3 value) => new Vector3(value.x, value.y, value.z);

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            if (probes == null)
                return;

            float size = Mathf.Max(0.025f, HandleSize() * 0.035f);
            for (int i = 0; i < probes.Length; ++i)
            {
                Vector3 position = transform.TransformPoint(probes[i]);
                Gizmos.DrawWireSphere(position, size);
            }
        }

        private float HandleSize()
        {
            if (Camera.current == null)
                return 1.0f;
            return Vector3.Distance(Camera.current.transform.position, transform.position) * 0.1f;
        }
#endif
    }
}
