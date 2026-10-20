using Aetheria.Runtime.Overrides;
using Aetheria.Runtime.Rendering.Fog.Volumetrics;
using Aetheria.Runtime.Utils;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace Aetheria.Runtime.Rendering.Passes
{
    internal sealed class CompositePass : System.IDisposable
    {
        private static readonly AetheriaProfiler.Marker Profile =
            AetheriaProfiler.CreateMarker("Composite", "Scene Composite");

        private const int ThreadGroupSize = 8;
        private const float PerceptualBlendEpsilon = 1.0e-5f;
        private const uint FeatureAtmosphere = 1u;
        private const uint FeatureCloudLayer = 2u;
        private const uint FeatureClouds = 4u;
        private const uint FeatureFog = 8u;
        private const uint FeatureFogSky = 16u;
        private const uint FeatureVolumetricFog = 32u;
        private const uint FeatureLightShafts = 64u;

        private readonly ComputeShader _shader;
        private readonly int _sceneKernel;
        private readonly int _sceneSplitKernel;
        private readonly RenderGraphDefaultTextures _defaultTextures = new();

        private sealed class PassData
        {
            public ComputeShader Shader;
            public int Kernel;
            public uint FeatureFlags;
            public TextureHandle SceneColor;
            public TextureHandle SceneDepth;
            public TextureHandle AerialPerspective;
            public TextureHandle AtmosphereTransmittance;
            public TextureHandle AtmosphereMultiScatter;
            public TextureHandle AtmosphereGroundIrradiance;
            public TextureHandle AtmosphereSkyView;
            public TextureHandle SpaceCubemap;
            public TextureHandle CloudHistoryLighting;
            public TextureHandle CloudHistoryData;
            public TextureHandle CloudLayerTexture;
            public TextureHandle FogSkyTexture;
            public TextureHandle IntegratedFog;
            public TextureHandle Output;
            public TextureHandle LightShaftsMask;
            public TextureHandle WaterSurfaceDepth;
            public TextureHandle BeforeRefraction;
            public Vector3 LightShaftsDirectionWS;
            public Color LightShaftsLightColor;
            public Vector4 LightShaftsTint;
            public float LightShaftsIntensity;
            public float LightShaftsAnisotropy;
            public float LightShaftsOcclusionMaskDarkness;
            public float LightShaftsOcclusionDepthRange;
            public float LightShaftsSunViewportPadding;
            public PhysicallyBasedSky PhysicallyBasedSky;
            public VisualEnvironment VisualEnvironment;
            public Vector3 CameraPositionWs;
            public int PerPixelRaymarchSamples;
            public int SpaceRaymarchSamples;
            public CloudLayerShaderData CloudLayer;
            public Vector4 CloudLayerViewForward;
            public Matrix4x4 CloudLayerPixelCoordToViewDirWS;
            public float CloudLayerPlanetaryRadius;
            public Vector4 CloudHistorySize;
            public int CloudOutputToFullResolutionScale;
            public Vector4 OutputSize;
            public float CloudNearPlane;
            public float ImprovedTransmittanceBlend;
            public bool AtmosphereEnabled;
            public bool CloudsEnabled;
            public bool CloudLayerEnabled;
            public bool FogEnabled;
            public bool FogSkyColorEnabled;
            public bool VolumetricFogEnabled;
            public bool LightShaftsEnabled;
            public bool RefractionSplit;
            public FogParameters FogParameters;
            public VolumetricContext VolumetricContext;
        }

        public CompositePass(ComputeShader shader)
        {
            _shader = shader;
            _sceneKernel = _shader.FindKernel("Scene");
            _sceneSplitKernel = _shader.FindKernel("SceneSplit");
        }

        public void RecordSceneComposite(RenderGraph renderGraph, ContextContainer frameData)
        {
            UniversalResourceData resources = frameData.Get<UniversalResourceData>();
            if (!resources.activeColorTexture.IsValid() || !resources.activeDepthTexture.IsValid())
                return;

            AtmosphereFrameData atmosphereFrame = frameData.Contains<AtmosphereFrameData>()
                ? frameData.Get<AtmosphereFrameData>()
                : null;
            bool atmosphereEnabled = atmosphereFrame is { IsActive: true }
                                     && atmosphereFrame.Volume
                                     && atmosphereFrame.AerialPerspective.IsValid();

            VolumetricCloudFrameData clouds = frameData.Contains<VolumetricCloudFrameData>()
                ? frameData.Get<VolumetricCloudFrameData>()
                : null;
            bool cloudsEnabled = atmosphereEnabled
                                 && clouds is { IsActive: true }
                                 && clouds.ResolvedLighting.IsValid()
                                 && clouds.ResolvedData.IsValid();

            CloudLayerFrameData cloudLayerFrame = frameData.Contains<CloudLayerFrameData>()
                ? frameData.Get<CloudLayerFrameData>()
                : null;
            bool cloudLayerEnabled = atmosphereEnabled
                                     && cloudLayerFrame is { IsActive: true }
                                     && cloudLayerFrame.Texture.IsValid()
                                     && atmosphereFrame.Transmittance.IsValid();

            FogFrameData fogFrame = frameData.Contains<FogFrameData>()
                ? frameData.Get<FogFrameData>()
                : null;
            bool fogEnabled = fogFrame is { IsActive: true, CompositeOpaque: true };
            bool fogSkyColorEnabled = fogEnabled
                                      && Mathf.Approximately(
                                          fogFrame.Parameters.ColorMode,
                                          (float)FogColorMode.SkyColor)
                                      && fogFrame.FilteredSky.IsValid();
            bool volumetricFogEnabled = fogEnabled
                                        && fogFrame.UseVolumetricFog
                                        && fogFrame.IntegratedFog.IsValid();

            ScreenSpaceLightShaftsFrameData lightShaftsFrame = frameData.Contains<ScreenSpaceLightShaftsFrameData>()
                ? frameData.Get<ScreenSpaceLightShaftsFrameData>()
                : null;
            bool lightShaftsEnabled = lightShaftsFrame is { IsActive: true }
                                      && lightShaftsFrame.Mask.IsValid();

            if (!atmosphereEnabled
                && !cloudsEnabled
                && !cloudLayerEnabled
                && !fogEnabled
                && !lightShaftsEnabled)
                return;

            TransparentRefractionFrameData refraction = frameData.Contains<TransparentRefractionFrameData>()
                ? frameData.Get<TransparentRefractionFrameData>()
                : null;
            bool refractionSplit = refraction is { isValid: true }
                                   && refraction.waterSurfaceDepth.IsValid()
                                   && refraction.beforeRefraction.IsValid();
            TextureHandle sceneDepth = refractionSplit && refraction.depthBufferPreRefraction.IsValid()
                ? refraction.depthBufferPreRefraction
                : resources.activeDepthTexture;

            TextureHandle sceneColor = resources.activeColorTexture;
            TextureDesc desc = renderGraph.GetTextureDesc(sceneColor);
            int outputWidth = Mathf.Max(1, desc.width);
            int outputHeight = Mathf.Max(1, desc.height);
            if (!GraphicsFormatUtility.IsHDRFormat(desc.colorFormat))
                desc.colorFormat = GraphicsFormat.R16G16B16A16_SFloat;
            desc.depthBufferBits = DepthBits.None;
            desc.msaaSamples = MSAASamples.None;
            desc.enableRandomWrite = true;
            desc.clearBuffer = false;
            desc.name = "Aetheria Scene Composite";
            TextureHandle output = renderGraph.CreateTexture(desc);

            TextureHandle black2D = renderGraph.defaultResources.blackTexture;
            TextureHandle white2D = renderGraph.defaultResources.whiteTexture;
            TextureHandle black3D = _defaultTextures.ImportBlack3D(renderGraph);
            TextureHandle blackCubemap = _defaultTextures.ImportBlackCubemap(renderGraph);
            TextureHandle black2DArray = _defaultTextures.ImportBlack2DArray(renderGraph);

            using var profiledPass = Profile.AddComputePass<PassData>(renderGraph, out var pass);
            var builder = profiledPass.Builder;
            pass.Shader = _shader;
            pass.Kernel = refractionSplit ? _sceneSplitKernel : _sceneKernel;
            pass.FeatureFlags = BuildFeatureFlags(
                atmosphereEnabled,
                cloudLayerEnabled,
                cloudsEnabled,
                fogEnabled,
                fogSkyColorEnabled,
                volumetricFogEnabled,
                lightShaftsEnabled);
            pass.SceneColor = sceneColor;
            pass.SceneDepth = sceneDepth;
            pass.AerialPerspective = atmosphereEnabled ? atmosphereFrame.AerialPerspective : black3D;
            pass.AtmosphereTransmittance = atmosphereEnabled && atmosphereFrame.Transmittance.IsValid()
                ? atmosphereFrame.Transmittance
                : white2D;
            pass.AtmosphereMultiScatter = atmosphereEnabled && atmosphereFrame.MultiScatter.IsValid()
                ? atmosphereFrame.MultiScatter
                : black2D;
            pass.AtmosphereGroundIrradiance = atmosphereEnabled && atmosphereFrame.GroundIrradiance.IsValid()
                ? atmosphereFrame.GroundIrradiance
                : black2D;
            pass.AtmosphereSkyView = atmosphereEnabled && atmosphereFrame.SkyView.IsValid()
                ? atmosphereFrame.SkyView
                : black2D;
            pass.SpaceCubemap = atmosphereEnabled && atmosphereFrame.SpaceCubemap.IsValid()
                ? atmosphereFrame.SpaceCubemap
                : blackCubemap;
            pass.CloudHistoryLighting = cloudsEnabled ? clouds.ResolvedLighting : black2D;
            pass.CloudHistoryData = cloudsEnabled ? clouds.ResolvedData : black2D;
            pass.CloudLayerTexture = cloudLayerEnabled ? cloudLayerFrame.Texture : black2DArray;
            pass.FogSkyTexture = fogSkyColorEnabled ? fogFrame.FilteredSky : blackCubemap;
            pass.IntegratedFog = volumetricFogEnabled ? fogFrame.IntegratedFog : black3D;
            pass.Output = output;
            pass.LightShaftsMask = lightShaftsEnabled ? lightShaftsFrame.Mask : black2D;
            pass.WaterSurfaceDepth = refractionSplit ? refraction.waterSurfaceDepth : TextureHandle.nullHandle;
            pass.BeforeRefraction = refractionSplit ? refraction.beforeRefraction : TextureHandle.nullHandle;
            pass.LightShaftsDirectionWS = lightShaftsEnabled ? lightShaftsFrame.LightDirectionWS : Vector3.zero;
            pass.LightShaftsLightColor = lightShaftsEnabled ? lightShaftsFrame.LightColor : Color.black;
            pass.LightShaftsTint = lightShaftsEnabled ? lightShaftsFrame.Tint : Vector4.one;
            pass.LightShaftsIntensity = lightShaftsEnabled ? lightShaftsFrame.Intensity : 0f;
            pass.LightShaftsAnisotropy = lightShaftsEnabled ? lightShaftsFrame.Anisotropy : 0f;
            pass.LightShaftsOcclusionMaskDarkness = lightShaftsEnabled ? lightShaftsFrame.OcclusionMaskDarkness : 0f;
            pass.LightShaftsOcclusionDepthRange = lightShaftsEnabled ? lightShaftsFrame.OcclusionDepthRange : 1f;
            pass.LightShaftsSunViewportPadding = lightShaftsEnabled ? lightShaftsFrame.SunViewportPadding : 0.3f;
            pass.AtmosphereEnabled = atmosphereEnabled;
            pass.CloudsEnabled = cloudsEnabled;
            pass.CloudLayerEnabled = cloudLayerEnabled;
            pass.FogEnabled = fogEnabled;
            pass.FogSkyColorEnabled = fogSkyColorEnabled;
            pass.VolumetricFogEnabled = volumetricFogEnabled;
            pass.LightShaftsEnabled = lightShaftsEnabled;
            pass.RefractionSplit = refractionSplit;
            pass.FogParameters = fogEnabled ? fogFrame.Parameters : default;
            pass.VolumetricContext = volumetricFogEnabled ? fogFrame.VolumetricContext : default;
            pass.PhysicallyBasedSky = atmosphereEnabled ? atmosphereFrame.Volume : null;
            pass.VisualEnvironment = atmosphereFrame?.VisualEnvironment;
            pass.CameraPositionWs = atmosphereFrame?.CameraPositionWs ?? Vector3.zero;
            pass.PerPixelRaymarchSamples = atmosphereEnabled ? atmosphereFrame.PerPixelRaymarchSamples : 0;
            pass.SpaceRaymarchSamples = atmosphereEnabled ? atmosphereFrame.SpaceRaymarchSamples : 0;
            pass.CloudHistorySize = cloudsEnabled ? clouds.ResolvedSize : Vector4.one;
            pass.CloudOutputToFullResolutionScale = cloudsEnabled && clouds.IsFullResolution ? 1 : 2;
            pass.OutputSize = new Vector4(outputWidth, outputHeight, 1.0f / outputWidth, 1.0f / outputHeight);
            pass.CloudNearPlane = cloudsEnabled ? clouds.NearPlane : 0.1f;
            pass.ImprovedTransmittanceBlend = cloudsEnabled
                ? Mathf.Clamp01(clouds.ImprovedTransmittanceBlend)
                : 0.0f;

            if (cloudLayerEnabled)
            {
                pass.CloudLayer = cloudLayerFrame.ShaderData;
                Camera camera = atmosphereFrame.Camera;
                Vector3 forward = camera ? camera.transform.forward : Vector3.forward;
                pass.CloudLayerViewForward = new Vector4(forward.x, forward.y, forward.z, 0.0f);
                pass.CloudLayerPlanetaryRadius = atmosphereFrame.VisualEnvironment.PlanetRadiusMeters;
                pass.CloudLayerPixelCoordToViewDirWS = camera
                    ? ComputePixelCoordToWorldSpaceViewDirectionMatrix(camera, pass.OutputSize)
                    : Matrix4x4.identity;
            }

            builder.UseTexture(pass.SceneColor, AccessFlags.Read);
            builder.UseTexture(pass.SceneDepth, AccessFlags.Read);
            builder.UseTexture(black2D, AccessFlags.Read);
            builder.UseTexture(white2D, AccessFlags.Read);
            builder.UseTexture(black3D, AccessFlags.Read);
            builder.UseTexture(blackCubemap, AccessFlags.Read);
            builder.UseTexture(black2DArray, AccessFlags.Read);
            if (atmosphereEnabled)
            {
                builder.UseTexture(atmosphereFrame.AerialPerspective, AccessFlags.Read);
                if (atmosphereFrame.Transmittance.IsValid()) builder.UseTexture(atmosphereFrame.Transmittance, AccessFlags.Read);
                if (atmosphereFrame.MultiScatter.IsValid()) builder.UseTexture(atmosphereFrame.MultiScatter, AccessFlags.Read);
                if (atmosphereFrame.GroundIrradiance.IsValid()) builder.UseTexture(atmosphereFrame.GroundIrradiance, AccessFlags.Read);
                if (atmosphereFrame.SkyView.IsValid()) builder.UseTexture(atmosphereFrame.SkyView, AccessFlags.Read);
                if (atmosphereFrame.SpaceCubemap.IsValid()) builder.UseTexture(atmosphereFrame.SpaceCubemap, AccessFlags.Read);
            }
            if (cloudsEnabled)
            {
                builder.UseTexture(clouds.ResolvedLighting, AccessFlags.Read);
                builder.UseTexture(clouds.ResolvedData, AccessFlags.Read);
            }
            if (cloudLayerEnabled)
                builder.UseTexture(cloudLayerFrame.Texture, AccessFlags.Read);
            if (fogSkyColorEnabled)
                builder.UseTexture(fogFrame.FilteredSky, AccessFlags.Read);
            if (volumetricFogEnabled)
                builder.UseTexture(fogFrame.IntegratedFog, AccessFlags.Read);
            if (lightShaftsEnabled)
                builder.UseTexture(lightShaftsFrame.Mask, AccessFlags.Read);
            if (refractionSplit)
            {
                builder.UseTexture(pass.WaterSurfaceDepth, AccessFlags.Read);
                builder.UseTexture(pass.BeforeRefraction, AccessFlags.ReadWrite);
            }
            builder.UseTexture(pass.Output, AccessFlags.Write);
            builder.SetRenderFunc(static (PassData data, ComputeGraphContext context) => ExecuteSceneComposite(context.cmd, data));
            resources.cameraColor = output;
        }

        private static uint BuildFeatureFlags(
            bool atmosphere,
            bool cloudLayer,
            bool clouds,
            bool fog,
            bool fogSky,
            bool volumetricFog,
            bool lightShafts)
        {
            uint flags = 0u;
            if (atmosphere) flags |= FeatureAtmosphere;
            if (cloudLayer) flags |= FeatureCloudLayer;
            if (clouds) flags |= FeatureClouds;
            if (fog) flags |= FeatureFog;
            if (fogSky) flags |= FeatureFogSky;
            if (volumetricFog) flags |= FeatureVolumetricFog;
            if (lightShafts) flags |= FeatureLightShafts;
            return flags;
        }

        private static void ExecuteSceneComposite(ComputeCommandBuffer command, PassData data)
        {
            ComputeShader shader = data.Shader;
            int kernel = data.Kernel;
            ShaderValueWriter writer = ShaderValueWriter.ForCompute(command, shader);

            command.SetComputeTextureParam(shader, kernel, ShaderProperties.Composite.SceneColorTexture, data.SceneColor);
            command.SetComputeTextureParam(shader, kernel, ShaderProperties.Composite.SceneDepthTexture, data.SceneDepth);
            command.SetComputeTextureParam(shader, kernel, ShaderProperties.Composite.OutputTexture, data.Output);
            command.SetComputeTextureParam(shader, kernel, ShaderProperties.Textures.AtmosphericScatteringId, data.AerialPerspective);
            command.SetComputeTextureParam(shader, kernel, ShaderProperties.Textures.TransmittanceId, data.AtmosphereTransmittance);
            command.SetComputeTextureParam(shader, kernel, ShaderProperties.Textures.MultiScatteringId, data.AtmosphereMultiScatter);
            command.SetComputeTextureParam(shader, kernel, ShaderProperties.Textures.GroundIrradianceId, data.AtmosphereGroundIrradiance);
            command.SetComputeTextureParam(shader, kernel, ShaderProperties.Textures.SkyViewId, data.AtmosphereSkyView);
            command.SetComputeTextureParam(shader, kernel, ShaderProperties.Textures.SpaceCubemapId, data.SpaceCubemap);
            command.SetComputeTextureParam(shader, kernel, ShaderProperties.VolumetricResolve.SourceLightingTexture, data.CloudHistoryLighting);
            command.SetComputeTextureParam(shader, kernel, ShaderProperties.VolumetricResolve.SourceDataTexture, data.CloudHistoryData);
            command.SetComputeTextureParam(shader, kernel, ShaderProperties.Composite.CloudGuideDepthTexture, data.SceneDepth);
            command.SetComputeTextureParam(shader, kernel, ShaderProperties.CloudLayer.TextureId, data.CloudLayerTexture);
            command.SetComputeTextureParam(shader, kernel, ShaderProperties.Fog.FilteredSky, data.FogSkyTexture);
            command.SetComputeTextureParam(shader, kernel, ShaderProperties.VolumetricFog.IntegratedFogTexture, data.IntegratedFog);
            command.SetComputeTextureParam(shader, kernel, ShaderProperties.Composite.LightShaftsMask, data.LightShaftsMask);

            if (data.RefractionSplit)
            {
                command.SetComputeTextureParam(
                    shader,
                    kernel,
                    ShaderProperties.Composite.WaterSurfaceDepth,
                    data.WaterSurfaceDepth,
                    0,
                    RenderTextureSubElement.Depth);
                command.SetComputeTextureParam(
                    shader,
                    kernel,
                    ShaderProperties.Composite.WaterStencil,
                    data.WaterSurfaceDepth,
                    0,
                    RenderTextureSubElement.Stencil);
                command.SetComputeTextureParam(shader, kernel, ShaderProperties.Composite.BeforeRefraction, data.BeforeRefraction);
            }

            writer.SetInt(ShaderProperties.Composite.FeatureFlags, unchecked((int)data.FeatureFlags));
            writer.SetVector(ShaderProperties.Composite.OutputSize, data.OutputSize);
            writer.SetInt(
                ShaderProperties.Composite.PerceptualEnabled,
                data.CloudsEnabled && data.ImprovedTransmittanceBlend > PerceptualBlendEpsilon ? 1 : 0);
            writer.SetFloat(ShaderProperties.Composite.ImprovedTransmittanceBlend, data.ImprovedTransmittanceBlend);
            writer.SetVector(ShaderProperties.VolumetricResolve.SourceSize, data.CloudHistorySize);
            writer.SetInt(ShaderProperties.VolumetricResolve.OutputToFullResolutionScale, data.CloudOutputToFullResolutionScale);
            writer.SetFloat(ShaderProperties.VolumetricResolve.NearPlane, data.CloudNearPlane);
            writer.SetVector(ShaderProperties.Composite.LightShaftsLightDirection, new Vector4(data.LightShaftsDirectionWS.x, data.LightShaftsDirectionWS.y, data.LightShaftsDirectionWS.z, 0f));
            writer.SetColor(ShaderProperties.Composite.LightShaftsLightColor, data.LightShaftsLightColor);
            writer.SetVector(ShaderProperties.Composite.LightShaftsTint, data.LightShaftsTint);
            writer.SetFloat(ShaderProperties.Composite.LightShaftsIntensity, data.LightShaftsIntensity);
            writer.SetFloat(ShaderProperties.Composite.LightShaftsAnisotropy, data.LightShaftsAnisotropy);
            writer.SetFloat(ShaderProperties.Composite.LightShaftsOcclusionMaskDarkness, data.LightShaftsOcclusionMaskDarkness);
            writer.SetFloat(ShaderProperties.Composite.LightShaftsOcclusionDepthRange, data.LightShaftsOcclusionDepthRange);
            writer.SetFloat(ShaderProperties.Composite.LightShaftsSunViewportPadding, data.LightShaftsSunViewportPadding);
            writer.SetFloat(ShaderProperties.Atmosphere.ScatteringMaxDistance, AtmosphereLutPass.ScatteringMaxDistance);

            if (data.AtmosphereEnabled)
            {
                AtmosphereLutPass.BindComputeParams(
                    command,
                    shader,
                    kernel,
                    data.PhysicallyBasedSky,
                    data.VisualEnvironment,
                    data.CameraPositionWs,
                    data.SpaceCubemap,
                    data.PerPixelRaymarchSamples,
                    data.SpaceRaymarchSamples);
            }

            if (data.CloudLayerEnabled)
            {
                writer.Write(data.CloudLayer);
                writer.SetVector(ShaderProperties.CloudLayer.ViewForward, data.CloudLayerViewForward);
                writer.SetMatrix(ShaderProperties.CloudLayer.PixelCoordToViewDirWS, data.CloudLayerPixelCoordToViewDirWS);
                writer.SetFloat(ShaderProperties.CloudLayer.PlanetaryRadius, data.CloudLayerPlanetaryRadius);
            }

            if (data.FogEnabled)
                writer.Write(data.FogParameters);
            if (data.VolumetricFogEnabled)
                VolumetricPassCommon.WriteSamplingContext(ref writer, data.VolumetricContext);

            command.DispatchCompute(
                shader,
                kernel,
                CoreUtils.DivRoundUp((int)data.OutputSize.x, ThreadGroupSize),
                CoreUtils.DivRoundUp((int)data.OutputSize.y, ThreadGroupSize),
                1);
        }

        private static Matrix4x4 ComputePixelCoordToWorldSpaceViewDirectionMatrix(Camera camera, Vector4 screenSize)
        {
            Matrix4x4 viewSpaceRasterTransform;
            if (camera.orthographic)
            {
                viewSpaceRasterTransform = new Matrix4x4(
                    new Vector4(-2.0f * screenSize.z, 0.0f, 0.0f, 0.0f),
                    new Vector4(0.0f, -2.0f * screenSize.w, 0.0f, 0.0f),
                    new Vector4(1.0f, 1.0f, -1.0f, 0.0f),
                    new Vector4(0.0f, 0.0f, 0.0f, 0.0f));
            }
            else
            {
                float tanHalfVertFoV = Mathf.Tan(0.5f * camera.fieldOfView * Mathf.Deg2Rad);
                Vector2 lensShift = camera.lensShift;
                float aspectRatio = screenSize.x * screenSize.w;
                float m21 = (1.0f - 2.0f * lensShift.y) * tanHalfVertFoV;
                float m11 = -2.0f * screenSize.w * tanHalfVertFoV;
                float m20 = (1.0f - 2.0f * lensShift.x) * tanHalfVertFoV * aspectRatio;
                float m00 = -2.0f * screenSize.z * tanHalfVertFoV * aspectRatio;
                viewSpaceRasterTransform = new Matrix4x4(
                    new Vector4(m00, 0.0f, 0.0f, 0.0f),
                    new Vector4(0.0f, m11, 0.0f, 0.0f),
                    new Vector4(m20, m21, -1.0f, 0.0f),
                    new Vector4(0.0f, 0.0f, 0.0f, 1.0f));
            }

            Matrix4x4 worldToViewMatrix = camera.worldToCameraMatrix;
            worldToViewMatrix.SetColumn(3, new Vector4(0.0f, 0.0f, 0.0f, 1.0f));
            worldToViewMatrix.SetRow(2, -worldToViewMatrix.GetRow(2));
            return Matrix4x4.Transpose(worldToViewMatrix.transpose * viewSpaceRasterTransform);
        }

        public void Dispose()
        {
            _defaultTextures.Dispose();
        }
    }
}
