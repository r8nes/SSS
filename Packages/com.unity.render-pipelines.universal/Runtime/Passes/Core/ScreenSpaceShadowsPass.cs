using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering.RenderGraphModule;

namespace UnityEngine.Rendering.Universal.Internal
{
    /// <summary>
    /// Resolves the main directional shadow map into a screen-space texture.
    /// The pass is owned and scheduled by UniversalRenderer.
    /// </summary>
    internal sealed class ScreenSpaceShadowsPass : ScriptableRenderPass
    {
        private static readonly int s_ScreenSpaceShadowmapTextureID = Shader.PropertyToID("_ScreenSpaceShadowmapTexture");
        private static readonly int s_CameraDepthTextureID = Shader.PropertyToID("_CameraDepthTexture");
        private static readonly int s_CameraNormalsTextureID = Shader.PropertyToID("_CameraNormalsTexture");
        private static readonly int s_MainLightShadowmapTextureID = Shader.PropertyToID("_MainLightShadowmapTexture");
        private static readonly int s_MainLightShadowmapTextureRawID = Shader.PropertyToID("_MainLightShadowmapTextureRaw");
        private static readonly int s_ReceiverDepthTextureID = Shader.PropertyToID("_ScreenSpaceShadowReceiverDepthTexture");
        private static readonly int s_ReceiverNormalsTextureID = Shader.PropertyToID("_ScreenSpaceShadowReceiverNormalsTexture");
        private static readonly int s_ReceiverEnabledID = Shader.PropertyToID("_ScreenSpaceShadowReceiverEnabled");
        private static readonly int s_ReceiverNormalEncodingID = Shader.PropertyToID("_ScreenSpaceShadowReceiverNormalEncoding");

        private readonly Material m_Material;

        internal bool isValid => m_Material != null;

        internal ScreenSpaceShadowsPass(Shader shader)
        {
            profilingSampler = new ProfilingSampler("Screen Space Shadows");
            renderPassEvent = RenderPassEvent.BeforeRenderingDeferredLights;
            m_Material = shader != null ? CoreUtils.CreateEngineMaterial(shader) : null;
        }

        internal void Dispose()
        {
            CoreUtils.Destroy(m_Material);
        }

        private sealed class PassData
        {
            internal Material material;
            internal TextureHandle output;
            internal TextureHandle depthTexture;
            internal TextureHandle normalsTexture;
            internal TextureHandle mainShadowTexture;
            internal TextureHandle receiverDepthTexture;
            internal TextureHandle receiverNormalsTexture;
            internal ScreenSpaceShadowReceiverNormalEncoding receiverNormalEncoding;
            internal bool hasReceiver;
        }

