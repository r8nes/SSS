using UnityEngine.Experimental.Rendering;

namespace UnityEngine.Rendering.Universal
{
    /// <summary>
    /// Raw camera normal history. The normal snapshot is taken from the camera normals texture before post-processing.
    /// The format matches the current frame normal texture so deferred oct-encoded normals and forward normal prepass
    /// normals keep the same representation across frames.
    /// </summary>
    public sealed class RawNormalHistory : CameraHistoryItem
    {
        private int[] m_Ids = new int[2];
        private static readonly string[] m_Names = new[]
        {
            "RawNormalHistory0",
            "RawNormalHistory1"
        };
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
        /// Get the current normal history texture.
        /// </summary>
        public RTHandle GetCurrentTexture(int eyeIndex = 0)
        {
            if ((uint)eyeIndex >= m_Ids.Length)
                return null;

            return GetCurrentFrameRT(m_Ids[eyeIndex]);
        }

        /// <summary>
        /// Get the previous normal history texture.
        /// </summary>
        public RTHandle GetPreviousTexture(int eyeIndex = 0)
        {
            if ((uint)eyeIndex >= m_Ids.Length)
                return null;

            return GetPreviousFrameRT(m_Ids[eyeIndex]);
        }

        private bool IsAllocated() => GetCurrentTexture() != null;

        private bool IsDirty(ref RenderTextureDescriptor desc) => m_DescKey != Hash128.Compute(ref desc);

        private void Alloc(ref RenderTextureDescriptor desc, bool xrMultipassEnabled)
        {
            AllocHistoryFrameRT(m_Ids[0], 2, ref desc, m_Names[0]);

            if (xrMultipassEnabled)
                AllocHistoryFrameRT(m_Ids[1], 2, ref desc, m_Names[1]);

            m_DescKey = Hash128.Compute(ref desc);
        }

        /// <inheritdoc />
        public override void Reset()
        {
            for (int i = 0; i < m_Ids.Length; i++)
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

        internal RenderTextureDescriptor GetHistoryDescriptor(ref RenderTextureDescriptor cameraDesc, GraphicsFormat normalFormat)
        {
            var normalDesc = cameraDesc;
            normalDesc.graphicsFormat = normalFormat;
            normalDesc.depthStencilFormat = GraphicsFormat.None;
            normalDesc.depthBufferBits = 0;
            normalDesc.mipCount = 0;
            normalDesc.msaaSamples = 1;
            normalDesc.bindMS = false;
            return normalDesc;
        }

        internal bool Update(ref RenderTextureDescriptor cameraDesc, GraphicsFormat normalFormat, bool xrMultipassEnabled = false)
        {
            if (cameraDesc.width <= 0 || cameraDesc.height <= 0 || normalFormat == GraphicsFormat.None)
                return false;

            var historyDesc = GetHistoryDescriptor(ref cameraDesc, normalFormat);

            if (IsDirty(ref historyDesc))
                Reset();

            if (!IsAllocated())
            {
                Alloc(ref historyDesc, xrMultipassEnabled);
                return true;
            }

            return false;
        }
    }
}
