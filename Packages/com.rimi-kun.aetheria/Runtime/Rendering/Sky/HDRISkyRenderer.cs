using System;
using Aetheria.Runtime.Overrides;
using Aetheria.Runtime.Sky;
using Aetheria.Runtime.Utils;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;

namespace Aetheria.Runtime.Rendering.Sky
{
    internal sealed class HDRISkyRenderer : SkyRenderer
    {
        internal static readonly int CubemapId = Shader.PropertyToID("_Cubemap");
        internal static readonly int FlowmapId = Shader.PropertyToID("_Flowmap");
        internal static readonly int SkyParamId = Shader.PropertyToID("_SkyParam");
        internal static readonly int FlowmapParamId = Shader.PropertyToID("_FlowmapParam");
        internal static readonly int DistortionModeId = Shader.PropertyToID("_HDRIDistortionMode");

        private Material _material;
        private float _scrollFactor;
        private float _lastTime;
        private int _compositeKernel;
        private int _compositeCloudsKernel;
        private int _compositeCloudLayerKernel;
        private int _compositeCloudsCloudLayerKernel;
        private RenderGraphTextureHandleCache _cubemapCache;
        private RenderGraphTextureHandleCache _flowmapCache;

        public override Material SkyboxMaterial => _material;

        public override void Build(in ResourceSnapshot resources)
        {
            _material = CoreUtils.CreateEngineMaterial(resources.Scene.HDRISky);
            var shader = resources.EnvironmentLighting.Composite;
            _compositeKernel = shader.FindKernel("CompositeHDRI");
            _compositeCloudsKernel = shader.FindKernel("CompositeHDRIClouds");
            _compositeCloudLayerKernel = shader.FindKernel("CompositeHDRICloudLayer");
            _compositeCloudsCloudLayerKernel = shader.FindKernel("CompositeHDRICloudsCloudLayer");
        }

        public override void Cleanup()
        {
            CoreUtils.Destroy(_material);
            _material = null;
            _cubemapCache.Dispose();
            _flowmapCache.Dispose();
            _scrollFactor = 0.0f;
            _lastTime = 0.0f;
        }

        public override void PrepareVisualSky(
            SkySettings settings,
            VisualEnvironment environment,
            Camera camera)
        {
            if (_material == null || settings is not HDRISky sky || !sky.hdriSky.value)
                return;

            float now = Application.isPlaying ? Time.time : Time.realtimeSinceStartup;
            if (_lastTime <= 0.0f)
                _lastTime = now;

            if (sky.distortionMode.value != HDRISky.DistortionMode.None)
                _scrollFactor += sky.scrollSpeed.GetValue(environment) * Mathf.Max(0.0f, now - _lastTime) * 0.01f;
            _lastTime = now;

            float phi = -Mathf.Deg2Rad * sky.rotation.value;
            float intensity = sky.GetIntensityFromSettings();

            _material.SetTexture(CubemapId, sky.hdriSky.value);
            _material.SetTexture(FlowmapId, sky.flowmap.value);
            _material.SetVector(SkyParamId,
                new Vector4(intensity, 0.0f, Mathf.Cos(phi), Mathf.Sin(phi)));
            _material.SetVector(FlowmapParamId, GetFlowmapParameters(sky, environment));
            _material.SetInt(DistortionModeId, (int)sky.distortionMode.value);
        }

        public override int GetDynamicHash(
            SkySettings settings,
            VisualEnvironment environment,
            Camera camera)
        {
            if (settings is not HDRISky sky || sky.distortionMode.value == HDRISky.DistortionMode.None)
                return 0;

            return HashCode.Combine(
                Mathf.FloorToInt(_scrollFactor * 10000.0f),
                sky.scrollOrientation.GetValue(environment),
                sky.scrollSpeed.GetValue(environment));
        }


        public override int GetEnvironmentKernel(bool cloudsEnabled, bool cloudLayerEnabled)
        {
            if (cloudsEnabled)
                return cloudLayerEnabled ? _compositeCloudsCloudLayerKernel : _compositeCloudsKernel;
            return cloudLayerEnabled ? _compositeCloudLayerKernel : _compositeKernel;
        }

        public override bool IsEnvironmentReady(
            SkySettings settings,
            VisualEnvironment environment,
            AtmosphereFrameData atmosphere) =>
            settings is HDRISky sky && environment && sky.hdriSky.value;

        public override SkyRenderGraphResources ImportEnvironmentResources(
            RenderGraph renderGraph,
            SkySettings settings)
        {
            if (settings is not HDRISky sky || !sky.hdriSky.value)
                return default;

            return new SkyRenderGraphResources(
                _cubemapCache.Import(renderGraph, sky.hdriSky.value),
                _flowmapCache.Import(
                    renderGraph,
                    sky.flowmap.value ? sky.flowmap.value : Texture2D.grayTexture));
        }

        public override void DeclareEnvironmentResources(
            IComputeRenderGraphBuilder builder,
            AtmosphereFrameData atmosphere,
            in SkyRenderGraphResources resources)
        {
            if (resources.Primary.IsValid())
                builder.UseTexture(resources.Primary);
            if (resources.Secondary.IsValid())
                builder.UseTexture(resources.Secondary);
        }

        public override void BindEnvironmentCompute(
            ComputeCommandBuffer command,
            ComputeShader shader,
            int kernel,
            SkySettings settings,
            VisualEnvironment environment,
            AtmosphereFrameData atmosphere,
            in SkyRenderGraphResources resources,
            Vector3 observerPositionWs)
        {
            if (settings is not HDRISky sky || !sky.hdriSky.value)
                return;

            command.SetComputeTextureParam(shader, kernel, CubemapId, resources.Primary);
            command.SetComputeTextureParam(shader, kernel, FlowmapId, resources.Secondary);

            float phi = -Mathf.Deg2Rad * sky.rotation.value;
            command.SetComputeVectorParam(shader, SkyParamId,
                new Vector4(sky.GetIntensityFromSettings(), 0.0f, Mathf.Cos(phi), Mathf.Sin(phi)));
            command.SetComputeVectorParam(shader, FlowmapParamId, GetFlowmapParameters(sky, environment));
            command.SetComputeIntParam(shader, DistortionModeId, (int)sky.distortionMode.value);
        }


        internal Vector4 GetFlowmapParameters(HDRISky sky, VisualEnvironment environment)
        {
            float rot = Mathf.Deg2Rad * (sky.scrollOrientation.GetValue(environment) - sky.rotation.value);
            bool upperHemisphereOnly =
                sky.upperHemisphereOnly.value || sky.distortionMode.value == HDRISky.DistortionMode.Procedural;
            return new Vector4(
                upperHemisphereOnly ? 1.0f : 0.0f,
                _scrollFactor / 200.0f,
                -Mathf.Cos(rot),
                -Mathf.Sin(rot));
        }

    }
}
