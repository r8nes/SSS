using UnityEngine.Rendering;

namespace UnityEngine.Rendering.Universal
{
    internal static class Fsr3Utils
    {
        internal static bool IsSupported(bool renderGraphEnabled)
        {
            return renderGraphEnabled
                && SystemInfo.supportsComputeShaders
                && SystemInfo.graphicsDeviceType != GraphicsDeviceType.OpenGLES3
                && SystemInfo.IsFormatSupported(UnityEngine.Experimental.Rendering.GraphicsFormat.R8_UNorm, UnityEngine.Experimental.Rendering.GraphicsFormatUsage.LoadStore);
        }

        internal static float GetRenderScale(Fsr3QualityMode quality)
        {
            return quality switch
            {
                Fsr3QualityMode.NativeAA => 1.0f,
                Fsr3QualityMode.UltraQuality => 1.0f / 1.2f,
                Fsr3QualityMode.Quality => 1.0f / 1.5f,
                Fsr3QualityMode.Balanced => 1.0f / 1.7f,
                Fsr3QualityMode.Performance => 1.0f / 2.0f,
                Fsr3QualityMode.UltraPerformance => 1.0f / 3.0f,
                _ => 1.0f / 1.5f,
            };
        }

        internal static float GetMipmapBias(float renderWidth, float outputWidth)
        {
            return Mathf.Log(renderWidth / outputWidth, 2.0f) - 1.0f;
        }

        internal static Vector2 CalculateJitter(UniversalCameraData cameraData)
        {
            int frameIndex = TemporalAA.CalculateTaaFrameIndex(ref cameraData.taaSettings);
            int renderWidth = Mathf.Max(1, cameraData.scaledWidth);
            int phaseCount = FidelityFX.FSR3.Fsr3Upscaler.GetJitterPhaseCount(renderWidth, cameraData.pixelWidth);
            FidelityFX.FSR3.Fsr3Upscaler.GetJitterOffset(out float x, out float y, frameIndex, phaseCount);
            return new Vector2(x, y);
        }

        internal static Matrix4x4 CalculateJitterMatrix(UniversalCameraData cameraData)
        {
            Vector2 jitter = CalculateJitter(cameraData);
            float renderWidth = Mathf.Max(1, cameraData.scaledWidth);
            float renderHeight = Mathf.Max(1, cameraData.scaledHeight);
            return Matrix4x4.Translate(new Vector3(jitter.x * (2.0f / renderWidth), jitter.y * (2.0f / renderHeight), 0.0f));
        }
    }
}
