using System;
using System.Collections.Generic;
using Aetheria.Runtime.Components.Volumetrics;
using Aetheria.Runtime.Data;
using UnityEngine;

namespace Aetheria.Runtime.Rendering.Fog.Volumetrics
{
    internal sealed class VolumetricVolumeCollector : IDisposable
    {
        private const int MaxVolumeCount = VolumetricGridConfig.MaxZoneCount;

        private readonly List<VolumetricFogZone> _activeZones = new(MaxVolumeCount);

        private readonly VolumetricLocalMediaCollection<VolumetricVolumeData> _collection =
            new(MaxVolumeCount, AppendDataHash);

        private readonly VolumetricTexture3DAtlas<Texture3D> _maskAtlas = new(
            VolumetricGridConfig.MaxAtlasGroups,
            "Volumetric Fog Zone Volume Mask Atlas 3D",
            TextureWrapMode.Repeat);

        public GraphicsBuffer Buffer => _collection.DataBuffer;
        public GraphicsBuffer BoundsBuffer => _collection.BoundsBuffer;
        public int Count => _collection.Count;
        public IVolumetricTextureAtlas MaskAtlas => _maskAtlas;

        public void Collect(VolumetricCullingContext culling)
        {
            _collection.Clear();
            _maskAtlas.BeginCollection();
            
            VolumetricFogZone.CollectActiveInstances(_activeZones);

            if (culling.IsValid)
            {
                float time = Application.isPlaying ? Time.time : Time.realtimeSinceStartup;
                
                for (int i = 0; i < _activeZones.Count && Count < MaxVolumeCount; ++i)
                {
                    var volume = _activeZones[i];
                    
                    if (!volume || !volume.isActiveAndEnabled)
                        continue;

                    volume.PrepareParameters(time);
                    volume.Parameters.Constrain();
                    
                    if (volume.Parameters.meanFreePath <= FogZoneParameters.MinFogDistance)
                        continue;

                    var scaledSize = volume.GetScaledSize(volume.transform);
                    var halfSize = Vector3.Max(scaledSize * 0.5f, Vector3.one * 0.0005f);
                    var worldBounds = volume.ComputeWorldBounds(halfSize);
                    
                    if (!IsVisible(volume, worldBounds, culling))
                        continue;

                    var volumeTransform = Matrix4x4.TRS(
                        volume.transform.position,
                        volume.transform.rotation,
                        halfSize);
                    
                    int textureIndex = -1;
                    
                    if (volume.Parameters.mask.texture)
                    {
                        _maskAtlas.TryGetSlot(
                            volume.Parameters.mask.texture,
                            volume.Parameters.mask.texture,
                            out textureIndex);
                    }

                    _collection.Add(
                        volume.Parameters.ConvertToEngineData(volumeTransform.inverse, halfSize, textureIndex),
                        VolumetricWorldBounds.FromLocalBounds(
                            volumeTransform,
                            new Bounds(Vector3.zero, Vector3.one * 2f),
                            worldBounds),
                        textureIndex >= 0);
                }
            }

            _collection.Upload(23);
        }

        private static bool IsVisible(
            VolumetricFogZone volume,
            Bounds bounds,
            VolumetricCullingContext culling)
        {
            if (!culling.IsVisible(bounds))
                return false;

            var parameters = volume.Parameters;
            
            return parameters is not
            {
                mode: VolumeShapingMode.Advanced,
                distanceFadeEnd: < FogZoneParameters.DisabledDistanceFade * 0.999f
            } || culling.DistanceTo(bounds) <= parameters.distanceFadeEnd;
        }

        private static int AppendDataHash(int hash, VolumetricVolumeData volume)
        {
            hash = VolumetricUtility.AppendVectorHash(hash, volume.WorldToLocal0);
            hash = VolumetricUtility.AppendVectorHash(hash, volume.WorldToLocal1);
            hash = VolumetricUtility.AppendVectorHash(hash, volume.WorldToLocal2);
            hash = VolumetricUtility.AppendVectorHash(hash, volume.ScatteringExtinction);
            hash = VolumetricUtility.AppendVectorHash(hash, volume.TextureTilingInvertFade);
            hash = VolumetricUtility.AppendVectorHash(hash, volume.TextureScrollRcpDistFade);
            hash = VolumetricUtility.AppendVectorHash(hash, volume.RcpPosFaceFadeEnd);
            hash = VolumetricUtility.AppendVectorHash(hash, volume.RcpNegFaceFadeBlend);
            hash = VolumetricUtility.AppendVectorHash(hash, volume.SizeFalloff);
            hash = VolumetricUtility.AppendVectorHash(hash, volume.MaskShaping);
            return VolumetricUtility.AppendVectorHash(hash, volume.MaskNearClampSampling);
        }

        public void Dispose()
        {
            _collection.Dispose();
            _maskAtlas.Dispose();
            _activeZones.Clear();
        }
    }
}