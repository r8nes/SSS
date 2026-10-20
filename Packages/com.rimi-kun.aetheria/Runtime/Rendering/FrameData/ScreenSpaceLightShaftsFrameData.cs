using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;

namespace Aetheria.Runtime.Rendering
{
    internal sealed class ScreenSpaceLightShaftsFrameData : ContextItem
    {
        internal bool IsActive;
        internal TextureHandle Mask;
        internal Vector3 LightDirectionWS;
        internal Color LightColor;
        internal Vector4 Tint;
        internal float Intensity;
        internal float Anisotropy;
        internal float OcclusionMaskDarkness;
        internal float OcclusionDepthRange;
        internal float SunViewportPadding;

        public override void Reset()
        {
            IsActive = false;
            Mask = TextureHandle.nullHandle;
            LightDirectionWS = default;
            LightColor = Color.black;
            Tint = Vector4.one;
            Intensity = 0f;
            Anisotropy = 0f;
            OcclusionMaskDarkness = 0f;
            OcclusionDepthRange = 1f;
            SunViewportPadding = 0.3f;
        }
    }
}
