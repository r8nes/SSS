using System;
using System.Collections.Generic;
using UnityEngine;

namespace Aetheria.Runtime.Rendering.Fog.Volumetrics
{
    internal sealed class VolumetricCullingContext
    {
        private readonly Plane[] _frustumPlanes = new Plane[6];

        public Camera Camera { get; private set; }
        public Vector3 CameraPosition { get; private set; }
        public float MaxDistance { get; private set; }
        public bool IsValid => Camera;

        public void Setup(Camera camera, float maxDistance)
        {
            Camera = camera;
            CameraPosition = camera ? camera.transform.position : default;
            MaxDistance = Mathf.Max(0.0f, maxDistance);

            if (camera)
                GeometryUtility.CalculateFrustumPlanes(camera, _frustumPlanes);
        }

        public void SetMaxDistance(float maxDistance) => MaxDistance = Mathf.Max(0.0f, maxDistance);

        public bool IsVisible(Bounds bounds) => IsVisible(bounds, MaxDistance);

        public bool IsVisible(Bounds bounds, float maxDistance)
        {
            if (!Camera || !GeometryUtility.TestPlanesAABB(_frustumPlanes, bounds))
                return false;

            float radius = bounds.extents.magnitude;
            float distanceToBounds = Mathf.Max(0.0f, Vector3.Distance(CameraPosition, bounds.center) - radius);

            return distanceToBounds <= Mathf.Max(0.0f, maxDistance);
        }

        public float DistanceTo(Bounds bounds)
        {
            float radius = bounds.extents.magnitude;
            return Mathf.Max(0.0f, Vector3.Distance(CameraPosition, bounds.center) - radius);
        }
    }

    internal sealed class VolumetricLocalMediaCollection<T> : IDisposable where T : struct
    {
        private readonly List<T> _data;
        private readonly List<VolumetricMediaBounds> _bounds;

        private readonly Func<int, T, int> _appendDataHash;

        private readonly VolumetricBufferSet<T> _buffers = new();
        private readonly VolumetricBufferSet<VolumetricMediaBounds> _boundsBuffers = new();

        public GraphicsBuffer DataBuffer => _buffers.Data;
        public GraphicsBuffer BoundsBuffer => _boundsBuffers.Data;
        public int Count => _data.Count;

        public VolumetricLocalMediaCollection(int capacity, Func<int, T, int> appendDataHash)
        {
            _data = new List<T>(capacity);
            _bounds = new List<VolumetricMediaBounds>(capacity);
            _appendDataHash = appendDataHash ?? throw new ArgumentNullException(nameof(appendDataHash));
        }

        public void Clear()
        {
            _data.Clear();
            _bounds.Clear();
        }

        public void Add(T data, Bounds bounds, bool textured = false)
            => Add(data, VolumetricWorldBounds.FromAxisAligned(bounds), textured);

        public void Add(T data, VolumetricWorldBounds bounds, bool textured = false)
        {
            _data.Add(data);
            _bounds.Add(bounds.ToGpuData(textured));
        }

        public void Upload(int hashSeed)
        {
            unchecked
            {
                int hash = hashSeed * 31 + Count;

                for (int i = 0; i < Count; ++i)
                {
                    hash = _appendDataHash(hash, _data[i]);
                    hash = VolumetricUtility.AppendVectorHash(hash, _bounds[i].CenterAndTextured);
                    hash = VolumetricUtility.AppendVectorHash(hash, _bounds[i].AxisX);
                    hash = VolumetricUtility.AppendVectorHash(hash, _bounds[i].AxisY);
                    hash = VolumetricUtility.AppendVectorHash(hash, _bounds[i].AxisZ);
                }

                _buffers.Upload(_data, Count, hash);
                _boundsBuffers.Upload(_bounds, Count, hash);
            }
        }

        public void Dispose()
        {
            _buffers.Dispose();
            _boundsBuffers.Dispose();
            Clear();
        }
    }
}