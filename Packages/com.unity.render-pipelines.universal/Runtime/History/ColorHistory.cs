using System;
using UnityEngine.Experimental.Rendering;

namespace UnityEngine.Rendering.Universal
{
    /// <summary>
    /// Base class for color history.
    /// Color space is linear RGB.
    /// Supports a consumer-requested Gaussian mip chain.
    /// MSAA is not supported and is resolved for the history.
    /// XR is supported.
    /// </summary>
    public abstract class ColorHistory : CameraHistoryItem
    {
        // Currently we are storing up to 2, one for each eye in the case of XR stereo rendering
        private int[] m_Ids = new int[2];

        /// <summary>
        /// The names to use for history items.
        /// </summary>
        protected string[] m_Names = new[]
        {
            "ColorHistory0",
            "ColorHistory1"
        };
        private RenderTextureDescriptor m_Descriptor;
        private Hash128 m_DescKey;
        // Store one jitter offset per eye per history RT. Currently the jitter offset
        // is the same for both eyes, but first of all this could change, but mostly
        // it's because eye jitters could be updated at different times.
        private Vector2[] m_JitterOffsets = new Vector2[2*2];
        private int m_LastWrittenMipCount = 1;
        private Vector2Int m_LastWrittenViewportSize;
        private Vector2Int m_LastWrittenRenderTargetSize;

        /// <inheritdoc />
        public override void OnCreate(BufferedRTHandleSystem owner, uint typeId)
        {
            base.OnCreate(owner, typeId);
            m_Ids[0] = MakeId(0);
            m_Ids[1] = MakeId(1);
            Array.Fill(m_JitterOffsets, Vector2.zero);
        }

        /// <summary>
        /// Get the current history texture.
        /// Current history might not be valid yet. It is valid only after executing the producing render pass.
        /// </summary>
        /// <param name="eyeIndex">Eye index, typically XRPass.multipassId.</param>
        /// <returns>The texture.</returns>
        public RTHandle GetCurrentTexture(int eyeIndex = 0)
        {
            if ((uint)eyeIndex >= m_Ids.Length)
                return null;

            return GetCurrentFrameRT(m_Ids[eyeIndex]);
        }

        /// <summary>
        /// Get the previous history texture.
        /// Previous history might not be valid yet. It is valid only after executing the producing render pass.
        /// </summary>
        /// <param name="eyeIndex">Eye index, typically XRPass.multipassId.</param>
        /// <returns>The texture.</returns>
        public RTHandle GetPreviousTexture(int eyeIndex = 0)
        {
            if ((uint)eyeIndex >= m_Ids.Length)
                return null;

            return GetPreviousFrameRT(m_Ids[eyeIndex]);
        }

        /// <summary>
        /// Get the current history jitter value.
        /// Current history might not be valid yet. It is valid only after executing the producing render pass.
        /// </summary>
        /// <param name="eyeIndex">Eye index, typically XRPass.multipassId.</param>
        /// <returns>The jitter value in NDC space [-1,1].</returns>
        public Vector2 GetCurrentJitter(int eyeIndex = 0)
        {
            if ((uint)eyeIndex >= m_Ids.Length)
                return Vector2.zero;

            int stableIndex = GetCurrentFrameRTStableIndex(m_Ids[eyeIndex]);
            if (stableIndex < 0)
                return Vector2.zero;

            return m_JitterOffsets[stableIndex + 2 * eyeIndex];
        }

        /// <summary>
        /// Get the previous history jitter value.
        /// </summary>
        /// <param name="eyeIndex">Eye index, typically XRPass.multipassId.</param>
        /// <returns>The jitter value in NDC space [-1,1].</returns>
        public Vector2 GetPreviousJitter(int eyeIndex = 0)
        {
            if ((uint)eyeIndex >= m_Ids.Length)
                return Vector2.zero;

            int stableIndex = GetPreviousFrameRTStableIndex(m_Ids[eyeIndex]);
            if (stableIndex < 0)
                return Vector2.zero;

            return m_JitterOffsets[stableIndex + 2 * eyeIndex];
        }

        private bool IsAllocated()
        {
            return GetCurrentTexture() != null;
        }

        // True if the desc changed, graphicsFormat etc.
        private bool IsDirty(ref RenderTextureDescriptor desc)
        {
            return m_DescKey != Hash128.Compute(ref desc);
        }

