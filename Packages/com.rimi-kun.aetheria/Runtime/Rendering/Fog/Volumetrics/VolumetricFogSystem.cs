using System;
using Aetheria.Runtime.Overrides;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace Aetheria.Runtime.Rendering.Fog.Volumetrics
{
    internal sealed class VolumetricFogSystem : IDisposable
    {
        private static readonly AetheriaProfiler.Marker RecordProfile =
            AetheriaProfiler.CreateMarker("Fog/Volumetrics", "Record");

        internal readonly struct Output
        {
            public readonly TextureHandle IntegratedFog;
            public readonly VolumetricContext Context;

            public bool IsValid => IntegratedFog.IsValid();

            public Output(TextureHandle integratedFog, VolumetricContext context)
            {
                IntegratedFog = integratedFog;
                Context = context;
            }
        }

        private const int FarthestDepthMip = 5;

        private readonly VolumetricFrameDataBuilder _frameDataBuilder = new();
        private readonly FogIntegrationPass _fogIntegrationPass;
        public VolumetricFogSystem(VolumetricFogResources resources)
        {
            _fogIntegrationPass = new FogIntegrationPass(
                resources.MediaClustering,
                resources.Integration,
                resources.Filtering);
        }

        public bool Record(
            RenderGraph renderGraph,
            ContextContainer frameContext,
            FogVolume fogVolume,
            AtmosphereFrameData atmosphere,
            Vector4 planetUpAltitude,
            out Output output)
        {
            using var recordScope = RecordProfile.ScopeCpu();
            output = default;

            if (!fogVolume || !fogVolume.IsVolumetricActive())
                return false;

            var resourceData = frameContext.Get<UniversalResourceData>();
            if (!resourceData.cameraDepthTexture.IsValid())
                return false;

            if (!frameContext.Contains<DepthPyramidFrameData>())
                return false;

            var depthPyramid = frameContext.Get<DepthPyramidFrameData>();
            if (!depthPyramid.isValid ||
                !depthPyramid.HasFarthestPyramid ||
                !depthPyramid.DepthPyramidFarthest.IsValid())
                return false;

            var cameraData = frameContext.Get<UniversalCameraData>();
            if (!cameraData.camera)
                return false;

            if (!_frameDataBuilder.TryBuildFrameData(
                    fogVolume,
                    atmosphere,
                    frameContext,
                    planetUpAltitude,
                    out var frameData))
                return false;

            var context = frameData.Context;
            var fogData = frameData.Settings.FogData;
            var resources = VolumetricResources.Create(
                renderGraph,
                context,
                frameData.Settings.Quality.VBufferGraphicsFormat);

            int farthestMip = Mathf.Min(FarthestDepthMip, depthPyramid.MipCount - 1);
            var farthestMipOffset = depthPyramid.GetMipOffset(farthestMip);
            var farthestMipSize = depthPyramid.GetMipSize(farthestMip);

            float resolutionRatio = context.FroxelWidth / (float)Mathf.Max(context.PixelWidth, 1);
            int dilationWidth = resolutionRatio < 0.1f ? 2 : resolutionRatio < 0.5f ? 1 : 0;

            var sceneDepth = resourceData.cameraDepthTexture;
            var mainLightShadowmap = resourceData.mainShadowsTexture;
            bool mainLightShadowsActive =
                frameData.MainLightShadowsActive && mainLightShadowmap.IsValid();

            var finalFog = _fogIntegrationPass.Record(
                renderGraph,
                context,
                resources,
                sceneDepth,
                depthPyramid.DepthPyramidFarthest,
                farthestMipOffset,
                farthestMipSize,
                dilationWidth,
                fogData,
                frameData.MainLight,
                frameData.AmbientProbeL0,
                frameData.PreviousAmbientProbeBuffer,
                frameData.TargetAmbientProbeBuffer,
                frameData.AmbientProbeBlendFactor,
                mainLightShadowmap,
                mainLightShadowsActive,
                frameData.Volumes,
                frameData.FogMasks);

            if (!finalFog.IsValid())
                return false;

            output = new Output(finalFog, context);
            return true;
        }

        public void Dispose()
        {
            _frameDataBuilder.Dispose();
            _fogIntegrationPass.Dispose();
        }
    }
}
