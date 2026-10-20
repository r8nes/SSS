using System;
using UnityEngine.Experimental.Rendering;

namespace UnityEngine.Rendering.Universal
{
    /// <summary>
    /// Raw render depth history. The depth snapshot is taken before post processing.
    /// A matching color is RawColorHistory.
    /// Format is the camera depth format or R32Float on platforms with limitations.
    /// If TemporalAA is enabled the depth is jittered.
    /// No mips. No depth pyramid.
    /// MSAA is not supported and is resolved for the history.
    /// XR is supported.
    /// </summary>
    public sealed class RawDepthHistory : CameraHistoryItem
    {
        private int[] m_Ids = new int[2];
        private static readonly string[] m_Names = new[]
        {
            "RawDepthHistory0",
            "RawDepthHistory1"
        };
        private RenderTextureDescriptor m_Descriptor;
        private Hash128 m_DescKey;
        private int m_LastWrittenFrame = -1;
        private Vector2Int m_LastWrittenViewportSize;
        private Vector2Int m_LastWrittenRenderTargetSize;

        /// <inheritdoc />
        public override void OnCreate(BufferedRTHandleSystem owner, uint typeId)
        {
            base.OnCreate(owner, typeId);
            m_Ids[0] = MakeId(0);
            m_Ids[1] = MakeId(1);
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
        /// </summary>
        /// <param name="eyeIndex">Eye index, typically XRPass.multipassId.</param>
        /// <returns>The texture.</returns>
        public RTHandle GetPreviousTexture(int eyeIndex = 0)
        {
            if ((uint)eyeIndex >= m_Ids.Length)
                return null;

            return GetPreviousFrameRT(m_Ids[eyeIndex]);
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
            // Generic type, we need double buffering.
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

            m_LastWrittenFrame = -1;
            frameIndex = 0;
            m_LastWrittenViewportSize = Vector2Int.zero;
            m_LastWrittenRenderTargetSize = Vector2Int.zero;
        }

        internal uint frameIndex { get; private set; }

        internal Vector2Int previousViewportSize => m_LastWrittenViewportSize;

        internal Vector2Int previousRenderTargetSize => m_LastWrittenRenderTargetSize;

        internal bool IsPreviousValid(uint frame) => frame > 0 &&
                                                     m_LastWrittenFrame == (int)(frame - 1);

        internal void SetCurrentFrameVersion(uint frame, RTHandle currentTexture, ref RenderTextureDescriptor viewportDesc)
        {
            m_LastWrittenFrame = (int)frame;
            frameIndex = frame + 1;
            m_LastWrittenViewportSize = new Vector2Int(Mathf.Max(1, viewportDesc.width), Mathf.Max(1, viewportDesc.height));
            m_LastWrittenRenderTargetSize = ResolveRenderTargetSize(currentTexture, m_LastWrittenViewportSize);
        }

        private static Vector2Int ResolveRenderTargetSize(RTHandle texture, Vector2Int fallback)
        {
            if (texture?.rt == null)
                return fallback;

            return new Vector2Int(Mathf.Max(1, texture.rt.width), Mathf.Max(1, texture.rt.height));
        }

        internal RenderTextureDescriptor GetHistoryDescriptor(ref RenderTextureDescriptor cameraDesc)
        {
            var depthDesc = cameraDesc;
            depthDesc.mipCount = 0;
            depthDesc.msaaSamples = 1;  // History copy should not have MSAA.

            return depthDesc;
        }

        // Return true if the RTHandles were reallocated.
        internal bool Update(ref RenderTextureDescriptor cameraDesc, bool xrMultipassEnabled)
        {
            if (cameraDesc.width > 0 && cameraDesc.height > 0 && (cameraDesc.depthStencilFormat != GraphicsFormat.None || cameraDesc.graphicsFormat != GraphicsFormat.None) )
            {
                var historyDesc = GetHistoryDescriptor(ref cameraDesc);

                if (IsDirty(ref historyDesc))
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
