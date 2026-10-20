using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Aetheria.Runtime.Rendering.Fog.Volumetrics
{
    internal readonly struct VolumetricContext
    {
        public readonly Camera Camera;

        public readonly int PixelWidth;
        public readonly int PixelHeight;

        public readonly int FroxelWidth;
        public readonly int FroxelHeight;
        public readonly int FroxelDepth;

        public readonly float NearPlane;
        public readonly float FarPlane;
        public readonly float LastSliceDistance;

        public readonly Vector3 CameraPositionWs;

        public readonly Vector4 GridParams;
        public readonly Vector4 DepthParams;
        public readonly Vector4 DepthEncodingParams;
        public readonly Vector4 DepthDecodingParams;
        public readonly Vector4 DepthStepParams;
        public readonly Vector4 ScreenParams;
        public readonly Vector4 CameraParams;
        public readonly Vector4 PlanetUpAltitude;
        public readonly Vector4 VBufferViewportSize;
        public readonly Vector4 ViewDirOriginWS;
        public readonly Vector4 ViewDirStepXWS;
        public readonly Vector4 ViewDirStepYWS;
        public readonly Vector4 ViewForwardWS;

        public readonly VBufferParameters VBuffer;

        public readonly bool GaussianFilterEnabled;
        public readonly bool UseBiquadraticReconstruction;

        public VolumetricContext(
            UniversalCameraData cameraData,
            VolumetricQualitySettings quality,
            float maxDistance,
            Vector4 planetUpAltitude)
        {
            Camera = cameraData.camera;

            PixelWidth = Mathf.Max(1, cameraData.cameraTargetDescriptor.width);
            PixelHeight = Mathf.Max(1, cameraData.cameraTargetDescriptor.height);

            var viewportSize = quality.ComputeViewportSize(PixelWidth, PixelHeight);
            FroxelWidth = viewportSize.x;
            FroxelHeight = viewportSize.y;
            FroxelDepth = viewportSize.z;

            NearPlane = Mathf.Max(Camera.nearClipPlane, 0.001f);

            VBuffer = new VBufferParameters(
                viewportSize,
                Mathf.Max(1.0f, maxDistance),
                NearPlane,
                Mathf.Max(Camera.farClipPlane, NearPlane + 0.01f),
                Camera.fieldOfView * Mathf.Deg2Rad,
                quality.SliceDistributionUniformity);

            FarPlane = Mathf.Max(NearPlane + 0.01f, VBuffer.LastSliceDistance);
            LastSliceDistance = VBuffer.LastSliceDistance;
            DepthEncodingParams = VBuffer.DepthEncodingParams;
            DepthDecodingParams = VBuffer.DepthDecodingParams;
            float inverseSliceCount = 1.0f / Mathf.Max(1, FroxelDepth);
            float depthStep = Mathf.Pow(2.0f, DepthDecodingParams.y * inverseSliceCount);
            float depthHalfStep = Mathf.Pow(2.0f, 0.5f * DepthDecodingParams.y * inverseSliceCount);
            DepthStepParams = new Vector4(depthStep, depthHalfStep, 0.0f, 0.0f);

            CameraPositionWs = Camera.transform.position;
            GaussianFilterEnabled = quality.GaussianFilterEnabled;
            UseBiquadraticReconstruction = quality.UseBiquadraticReconstruction;

            GridParams = new Vector4(FroxelWidth, FroxelHeight, FroxelDepth, 1f / FroxelDepth);
            DepthParams = new Vector4(NearPlane, FarPlane, LastSliceDistance, 1f / Mathf.Max(LastSliceDistance, 1e-6f));
            ScreenParams = new Vector4(PixelWidth, PixelHeight, 1f / PixelWidth, 1f / PixelHeight);
            CameraParams = new Vector4(CameraPositionWs.x, CameraPositionWs.y, CameraPositionWs.z, Time.time);
            PlanetUpAltitude = planetUpAltitude;
            VBufferViewportSize = VBuffer.ViewportSizeAndInvSize;

            var gpuProjection = GL.GetGPUProjectionMatrix(Camera.projectionMatrix, true);
            var inverseViewProjection = (gpuProjection * cameraData.GetViewMatrix()).inverse;
            ComputeViewDirectionBasis(
                inverseViewProjection,
                CameraPositionWs,
                FroxelWidth,
                FroxelHeight,
                out Vector4 viewDirOriginWs,
                out Vector4 viewDirStepXWs,
                out Vector4 viewDirStepYWs);
            ViewDirOriginWS = viewDirOriginWs;
            ViewDirStepXWS = viewDirStepXWs;
            ViewDirStepYWS = viewDirStepYWs;
            Vector3 viewForward = Camera.transform.forward.normalized;
            ViewForwardWS = new Vector4(viewForward.x, viewForward.y, viewForward.z, 0.0f);
        }
        private static void ComputeViewDirectionBasis(
            Matrix4x4 inverseViewProjection,
            Vector3 cameraPositionWS,
            int width,
            int height,
            out Vector4 originWS,
            out Vector4 stepXWS,
            out Vector4 stepYWS)
        {
            float inverseWidth = 1.0f / Mathf.Max(1, width);
            float inverseHeight = 1.0f / Mathf.Max(1, height);
            float clipYSign = SystemInfo.graphicsUVStartsAtTop ? -1.0f : 1.0f;
            float farDeviceDepth = SystemInfo.usesReversedZBuffer ? 0.0f : 1.0f;

            var clipOrigin = new Vector4(
                inverseWidth - 1.0f,
                clipYSign * (inverseHeight - 1.0f),
                farDeviceDepth,
                1.0f);
            var clipStepX = new Vector4(2.0f * inverseWidth, 0.0f, 0.0f, 0.0f);
            var clipStepY = new Vector4(0.0f, clipYSign * 2.0f * inverseHeight, 0.0f, 0.0f);

            Vector3 origin = TransformClipToCameraRay(
                inverseViewProjection, clipOrigin, cameraPositionWS);
            Vector3 stepX = TransformClipToCameraRay(
                inverseViewProjection, clipStepX, cameraPositionWS);
            Vector3 stepY = TransformClipToCameraRay(
                inverseViewProjection, clipStepY, cameraPositionWS);
            originWS = new Vector4(origin.x, origin.y, origin.z, 0.0f);
            stepXWS = new Vector4(stepX.x, stepX.y, stepX.z, 0.0f);
            stepYWS = new Vector4(stepY.x, stepY.y, stepY.z, 0.0f);
        }

        private static Vector3 TransformClipToCameraRay(
            Matrix4x4 inverseViewProjection,
            Vector4 clipPosition,
            Vector3 cameraPositionWS)
        {
            Vector4 worldPosition = inverseViewProjection * clipPosition;
            return new Vector3(
                worldPosition.x - cameraPositionWS.x * worldPosition.w,
                worldPosition.y - cameraPositionWS.y * worldPosition.w,
                worldPosition.z - cameraPositionWS.z * worldPosition.w);
        }
    }
}