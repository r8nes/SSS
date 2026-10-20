using Aetheria.Runtime.Data;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Aetheria.Runtime.Components.Volumetrics
{
    public enum VolumetricFogMaskColliderSource
    {
        [InspectorName("This GameObject")] Self = 0,
        [InspectorName("This + Children")] SelfAndChildren = 1,
        Explicit = 2,
    }


    [ExecuteAlways]
    [DisallowMultipleComponent]
    [AddComponentMenu("Aetheria/Atmosphere/Volumetric Fog Mask")]
    public sealed class VolumetricFogMask : MonoBehaviour
    {
        internal const float DisabledDistanceFade = 1000f;

        private static readonly List<VolumetricFogMask> ActiveMasks = new();

        [SerializeField]
        private VolumetricFogMaskColliderSource colliderSource = VolumetricFogMaskColliderSource.SelfAndChildren;

        [SerializeField] private bool includeInactiveChildren;
        [SerializeField] private Collider[] explicitColliders = Array.Empty<Collider>();
        [SerializeField] private VolumeShapingMode mode = VolumeShapingMode.Simple;
        [SerializeField, Min(0f)] private float edgeFade = 0.15f;
        [SerializeField] private VolumeMaskParameters mask = VolumeMaskParameters.Default();
        [SerializeField] private Vector3 positiveFade = Vector3.one * 0.15f;
        [SerializeField] private Vector3 negativeFade = Vector3.one * 0.15f;
        [SerializeField, Min(0f)] private float distanceFadeStart = DisabledDistanceFade;
        [SerializeField, Min(0f)] private float distanceFadeEnd = DisabledDistanceFade;
        [SerializeField] private VolumeFalloffMode falloffMode = VolumeFalloffMode.Linear;

        private readonly List<Collider> _resolvedColliders = new(8);
        private readonly List<Collider> _componentSearchBuffer = new(8);
        
        private readonly HashSet<int> _resolvedColliderIds = new();
        
        private bool _colliderCacheDirty = true;

        internal static bool HasAnyActive
        {
            get
            {
                PruneInactiveMasks();
                return ActiveMasks.Count > 0;
            }
        }

        internal static IReadOnlyList<VolumetricFogMask> ActiveInstances
        {
            get
            {
                PruneInactiveMasks();
                return ActiveMasks;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState() => ActiveMasks.Clear();

        internal void GetResolvedColliders(List<Collider> destination)
        {
            ConstrainSettings();
            RefreshColliderCacheIfNeeded();

            for (int i = 0; i < _resolvedColliders.Count; ++i)
                destination.Add(_resolvedColliders[i]);
        }

        private void RefreshColliderCacheIfNeeded()
        {
            if (!_colliderCacheDirty) return;

            _colliderCacheDirty = false;
            _resolvedColliders.Clear();
            _resolvedColliderIds.Clear();

            switch (colliderSource)
            {
                case VolumetricFogMaskColliderSource.Self:
                    AppendCollider(GetComponent<Collider>());
                    break;
                case VolumetricFogMaskColliderSource.SelfAndChildren:
                {
                    _componentSearchBuffer.Clear();
                    GetComponentsInChildren(includeInactiveChildren, _componentSearchBuffer);
                    
                    for (int i = 0; i < _componentSearchBuffer.Count; ++i)
                        AppendCollider(_componentSearchBuffer[i]);
                    
                    _componentSearchBuffer.Clear();
                    break;
                }
                case VolumetricFogMaskColliderSource.Explicit:
                    if (explicitColliders == null)
                        break;
                    for (int i = 0; i < explicitColliders.Length; ++i)
                        AppendCollider(explicitColliders[i]);
                    break;
            }
        }

        private void AppendCollider(Collider col)
        {
            if (!col) return;

            int colliderId = col.GetInstanceID();
            
            if (!_resolvedColliderIds.Add(colliderId))
                return;

            _resolvedColliders.Add(col);
        }

        private static void PruneInactiveMasks()
        {
            for (int i = ActiveMasks.Count - 1; i >= 0; --i)
            {
                var mask = ActiveMasks[i];
                
                if (mask && mask.isActiveAndEnabled && mask.gameObject.scene.IsValid())
                    continue;

                ActiveMasks.RemoveAt(i);
            }
        }

        private void MarkColliderCacheDirty() => _colliderCacheDirty = true;

        internal VolumetricFogMaskData CreateEngineData(
            Matrix4x4 localToWorld,
            Vector3 halfSize,
            int atlasIndex,
            Vector4 shapeParams)
        {
            ConstrainSettings();

            bool advanced = mode == VolumeShapingMode.Advanced;
            
            var effectivePositiveFade = advanced ? positiveFade : Vector3.one * edgeFade;
            var effectiveNegativeFade = advanced ? negativeFade : Vector3.one * edgeFade;
            
            float effectiveDistanceFadeStart = advanced ? distanceFadeStart : DisabledDistanceFade;
            float effectiveDistanceFadeEnd = advanced ? distanceFadeEnd : DisabledDistanceFade;
            
            var effectiveFalloffMode =
                advanced ? falloffMode : VolumeFalloffMode.Linear;

            return VolumetricFogMaskData.Create(
                localToWorld,
                halfSize,
                effectivePositiveFade,
                effectiveNegativeFade,
                effectiveDistanceFadeStart,
                effectiveDistanceFadeEnd,
                effectiveFalloffMode,
                mask,
                atlasIndex,
                shapeParams);
        }

        public void ConstrainSettings()
        {
            edgeFade = Mathf.Max(0f, edgeFade);
            mask.Constrain();

            if (mode == VolumeShapingMode.Simple)
            {
                positiveFade = Vector3.one * edgeFade;
                negativeFade = Vector3.one * edgeFade;
                distanceFadeStart = DisabledDistanceFade;
                distanceFadeEnd = DisabledDistanceFade;
                falloffMode = VolumeFalloffMode.Linear;
            }
            else
            {
                positiveFade = Vector3.Max(Vector3.zero, positiveFade);
                negativeFade = Vector3.Max(Vector3.zero, negativeFade);
                distanceFadeStart = Mathf.Max(0f, distanceFadeStart);
                distanceFadeEnd = Mathf.Max(distanceFadeStart, distanceFadeEnd);
            }
        }

        private void OnEnable()
        {
            MarkColliderCacheDirty();

            if (!ActiveMasks.Contains(this))
                ActiveMasks.Add(this);
        }

        private void OnDisable() => ActiveMasks.Remove(this);
        private void OnDestroy() => ActiveMasks.Remove(this);
        private void OnTransformChildrenChanged() => MarkColliderCacheDirty();
        private void OnTransformParentChanged() => MarkColliderCacheDirty();

#if UNITY_EDITOR
        private void OnValidate()
        {
            explicitColliders ??= Array.Empty<Collider>();

            ConstrainSettings();
            MarkColliderCacheDirty();
        }

        private void OnDrawGizmosSelected()
        {
            RefreshColliderCacheIfNeeded();

            var previousColor = Gizmos.color;
            Gizmos.color = new Color(0.15f, 0.95f, 0.9f, 0.8f);

            for (int i = 0; i < _resolvedColliders.Count; ++i)
            {
                var resolvedCollider = _resolvedColliders[i];
                
                if (!resolvedCollider)
                    continue;

                var bounds = resolvedCollider.bounds;
                Gizmos.DrawWireCube(bounds.center, bounds.size);
            }

            Gizmos.color = previousColor;
        }
#endif
    }
}