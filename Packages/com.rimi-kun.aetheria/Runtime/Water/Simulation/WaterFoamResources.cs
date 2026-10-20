using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Experimental.Rendering;

namespace Aetheria.Runtime.Water
{
    internal sealed class WaterFoamResources
    {
        internal RTHandle current;
        internal RTHandle history;

        internal static WaterFoamResources Create(int resolution, int lodCount)
        {
            lodCount = Mathf.Clamp(lodCount, 2, WaterLimits.MaxLodCount);
            return new WaterFoamResources
            {
                current = Allocate(resolution, lodCount, "Water Foam LOD Current"),
                history = Allocate(resolution, lodCount, "Water Foam LOD History")
            };
        }

        internal bool IsValid()
        {
            return current != null
                && history != null;
        }

        internal void PrepareSimulationStep()
        {
            (current, history) = (history, current);
        }

        internal void CommitHistoryAsCurrent()
        {
            (current, history) = (history, current);
        }

        internal void Release()
        {
            RTHandles.Release(history);
            RTHandles.Release(current);
            history = null;
            current = null;
        }

        private static RTHandle Allocate(int resolution, int lodCount, string name)
        {
            RTHandle texture = RTHandles.Alloc(
                resolution,
                resolution,
                lodCount,
                dimension: TextureDimension.Tex2DArray,
                colorFormat: GraphicsFormat.R16G16B16A16_SFloat,
                filterMode: FilterMode.Bilinear,
                enableRandomWrite: true,
                useMipMap: false,
                autoGenerateMips: false,
                wrapMode: TextureWrapMode.Clamp,
                name: name);
            texture.rt.Create();
            return texture;
        }
    }
}
