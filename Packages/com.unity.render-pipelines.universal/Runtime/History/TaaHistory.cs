using UnityEngine.Experimental.Rendering;

namespace UnityEngine.Rendering.Universal
{
    /// <summary>
    /// Temporal AA persistent history. Desktop post-processing uses a true two-frame ping-pong
    /// history so the resolve writes directly into the current history texture; no full-screen
    /// history copy is required after TAA.
    /// </summary>
    public sealed class TaaHistory : CameraHistoryItem
    {
        private int m_AccumulationTextureId;
        private int m_AccumulationVersion = -1;
        private RenderTextureDescriptor m_Descriptor;
        private Hash128 m_DescKey;

        public override void OnCreate(BufferedRTHandleSystem owner, uint typeId)
        {
            base.OnCreate(owner, typeId);
            m_AccumulationTextureId = MakeId(0);
        }

        public override void Reset()
        {
            ReleaseHistoryFrameRT(m_AccumulationTextureId);
            m_AccumulationVersion = -1;
            m_Descriptor.width = 0;
            m_Descriptor.height = 0;
            m_Descriptor.graphicsFormat = GraphicsFormat.None;
            m_DescKey = Hash128.Compute(0);
        }

        /// <summary>Texture written by the current TAA resolve.</summary>
        public RTHandle GetAccumulationTexture() => GetCurrentFrameRT(m_AccumulationTextureId);

        /// <summary>Previous frame accumulation sampled by the current TAA resolve.</summary>
        public RTHandle GetPreviousAccumulationTexture() => GetPreviousFrameRT(m_AccumulationTextureId);

        public int GetAccumulationVersion() => m_AccumulationVersion;

        internal void SetAccumulationVersion(int version) => m_AccumulationVersion = version;

        private bool IsValid() => GetAccumulationTexture() != null && GetPreviousAccumulationTexture() != null;

        private bool IsDirty(ref RenderTextureDescriptor desc) => m_DescKey != Hash128.Compute(ref desc);

        private void Alloc(ref RenderTextureDescriptor desc)
        {
            // Two buffered frames are intentional: previous is sampled while current is the TAA output.
            AllocHistoryFrameRT(m_AccumulationTextureId, 2, ref desc, "TaaAccumulationTex");
            m_Descriptor = desc;
            m_DescKey = Hash128.Compute(ref desc);
        }

        internal bool Update(ref RenderTextureDescriptor cameraDesc)
        {
            if (cameraDesc.width <= 0 || cameraDesc.height <= 0 || cameraDesc.graphicsFormat == GraphicsFormat.None)
                return false;

            var taaDesc = TemporalAA.TemporalAADescFromCameraDesc(ref cameraDesc);
            if (IsDirty(ref taaDesc))
                Reset();

            if (IsValid())
                return false;

            Alloc(ref taaDesc);
            return true;
        }
    }
}
