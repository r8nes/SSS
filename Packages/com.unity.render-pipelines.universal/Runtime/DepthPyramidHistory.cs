using UnityEngine.Experimental.Rendering;

namespace UnityEngine.Rendering.Universal
{
    public readonly struct DepthPyramidHistoryData
    {
        public readonly RenderTexture FarthestTexture;
        public readonly GraphicsBuffer MipLevelOffsetsBuffer;
        public readonly int MipCount;
        public readonly int Width;
        public readonly int Height;
        public readonly Vector4 Mip0Size;

        public bool isValid =>
            FarthestTexture != null &&
            MipLevelOffsetsBuffer != null &&
            MipCount > 0 &&
            Width > 0 &&
            Height > 0;

        internal DepthPyramidHistoryData(
            RenderTexture farthestTexture,
            GraphicsBuffer mipLevelOffsetsBuffer,
            int mipCount,
            int width,
            int height)
        {
            FarthestTexture = farthestTexture;
            MipLevelOffsetsBuffer = mipLevelOffsetsBuffer;
            MipCount = mipCount;
            Width = width;
            Height = height;
            Mip0Size = new Vector4(width, height, 1f / width, 1f / height);
        }
    }

    internal sealed class DepthPyramidHistoryItem : CameraHistoryItem
    {
        private int m_Id;
        private Hash128 m_DescriptorKey;
        private RTHandle m_LastProducedTexture;
        private GraphicsBuffer m_MipLevelOffsetsBuffer;
        private int m_MipCount;
        private int m_Width;
        private int m_Height;

        public override void OnCreate(BufferedRTHandleSystem owner, uint typeId)
        {
            base.OnCreate(owner, typeId);
            m_Id = MakeId(0);
        }

        public RTHandle GetCurrentTexture() => GetCurrentFrameRT(m_Id);

        public void Update(int width, int height)
        {
            var descriptor = new RenderTextureDescriptor(width, height)
            {
                graphicsFormat = GraphicsFormat.R32_SFloat,
                depthStencilFormat = GraphicsFormat.None,
                msaaSamples = 1,
                volumeDepth = 1,
                dimension = TextureDimension.Tex2D,
                useMipMap = false,
                autoGenerateMips = false,
                enableRandomWrite = true,
                useDynamicScale = false,
                sRGB = false
            };

            Hash128 descriptorKey = Hash128.Compute(ref descriptor);
            if (m_DescriptorKey != descriptorKey)
            {
                Reset();
                m_DescriptorKey = descriptorKey;
            }

            if (GetCurrentTexture() == null)
                AllocHistoryFrameRT(m_Id, 2, ref descriptor, "DepthPyramidFarthestHistory");
        }

        public void MarkProduced(GraphicsBuffer mipLevelOffsetsBuffer, int mipCount, int width, int height)
        {
            m_LastProducedTexture = GetCurrentTexture();
            m_MipLevelOffsetsBuffer = mipLevelOffsetsBuffer;
            m_MipCount = mipCount;
            m_Width = width;
            m_Height = height;
        }

        public bool TryGetLatest(out DepthPyramidHistoryData data)
        {
            if (m_LastProducedTexture?.rt != null &&
                m_MipLevelOffsetsBuffer != null &&
                m_MipCount > 0 &&
                m_Width > 0 &&
                m_Height > 0)
            {
                data = new DepthPyramidHistoryData(
                    m_LastProducedTexture.rt,
                    m_MipLevelOffsetsBuffer,
                    m_MipCount,
                    m_Width,
                    m_Height);
                return true;
            }

            data = default;
            return false;
        }

        public override void Reset()
        {
            ReleaseHistoryFrameRT(m_Id);
            m_LastProducedTexture = null;
            m_MipLevelOffsetsBuffer = null;
            m_MipCount = 0;
            m_Width = 0;
            m_Height = 0;
        }
    }

    public static class DepthPyramidHistory
    {
        public static bool TryGet(Camera camera, out DepthPyramidHistoryData data)
        {
            UniversalCameraHistory history = GetHistory(camera);
            if (history != null)
            {
                history.RequestAccess<DepthPyramidHistoryItem>();
                DepthPyramidHistoryItem item = history.GetHistoryForRead<DepthPyramidHistoryItem>();
                if (item != null && item.TryGetLatest(out data))
                    return true;
            }

            data = default;
            return false;
        }

        private static UniversalCameraHistory GetHistory(Camera camera)
        {
            if (camera == null)
                return null;

            if (camera.TryGetComponent(out UniversalAdditionalCameraData additionalCameraData))
                return additionalCameraData.historyManager;

#if UNITY_EDITOR
            if (camera.cameraType == CameraType.SceneView)
                return UniversalSceneViewCameraHistory.GetOrCreate(camera);
#endif

            return null;
        }
    }
}
