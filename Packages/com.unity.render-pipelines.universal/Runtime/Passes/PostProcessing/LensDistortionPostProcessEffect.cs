using System.Runtime.CompilerServices;

namespace UnityEngine.Rendering.Universal
{
    internal sealed class LensDistortionPostProcessEffect
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Setup(Material material, LensDistortion lensDistortion, bool isSceneView)
        {
            float amount = 1.6f * Mathf.Max(Mathf.Abs(lensDistortion.intensity.value * 100f), 1f);
            float theta = Mathf.Deg2Rad * Mathf.Min(160f, amount);
            float sigma = 2f * Mathf.Tan(theta * 0.5f);
            var center = lensDistortion.center.value * 2f - Vector2.one;
            var p1 = new Vector4(
                center.x,
                center.y,
                Mathf.Max(lensDistortion.xMultiplier.value, 1e-4f),
                Mathf.Max(lensDistortion.yMultiplier.value, 1e-4f)
            );
            var p2 = new Vector4(
                lensDistortion.intensity.value >= 0f ? theta : 1f / theta,
                sigma,
                1f / lensDistortion.scale.value,
                lensDistortion.intensity.value * 100f
            );

            material.SetVector(PostProcessShaderConstants._Distortion_Params1, p1);
            material.SetVector(PostProcessShaderConstants._Distortion_Params2, p2);
        }
    }
}
