using UnityEngine.Rendering.RenderGraphModule;

namespace UnityEngine.Rendering.Universal
{
    internal sealed class TemporalAAPostProcessPass : PostProcessPass
    {
        private readonly int[] m_Kernels;

        public TemporalAAPostProcessPass(PostProcessMaterialLibrary materials)
            : base(materials)
        {
            ComputeShader shader = materials.temporalAntialiasing;
            m_Kernels = new[]
            {
                shader.FindKernel("TaaVeryLow"),
                shader.FindKernel("TaaLow"),
                shader.FindKernel("TaaMedium"),
                shader.FindKernel("TaaHigh"),
                shader.FindKernel("TaaVeryHigh")
            };
        }

        public TextureHandle Render(RenderGraph renderGraph, UniversalResourceData resourceData,
            UniversalCameraData cameraData, in TextureHandle source)
        {
            var cameraDepth = resourceData.cameraDepth;
            var motionVectors = resourceData.motionVectorColor;
            var taaSource = source;

            Debug.Assert(motionVectors.IsValid(), "MotionVectors are invalid. TAA requires a motion vector texture.");

            int kernel = m_Kernels[(int)cameraData.taaSettings.quality];
            return TemporalAA.Render(renderGraph, Materials.temporalAntialiasing, kernel, cameraData,
                ref taaSource, ref cameraDepth, ref motionVectors);
        }
    }
}
