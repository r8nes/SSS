using System;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering.RenderGraphModule;

namespace UnityEngine.Rendering.Universal.Internal
{
    internal static class ContactShadowsShaderIDs
    {
        internal static readonly int ContactShadowTexture = Shader.PropertyToID("_ContactShadowTexture");
        internal static readonly int ContactShadowTextureUAV = Shader.PropertyToID("_ContactShadowTextureUAV");
        internal static readonly int ContactShadowParamsParameters = Shader.PropertyToID("_ContactShadowParamsParameters");
        internal static readonly int ContactShadowParamsParameters2 = Shader.PropertyToID("_ContactShadowParamsParameters2");
        internal static readonly int ContactShadowParamsParameters3 = Shader.PropertyToID("_ContactShadowParamsParameters3");
        internal static readonly int ContactShadowFrameIndex = Shader.PropertyToID("_ContactShadowFrameIndex");
        internal static readonly int ContactShadowTaaEnabled = Shader.PropertyToID("_ContactShadowTaaEnabled");
        internal static readonly int MainLightContactShadowMask = Shader.PropertyToID("_MainLightContactShadowMask");
        internal static readonly int ContactShadowOpacity = Shader.PropertyToID("_ContactShadowOpacity");
    }

    /// <summary>
    /// Native RenderGraph port of HDRP's raster Contact Shadows pass, restricted to the URP main Directional Light.
    /// </summary>
    internal sealed class ContactShadowsPass : ScriptableRenderPass
    {
        const int k_TileSize = 8;
        const int k_MainDirectionalLightMask = 1;

        readonly ComputeShader m_ComputeShader;
        readonly int m_Kernel;

        internal bool isValid => m_ComputeShader != null && m_Kernel >= 0;

        internal ContactShadowsPass(ComputeShader computeShader)
        {
            profilingSampler = new ProfilingSampler("Contact Shadows");
            renderPassEvent = RenderPassEvent.BeforeRenderingDeferredLights;
            m_ComputeShader = computeShader;
            m_Kernel = computeShader != null && computeShader.HasKernel("DeferredContactShadow")
                ? computeShader.FindKernel("DeferredContactShadow")
                : -1;
        }

        internal void Dispose()
        {
        }

        sealed class PassData
        {
            internal ComputeShader computeShader;
            internal int kernel;
            internal TextureHandle depthPyramid;
            internal TextureHandle output;
            internal Vector4 mip0Size;
            internal Vector4 params1;
            internal Vector4 params2;
            internal Vector4 params3;
            internal int frameIndex;
            internal int taaEnabled;
            internal int tilesX;
            internal int tilesY;
            internal float opacity;
        }

