using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Aetheria.Runtime.Overrides
{
    [Serializable]
    [VolumeComponentMenu("Aetheria/Screen Space Light Shafts")]
    [SupportedOnRenderPipeline(typeof(UniversalRenderPipelineAsset))]
    public sealed class ScreenSpaceLightShafts : VolumeComponent, IPostProcessComponent
    {
        public BoolParameter enabled = new(false);
        public MinFloatParameter intensity = new(1f, 0f);
        public ColorParameter tint = new(Color.white);
        public ClampedFloatParameter anisotropy = new(0.8f, 0f, 0.98f);
        public ClampedFloatParameter occlusionMaskDarkness = new(0.05f, 0f, 1f);
        public ClampedFloatParameter occlusionDepthRange = new(300f, 1f, 10000f);

        public bool IsActive() => enabled.value && intensity.value > 0f && occlusionDepthRange.value > 0f;
        public bool IsTileCompatible() => false;
    }
}
