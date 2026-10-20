using System;
using Aetheria.Runtime.Overrides;
using Aetheria.Runtime.Rendering;
using UnityEngine;

namespace Aetheria.Runtime.Sky
{
    internal sealed class SkyUpdateContext : IDisposable
    {
        private readonly ResourceSnapshot _resources;
        private SkySettings _skySettings;

        public SkyRenderer SkyRenderer { get; private set; }
        public Material SkyboxMaterial => SkyRenderer?.SkyboxMaterial;
        public SkySettings SkySettings => _skySettings;

        public SkyUpdateContext(in ResourceSnapshot resources) => _resources = resources;

        public void SetSkySettings(SkySettings value)
        {
            if (SkyRenderer != null &&
                (value == null || value.GetSkyRendererType() != SkyRenderer.GetType()))
            {
                SkyRenderer.Cleanup();
                SkyRenderer = null;
            }

            _skySettings = value;
            if (_skySettings == null || SkyRenderer != null)
                return;

            var rendererType = _skySettings.GetSkyRendererType();
            SkyRenderer = (SkyRenderer)Activator.CreateInstance(rendererType);
            SkyRenderer.Build(_resources);
        }

        public void PrepareVisualSky(VisualEnvironment environment, Camera camera)
        {
            if (_skySettings == null || SkyRenderer == null)
                return;

            SkyRenderer.PrepareVisualSky(_skySettings, environment, camera);
        }

        public void Dispose()
        {
            SkyRenderer?.Cleanup();
            SkyRenderer = null;
            _skySettings = null;
        }
    }
}
