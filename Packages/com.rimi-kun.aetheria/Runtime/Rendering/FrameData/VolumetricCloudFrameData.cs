using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;

namespace Aetheria.Runtime.Rendering
{
    internal sealed class VolumetricCloudFrameData : ContextItem
    {
        public bool IsActive;
        public TextureHandle ResolvedLighting = TextureHandle.nullHandle;
        public TextureHandle ResolvedData = TextureHandle.nullHandle;
        public TextureHandle DepthPyramid = TextureHandle.nullHandle;
        public Vector4 ResolvedSize;
        public bool IsFullResolution;
        public float NearPlane;
        public float ImprovedTransmittanceBlend;

        public override void Reset()
        {
            IsActive = false;
            ResolvedLighting = TextureHandle.nullHandle;
            ResolvedData = TextureHandle.nullHandle;
            DepthPyramid = TextureHandle.nullHandle;
            ResolvedSize = Vector4.zero;
            IsFullResolution = false;
            NearPlane = 0.0f;
            ImprovedTransmittanceBlend = 0.0f;
        }
    }
}
