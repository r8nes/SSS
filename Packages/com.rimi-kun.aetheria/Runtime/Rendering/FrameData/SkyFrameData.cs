using Aetheria.Runtime.Overrides;
using Aetheria.Runtime.Sky;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;

namespace Aetheria.Runtime.Rendering
{
    public sealed class SkyFrameData : ContextItem
    {
        public bool IsActive;
        public Camera Camera;
        public VisualEnvironment VisualEnvironment;
        public SkySettings Settings;
        internal SkyRenderer Renderer;
        internal EnvironmentRadianceFrameData Environment;

        public override void Reset()
        {
            IsActive = false;
            Camera = null;
            VisualEnvironment = null;
            Settings = null;
            Renderer = null;
            Environment.Reset();
        }
    }
}
