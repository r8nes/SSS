using System;
using System.Collections.Generic;
using UnityEngine.Rendering.RenderGraphModule;

namespace UnityEngine.Rendering.Universal
{
    /// <summary>
    /// Let customizable actions inject commands to capture the camera output.
    ///
    /// You can use this pass to inject capture commands into a command buffer
    /// with the goal of having camera capture happening in external code.
    /// </summary>
    internal class CapturePass : ScriptableRenderPass
    {
#if URP_COMPATIBILITY_MODE
        RTHandle m_CameraColorHandle;
#endif
#if DEVELOPMENT_BUILD || UNITY_EDITOR
        private static readonly HashSet<string> s_LoggedCaptureActions = new();
#endif

        private static void SetupReadbackSafeViewport(CommandBuffer cmd, int width, int height)
        {
            if (cmd == null || width <= 0 || height <= 0)
                return;

            cmd.DisableScissorRect();
            cmd.SetViewport(new Rect(0f, 0f, width, height));
        }

        private static void LogCaptureActionOnce(Action<RenderTargetIdentifier, CommandBuffer> captureAction)
        {
#if DEVELOPMENT_BUILD || UNITY_EDITOR
            if (captureAction == null)
                return;

            var method = captureAction.Method;
            string actionId = $"{method.DeclaringType?.FullName}.{method.Name}";
            if (s_LoggedCaptureActions.Add(actionId))
            {
                Debug.Log($"[URP/CapturePass] Capture action detected: {actionId}");
            }
#endif
        }

        public CapturePass(RenderPassEvent evt)
        {
            base.profilingSampler = new ProfilingSampler("Capture Camera output");
            renderPassEvent = evt;
        }

#if URP_COMPATIBILITY_MODE
        /// <inheritdoc/>
        [Obsolete(DeprecationMessage.CompatibilityScriptingAPIObsoleteFrom2023_3)]
        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            CommandBuffer cmdBuf = renderingData.commandBuffer;

            m_CameraColorHandle = renderingData.cameraData.renderer.GetCameraColorBackBuffer(cmdBuf);

            using (new ProfilingScope(cmdBuf, profilingSampler))
            {
                var colorAttachmentIdentifier = m_CameraColorHandle.nameID;
                var captureActions = renderingData.cameraData.captureActions;

                int targetWidth = renderingData.cameraData.scaledWidth;
                int targetHeight = renderingData.cameraData.scaledHeight;
                if (m_CameraColorHandle != null && m_CameraColorHandle.rt != null)
                {
                    targetWidth = m_CameraColorHandle.rt.width;
                    targetHeight = m_CameraColorHandle.rt.height;
                }

                SetupReadbackSafeViewport(cmdBuf, targetWidth, targetHeight);

                for (captureActions.Reset(); captureActions.MoveNext();)
                {
                    var captureAction = captureActions.Current;
                    LogCaptureActionOnce(captureAction);
                    captureAction(colorAttachmentIdentifier, renderingData.commandBuffer);
                }
            }
        }
#endif

        private class UnsafePassData
        {
            internal TextureHandle source;
            internal int sourceWidth;
            internal int sourceHeight;
            public IEnumerator<Action<RenderTargetIdentifier, CommandBuffer>> captureActions;
        }

        // This function needs to add an unsafe render pass to Render Graph because a raster render pass, which is typically
        // used for rendering with Render Graph, cannot perform the texture readback operations performed with the command
        // buffer in CameraTextureProvider. Unsafe passes can do certain operations that raster render passes cannot do and
        // have access to the full command buffer API.
        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();
            UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();

            using (var builder = renderGraph.AddUnsafePass<UnsafePassData>(passName, out var passData, profilingSampler))
            {
                // Setup up the pass data with cameraColor, which has the correct orientation and position in a built player
                passData.source = resourceData.cameraColor;
                passData.sourceWidth = Mathf.Max(1, cameraData.scaledWidth);
                passData.sourceHeight = Mathf.Max(1, cameraData.scaledHeight);
                passData.captureActions = cameraData.captureActions;

                // Setup up the builder
                builder.AllowPassCulling(false);
                builder.UseTexture(resourceData.cameraColor);
                builder.SetRenderFunc((UnsafePassData data, UnsafeGraphContext unsafeContext) =>
                {
                    var nativeCommandBuffer = CommandBufferHelpers.GetNativeCommandBuffer(unsafeContext.cmd);
                    var captureActions = data.captureActions;
                    SetupReadbackSafeViewport(nativeCommandBuffer, data.sourceWidth, data.sourceHeight);
                    for (data.captureActions.Reset(); data.captureActions.MoveNext();)
                    {
                        var captureAction = captureActions.Current;
                        LogCaptureActionOnce(captureAction);
                        captureAction(data.source, nativeCommandBuffer);
                    }
                });
            }
        }
    }
}
