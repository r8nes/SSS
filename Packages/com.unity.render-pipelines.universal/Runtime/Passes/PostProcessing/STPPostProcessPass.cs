using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering.RenderGraphModule;

namespace UnityEngine.Rendering.Universal
{
    internal sealed class STPPostProcessPass : PostProcessPass
    {
        public STPPostProcessPass(PostProcessMaterialLibrary materials)
            : base(materials)
        {
        }


        private const string _UpscaledColorTargetName = "_CameraColorUpscaledSTP";

        public TextureHandle Render(RenderGraph renderGraph, UniversalResourceData resourceData, UniversalCameraData cameraData, in TextureHandle source)
        {
            var cameraDepth = resourceData.cameraDepthTexture;
            var motionVectors = resourceData.motionVectorColor;

            Debug.Assert(motionVectors.IsValid(), "MotionVectors are invalid. STP requires a motion vector texture.");

            var srcDesc = source.GetDescriptor(renderGraph);

            var destDesc = GetCompatibleDescriptor(srcDesc,
                cameraData.pixelWidth,
                cameraData.pixelHeight,
                // Avoid enabling sRGB because STP works with compute shaders which can't output sRGB automatically.
                GraphicsFormatUtility.GetLinearFormat(srcDesc.format));

            // STP uses compute shaders so all render textures must enable random writes
            destDesc.enableRandomWrite = true;

            var destination = CreateCompatibleTexture(renderGraph, destDesc, _UpscaledColorTargetName, false, FilterMode.Bilinear);

            int frameIndex = Time.frameCount;
            var noiseTexture = Materials.resources.textures.blueNoise16LTex[frameIndex & (Materials.resources.textures.blueNoise16LTex.Length - 1)];

            StpUtils.Execute(renderGraph, resourceData, cameraData, source, cameraDepth, motionVectors, destination, noiseTexture);

            // Update the camera resolution to reflect the upscaled size
            UpdateCameraResolution(renderGraph, cameraData, new Vector2Int(destDesc.width, destDesc.height));
            return destination;
        }

    }
}
