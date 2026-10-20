using System;
using System.Collections.Generic;
using Aetheria.Runtime.Components.Volumetrics;
using Aetheria.Runtime.Data;
using UnityEngine;

namespace Aetheria.Runtime.Rendering.Fog.Volumetrics
{
    internal sealed class VolumetricFogMaskCollector : IDisposable
    {
        private const int MaxMaskCount = VolumetricGridConfig.MaxFogMaskCount;

        private readonly VolumetricLocalMediaCollection<VolumetricFogMaskData> _collection =
            new(MaxMaskCount, AppendDataHash);

        private readonly List<Collider> _resolvedColliders = new(64);
        private readonly HashSet<int> _seenColliders = new(64);
        private readonly List<MaskCandidate> _candidates = new(64);

        private readonly VolumetricTexture3DAtlas<int> _maskAtlas = new(
            VolumetricGridConfig.MaxAtlasGroups,
            "Volumetric Fog Mask Atlas 3D",
            TextureWrapMode.Clamp);

        private readonly struct MaskCandidate
        {
            public readonly VolumetricFogMask Mask;
            public readonly Collider Collider;
            public readonly Bounds Bounds;

            public MaskCandidate(VolumetricFogMask mask, Collider collider, Bounds bounds)
            {
                Mask = mask;
                Collider = collider;
                Bounds = bounds;
            }
        }

        public GraphicsBuffer Buffer => _collection.DataBuffer;
        public GraphicsBuffer BoundsBuffer => _collection.BoundsBuffer;
        public int Count => _collection.Count;
        public IVolumetricTextureAtlas MaskAtlas => _maskAtlas;

        public void Collect(VolumetricCullingContext culling)
        {
            _collection.Clear();
            _resolvedColliders.Clear();
            _seenColliders.Clear();
            _candidates.Clear();
            _maskAtlas.BeginCollection();
            
            VolumetricFogMaskTextureCache.BeginCollection();

            if (culling.IsValid && VolumetricFogMask.HasAnyActive)
            {
                var activeMasks = VolumetricFogMask.ActiveInstances;
                
                for (int i = 0; i < activeMasks.Count; ++i)
                {
                    var mask = activeMasks[i];
                    if (!mask || !mask.isActiveAndEnabled)
                        continue;

                    mask.GetResolvedColliders(_resolvedColliders);
                    
                    for (int colliderIndex = 0; colliderIndex < _resolvedColliders.Count; ++colliderIndex)
                    {
                        var collider = _resolvedColliders[colliderIndex];
                        
                        if (!collider || !collider.enabled || !collider.gameObject.activeInHierarchy)
                            continue;

                        int colliderId = collider.GetInstanceID();
                        
                        if (!_seenColliders.Add(colliderId))
                            continue;
                        
                        var bounds = collider.bounds;
                        
                        if (!culling.IsVisible(bounds))
                            continue;

                        if (!VolumetricFogMaskTextureCache.TryGetShape(
                                collider, out _, out _))
                        {
                            VolumetricFogMaskTextureCache.MarkUsed(collider);
                            VolumetricFogMaskTextureCache.RequestTexture(collider);
                        }

                        _candidates.Add(new MaskCandidate(mask, collider, bounds));
                    }

                    _resolvedColliders.Clear();
                }

                VolumetricFogMaskTextureCache.ProcessPendingBudgeted();

                for (int i = 0; i < _candidates.Count && Count < MaxMaskCount; ++i)
                {
                    var candidate = _candidates[i];
                    var collider = candidate.Collider;
                    
                    if (!collider) continue;

                    int textureIndex = -1;
                    
                    if (!VolumetricFogMaskTextureCache.TryGetShape(
                            collider, out var localBounds, out var shapeParams))
                    {
                        if (!VolumetricFogMaskTextureCache.TryGetTexture(
                                collider, out var texture, out localBounds) ||
                            !texture ||
                            !_maskAtlas.TryGetSlot(
                                collider.GetInstanceID(), texture, out textureIndex))
                        {
                            continue;
                        }

                        shapeParams = Vector4.zero;
                    }

                    var localToWorld = collider.transform.localToWorldMatrix *
                                       Matrix4x4.TRS(
                                           localBounds.center,
                                           Quaternion.identity,
                                           Vector3.Max(localBounds.extents, Vector3.one * 0.0005f));
                    var halfSize = new Vector3(
                        localToWorld.GetColumn(0).magnitude,
                        localToWorld.GetColumn(1).magnitude,
                        localToWorld.GetColumn(2).magnitude);

                    _collection.Add(
                        candidate.Mask.CreateEngineData(
                            localToWorld, halfSize, textureIndex, shapeParams),
                        VolumetricWorldBounds.FromLocalBounds(
                            localToWorld,
                            new Bounds(Vector3.zero, Vector3.one * 2f),
                            candidate.Bounds),
                        textureIndex >= 0);
                }
            }

            _collection.Upload(29);
        }

        private static int AppendDataHash(int hash, VolumetricFogMaskData mask)
        {
            hash = VolumetricUtility.AppendVectorHash(hash, mask.WorldToLocal0);
            hash = VolumetricUtility.AppendVectorHash(hash, mask.WorldToLocal1);
            hash = VolumetricUtility.AppendVectorHash(hash, mask.WorldToLocal2);
            hash = VolumetricUtility.AppendVectorHash(hash, mask.PositiveFadeStrength);
            hash = VolumetricUtility.AppendVectorHash(hash, mask.NegativeFadeFalloff);
            hash = VolumetricUtility.AppendVectorHash(hash, mask.MaskShaping);
            hash = VolumetricUtility.AppendVectorHash(hash, mask.DistanceNearClamp);
            hash = VolumetricUtility.AppendVectorHash(hash, mask.HalfSizeSampling);
            return VolumetricUtility.AppendVectorHash(hash, mask.ShapeParams);
        }

        public void Dispose()
        {
            _collection.Dispose();
            _maskAtlas.Dispose();
            
            VolumetricFogMaskTextureCache.ReleaseAll();
            
            _resolvedColliders.Clear();
            _seenColliders.Clear();
            _candidates.Clear();
        }
    }
}