        internal TextureHandle Render(RenderGraph renderGraph, ContextContainer frameData, ContactShadows settings)
        {
            DepthPyramidFrameData depthPyramidData = frameData.GetOrCreate<DepthPyramidFrameData>();
            if (!isValid || settings == null || !depthPyramidData.isValid || depthPyramidData.MipCount < 2)
                return TextureHandle.nullHandle;

            UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();
            // Contact Shadows intentionally use a non-XR Texture2D path. Keep the rest of URP XR untouched.
            if (cameraData.xr.enabled)
                return TextureHandle.nullHandle;

            RenderTextureDescriptor desc = cameraData.cameraTargetDescriptor;
            desc.depthStencilFormat = GraphicsFormat.None;
            desc.msaaSamples = 1;
            desc.graphicsFormat = GraphicsFormat.R32_UInt;
            desc.enableRandomWrite = true;
            desc.dimension = TextureDimension.Tex2D;
            desc.volumeDepth = 1;
            desc.vrUsage = VRTextureUsage.None;

            TextureHandle output = UniversalRenderer.CreateRenderGraphTexture(
                renderGraph, desc, "ContactShadowsBuffer", false, FilterMode.Point);

            Vector2Int actualSize = new Vector2Int(
                Mathf.Max(1, cameraData.cameraTargetDescriptor.width),
                Mathf.Max(1, cameraData.cameraTargetDescriptor.height));
            if (DynamicResolutionHandler.instance.HardwareDynamicResIsEnabled())
                actualSize = DynamicResolutionHandler.instance.ApplyScalesOnSize(actualSize);
            actualSize.x = Mathf.Clamp(actualSize.x, 1, depthPyramidData.Width);
            actualSize.y = Mathf.Clamp(actualSize.y, 1, depthPyramidData.Height);

            float contactShadowRange = Mathf.Clamp(settings.fadeDistance.value, 0.0f, settings.maxDistance.value);
            float contactShadowFadeEnd = settings.maxDistance.value;
            float contactShadowOneOverFadeRange = 1.0f / Math.Max(1e-6f, contactShadowRange);
            float contactShadowMinDist = Mathf.Min(settings.minDistance.value, contactShadowFadeEnd);
            float contactShadowFadeIn = Mathf.Clamp(settings.fadeInDistance.value, 1e-6f, contactShadowFadeEnd);
            int firstMipOffsetY = depthPyramidData.GetMipOffset(1).y;

            using (var builder = renderGraph.AddComputePass<PassData>("Contact Shadows", out var passData, profilingSampler))
            {
                passData.computeShader = m_ComputeShader;
                passData.kernel = m_Kernel;
                passData.depthPyramid = depthPyramidData.DepthPyramid;
                passData.output = output;
                passData.mip0Size = new Vector4(actualSize.x, actualSize.y,
                    1.0f / actualSize.x, 1.0f / actualSize.y);
                passData.params1 = new Vector4(settings.length.value, settings.distanceScaleFactor.value,
                    contactShadowFadeEnd, contactShadowOneOverFadeRange);
                passData.params2 = new Vector4(firstMipOffsetY, contactShadowMinDist, contactShadowFadeIn,
                    settings.rayBias.value * 0.01f);
                passData.params3 = new Vector4(settings.sampleCount, settings.thicknessScale.value * 10.0f, 0.0f, 0.0f);
                bool taaEnabled = cameraData.IsTemporalAAEnabled();
                int taaFrameIndex = taaEnabled
                    ? TemporalAA.CalculateTaaFrameIndex(ref cameraData.taaSettings)
                    : 0;
                passData.frameIndex = Mathf.Max(taaFrameIndex, 0);
                passData.taaEnabled = taaEnabled ? 1 : 0;
                passData.tilesX = (actualSize.x + k_TileSize - 1) / k_TileSize;
                passData.tilesY = (actualSize.y + k_TileSize - 1) / k_TileSize;
                passData.opacity = settings.opacity.value;

                builder.UseTexture(passData.depthPyramid, AccessFlags.Read);
                builder.UseTexture(output, AccessFlags.Write);
                builder.AllowGlobalStateModification(true);
                builder.AllowPassCulling(false);
                builder.SetGlobalTextureAfterPass(output, ContactShadowsShaderIDs.ContactShadowTexture);

                builder.SetRenderFunc(static (PassData data, ComputeGraphContext context) =>
                {
                    var cmd = context.cmd;
                    ComputeShader cs = data.computeShader;
                    int kernel = data.kernel;

                    cmd.SetComputeVectorParam(cs, ContactShadowsShaderIDs.ContactShadowParamsParameters, data.params1);
                    cmd.SetComputeVectorParam(cs, ContactShadowsShaderIDs.ContactShadowParamsParameters2, data.params2);
                    cmd.SetComputeVectorParam(cs, ContactShadowsShaderIDs.ContactShadowParamsParameters3, data.params3);
                    cmd.SetComputeIntParam(cs, ContactShadowsShaderIDs.ContactShadowFrameIndex, data.frameIndex);
                    cmd.SetComputeIntParam(cs, ContactShadowsShaderIDs.ContactShadowTaaEnabled, data.taaEnabled);

                    cmd.SetComputeTextureParam(cs, kernel, DepthPyramidShaderIDs.DepthPyramid, data.depthPyramid);
                    cmd.SetComputeVectorParam(cs, DepthPyramidShaderIDs.DepthPyramidMip0Size, data.mip0Size);
                    cmd.SetComputeTextureParam(cs, kernel, ContactShadowsShaderIDs.ContactShadowTextureUAV, data.output);

                    cmd.DispatchCompute(cs, kernel, data.tilesX, data.tilesY, 1);
                    cmd.SetGlobalInt(ContactShadowsShaderIDs.MainLightContactShadowMask, k_MainDirectionalLightMask);
                    cmd.SetGlobalFloat(ContactShadowsShaderIDs.ContactShadowOpacity, data.opacity);
                });
            }

            return output;
        }
    }
}
