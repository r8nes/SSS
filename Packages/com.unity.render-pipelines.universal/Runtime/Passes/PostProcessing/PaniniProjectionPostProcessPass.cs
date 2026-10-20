using System.Runtime.CompilerServices;

namespace UnityEngine.Rendering.Universal
{
    internal readonly struct PaniniProjectionParameters
    {
        internal readonly Vector4 shaderParameters;
        internal readonly bool enabled;

        internal PaniniProjectionParameters(Vector4 shaderParameters, bool enabled)
        {
            this.shaderParameters = shaderParameters;
            this.enabled = enabled;
        }

        internal static PaniniProjectionParameters Disabled => new PaniniProjectionParameters(Vector4.zero, false);
    }

    /// <summary>
    /// Panini no longer creates a full-resolution intermediate. This class only converts the
    /// volume/camera state into sampling parameters consumed by Bloom and the presentation pass.
    /// </summary>
    internal sealed class PaniniProjectionPostProcessPass : PostProcessPass<PaniniProjection>
    {
        public PaniniProjectionPostProcessPass(PostProcessMaterialLibrary materials) : base(materials) { }

        internal PaniniProjectionParameters GetParameters(Camera camera, int width, int height)
        {
            if (Settings == null || !Settings.IsActive())
                return PaniniProjectionParameters.Disabled;

            float distance = Settings.distance.value;
            Vector2 viewExtents = CalcViewExtents(camera, width, height);
            Vector2 cropExtents = CalcCropExtents(camera, distance, width, height);
            float scaleF = Mathf.Min(cropExtents.x / viewExtents.x, cropExtents.y / viewExtents.y);
            float cropScale = Mathf.Lerp(1f, Mathf.Clamp01(scaleF), Settings.cropToFit.value);
            return new PaniniProjectionParameters(new Vector4(viewExtents.x, viewExtents.y, distance, cropScale), true);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Vector2 CalcViewExtents(Camera camera, int width, int height)
        {
            float fovY = camera.fieldOfView * Mathf.Deg2Rad;
            float aspect = width / (float)Mathf.Max(1, height);
            float viewExtY = Mathf.Tan(0.5f * fovY);
            return new Vector2(aspect * viewExtY, viewExtY);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Vector2 CalcCropExtents(Camera camera, float d, int width, int height)
        {
            float viewDist = 1f + d;
            Vector2 projPos = CalcViewExtents(camera, width, height);
            float projHyp = Mathf.Sqrt(projPos.x * projPos.x + 1f);
            float cylDistMinusD = 1f / projHyp;
            float cylDist = cylDistMinusD + d;
            Vector2 cylPos = projPos * cylDistMinusD;
            return cylPos * (viewDist / cylDist);
        }
    }
}
