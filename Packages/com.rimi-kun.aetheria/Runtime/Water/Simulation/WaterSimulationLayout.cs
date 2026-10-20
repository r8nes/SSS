using UnityEngine;
using Unity.Mathematics;

namespace Aetheria.Runtime.Water
{
    internal static class WaterSimulationLayout
    {
        internal static int CascadeCount => WaterLimits.CascadeCount;

        internal readonly struct CascadeSamplingContext
        {
            internal readonly float2 currentDirection;

            internal CascadeSamplingContext(float2 currentDirection)
            {
                this.currentDirection = currentDirection;
            }
        }

        internal readonly struct CascadeSamplingParameters
        {
            internal readonly Vector4 scaleOffset;
            internal readonly Vector4 scalesFade;

            internal CascadeSamplingParameters(Vector4 scaleOffset, Vector4 scalesFade)
            {
                this.scaleOffset = scaleOffset;
                this.scalesFade = scalesFade;
            }
        }

        internal static CascadeSamplingContext EvaluateCascadeSamplingContext(WaterSurface surface)
        {
            float angle = NormalizeAngle(surface.currentDirection) * Mathf.Deg2Rad;
            return new CascadeSamplingContext(new float2(Mathf.Cos(angle), Mathf.Sin(angle)));
        }

        internal static CascadeSamplingParameters EvaluateCascadeSamplingParameters(
            WaterSurface surface,
            WaterSimulationState simulation,
            in CascadeSamplingContext context,
            int cascadeIndex)
        {
            WaterCascadeRenderingParameters rendering = simulation.renderingParameters.GetCascade(cascadeIndex);
            float distance = simulation.EvaluateCurrentDistance(rendering.currentSpeed);
            float2 offset = context.currentDirection * distance * rendering.inverseTileLength;
            return new CascadeSamplingParameters(
                new Vector4(rendering.inverseTileLength.x, rendering.inverseTileLength.y, offset.x, offset.y),
                new Vector4(rendering.displacementScale, rendering.normalScale,
                    rendering.distanceFadeScale, rendering.distanceFadeBias));
        }

        internal static int GetCascadeResolution(WaterSimulationResolution resolution)
        {
            return (int)resolution;
        }

        internal static float NormalizeAngle(float degrees)
        {
            float angle = degrees % 360.0f;
            return angle < 0.0f ? angle + 360.0f : angle;
        }

        internal static void GetFftKernels(
            UnityEngine.ComputeShader fourierTransform,
            WaterSimulationResolution resolution,
            out int rowKernel,
            out int transposeKernel)
        {
            string suffix = resolution switch
            {
                WaterSimulationResolution.High256 => "256",
                WaterSimulationResolution.Medium128 => "128",
                _ => "64",
            };
            rowKernel = fourierTransform.FindKernel($"WaterRowFFT_{suffix}");
            transposeKernel = fourierTransform.FindKernel("WaterTransposeComplex");
        }
    }
}
