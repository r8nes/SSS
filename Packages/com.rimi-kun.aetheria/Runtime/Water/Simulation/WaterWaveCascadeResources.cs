using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Experimental.Rendering;

namespace Aetheria.Runtime.Water
{
    internal sealed class WaterWaveCascadeResources
    {
        internal const int DisplacementSlice = 0;
        internal const int DeformationGradientSlice = 1;
        internal const int AdditionalDataSlice = 2;
        internal const int SliceCount = 3;

        internal readonly RTHandle waveField;

        private WaterWaveCascadeResources(RTHandle waveField)
        {
            this.waveField = waveField;
        }

        internal static WaterWaveCascadeResources Create(int resolution, int cascadeIndex)
        {
            RTHandle texture = RTHandles.Alloc(
                resolution,
                resolution,
                SliceCount,
                dimension: TextureDimension.Tex2DArray,
                colorFormat: GraphicsFormat.R16G16B16A16_SFloat,
                filterMode: FilterMode.Trilinear,
                enableRandomWrite: true,
                useMipMap: true,
                autoGenerateMips: false,
                anisoLevel: 4,
                wrapMode: TextureWrapMode.Repeat,
                name: $"Water Wave Field Cascade {cascadeIndex}");
            texture.rt.Create();
            return new WaterWaveCascadeResources(texture);
        }

        internal bool IsValid() => waveField != null;

        internal void Release()
        {
            RTHandles.Release(waveField);
        }
    }
}
