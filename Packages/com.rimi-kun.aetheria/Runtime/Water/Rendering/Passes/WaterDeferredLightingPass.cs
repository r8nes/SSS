using Aetheria.Runtime.Rendering;
using Aetheria.Runtime.Rendering.Fog.Volumetrics;
using Aetheria.Runtime.Rendering.Passes;
using Aetheria.Runtime.Utils;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.RenderGraphModule;

namespace Aetheria.Runtime.Water
{
    internal static class WaterDeferredLightingPass
    {
        private sealed class PassData
        {
            public ComputeShader deferredLightingShader;
            public int deferredLightingKernel;
            public float ambientProbeDimmer;
            public float environmentReflectionIntensity;
            public Vector4 resolution;
            public GraphicsBuffer surfaceProfileBuffer;
            public TextureHandle causticsTexture;
            public TextureHandle causticsDistortionTexture;
            public TextureHandle sceneColor;
            public TextureHandle colorBuffer;
            public TextureHandle opaqueDepth;
            public TextureHandle depthBuffer;
            public TextureHandle materialGBuffer;
            public TextureHandle surfaceGBuffer;
            public TextureHandle geometryGBuffer;
            public TextureHandle metadataGBuffer;
            public TextureHandle screenSpaceReflection;
            public bool hasScreenSpaceReflection;
            public TextureHandle screenSpaceShadows;
            public bool hasScreenSpaceShadows;
            public TextureHandle mainShadows;
            public TextureHandle additionalShadows;
            public TextureHandle beforeRefraction;
            public bool compositeBeforeRefraction;
            public int lightingTileCountX;
            public int lightingTileCountY;
            public TextureHandle aerialPerspective;
            public TextureHandle fogSkyTexture;
            public TextureHandle integratedFog;
            public bool atmosphereEnabled;
            public bool fogEnabled;
            public bool fogSkyColorEnabled;
            public bool volumetricFogEnabled;
            public FogParameters fogParameters;
            public VolumetricContext volumetricContext;
        }

