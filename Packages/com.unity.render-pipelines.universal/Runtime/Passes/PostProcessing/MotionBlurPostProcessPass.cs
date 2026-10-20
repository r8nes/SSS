using System;
using UnityEngine.Rendering.RenderGraphModule;

namespace UnityEngine.Rendering.Universal
{
    internal sealed class MotionBlurPostProcessPass : PostProcessPass<MotionBlur>
    {
        private const int k_ThreadGroupSizeX = 16;
        private const int k_ThreadGroupSizeY = 8;

        private readonly ComputeShader m_Shader;
        private readonly int[] m_CameraKernels = new int[3];
        private readonly int[] m_ObjectKernels = new int[3];

        private static readonly int s_InputTexture = Shader.PropertyToID("_InputTexture");
        private static readonly int s_OutputTexture = Shader.PropertyToID("_OutputTexture");
        private static readonly int s_MotionVectorTexture = Shader.PropertyToID("_MotionVectorTexture");
        private static readonly int s_CameraDepthTexture = Shader.PropertyToID("_CameraDepthTexture");
        private static readonly int s_OverlayExclusionDepthTexture = Shader.PropertyToID("_OverlayMotionBlurExclusionDepthTexture");
        private static readonly int s_SourceSize = Shader.PropertyToID("_SourceSize");
        private static readonly int s_InputUVParams = Shader.PropertyToID("_InputUVParams");
        private static readonly int s_MotionVectorUVParams = Shader.PropertyToID("_MotionVectorUVParams");
        private static readonly int s_DepthUVParams = Shader.PropertyToID("_DepthUVParams");
        private static readonly int s_OverlayExclusionDepthUVParams = Shader.PropertyToID("_OverlayMotionBlurExclusionDepthUVParams");
        private static readonly int s_OverlayExclusionEnabled = Shader.PropertyToID("_OverlayMotionBlurExclusionEnabled");
        private static readonly int s_Intensity = Shader.PropertyToID("_Intensity");
        private static readonly int s_Clamp = Shader.PropertyToID("_Clamp");
        private static readonly int s_AlphaOutput = Shader.PropertyToID("_AlphaOutput");
        private static readonly int s_PrevReprojection = Shader.PropertyToID("_MotionBlurPrevReprojection");
        private static readonly int s_CurrentReprojection = Shader.PropertyToID("_MotionBlurCurrentReprojection");

        public MotionBlurPostProcessPass(PostProcessMaterialLibrary materials)
            : base(materials)
        {
            m_Shader = materials.motionBlurCS;
            if (m_Shader == null)
                return;

            m_CameraKernels[0] = m_Shader.FindKernel("KCameraLow");
            m_CameraKernels[1] = m_Shader.FindKernel("KCameraMedium");
            m_CameraKernels[2] = m_Shader.FindKernel("KCameraHigh");
            m_ObjectKernels[0] = m_Shader.FindKernel("KObjectLow");
            m_ObjectKernels[1] = m_Shader.FindKernel("KObjectMedium");
            m_ObjectKernels[2] = m_Shader.FindKernel("KObjectHigh");
        }

        private sealed class MotionBlurPassData
        {
            internal ComputeShader shader;
            internal int kernel;
            internal TextureHandle source;
            internal TextureHandle destination;
            internal TextureHandle depth;
            internal TextureHandle motionVectors;
            internal TextureHandle overlayExclusionDepth;
            internal float intensity;
            internal float clamp;
            internal int alphaOutput;
            internal Matrix4x4 prevReprojection;
            internal Matrix4x4 currentReprojection;
            internal bool useObjectMotion;
            internal bool hasOverlayExclusion;
        }

        private static Vector2Int GetActiveViewportSize(RTHandle handle)
        {
            if (handle == null)
                return Vector2Int.one;

            Vector2Int size;
            if (handle.useScaling)
            {
                size = handle.GetScaledSize(handle.rtHandleProperties.currentViewportSize);
            }
            else if (handle.rt != null)
            {
                size = new Vector2Int(handle.rt.width, handle.rt.height);
            }
            else if (handle.externalTexture != null)
            {
                size = new Vector2Int(handle.externalTexture.width, handle.externalTexture.height);
            }
            else
            {
                return Vector2Int.one;
            }

            if (handle.rt != null && handle.rt.useDynamicScale)
            {
                size.x = Mathf.RoundToInt(size.x * ScalableBufferManager.widthScaleFactor);
                size.y = Mathf.RoundToInt(size.y * ScalableBufferManager.heightScaleFactor);
            }

            size.x = Mathf.Max(1, size.x);
            size.y = Mathf.Max(1, size.y);
            return size;
        }