        private void Alloc(ref RenderTextureDescriptor desc, bool xrMultipassEnabled)
        {
            // In generic case, the current texture might not have been written yet. We need double buffering.
            AllocHistoryFrameRT(m_Ids[0], 2, ref desc, m_Names[0]);

            if(xrMultipassEnabled)
                AllocHistoryFrameRT(m_Ids[1], 2, ref desc, m_Names[1]);

            m_Descriptor = desc;
            m_DescKey = Hash128.Compute(ref desc);
        }

        /// <summary>
        /// Release the history texture(s).
        /// </summary>
        public override void Reset()
        {
            for(int i = 0; i < m_Ids.Length; i++)
                ReleaseHistoryFrameRT(m_Ids[i]);

            m_LastWrittenMipCount = 1;
            m_LastWrittenViewportSize = Vector2Int.zero;
            m_LastWrittenRenderTargetSize = Vector2Int.zero;
        }

        internal int previousMipCount => m_LastWrittenMipCount;
        internal Vector2Int previousViewportSize => m_LastWrittenViewportSize;
        internal Vector2Int previousRenderTargetSize => m_LastWrittenRenderTargetSize;

        internal void SetCurrentFrameVersion(RTHandle currentTexture, Vector2Int viewportSize, int mipCount)
        {
            m_LastWrittenMipCount = Mathf.Max(1, mipCount);
            m_LastWrittenViewportSize = new Vector2Int(Mathf.Max(1, viewportSize.x), Mathf.Max(1, viewportSize.y));
            m_LastWrittenRenderTargetSize = currentTexture?.rt != null
                ? new Vector2Int(Mathf.Max(1, currentTexture.rt.width), Mathf.Max(1, currentTexture.rt.height))
                : m_LastWrittenViewportSize;
        }

        internal RenderTextureDescriptor GetHistoryDescriptor(ref RenderTextureDescriptor cameraDesc, int requestedMipCount = 1)
        {
            var colorDesc = cameraDesc;
            int mipCount = Mathf.Max(1, requestedMipCount);
            colorDesc.depthStencilFormat = GraphicsFormat.None;
            colorDesc.mipCount = mipCount;
            colorDesc.useMipMap = mipCount > 1;
            colorDesc.autoGenerateMips = false;
            colorDesc.enableRandomWrite = mipCount > 1;
            colorDesc.msaaSamples = 1;
            if (mipCount > 1 && !SystemInfo.IsFormatSupported(colorDesc.graphicsFormat, GraphicsFormatUsage.LoadStore))
                colorDesc.graphicsFormat = GraphicsFormat.R16G16B16A16_SFloat;

            return colorDesc;
        }

        // Return true if the RTHandles were reallocated.
        internal bool Update(UniversalCameraData cameraData, bool xrMultipassEnabled = false, in RenderTextureDescriptor? cameraDescOverride = null, int requestedMipCount = 1)
        {
#if ENABLE_VR && ENABLE_XR_MODULE
            int eyeIndex = (cameraData.xr.enabled && !cameraData.xr.singlePassEnabled) ? cameraData.xr.multipassId : 0;
#else
            const int eyeIndex = 0;
#endif
            Debug.Assert(eyeIndex < m_JitterOffsets.Length);
            int stableIndex = GetCurrentFrameRTStableIndex(m_Ids[eyeIndex]);
            if (stableIndex >= 0)
            {
                // Update the current jitter value for the current eye. Note that we could handle the case where
                // this is the first time or if we reallocate the buffers except there's no clear definition of
                // what we should use for a sensible previous jitter value in that case. So might just leave it as is.
                m_JitterOffsets[stableIndex + 2 * eyeIndex] = cameraData.jitter;
            }

            RenderTextureDescriptor cameraDesc = cameraDescOverride ?? cameraData.cameraTargetDescriptor;

            if (cameraDesc.width > 0 && cameraDesc.height > 0 && cameraDesc.graphicsFormat != GraphicsFormat.None)
            {
                var historyDesc = GetHistoryDescriptor(ref cameraDesc, requestedMipCount);

                Camera camera = cameraData.camera;
                bool isPreview = camera.cameraType == CameraType.Preview;
                bool isRenderRequest = camera.isProcessingRenderRequest;
                if (!isPreview && !isRenderRequest && IsDirty(ref historyDesc))
                    Reset();

                if (!IsAllocated())
                {
                    Alloc(ref historyDesc, xrMultipassEnabled);
                    return true;
                }
            }

            return false;
        }
    }
}
