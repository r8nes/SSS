using Aetheria.Runtime.Data;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Aetheria.Runtime.Components.Volumetrics
{
    [Serializable]
    public struct FogZoneParameters
    {
        internal const float MinFogDistance = 0.05f;
        internal const float DisabledDistanceFade = 10000f;

        public VolumeShapingMode mode;
        [ColorUsage(false, true)] public Color albedo;
        [Min(MinFogDistance)] public float meanFreePath;
        public LocalMediumBlendMode blendingMode;
        public int priority;
        public VolumeScaleMode scaleMode;
        public Vector3 size;
        [Min(0f)] public float edgeFade;
        public VolumeTextureMask mask;
        public Vector3 positiveFade;
        public Vector3 negativeFade;
        public bool invertFade;
        public float distanceFadeStart;
        public float distanceFadeEnd;
        public VolumeFalloffMode falloffMode;

        public FogZoneParameters(Color color, float meanFreePath)
        {
            mode = VolumeShapingMode.Simple;
            albedo = color;
            this.meanFreePath = meanFreePath;
            blendingMode = LocalMediumBlendMode.Additive;
            priority = 0;
            scaleMode = VolumeScaleMode.ScaleInvariant;
            size = Vector3.one;
            edgeFade = 0.1f;
            mask = VolumeTextureMask.Default(Vector3.one);
            positiveFade = Vector3.one * 0.1f;
            negativeFade = Vector3.one * 0.1f;
            invertFade = false;
            distanceFadeStart = DisabledDistanceFade;
            distanceFadeEnd = DisabledDistanceFade;
            falloffMode = VolumeFalloffMode.Linear;
        }

        internal void Update(float time) => mask.Update(time);

        public void Constrain()
        {
            albedo.r = Mathf.Max(0f, albedo.r);
            albedo.g = Mathf.Max(0f, albedo.g);
            albedo.b = Mathf.Max(0f, albedo.b);
            albedo.a = 1f;
            meanFreePath = Mathf.Clamp(meanFreePath, MinFogDistance, float.MaxValue);
            size = Vector3.Max(Vector3.one * 0.001f,
                new Vector3(Mathf.Abs(size.x), Mathf.Abs(size.y), Mathf.Abs(size.z)));
            edgeFade = Mathf.Max(0f, edgeFade);
            mask.Constrain();

            if (mode == VolumeShapingMode.Simple)
            {
                if (blendingMode != LocalMediumBlendMode.Overwrite)
                    blendingMode = LocalMediumBlendMode.Additive;
                
                positiveFade = Vector3.one * edgeFade;
                negativeFade = Vector3.one * edgeFade;
                invertFade = false;
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

        internal VolumetricVolumeData ConvertToEngineData(
            Matrix4x4 worldToLocal,
            Vector3 halfSize,
            int textureIndex)
        {
            float extinction = 1f / Mathf.Max(meanFreePath, MinFogDistance);
            var linearAlbedo = albedo.linear;
            
            Vector3 scattering = new(
                Mathf.Max(0f, linearAlbedo.r) * extinction,
                Mathf.Max(0f, linearAlbedo.g) * extinction,
                Mathf.Max(0f, linearAlbedo.b) * extinction);

            bool advanced = mode == VolumeShapingMode.Advanced;
            
            var posFade = advanced ? positiveFade : Vector3.one * edgeFade;
            var negFade = advanced ? negativeFade : Vector3.one * edgeFade;
            
            bool invert = advanced && invertFade;
            
            var falloff = advanced ? falloffMode : VolumeFalloffMode.Linear;
            
            float fadeStart = advanced ? distanceFadeStart : DisabledDistanceFade;
            float fadeEnd = advanced ? distanceFadeEnd : DisabledDistanceFade;
            
            var safePositiveFade = Vector3.Max(posFade, Vector3.one * 1.0e-6f);
            var safeNegativeFade = Vector3.Max(negFade, Vector3.one * 1.0e-6f);
            
            float distFadeLen = Mathf.Max(fadeEnd - fadeStart, 0.00001526f);
            
            var shaping = mask.parameters;
            
            float maskRange = Mathf.Max(shaping.remapMax - shaping.remapMin, 1.0e-4f);

            return new VolumetricVolumeData
            {
                WorldToLocal0 = worldToLocal.GetRow(0),
                WorldToLocal1 = worldToLocal.GetRow(1),
                WorldToLocal2 = worldToLocal.GetRow(2),
                ScatteringExtinction = new Vector4(scattering.x, scattering.y, scattering.z, extinction),
                TextureTilingInvertFade = new Vector4(mask.tiling.x, mask.tiling.y, mask.tiling.z, invert ? 1f : 0f),
                TextureScrollRcpDistFade = new Vector4(mask.offset.x, mask.offset.y, mask.offset.z, 1f / distFadeLen),
                RcpPosFaceFadeEnd = new Vector4(1f / safePositiveFade.x, 1f / safePositiveFade.y,
                    1f / safePositiveFade.z,
                    fadeEnd / distFadeLen),
                RcpNegFaceFadeBlend = new Vector4(1f / safeNegativeFade.x, 1f / safeNegativeFade.y,
                    1f / safeNegativeFade.z,
                    (float)blendingMode),
                SizeFalloff = new Vector4(Mathf.Max(halfSize.x, 0.0005f), Mathf.Max(halfSize.y, 0.0005f),
                    Mathf.Max(halfSize.z, 0.0005f), (float)falloff),
                MaskShaping = new Vector4(shaping.remapMin, 1f / maskRange, shaping.power, shaping.strength),
                MaskNearClampSampling = new Vector4(textureIndex, shaping.nearClampStartDistance,
                    shaping.nearClampEnabled ? 1f / shaping.nearClampFadeDistance : 0f, (float)shaping.samplingMode),
            };
        }
    }

    [ExecuteAlways]
    [DisallowMultipleComponent]
    [AddComponentMenu("Aetheria/Atmosphere/Local Volumetric Fog")]
    public sealed class VolumetricFogZone : MonoBehaviour
    {
        private static readonly ActivePriorityRegistry<VolumetricFogZone> ActiveZones =
            new(static zone => zone.parameters.priority);

        [SerializeField] private FogZoneParameters parameters = new(Color.white, 10f);

        public FogZoneParameters Parameters => parameters;

        internal static void CollectActiveInstances(List<VolumetricFogZone> destination) =>
            ActiveZones.Collect(destination);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState() => ActiveZones.Clear();

        internal void PrepareParameters(float time) => parameters.Update(time);

        private Vector3 GetEffectiveScale(Transform tr) =>
            parameters.scaleMode == VolumeScaleMode.InheritFromHierarchy ? tr.lossyScale : Vector3.one;

        internal Vector3 GetScaledSize(Transform tr) =>
            Vector3.Max(Vector3.one * 0.001f, Vector3.Scale(parameters.size, GetEffectiveScale(tr)));

        internal Bounds ComputeWorldBounds(Vector3 halfSize)
        {
            var localToWorld = Matrix4x4.TRS(transform.position, transform.rotation, halfSize);
            var first = localToWorld.MultiplyPoint3x4(new Vector3(-1f, -1f, -1f));
            var bounds = new Bounds(first, Vector3.zero);
            
            for (int z = -1; z <= 1; z += 2)
            for (int y = -1; y <= 1; y += 2)
            for (int x = -1; x <= 1; x += 2)
                bounds.Encapsulate(localToWorld.MultiplyPoint3x4(new Vector3(x, y, z)));
            
            return bounds;
        }

        private void OnEnable() => ActiveZones.Register(this);
        private void OnDisable() => ActiveZones.Unregister(this);
        private void OnDestroy() => ActiveZones.Unregister(this);

#if UNITY_EDITOR
        private void OnValidate() => parameters.Constrain();

        private void OnDrawGizmosSelected()
        {
            var previousMatrix = Gizmos.matrix;
            var previousColor = Gizmos.color;
            Gizmos.matrix = Matrix4x4.TRS(transform.position, transform.rotation, GetScaledSize(transform));
            Gizmos.color = new Color(parameters.albedo.r, parameters.albedo.g, parameters.albedo.b, 0.55f);
            Gizmos.DrawWireCube(Vector3.zero, Vector3.one);
            Gizmos.matrix = previousMatrix;
            Gizmos.color = previousColor;
        }
#endif
    }
}