        internal TextureHandle Render(
            RenderGraph renderGraph,
            ContextContainer frameData,
            TextureHandle depthTexture,
            TextureHandle normalsTexture,
            TextureHandle mainShadowTexture)
        {
            if (m_Material == null || !depthTexture.IsValid() || !normalsTexture.IsValid() || !mainShadowTexture.IsValid())
                return TextureHandle.nullHandle;

            UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();
            ScreenSpaceShadowReceiverData receiver = frameData.GetOrCreate<ScreenSpaceShadowReceiverData>();
            bool hasReceiver = receiver.isValid && receiver.depth.IsValid() && receiver.normals.IsValid();

            RenderTextureDescriptor desc = cameraData.cameraTargetDescriptor;
            desc.depthStencilFormat = GraphicsFormat.None;
            desc.msaaSamples = 1;
            desc.graphicsFormat = hasReceiver
                ? GraphicsFormat.R8G8_UNorm
                : GraphicsFormat.R8_UNorm;

            TextureHandle output = UniversalRenderer.CreateRenderGraphTexture(
                renderGraph,
                desc,
                "_ScreenSpaceShadowmapTexture",
                false,
                FilterMode.Point);

            // HDRP's raster directional screen-space shadow resolve is also an explicit fullscreen
            // pass between opaque prepass/GBuffer production and deferred lighting. Keep this pass
            // unmergeable so it cannot be folded into the GBuffer or deferred native render pass.
            using (var builder = renderGraph.AddUnsafePass<PassData>(passName, out var passData, profilingSampler))
            {
                passData.material = m_Material;
                passData.output = output;
                passData.depthTexture = depthTexture;
                passData.normalsTexture = normalsTexture;
                passData.mainShadowTexture = mainShadowTexture;
                passData.receiverDepthTexture = hasReceiver ? receiver.depth : TextureHandle.nullHandle;
                passData.receiverNormalsTexture = hasReceiver ? receiver.normals : TextureHandle.nullHandle;
                passData.receiverNormalEncoding = receiver.normalEncoding;
                passData.hasReceiver = hasReceiver;

                builder.UseTexture(output, AccessFlags.WriteAll);
                builder.UseTexture(depthTexture, AccessFlags.Read);
                builder.UseTexture(normalsTexture, AccessFlags.Read);
                builder.UseTexture(mainShadowTexture, AccessFlags.Read);
                if (hasReceiver)
                {
                    builder.UseTexture(receiver.depth, AccessFlags.Read);
                    builder.UseTexture(receiver.normals, AccessFlags.Read);
                }
                builder.AllowGlobalStateModification(true);
                builder.AllowPassCulling(false);
                builder.SetGlobalTextureAfterPass(output, s_ScreenSpaceShadowmapTextureID);

                builder.SetRenderFunc(static (PassData data, UnsafeGraphContext context) =>
                {
                    UnsafeCommandBuffer cmd = context.cmd;

                    // Keep all TextureHandle operations on the RenderGraph command wrapper. This lets
                    // RenderGraph resolve the physical resources and their lifetime/barriers correctly.
                    cmd.SetGlobalTexture(s_CameraDepthTextureID, data.depthTexture);
                    cmd.SetGlobalTexture(s_CameraNormalsTextureID, data.normalsTexture);
                    cmd.SetGlobalTexture(s_MainLightShadowmapTextureID, data.mainShadowTexture);
                    cmd.SetGlobalTexture(s_MainLightShadowmapTextureRawID, data.mainShadowTexture);
                    cmd.SetGlobalInt(s_ReceiverEnabledID, data.hasReceiver ? 1 : 0);
                    cmd.SetGlobalInt(s_ReceiverNormalEncodingID, (int)data.receiverNormalEncoding);
                    if (data.hasReceiver)
                    {
                        cmd.SetGlobalTexture(s_ReceiverDepthTextureID, data.receiverDepthTexture);
                        cmd.SetGlobalTexture(s_ReceiverNormalsTextureID, data.receiverNormalsTexture);
                    }
                    cmd.SetRenderTarget(data.output);

                    // The source is not sampled through _BlitTexture; Blitter only supplies the XR-aware draw.
                    Blitter.BlitTexture(cmd, data.depthTexture, Vector2.one, data.material, 0);
                    SetScreenSpaceKeywords(cmd);
                });
            }

            return output;
        }

        private static void SetScreenSpaceKeywords(IBaseCommandBuffer cmd)
        {
            cmd.SetKeyword(ShaderGlobalKeywords.MainLightShadows, false);
            cmd.SetKeyword(ShaderGlobalKeywords.MainLightShadowCascades, false);
            cmd.SetKeyword(ShaderGlobalKeywords.MainLightShadowScreen, true);
        }
    }

    /// <summary>
    /// Restores atlas-based main-light shadow keywords before transparent rendering.
    /// </summary>
    internal sealed class ScreenSpaceShadowsKeywordResetPass : ScriptableRenderPass
    {
        internal ScreenSpaceShadowsKeywordResetPass()
        {
            profilingSampler = new ProfilingSampler("Restore Main Light Shadow Keywords");
            renderPassEvent = RenderPassEvent.BeforeRenderingTransparents;
        }

        private sealed class PassData
        {
            internal UniversalShadowData shadowData;
        }

        internal void Render(RenderGraph renderGraph, ContextContainer frameData)
        {
            using (var builder = renderGraph.AddUnsafePass<PassData>(passName, out var passData, profilingSampler))
            {
                passData.shadowData = frameData.Get<UniversalShadowData>();
                builder.AllowGlobalStateModification(true);
                builder.AllowPassCulling(false);
                builder.SetRenderFunc(static (PassData data, UnsafeGraphContext context) =>
                {
                    RestoreKeywords(context.cmd, data.shadowData);
                });
            }
        }

        private static void RestoreKeywords(IBaseCommandBuffer cmd, UniversalShadowData shadowData)
        {
            int cascadesCount = shadowData.mainLightShadowCascadesCount;
            bool mainLightShadows = shadowData.supportsMainLightShadows;
            bool receiveShadowsNoCascade = mainLightShadows && cascadesCount == 1;
            bool receiveShadowsCascades = mainLightShadows && cascadesCount > 1;

            cmd.SetKeyword(ShaderGlobalKeywords.MainLightShadowScreen, false);
            cmd.SetKeyword(ShaderGlobalKeywords.MainLightShadows, receiveShadowsNoCascade);
            cmd.SetKeyword(ShaderGlobalKeywords.MainLightShadowCascades, receiveShadowsCascades);
        }
    }
}