        internal static void Record(
            WaterShadingResources lighting,
            RenderGraph renderGraph,
            UniversalResourceData resourceData,
            TextureHandle sceneColor,
            WaterCameraContext cameraContext,
            WaterFrameResources frame,
            TextureHandle target,
            AtmosphereFrameData atmosphereFrame,
            FogFrameData fogFrame,
            bool atmosphereEnabled,
            bool fogEnabled,
            bool fogSkyColorEnabled,
            bool volumetricFogEnabled,
            bool hasWaterScreenSpaceShadows,
            TextureHandle beforeRefraction,
            bool compositeBeforeRefraction)
        {
            if (lighting == null || !lighting.IsValid() || !sceneColor.IsValid() || !target.IsValid())
                return;

            using var profiledPass = WaterProfiling.DeferredLighting.AddComputePass<PassData>(renderGraph, out var passData);
            var builder = profiledPass.Builder;
            passData.deferredLightingShader = lighting.deferredShader;
            passData.ambientProbeDimmer = frame.cameraRenderData.ambientProbeDimmer;
            passData.environmentReflectionIntensity = frame.cameraRenderData.environmentReflectionIntensity;
            passData.surfaceProfileBuffer = frame.cameraRenderData.surfaceProfileBuffer;
            passData.causticsTexture = lighting.ImportCaustics(renderGraph);
            passData.causticsDistortionTexture = lighting.ImportCausticsDistortion(renderGraph);
            passData.resolution = cameraContext.renderResolution;
            passData.sceneColor = sceneColor;
            passData.colorBuffer = target;
            passData.opaqueDepth = frame.opaqueDepth;
            passData.depthBuffer = frame.waterDepth;
            passData.materialGBuffer = frame.materialGBuffer;
            passData.surfaceGBuffer = frame.surfaceGBuffer;
            passData.geometryGBuffer = frame.geometryGBuffer;
            passData.metadataGBuffer = frame.metadataGBuffer;
            passData.hasScreenSpaceReflection = resourceData.ssrTexture.IsValid();
            passData.screenSpaceReflection = passData.hasScreenSpaceReflection ? resourceData.ssrTexture : renderGraph.defaultResources.blackTexture;
            passData.deferredLightingKernel = compositeBeforeRefraction
                ? lighting.deferredLightingRefractionKernel
                : lighting.deferredLightingKernel;
            passData.screenSpaceShadows = resourceData.screenSpaceShadowsTexture;
            passData.hasScreenSpaceShadows = hasWaterScreenSpaceShadows;
            passData.mainShadows = hasWaterScreenSpaceShadows
                ? TextureHandle.nullHandle
                : resourceData.mainShadowsTexture;
            passData.additionalShadows = resourceData.additionalShadowsTexture;
            passData.beforeRefraction = beforeRefraction;
            passData.compositeBeforeRefraction = compositeBeforeRefraction;
            passData.lightingTileCountX = cameraContext.lightingTileCountX;
            passData.lightingTileCountY = cameraContext.lightingTileCountY;
            passData.atmosphereEnabled = atmosphereEnabled;
            passData.fogEnabled = fogEnabled;
            passData.fogSkyColorEnabled = fogSkyColorEnabled;
            passData.volumetricFogEnabled = volumetricFogEnabled;
            passData.aerialPerspective = atmosphereEnabled ? atmosphereFrame.AerialPerspective : lighting.ImportBlack3D(renderGraph);
            passData.fogSkyTexture = fogSkyColorEnabled ? fogFrame.FilteredSky : lighting.ImportBlackCubemap(renderGraph);
            passData.integratedFog = volumetricFogEnabled ? fogFrame.IntegratedFog : lighting.ImportBlack3D(renderGraph);
            passData.fogParameters = fogEnabled ? fogFrame.Parameters : default;
            passData.volumetricContext = volumetricFogEnabled ? fogFrame.VolumetricContext : default;

            builder.UseAllGlobalTextures(true);
            builder.UseTexture(passData.sceneColor, AccessFlags.Read);
            builder.UseTexture(passData.colorBuffer, AccessFlags.Write);
            builder.UseTexture(passData.opaqueDepth, AccessFlags.Read);
            builder.UseTexture(passData.depthBuffer, AccessFlags.Read);
            builder.UseTexture(passData.materialGBuffer, AccessFlags.Read);
            builder.UseTexture(passData.surfaceGBuffer, AccessFlags.Read);
            builder.UseTexture(passData.geometryGBuffer, AccessFlags.Read);
            builder.UseTexture(passData.metadataGBuffer, AccessFlags.Read);
            builder.UseTexture(passData.screenSpaceReflection, AccessFlags.Read);
            if (passData.hasScreenSpaceShadows)
                builder.UseTexture(passData.screenSpaceShadows, AccessFlags.Read);
            builder.UseTexture(passData.causticsTexture, AccessFlags.Read);
            builder.UseTexture(passData.causticsDistortionTexture, AccessFlags.Read);
            if (passData.mainShadows.IsValid())
                builder.UseTexture(passData.mainShadows, AccessFlags.Read);
            if (passData.additionalShadows.IsValid())
                builder.UseTexture(passData.additionalShadows, AccessFlags.Read);
            builder.UseTexture(passData.aerialPerspective, AccessFlags.Read);
            builder.UseTexture(passData.fogSkyTexture, AccessFlags.Read);
            builder.UseTexture(passData.integratedFog, AccessFlags.Read);
            if (passData.compositeBeforeRefraction)
                builder.UseTexture(passData.beforeRefraction, AccessFlags.Read);
            if (passData.surfaceProfileBuffer != null)
                builder.UseBuffer(renderGraph.ImportBuffer(passData.surfaceProfileBuffer), AccessFlags.Read);

            builder.SetRenderFunc(static (PassData data, ComputeGraphContext context) =>
            {
                var cmd = context.cmd;
                int kernel = data.deferredLightingKernel;
                var writer = ShaderValueWriter.ForCompute(cmd, data.deferredLightingShader);
                cmd.SetComputeFloatParam(data.deferredLightingShader, WaterShaderIDs._WaterAmbientProbeDimmer, data.ambientProbeDimmer);
                cmd.SetComputeFloatParam(data.deferredLightingShader, WaterShaderIDs._WaterEnvironmentReflectionIntensity, data.environmentReflectionIntensity);
                cmd.SetComputeVectorParam(data.deferredLightingShader, WaterShaderIDs._WaterRenderResolution, data.resolution);
                cmd.SetComputeIntParam(data.deferredLightingShader, WaterShaderIDs._WaterHasScreenSpaceReflection, data.hasScreenSpaceReflection ? 1 : 0);
                cmd.SetComputeIntParam(data.deferredLightingShader, WaterShaderIDs._WaterHasScreenSpaceShadows, data.hasScreenSpaceShadows ? 1 : 0);
                cmd.SetComputeIntParam(data.deferredLightingShader, WaterShaderIDs._WaterSurfaceMediumAtmosphereEnabled, data.atmosphereEnabled ? 1 : 0);
                cmd.SetComputeIntParam(data.deferredLightingShader, WaterShaderIDs._WaterSurfaceMediumFogEnabled, data.fogEnabled ? 1 : 0);
                cmd.SetComputeIntParam(data.deferredLightingShader, WaterShaderIDs._WaterSurfaceMediumFogSkyEnabled, data.fogSkyColorEnabled ? 1 : 0);
                cmd.SetComputeIntParam(data.deferredLightingShader, WaterShaderIDs._WaterSurfaceMediumVolumetricFogEnabled, data.volumetricFogEnabled ? 1 : 0);

                if (data.surfaceProfileBuffer != null)
                    cmd.SetComputeBufferParam(data.deferredLightingShader, kernel, WaterShaderIDs._WaterSurfaceProfiles, data.surfaceProfileBuffer);
                cmd.SetComputeTextureParam(data.deferredLightingShader, kernel, WaterShaderIDs._WaterMaterialGBuffer, data.materialGBuffer);
                cmd.SetComputeTextureParam(data.deferredLightingShader, kernel, WaterShaderIDs._WaterSurfaceGBuffer, data.surfaceGBuffer);
                cmd.SetComputeTextureParam(data.deferredLightingShader, kernel, WaterShaderIDs._WaterGeometryGBuffer, data.geometryGBuffer);
                cmd.SetComputeTextureParam(data.deferredLightingShader, kernel, WaterShaderIDs._WaterMetadataGBuffer, data.metadataGBuffer);
                cmd.SetComputeTextureParam(data.deferredLightingShader, kernel, WaterShaderIDs._WaterSceneColorTexture, data.sceneColor);
                if (data.compositeBeforeRefraction)
                    cmd.SetComputeTextureParam(data.deferredLightingShader, kernel, ShaderProperties.Composite.BeforeRefraction, data.beforeRefraction);
                cmd.SetComputeTextureParam(data.deferredLightingShader, kernel, WaterShaderIDs._ScreenSpaceReflectionTexture, data.screenSpaceReflection);
                if (data.hasScreenSpaceShadows)
                    cmd.SetComputeTextureParam(data.deferredLightingShader, kernel, WaterShaderIDs._ScreenSpaceShadowmapTexture, data.screenSpaceShadows);
                if (data.mainShadows.IsValid())
                    cmd.SetComputeTextureParam(data.deferredLightingShader, kernel, WaterShaderIDs._MainLightShadowmapTexture, data.mainShadows);
                if (data.additionalShadows.IsValid())
                    cmd.SetComputeTextureParam(data.deferredLightingShader, kernel, WaterShaderIDs._AdditionalLightsShadowmapTexture, data.additionalShadows);
                cmd.SetComputeTextureParam(data.deferredLightingShader, kernel, WaterShaderIDs._WaterOpaqueDepthTexture, data.opaqueDepth);
                cmd.SetComputeTextureParam(data.deferredLightingShader, kernel, WaterShaderIDs._WaterDepthTexture, data.depthBuffer);
                cmd.SetComputeTextureParam(data.deferredLightingShader, kernel, WaterShaderIDs._WaterStencilTexture, data.depthBuffer, 0, RenderTextureSubElement.Stencil);
                cmd.SetComputeTextureParam(data.deferredLightingShader, kernel, WaterShaderIDs._WaterCausticsTexture, data.causticsTexture);
                cmd.SetComputeTextureParam(data.deferredLightingShader, kernel, WaterShaderIDs._WaterCausticsDistortionTexture, data.causticsDistortionTexture);
                cmd.SetComputeTextureParam(data.deferredLightingShader, kernel, WaterShaderIDs._WaterColorRW, data.colorBuffer);

                cmd.SetComputeTextureParam(data.deferredLightingShader, kernel, ShaderProperties.Textures.AtmosphericScatteringId, data.aerialPerspective);
                writer.SetFloat(ShaderProperties.Atmosphere.ScatteringMaxDistance, AtmosphereLutPass.ScatteringMaxDistance);
                cmd.SetComputeTextureParam(data.deferredLightingShader, kernel, ShaderProperties.Fog.FilteredSky, data.fogSkyTexture);
                cmd.SetComputeTextureParam(data.deferredLightingShader, kernel, ShaderProperties.VolumetricFog.IntegratedFogTexture, data.integratedFog);

                if (data.fogEnabled)
                    writer.Write(data.fogParameters);
                if (data.volumetricFogEnabled)
                    VolumetricPassCommon.WriteSamplingContext(ref writer, data.volumetricContext);

                cmd.DispatchCompute(data.deferredLightingShader, kernel, data.lightingTileCountX, data.lightingTileCountY, 1);
            });
        }
    }
}
