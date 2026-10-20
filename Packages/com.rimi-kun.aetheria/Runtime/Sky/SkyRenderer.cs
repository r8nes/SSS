using Aetheria.Runtime.Overrides;
using Aetheria.Runtime.Rendering;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;

namespace Aetheria.Runtime.Sky
{
    internal abstract class SkyRenderer
    {
        public abstract Material SkyboxMaterial { get; }
        public virtual bool RequiresAtmosphere => false;

        public abstract void Build(in ResourceSnapshot resources);
        public abstract void Cleanup();
        public abstract void PrepareVisualSky(SkySettings settings, VisualEnvironment environment, Camera camera);
        public virtual int GetDynamicHash(SkySettings settings, VisualEnvironment environment, Camera camera) => 0;

        public abstract int GetEnvironmentKernel(bool cloudsEnabled, bool cloudLayerEnabled);

        public abstract bool IsEnvironmentReady(
            SkySettings settings,
            VisualEnvironment environment,
            AtmosphereFrameData atmosphere);

        public virtual SkyRenderGraphResources ImportEnvironmentResources(
            RenderGraph renderGraph,
            SkySettings settings) => default;

        public abstract void DeclareEnvironmentResources(
            IComputeRenderGraphBuilder builder,
            AtmosphereFrameData atmosphere,
            in SkyRenderGraphResources resources);

        public abstract void BindEnvironmentCompute(
            ComputeCommandBuffer command,
            ComputeShader shader,
            int kernel,
            SkySettings settings,
            VisualEnvironment environment,
            AtmosphereFrameData atmosphere,
            in SkyRenderGraphResources resources,
            Vector3 observerPositionWs);
    }

    internal readonly struct SkyRenderGraphResources
    {
        public readonly TextureHandle Primary;
        public readonly TextureHandle Secondary;

        public SkyRenderGraphResources(TextureHandle primary, TextureHandle secondary = default)
        {
            Primary = primary;
            Secondary = secondary;
        }
    }
}
