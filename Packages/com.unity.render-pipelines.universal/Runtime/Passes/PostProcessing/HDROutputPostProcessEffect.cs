using System.Runtime.CompilerServices;

namespace UnityEngine.Rendering.Universal
{
    internal static class HDROutputPostProcessEffect
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Setup(HDROutputUtils.HDRDisplayInformation hdrDisplayInformation, ColorGamut hdrDisplayColorGamut, Material material, HDROutputUtils.Operation hdrOperations, bool rendersOverlayUI, Tonemapping tonemapping)
        {
            UniversalRenderPipeline.GetHDROutputLuminanceParameters(hdrDisplayInformation, hdrDisplayColorGamut, tonemapping, out var hdrOutputLuminanceParams);
            material.SetVector(ShaderPropertyId.hdrOutputLuminanceParams, hdrOutputLuminanceParams);

            HDROutputUtils.ConfigureHDROutput(material, hdrDisplayColorGamut, hdrOperations);
        }
    }
}
