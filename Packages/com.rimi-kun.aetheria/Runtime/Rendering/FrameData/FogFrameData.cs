using Aetheria.Runtime.Rendering.Fog.Volumetrics;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;

namespace Aetheria.Runtime.Rendering
{
    internal sealed class FogFrameData : ContextItem
    {
        public bool IsActive;
        public bool UseVolumetricFog;
        public bool CompositeOpaque;
        public FogParameters Parameters;
        public TextureHandle FilteredSky;
        public TextureHandle IntegratedFog;
        public VolumetricContext VolumetricContext;

        public override void Reset()
        {
            IsActive = false;
            UseVolumetricFog = false;
            CompositeOpaque = false;
            Parameters = default;
            FilteredSky = TextureHandle.nullHandle;
            IntegratedFog = TextureHandle.nullHandle;
            VolumetricContext = default;
        }
    }
}
