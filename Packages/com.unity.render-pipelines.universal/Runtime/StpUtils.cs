using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering.RenderGraphModule;

namespace UnityEngine.Rendering.Universal
{
    internal static class StpUtils
    {
        private const int StpResponsiveStencilMask = 1 << 7;

        static void CalculateJitter(int frameIndex, out Vector2 jitter, out bool allowScaling)
        {
            // NOTE: STP's jitter must be negated in URP due to y-flip
            jitter = -STP.Jit16(frameIndex);
            allowScaling = false;
        }

        // Static allocation of JitterFunc delegate to avoid GC
        internal static TemporalAA.JitterFunc s_JitterFunc = CalculateJitter;

        static void PopulateStpConfig(UniversalCameraData cameraData, TextureHandle inputColor, TextureHandle inputDepth, TextureHandle inputMotion, TextureHandle inputStencil, int stencilMask, int debugViewIndex, TextureHandle debugView, TextureHandle destination, Texture2D noiseTexture, out STP.Config config)
        {
            cameraData.camera.TryGetComponent<UniversalAdditionalCameraData>(out var additionalCameraData);
            Debug.Assert(additionalCameraData != null);

            var motionData = additionalCameraData.motionVectorsPersistentData;
            Debug.Assert(motionData != null);

            // Dynamic scaling isn't supported
            config.enableHwDrs = false;

            // Desktop URP post-processing is single-view and uses ordinary 2D textures.
            config.enableTexArray = false;

            // The motion scaling feature is only active outside of test environments.
            // If we allowed it to run during automated graphics tests, the results of each test run would be dependent on system performance.
#if LWRP_DEBUG_STATIC_POSTFX
            config.enableMotionScaling = false;
#else
            config.enableMotionScaling = true;
#endif

            config.noiseTexture = noiseTexture;

            config.inputColor = inputColor;
            config.inputDepth = inputDepth;
            config.inputMotion = inputMotion;

            config.inputStencil = inputStencil;
            config.stencilMask = stencilMask;

            config.debugView = debugView;

            config.destination = destination;

            var stpHistory = cameraData.stpHistory;
            Debug.Assert(stpHistory != null);

            config.historyContext = stpHistory.GetHistoryContext(0);

            config.nearPlane = cameraData.camera.nearClipPlane;
            config.farPlane = cameraData.camera.farClipPlane;
            config.frameIndex = TemporalAA.CalculateTaaFrameIndex(ref cameraData.taaSettings);
            config.hasValidHistory = !cameraData.resetHistory;
            config.debugViewIndex = debugViewIndex;

            config.deltaTime = motionData.deltaTime;
            config.lastDeltaTime = motionData.lastDeltaTime;

            // Note: The current and prior image sizes are always identical since DRS is not currently supported.
            config.currentImageSize = new Vector2Int(cameraData.cameraTargetDescriptor.width, cameraData.cameraTargetDescriptor.height);
            config.priorImageSize = config.currentImageSize;
            config.outputImageSize = new Vector2Int(cameraData.pixelWidth, cameraData.pixelHeight);

            Debug.Assert(STP.perViewConfigs.Length > 0);

            STP.PerViewConfig perViewConfig;

            // STP requires non-jittered versions of the current, previous, and "previous previous" matrices.
            perViewConfig.currentProj = motionData.projection;
            perViewConfig.lastProj = motionData.previousProjection;
            perViewConfig.lastLastProj = motionData.previousPreviousProjection;

            perViewConfig.currentView = motionData.view;
            perViewConfig.lastView = motionData.previousView;
            perViewConfig.lastLastView = motionData.previousPreviousView;

            // STP assumes the view matrices contain the world-space camera position.
            Vector3 currentPosition = motionData.worldSpaceCameraPos;
            Vector3 lastPosition = motionData.previousWorldSpaceCameraPos;
            Vector3 lastLastPosition = motionData.previousPreviousWorldSpaceCameraPos;

            perViewConfig.currentView.SetColumn(3, new Vector4(-currentPosition.x, -currentPosition.y, -currentPosition.z, 1.0f));
            perViewConfig.lastView.SetColumn(3, new Vector4(-lastPosition.x, -lastPosition.y, -lastPosition.z, 1.0f));
            perViewConfig.lastLastView.SetColumn(3, new Vector4(-lastLastPosition.x, -lastLastPosition.y, -lastLastPosition.z, 1.0f));

            STP.perViewConfigs[0] = perViewConfig;
            config.numActiveViews = 1;
            config.perViewConfigs = STP.perViewConfigs;
        }

        static internal void Execute(RenderGraph renderGraph, UniversalResourceData resourceData, UniversalCameraData cameraData, TextureHandle inputColor, TextureHandle inputDepth, TextureHandle inputMotion, TextureHandle destination, Texture2D noiseTexture)
        {
            var debugView = TextureHandle.nullHandle;
            int debugViewIndex = 0;

            DebugHandler debugHandler = ScriptableRenderPass.GetActiveDebugHandler(cameraData);
            if ((debugHandler != null) && debugHandler.TryGetFullscreenDebugMode(out var fullscreenDebugMode))
            {
                if (fullscreenDebugMode == DebugFullScreenMode.STP)
                {
                    debugView = renderGraph.CreateTexture(new TextureDesc(cameraData.pixelWidth, cameraData.pixelHeight, false, false)
                    {
                        name = "STP Debug View",
                        format = GraphicsFormat.R8G8B8A8_UNorm,
                        clearBuffer = true,
                        enableRandomWrite = true
                    });

                    debugViewIndex = debugHandler.stpDebugViewIndex;

                    // Save the debug view texture so it can be displayed later
                    resourceData.stpDebugView = debugView;
                }
            }

            TextureHandle inputStencil = TextureHandle.nullHandle;
            int stencilMask = 0;
            TextureHandle activeDepth = resourceData.activeDepthTexture;
            if (activeDepth.IsValid())
            {
                TextureDesc depthDesc = renderGraph.GetTextureDesc(activeDepth);
                if (GraphicsFormatUtility.IsStencilFormat(depthDesc.format))
                {
                    inputStencil = activeDepth;
                    stencilMask = StpResponsiveStencilMask;
                }
            }

            PopulateStpConfig(cameraData, inputColor, inputDepth, inputMotion, inputStencil, stencilMask, debugViewIndex, debugView, destination, noiseTexture, out var config);
            STP.Execute(renderGraph, ref config);
        }
    }
}
