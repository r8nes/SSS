using System;
using Aetheria.Runtime.Overrides;
using Aetheria.Runtime.Rendering;
using UnityEngine;

namespace Aetheria.Runtime.Sky
{
    internal sealed class SkyContextManager : IDisposable
    {
        private readonly PerCameraDataManager<SkyUpdateContext> _contexts;

        public SkyContextManager(in ResourceSnapshot resources)
        {
            var snapshot = resources;
            _contexts = new PerCameraDataManager<SkyUpdateContext>(() => new SkyUpdateContext(snapshot));
        }

        public SkyUpdateContext Prepare(
            Camera camera,
            VisualEnvironment visualEnvironment,
            SkySettings skySettings)
        {
            if (!camera)
                return null;

            var context = _contexts.GetOrAddData(camera);
            context.SetSkySettings(skySettings);

            if (visualEnvironment && skySettings != null)
                context.PrepareVisualSky(visualEnvironment, camera);

            return context;
        }

        public bool TryGet(Camera camera, out SkyUpdateContext context) =>
            _contexts.TryGetData(camera, out context);

        public void Dispose() => _contexts.Dispose();
    }
}
