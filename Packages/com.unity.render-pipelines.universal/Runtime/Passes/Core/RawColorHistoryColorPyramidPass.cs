#if URP_COMPATIBILITY_MODE
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal.Internal;

namespace UnityEngine.Rendering.Universal
{
    internal sealed class RawColorHistoryColorPyramidPass : ScriptableRenderPass
    {
        private static readonly ProfilingSampler s_GenerateRawColorHistoryColorPyramidSampler = new("Generate Raw Color History Color Pyramid");
        private readonly ColorPyramidGenerator m_ColorPyramidGenerator;
        private RTHandle m_Source;
        private RTHandle m_Destination;
        private Vector2Int m_Size;

        internal RawColorHistoryColorPyramidPass(RenderPassEvent evt, ColorPyramidGenerator colorPyramidGenerator)
        {
            profilingSampler = new ProfilingSampler("ColorPyramid");
            renderPassEvent = evt;
            m_ColorPyramidGenerator = colorPyramidGenerator;
            useNativeRenderPass = false;
        }

        internal void Setup(RTHandle source, RTHandle destination, Vector2Int size)
        {
            m_Source = source;
            m_Destination = destination;
            m_Size = new Vector2Int(Mathf.Max(1, size.x), Mathf.Max(1, size.y));
        }

        [System.Obsolete(DeprecationMessage.CompatibilityScriptingAPIObsoleteFrom2023_3)]
        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            if (m_ColorPyramidGenerator == null || !m_ColorPyramidGenerator.isValid ||
                m_Source == null || m_Source.rt == null ||
                m_Destination == null || m_Destination.rt == null || m_Destination.rt.mipmapCount <= 1)
                return;

            var cmd = renderingData.commandBuffer;
            using (new ProfilingScope(CommandBufferHelpers.GetRasterCommandBuffer(cmd), s_GenerateRawColorHistoryColorPyramidSampler))
            {
                m_ColorPyramidGenerator.RenderColorPyramid(cmd, m_Size, m_Source, m_Destination.rt);
            }
        }
    }
}
#endif
