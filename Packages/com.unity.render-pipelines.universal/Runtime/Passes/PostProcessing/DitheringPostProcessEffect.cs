using System.Runtime.CompilerServices;

namespace UnityEngine.Rendering.Universal
{
    internal sealed class DitheringPostProcessEffect
    {
        private int m_DitheringTextureIndex;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Setup(Material material, PostProcessData resources, bool isDitheringEnabled, int width, int height, bool hasFinalPass)
        {
            if (!hasFinalPass && isDitheringEnabled)
                SetupFinalPass(material, resources, true, width, height);
        }

        public void SetupFinalPass(Material material, PostProcessData resources, bool isDitheringEnabled, int width, int height)
        {
            if (!isDitheringEnabled)
                return;

            m_DitheringTextureIndex = PostProcessUtils.ConfigureDithering(
                resources,
                m_DitheringTextureIndex,
                width, height,
                material
            );
        }
    }
}
