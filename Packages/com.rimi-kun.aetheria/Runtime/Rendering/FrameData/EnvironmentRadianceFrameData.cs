using UnityEngine.Rendering.RenderGraphModule;

namespace Aetheria.Runtime.Rendering
{
    internal struct EnvironmentRadianceFrameData
    {
        public bool IsReady;
        public TextureHandle LightingCubemap;
        public TextureHandle PendingTargetCubemap;
        public BufferHandle PendingTargetAmbientProbe;
        public BufferHandle PreviousAmbientProbe;
        public BufferHandle TargetAmbientProbe;
        public float AmbientProbeBlendFactor;

        public void Reset()
        {
            IsReady = false;
            LightingCubemap = TextureHandle.nullHandle;
            PendingTargetCubemap = TextureHandle.nullHandle;
            PendingTargetAmbientProbe = BufferHandle.nullHandle;
            PreviousAmbientProbe = BufferHandle.nullHandle;
            TargetAmbientProbe = BufferHandle.nullHandle;
            AmbientProbeBlendFactor = 1.0f;
        }
    }
}
