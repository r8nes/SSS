using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Aetheria.Runtime.Rendering.Fog.Volumetrics
{
    internal static class VolumetricFogMaskTextureCache
    {
        private static readonly AetheriaProfiler.Marker BakeProfile =
            AetheriaProfiler.CreateMarker("Fog/Volumetrics", "Bake Collider Mask");

        private const int BakeResolution = 32;
        private const int BakeSupersample = 2;
        private const int MaxBakesPerFrame = 1;

        private sealed class CacheEntry
        {
            public int Hash = int.MinValue;
            public Bounds LocalBounds;
            public Texture3D Texture;
            public bool Pending;
        }

        private static readonly Dictionary<int, CacheEntry> Entries = new();
        private static readonly Queue<Collider> PendingColliders = new();
        private static readonly HashSet<int> UsedColliderIds = new();
        private static readonly List<int> StaleColliderIds = new();

        private static byte[] _bakeCoverage;
        private static int _lastProcessedFrame = int.MinValue;
        private static int _editProcessTick;
        private static int _collectionFrame = int.MinValue;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState() => ReleaseAll();

        public static void BeginCollection()
        {
            int frame = Time.frameCount;
            
            if (_collectionFrame == frame)
                return;

            ReleaseUnusedEntries();
            UsedColliderIds.Clear();
            _collectionFrame = frame;
        }

        public static void MarkUsed(Collider collider)
        {
            if (collider)
                UsedColliderIds.Add(collider.GetInstanceID());
        }

        private static void ReleaseUnusedEntries()
        {
            StaleColliderIds.Clear();
            
            foreach (var pair in Entries)
            {
                if (!UsedColliderIds.Contains(pair.Key))
                    StaleColliderIds.Add(pair.Key);
            }

            for (int i = 0; i < StaleColliderIds.Count; ++i)
            {
                int colliderId = StaleColliderIds[i];
                var entry = Entries[colliderId];
                
                if (entry.Texture)
                    CoreUtils.Destroy(entry.Texture);

                Entries.Remove(colliderId);
            }

        }

        public static bool TryGetTexture(Collider collider, out Texture3D texture, out Bounds localBounds)
        {
            texture = null;
            localBounds = default;

            if (!collider)
                return false;

            if (!TryGetLocalBounds(collider, out localBounds))
                return false;

            int colliderId = collider.GetInstanceID();

            if (!Entries.TryGetValue(colliderId, out var entry) || !entry.Texture)
                return false;

            int hash = ComputeHash(collider, localBounds);

            if (entry.Hash != hash)
                return false;

            texture = entry.Texture;
            localBounds = entry.LocalBounds;
            return true;
        }

        public static void RequestTexture(Collider collider)
        {
            if (!collider) return;
            if (!TryGetLocalBounds(collider, out var localBounds)) return;

            int colliderId = collider.GetInstanceID();
            int hash = ComputeHash(collider, localBounds);

            if (!Entries.TryGetValue(colliderId, out var entry))
            {
                entry = new CacheEntry();
                Entries.Add(colliderId, entry);
            }

            if (entry.Texture && entry.Hash == hash) return;
            if (entry.Pending) return;

            entry.Pending = true;
            PendingColliders.Enqueue(collider);
        }

        public static bool TryGetShape(
            Collider collider,
            out Bounds localBounds,
            out Vector4 shapeParams)
        {
            shapeParams = default;
            
            if (!TryGetLocalBounds(collider, out localBounds))
                return false;

            switch (collider)
            {
                case BoxCollider:
                    shapeParams.x = 1f;
                    return true;
                case SphereCollider:
                    shapeParams.x = 2f;
                    return true;
                case CapsuleCollider capsule:
                {
                    float radius = Mathf.Max(capsule.radius, 0.0005f);
                    float halfHeight = Mathf.Max(capsule.height * 0.5f, radius);
                    shapeParams = new Vector4(
                        3f,
                        capsule.direction,
                        Mathf.Max(halfHeight - radius, 0f) / halfHeight,
                        halfHeight / radius);
                    return true;
                }
                case CharacterController controller:
                {
                    float radius = Mathf.Max(controller.radius, 0.0005f);
                    float halfHeight = Mathf.Max(controller.height * 0.5f, radius);
                    shapeParams = new Vector4(
                        3f,
                        1f,
                        Mathf.Max(halfHeight - radius, 0f) / halfHeight,
                        halfHeight / radius);
                    return true;
                }
                default:
                    return false;
            }
        }

        public static void ProcessPendingBudgeted()
        {
            int frame = Application.isPlaying ? Time.frameCount : ++_editProcessTick;

            if (_lastProcessedFrame == frame) return;

            _lastProcessedFrame = frame;

            ProcessPending(MaxBakesPerFrame);
        }

        public static void ProcessPending(int maxBakes)
        {
            int baked = 0;
            int guard = PendingColliders.Count;

            while (baked < maxBakes && guard-- > 0 && PendingColliders.Count > 0)
            {
                var collider = PendingColliders.Dequeue();

                if (!collider) continue;

                int colliderId = collider.GetInstanceID();

                if (!Entries.TryGetValue(colliderId, out var entry)) continue;

                entry.Pending = false;

                if (!TryGetLocalBounds(collider, out var localBounds)) continue;

                int hash = ComputeHash(collider, localBounds);

                if (entry.Texture && entry.Hash == hash) continue;

                Texture3D texture;
                
                using (BakeProfile.CpuMarker.Auto())
                    texture = BakeTexture(collider, localBounds);

                if (!texture) continue;

                if (entry.Texture)
                    CoreUtils.Destroy(entry.Texture);

                entry.Hash = hash;
                entry.LocalBounds = localBounds;
                entry.Texture = texture;
                baked++;
            }

        }

        public static void ReleaseAll()
        {
            foreach (var pair in Entries)
            {
                if (pair.Value.Texture)
                    CoreUtils.Destroy(pair.Value.Texture);
            }

            Entries.Clear();
            PendingColliders.Clear();
            UsedColliderIds.Clear();
            StaleColliderIds.Clear();
            _bakeCoverage = null;
            _lastProcessedFrame = int.MinValue;
            _editProcessTick = 0;
            _collectionFrame = int.MinValue;
        }

        private static Texture3D BakeTexture(Collider collider, Bounds localBounds)
        {
            const int _sampleCount = BakeSupersample * BakeSupersample * BakeSupersample;
            const float _invResolution = 1f / BakeResolution;
            const float _invSupersample = 1f / BakeSupersample;

            var extents = Vector3.Max(localBounds.extents, Vector3.one * 0.0005f);
            var center = localBounds.center;
            var localToWorld = collider.transform.localToWorldMatrix;

            float epsilon = Mathf.Max(collider.bounds.size.magnitude / (BakeResolution * 96f), 1.0e-5f);
            float epsilonSqr = epsilon * epsilon;

            var coverage = GetBakeCoverageBuffer(BakeResolution * BakeResolution * BakeResolution);
            int index = 0;

            for (int z = 0; z < BakeResolution; ++z)
            {
                for (int y = 0; y < BakeResolution; ++y)
                {
                    for (int x = 0; x < BakeResolution; ++x)
                    {
                        int insideCount = 0;

                        for (int sz = 0; sz < BakeSupersample; ++sz)
                        {
                            float sampleZ = (z + (sz + 0.5f) * _invSupersample) * _invResolution * 2f - 1f;

                            for (int sy = 0; sy < BakeSupersample; ++sy)
                            {
                                float sampleY = (y + (sy + 0.5f) * _invSupersample) * _invResolution * 2f - 1f;

                                for (int sx = 0; sx < BakeSupersample; ++sx)
                                {
                                    float sampleX = (x + (sx + 0.5f) * _invSupersample) * _invResolution * 2f - 1f;

                                    var localSample =
                                        center + Vector3.Scale(new Vector3(sampleX, sampleY, sampleZ), extents);
                                    var worldSample = localToWorld.MultiplyPoint3x4(localSample);
                                    var closestPoint = Physics.ClosestPoint(worldSample, collider,
                                        collider.transform.position,
                                        collider.transform.rotation);

                                    if ((closestPoint - worldSample).sqrMagnitude <= epsilonSqr)
                                        insideCount++;
                                }
                            }
                        }

                        coverage[index++] = (byte)Mathf.RoundToInt(255f * insideCount / _sampleCount);
                    }
                }
            }

            var texture = new Texture3D(BakeResolution, BakeResolution, BakeResolution, TextureFormat.R8, false)
            {
                name = $"VolumetricFogMask_{collider.GetInstanceID()}",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Trilinear,
                anisoLevel = 0,
                hideFlags = HideFlags.HideAndDontSave,
            };

            texture.SetPixelData(coverage, 0);
            texture.Apply(false, true);
            return texture;
        }

        private static byte[] GetBakeCoverageBuffer(int requiredLength)
        {
            if (_bakeCoverage == null || _bakeCoverage.Length < requiredLength)
                _bakeCoverage = new byte[requiredLength];

            return _bakeCoverage;
        }

        private static int ComputeHash(Collider collider, Bounds localBounds)
        {
            unchecked
            {
                int hash = 17;
                hash = hash * 31 + collider.GetType().GetHashCode();
                hash = VolumetricUtility.AppendVectorHash(hash, localBounds.center);
                hash = VolumetricUtility.AppendVectorHash(hash, localBounds.extents);

                switch (collider)
                {
                    case BoxCollider box:
                        hash = VolumetricUtility.AppendVectorHash(hash, box.center);
                        hash = VolumetricUtility.AppendVectorHash(hash, box.size);
                        break;
                    case SphereCollider sphere:
                        hash = VolumetricUtility.AppendVectorHash(hash, sphere.center);
                        hash = hash * 31 + sphere.radius.GetHashCode();
                        break;
                    case CapsuleCollider capsule:
                        hash = VolumetricUtility.AppendVectorHash(hash, capsule.center);
                        hash = hash * 31 + capsule.radius.GetHashCode();
                        hash = hash * 31 + capsule.height.GetHashCode();
                        hash = hash * 31 + capsule.direction;
                        break;
                    case MeshCollider meshCollider:
                        hash = hash * 31 + (meshCollider.sharedMesh ? meshCollider.sharedMesh.GetInstanceID() : 0);
                        if (meshCollider.sharedMesh)
                        {
                            hash = VolumetricUtility.AppendVectorHash(hash, meshCollider.sharedMesh.bounds.center);
                            hash = VolumetricUtility.AppendVectorHash(hash, meshCollider.sharedMesh.bounds.extents);
                        }

                        break;
                    case TerrainCollider terrainCollider:
                        hash = hash * 31 + (terrainCollider.terrainData
                            ? terrainCollider.terrainData.GetInstanceID()
                            : 0);
                        if (terrainCollider.terrainData)
                            hash = VolumetricUtility.AppendVectorHash(hash, terrainCollider.terrainData.size);
                        break;
                    case CharacterController controller:
                        hash = VolumetricUtility.AppendVectorHash(hash, controller.center);
                        hash = hash * 31 + controller.radius.GetHashCode();
                        hash = hash * 31 + controller.height.GetHashCode();
                        break;
                }

                return hash;
            }
        }

        private static bool TryGetLocalBounds(Collider collider, out Bounds localBounds)
        {
            switch (collider)
            {
                case BoxCollider box:
                    localBounds = new Bounds(box.center, Vector3.Max(box.size, Vector3.one * 0.001f));
                    return true;
                case SphereCollider sphere:
                {
                    float diameter = Mathf.Max(sphere.radius * 2f, 0.001f);
                    localBounds = new Bounds(sphere.center, Vector3.one * diameter);
                    return true;
                }
                case CapsuleCollider capsule:
                {
                    float radius = Mathf.Max(capsule.radius, 0.0005f);
                    float cylinderHeight = Mathf.Max(capsule.height, radius * 2f);

                    var size = Vector3.one * (radius * 2f);

                    size[capsule.direction] = cylinderHeight;
                    localBounds = new Bounds(capsule.center, size);

                    return true;
                }
                case MeshCollider meshCollider when meshCollider.sharedMesh:
                {
                    localBounds = meshCollider.sharedMesh.bounds;
                    localBounds.extents = Vector3.Max(localBounds.extents, Vector3.one * 0.0005f);
                    return true;
                }
                case TerrainCollider terrainCollider when terrainCollider.terrainData:
                {
                    var terrainSize = Vector3.Max(terrainCollider.terrainData.size, Vector3.one * 0.001f);
                    localBounds = new Bounds(terrainSize * 0.5f, terrainSize);
                    return true;
                }
                case CharacterController controller:
                {
                    float radius = Mathf.Max(controller.radius, 0.0005f);
                    float height = Mathf.Max(controller.height, radius * 2f);
                    localBounds = new Bounds(controller.center, new Vector3(radius * 2f, height, radius * 2f));
                    return true;
                }
                default:
                    return TryGetColliderLocalBounds(collider, out localBounds);
            }
        }

        private static bool TryGetColliderLocalBounds(Collider collider, out Bounds localBounds)
        {
            if (!collider)
            {
                localBounds = default;
                return false;
            }

            var worldBounds = collider.bounds;

            if (worldBounds.extents.sqrMagnitude <= 1.0e-8f)
            {
                localBounds = default;
                return false;
            }

            var worldToLocal = collider.transform.worldToLocalMatrix;
            var first = worldToLocal.MultiplyPoint3x4(worldBounds.min);
            localBounds = new Bounds(first, Vector3.zero);

            for (int z = 0; z <= 1; ++z)
            {
                for (int y = 0; y <= 1; ++y)
                {
                    for (int x = 0; x <= 1; ++x)
                    {
                        var corner = new Vector3(
                            x == 0 ? worldBounds.min.x : worldBounds.max.x,
                            y == 0 ? worldBounds.min.y : worldBounds.max.y,
                            z == 0 ? worldBounds.min.z : worldBounds.max.z);

                        localBounds.Encapsulate(worldToLocal.MultiplyPoint3x4(corner));
                    }
                }
            }

            localBounds.extents = Vector3.Max(localBounds.extents, Vector3.one * 0.0005f);
            return true;
        }
    }
}