        private static Vector4 BuildSizeVector(Vector2Int size)
        {
            float width = Mathf.Max(1, size.x);
            float height = Mathf.Max(1, size.y);
            return new Vector4(width, height, 1.0f / width, 1.0f / height);
        }

        private static Vector4 BuildUVParams(RTHandle handle)
        {
            if (handle == null)
                return new Vector4(1f, 1f, 1f, 1f);

            Vector2Int viewportSize = GetActiveViewportSize(handle);
            float width = 1f;
            float height = 1f;
            if (handle.rt != null)
            {
                width = Mathf.Max(1, handle.rt.width);
                height = Mathf.Max(1, handle.rt.height);
                if (handle.rt.useDynamicScale)
                {
                    width *= ScalableBufferManager.widthScaleFactor;
                    height *= ScalableBufferManager.heightScaleFactor;
                }
            }
            else if (handle.externalTexture != null)
            {
                width = Mathf.Max(1, handle.externalTexture.width);
                height = Mathf.Max(1, handle.externalTexture.height);
            }

            float scaleX = Mathf.Clamp01(viewportSize.x / width);
            float scaleY = Mathf.Clamp01(viewportSize.y / height);
            return new Vector4(scaleX, scaleY, 1.0f / width, 1.0f / height);
        }

        public TextureHandle Render(RenderGraph renderGraph, UniversalResourceData resourceData, UniversalCameraData cameraData, in TextureHandle source)
        {
            if (m_Shader == null)
                throw new InvalidOperationException("MotionBlur.compute is required for motion blur.");

            var sourceDesc = source.GetDescriptor(renderGraph);
            var destinationDesc = sourceDesc;
            MakeComputeCompatible(ref destinationDesc);
            destinationDesc.name = "_MotionBlurTarget";
            destinationDesc.filterMode = FilterMode.Bilinear;
            var destination = renderGraph.CreateTexture(destinationDesc);

            bool useObjectMotion = Settings.mode.value == MotionBlurMode.CameraAndObjects;
            int quality = Mathf.Clamp((int)Settings.quality.value, 0, 2);
            int kernel = useObjectMotion ? m_ObjectKernels[quality] : m_CameraKernels[quality];

            Matrix4x4 prevReprojection = Matrix4x4.identity;
            Matrix4x4 currentReprojection = Matrix4x4.identity;
            if (!useObjectMotion)
            {
                MotionVectorsPersistentData motionData = null;
                if (cameraData.camera.TryGetComponent<UniversalAdditionalCameraData>(out var additionalCameraData))
                    motionData = additionalCameraData.motionVectorsPersistentData;
#if UNITY_EDITOR
                else if (cameraData.isSceneViewCamera)
                    motionData = UniversalSceneViewCameraHistory.GetMotionVectorsPersistentData(cameraData.camera);
#endif

                if (motionData != null)
                {
                    Matrix4x4 jitteredViewProjection = cameraData.GetGPUProjectionMatrix(true) * cameraData.GetViewMatrix();
                    Matrix4x4 inverseJitteredViewProjection = jitteredViewProjection.inverse;
                    prevReprojection = motionData.previousViewProjection * inverseJitteredViewProjection;
                    currentReprojection = motionData.viewProjection * inverseJitteredViewProjection;
                }
            }

            TextureHandle depth = resourceData.cameraDepthTexture;
            TextureHandle motionVectors = resourceData.motionVectorColor;
            bool hasOverlayExclusion = resourceData.motionBlurExclusionDepthTexture.IsValid();
            TextureHandle overlayExclusionDepth = hasOverlayExclusion
                ? resourceData.motionBlurExclusionDepthTexture
                : cameraData.xr.enabled
                    ? renderGraph.defaultResources.blackTextureXR
                    : renderGraph.defaultResources.blackTexture;

            if (useObjectMotion)
            {
                Debug.Assert(ScriptableRenderer.current.SupportsMotionVectors(), "Current renderer does not support motion vectors.");
                Debug.Assert(motionVectors.IsValid(), "Motion vectors are invalid. Per-object motion blur requires a motion vector texture.");
            }
            else
            {
                Debug.Assert(depth.IsValid(), "Camera depth texture is invalid. Per-camera motion blur requires a depth texture.");
            }

            using (var builder = renderGraph.AddComputePass<MotionBlurPassData>("Motion Blur", out var passData,
                       ProfilingSampler.Get(URPProfileId.MotionBlur)))
            {
                passData.shader = m_Shader;
                passData.kernel = kernel;
                passData.source = source;
                passData.destination = destination;
                passData.depth = depth;
                passData.motionVectors = motionVectors;
                passData.overlayExclusionDepth = overlayExclusionDepth;
                passData.intensity = Settings.intensity.value;
                passData.clamp = Settings.clamp.value;
                passData.alphaOutput = cameraData.isAlphaOutputEnabled ? 1 : 0;
                passData.prevReprojection = prevReprojection;
                passData.currentReprojection = currentReprojection;
                passData.useObjectMotion = useObjectMotion;
                passData.hasOverlayExclusion = hasOverlayExclusion;

                builder.UseTexture(source, AccessFlags.Read);
                builder.UseTexture(destination, AccessFlags.Write);
                builder.UseTexture(overlayExclusionDepth, AccessFlags.Read);
                if (useObjectMotion)
                    builder.UseTexture(motionVectors, AccessFlags.Read);
                else
                    builder.UseTexture(depth, AccessFlags.Read);

                builder.SetRenderFunc(static (MotionBlurPassData data, ComputeGraphContext context) =>
                {
                    var cmd = context.cmd;
                    RTHandle source = data.source;
                    RTHandle destination = data.destination;
                    RTHandle motionVectors = data.useObjectMotion ? (RTHandle)data.motionVectors : null;
                    RTHandle depth = data.useObjectMotion ? null : (RTHandle)data.depth;
                    RTHandle overlayExclusionDepth = data.overlayExclusionDepth;

                    Vector2Int viewportSize = GetActiveViewportSize(destination);
                    Vector4 sourceSize = BuildSizeVector(viewportSize);
                    Vector4 inputUVParams = BuildUVParams(source);
                    Vector4 motionVectorUVParams = data.useObjectMotion ? BuildUVParams(motionVectors) : Vector4.zero;
                    Vector4 depthUVParams = data.useObjectMotion ? Vector4.zero : BuildUVParams(depth);
                    Vector4 overlayExclusionDepthUVParams = BuildUVParams(overlayExclusionDepth);

                    cmd.SetComputeTextureParam(data.shader, data.kernel, s_InputTexture, data.source);
                    cmd.SetComputeTextureParam(data.shader, data.kernel, s_OutputTexture, data.destination);
                    cmd.SetComputeTextureParam(data.shader, data.kernel, s_OverlayExclusionDepthTexture, data.overlayExclusionDepth);
                    if (data.useObjectMotion)
                        cmd.SetComputeTextureParam(data.shader, data.kernel, s_MotionVectorTexture, data.motionVectors);
                    else
                        cmd.SetComputeTextureParam(data.shader, data.kernel, s_CameraDepthTexture, data.depth);
                    cmd.SetComputeVectorParam(data.shader, s_SourceSize, sourceSize);
                    cmd.SetComputeVectorParam(data.shader, s_InputUVParams, inputUVParams);
                    cmd.SetComputeVectorParam(data.shader, s_MotionVectorUVParams, motionVectorUVParams);
                    cmd.SetComputeVectorParam(data.shader, s_DepthUVParams, depthUVParams);
                    cmd.SetComputeVectorParam(data.shader, s_OverlayExclusionDepthUVParams, overlayExclusionDepthUVParams);
                    cmd.SetComputeIntParam(data.shader, s_OverlayExclusionEnabled, data.hasOverlayExclusion ? 1 : 0);
                    cmd.SetComputeFloatParam(data.shader, s_Intensity, data.intensity);
                    cmd.SetComputeFloatParam(data.shader, s_Clamp, data.clamp);
                    cmd.SetComputeIntParam(data.shader, s_AlphaOutput, data.alphaOutput);
                    if (!data.useObjectMotion)
                    {
                        cmd.SetComputeMatrixParam(data.shader, s_PrevReprojection, data.prevReprojection);
                        cmd.SetComputeMatrixParam(data.shader, s_CurrentReprojection, data.currentReprojection);
                    }
                    cmd.DispatchCompute(data.shader, data.kernel,
                        (viewportSize.x + k_ThreadGroupSizeX - 1) / k_ThreadGroupSizeX,
                        (viewportSize.y + k_ThreadGroupSizeY - 1) / k_ThreadGroupSizeY,
                        1);
                });
            }

            return destination;
        }
    }
}
