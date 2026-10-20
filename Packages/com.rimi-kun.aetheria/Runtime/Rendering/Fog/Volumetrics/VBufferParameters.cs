using UnityEngine;

namespace Aetheria.Runtime.Rendering.Fog.Volumetrics
{
    internal readonly struct VBufferParameters
    {
        private readonly Vector3Int _viewportSize;
    
        public readonly Vector4 DepthEncodingParams;
        public readonly Vector4 DepthDecodingParams;
    
        public readonly float LastSliceDistance;

        public Vector4 ViewportSizeAndInvSize => new(
            _viewportSize.x,
            _viewportSize.y,
            1.0f / Mathf.Max(1, _viewportSize.x),
            1.0f / Mathf.Max(1, _viewportSize.y));

        public VBufferParameters(
            Vector3Int viewportSize,
            float depthExtent,
            float cameraNearPlane,
            float cameraFarPlane,
            float cameraVerticalFoVRadians,
            float sliceDistributionUniformity)
        {
            _viewportSize = new Vector3Int(
                Mathf.Max(1, viewportSize.x),
                Mathf.Max(1, viewportSize.y),
                Mathf.Max(1, viewportSize.z));

            float aspectRatio = _viewportSize.x / (float)Mathf.Max(1, _viewportSize.y);
            float farPlaneHeight = 2.0f * Mathf.Tan(0.5f * cameraVerticalFoVRadians) * cameraFarPlane;
            float farPlaneWidth = farPlaneHeight * aspectRatio;
            float farPlaneMaxDim = Mathf.Max(farPlaneWidth, farPlaneHeight);
            float farPlaneSphereCapDistance =
                Mathf.Sqrt(cameraFarPlane * cameraFarPlane + 0.25f * farPlaneMaxDim * farPlaneMaxDim);

            float nearDistance = Mathf.Max(cameraNearPlane, 0.001f);
            float farDistance = Mathf.Max(nearDistance + 0.001f,
                Mathf.Min(nearDistance + Mathf.Max(depthExtent, 0.001f), farPlaneSphereCapDistance));

            float c = 2.0f - 2.0f * Mathf.Clamp01(sliceDistributionUniformity);
            c = Mathf.Max(c, 0.001f);

            DepthEncodingParams = ComputeLogarithmicDepthEncodingParams(nearDistance, farDistance, c);
            DepthDecodingParams = ComputeLogarithmicDepthDecodingParams(nearDistance, farDistance, c);
            LastSliceDistance = ComputeLastSliceDistance(_viewportSize.z, DepthDecodingParams);
        }

        private static Vector4 ComputeLogarithmicDepthEncodingParams(float nearPlane, float farPlane, float c)
        {
            Vector4 depthParams = default;
            depthParams.y = 1.0f / Mathf.Log(c * (farPlane - nearPlane) + 1.0f, 2.0f);
            depthParams.x = Mathf.Log(c, 2.0f) * depthParams.y;
            depthParams.z = nearPlane - 1.0f / c;
            depthParams.w = 0.0f;
            return depthParams;
        }

        private static Vector4 ComputeLogarithmicDepthDecodingParams(float nearPlane, float farPlane, float c)
        {
            Vector4 depthParams = default;
            depthParams.x = 1.0f / c;
            depthParams.y = Mathf.Log(c * (farPlane - nearPlane) + 1.0f, 2.0f);
            depthParams.z = nearPlane - 1.0f / c;
            depthParams.w = 0.0f;
            return depthParams;
        }

        public static float DecodeLogarithmicDepthGeneralized(float encodedDepth, Vector4 decodingParams)
        {
            const float _ln2 = 0.69314718f;
            return decodingParams.x * Mathf.Exp(_ln2 * encodedDepth * decodingParams.y) + decodingParams.z;
        }

        public static float ComputeLastSliceDistance(int sliceCount, Vector4 decodingParams)
        {
            float d = 1.0f - 0.5f / Mathf.Max(1, sliceCount);
            return DecodeLogarithmicDepthGeneralized(d, decodingParams);
        }
    }
}