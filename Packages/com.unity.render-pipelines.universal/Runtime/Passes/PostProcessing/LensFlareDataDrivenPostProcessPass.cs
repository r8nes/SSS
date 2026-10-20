using UnityEngine.Rendering.RenderGraphModule;

namespace UnityEngine.Rendering.Universal
{
    internal sealed class LensFlareDataDrivenPostProcessPass : PostProcessPass
    {
        public LensFlareDataDrivenPostProcessPass(PostProcessMaterialLibrary materials)
            : base(materials)
        {
        }

        private class LensFlarePassData
        {
            internal TextureHandle destinationTexture;
            internal UniversalCameraData cameraData;
            internal Material material;
            internal Rect viewport;
            internal float width;
            internal float height;
        }

        public void LensFlareDataDrivenComputeOcclusion(RenderGraph renderGraph, UniversalResourceData resourceData, UniversalCameraData cameraData, in TextureDesc srcDesc)
        {
            if (!LensFlareCommonSRP.IsOcclusionRTCompatible())
                return;

            // Core owns the occlusion target bind and exposes this draw sequence only through
            // UnsafeCommandBuffer. Keep the unsafe boundary local and declare both target and depth.
            using (var builder = renderGraph.AddUnsafePass<LensFlarePassData>("Lens Flare Compute Occlusion", out var passData, ProfilingSampler.Get(URPProfileId.LensFlareDataDrivenComputeOcclusion)))
            {
                var occlusionHandle = renderGraph.ImportTexture(LensFlareCommonSRP.occlusionRT);
                builder.UseTexture(occlusionHandle, AccessFlags.Write);
                passData.cameraData = cameraData;
                passData.viewport = cameraData.pixelRect;
                passData.material = Materials.lensFlareDataDriven;
                passData.width = (float)srcDesc.width;
                passData.height = (float)srcDesc.height;
                // Panini is a virtual transform of the complete pre-presentation image. Flare
                // geometry is therefore generated in the unprojected camera domain and warped once
                // together with the scene by Bloom/presentation consumers.

                builder.UseTexture(resourceData.cameraDepthTexture, AccessFlags.Read);

                builder.SetRenderFunc(
                    static (LensFlarePassData data, UnsafeGraphContext ctx) =>
                    {
                        var camera = data.cameraData.camera;
                        var gpuNonJitteredProj = GL.GetGPUProjectionMatrix(data.cameraData.GetProjectionMatrixNoJitter(), true);
                        var nonJitteredViewProjMatrix = gpuNonJitteredProj * data.cameraData.GetViewMatrix();

                        LensFlareCommonSRP.ComputeOcclusion(
                            data.material, camera, data.cameraData.xr, 0,
                            data.width, data.height,
                            false, 1.0f, 1.0f, true,
                            camera.transform.position,
                            nonJitteredViewProjMatrix,
                            ctx.cmd,
                            false, false, null, null);
                    });
            }
        }

        public void RenderLensFlareDataDriven(RenderGraph renderGraph, UniversalResourceData resourceData, UniversalCameraData cameraData, in TextureHandle destination, in TextureDesc srcDesc)
        {
            using (var builder = renderGraph.AddUnsafePass<LensFlarePassData>("Lens Flare Data Driven Pass", out var passData, ProfilingSampler.Get(URPProfileId.LensFlareDataDrivenRender)))
            {
                // LensFlareCommonSRP owns a variable-length draw sequence and binds its target internally.
                // Core 17.3 exposes that sequence only through UnsafeCommandBuffer, so an unsafe pass is the
                // narrow, explicit boundary here. All resources touched by the Core API are still declared.
                passData.destinationTexture = destination;
                // Lens-flare elements blend over the existing camera color.
                builder.UseTexture(destination, AccessFlags.ReadWrite);
                passData.cameraData = cameraData;
                passData.material = Materials.lensFlareDataDriven;
                passData.width = (float)srcDesc.width;
                passData.height = (float)srcDesc.height;
                passData.viewport.x = 0.0f;
                passData.viewport.y = 0.0f;
                passData.viewport.width = (float)srcDesc.width;
                passData.viewport.height = (float)srcDesc.height;
                if (LensFlareCommonSRP.IsOcclusionRTCompatible())
                {
                    var occlusionHandle = renderGraph.ImportTexture(LensFlareCommonSRP.occlusionRT);
                    builder.UseTexture(occlusionHandle, AccessFlags.Read);
                }
                else
                {
                    builder.UseTexture(resourceData.cameraDepthTexture, AccessFlags.Read);
                }

                builder.SetRenderFunc(static (LensFlarePassData data, UnsafeGraphContext ctx) =>
                {
                    var camera = data.cameraData.camera;
                    var gpuNonJitteredProj = GL.GetGPUProjectionMatrix(data.cameraData.GetProjectionMatrixNoJitter(), true);
                    var nonJitteredViewProjMatrix = gpuNonJitteredProj * data.cameraData.GetViewMatrix();

                    LensFlareCommonSRP.DoLensFlareDataDrivenCommon(
                        data.material, camera, data.viewport, data.cameraData.xr, 0,
                        data.width, data.height,
                        false, 1.0f, 1.0f,
                        true,
                        camera.transform.position,
                        nonJitteredViewProjMatrix,
                        ctx.cmd,
                        false, false, null, null,
                        data.destinationTexture,
                        GetLensFlareLightAttenuation,
                        false);
                });
            }
        }

        private static float GetLensFlareLightAttenuation(Light light, Camera cam, Vector3 wo)
        {
            // Must always be true
            if (light != null)
            {
                switch (light.type)
                {
                    case LightType.Directional:
                        return LensFlareCommonSRP.ShapeAttenuationDirLight(light.transform.forward, cam.transform.forward);
                    case LightType.Point:
                        return LensFlareCommonSRP.ShapeAttenuationPointLight();
                    case LightType.Spot:
                        return LensFlareCommonSRP.ShapeAttenuationSpotConeLight(light.transform.forward, wo, light.spotAngle, light.innerSpotAngle / 180.0f);
                    default:
                        return 1.0f;
                }
            }

            return 1.0f;
        }
    }
}
