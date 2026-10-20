using Aetheria.Runtime.Components.Volumetrics;
using System.Runtime.InteropServices;
using UnityEngine;

namespace Aetheria.Runtime.Data
{
    [StructLayout(LayoutKind.Sequential)]
    internal struct VolumetricVolumeData
    {
        public Vector4 WorldToLocal0;
        public Vector4 WorldToLocal1;
        public Vector4 WorldToLocal2;
        public Vector4 ScatteringExtinction;
        public Vector4 TextureTilingInvertFade;
        public Vector4 TextureScrollRcpDistFade;
        public Vector4 RcpPosFaceFadeEnd;
        public Vector4 RcpNegFaceFadeBlend;
        public Vector4 SizeFalloff;
        public Vector4 MaskShaping;
        public Vector4 MaskNearClampSampling;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct VolumetricFogMaskData
    {
        public Vector4 WorldToLocal0;
        public Vector4 WorldToLocal1;
        public Vector4 WorldToLocal2;
        public Vector4 PositiveFadeStrength;
        public Vector4 NegativeFadeFalloff;
        public Vector4 MaskShaping;
        public Vector4 DistanceNearClamp;
        public Vector4 HalfSizeSampling;
        public Vector4 ShapeParams;

        public static VolumetricFogMaskData Create(
            Matrix4x4 localToWorld,
            Vector3 halfSize,
            Vector3 positiveFade,
            Vector3 negativeFade,
            float distanceFadeStart,
            float distanceFadeEnd,
            VolumeFalloffMode falloffMode,
            in VolumeMaskParameters mask,
            int atlasIndex,
            Vector4 shapeParams)
        {
            var worldToLocal = localToWorld.inverse;
            var safeHalfSize = Vector3.Max(halfSize, Vector3.one * 0.0005f);
            var safePositiveFade = Vector3.Max(positiveFade, Vector3.one * 1.0e-6f);
            var safeNegativeFade = Vector3.Max(negativeFade, Vector3.one * 1.0e-6f);
            float safeDistanceFadeLength = Mathf.Max(distanceFadeEnd - distanceFadeStart, 0.00001526f);

            return new VolumetricFogMaskData
            {
                WorldToLocal0 = worldToLocal.GetRow(0),
                WorldToLocal1 = worldToLocal.GetRow(1),
                WorldToLocal2 = worldToLocal.GetRow(2),
                PositiveFadeStrength = new Vector4(
                    1f / safePositiveFade.x,
                    1f / safePositiveFade.y,
                    1f / safePositiveFade.z,
                    Mathf.Clamp01(mask.strength)),
                NegativeFadeFalloff = new Vector4(
                    1f / safeNegativeFade.x,
                    1f / safeNegativeFade.y,
                    1f / safeNegativeFade.z,
                    (float)falloffMode),
                MaskShaping = new Vector4(
                    mask.remapMin,
                    1f / (mask.remapMax - mask.remapMin),
                    mask.power,
                    atlasIndex),
                DistanceNearClamp = new Vector4(
                    1f / safeDistanceFadeLength,
                    distanceFadeEnd / safeDistanceFadeLength,
                    mask.nearClampStartDistance,
                    mask.nearClampEnabled ? 1f / mask.nearClampFadeDistance : 0f),
                HalfSizeSampling = new Vector4(
                    safeHalfSize.x,
                    safeHalfSize.y,
                    safeHalfSize.z,
                    (float)mask.samplingMode),
                ShapeParams = shapeParams,
            };
        }
    }
}