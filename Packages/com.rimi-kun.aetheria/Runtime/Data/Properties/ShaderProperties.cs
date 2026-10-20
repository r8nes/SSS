using UnityEngine;

namespace Aetheria.Runtime.Rendering
{
    internal static partial class ShaderProperties
    {
        internal static class Textures
        {
            public const string Transmittance = "_TransmittanceLut";
            public const string MultiScattering = "_MultiScatterLut";
            public const string GroundIrradiance = "_GroundIrradianceLut";
            public const string SkyView = "_SkyViewLut";
            public const string AtmosphericScattering = "_AtmosphericScatteringLut";
            public const string SpaceCubemap = "_SpaceCubemap";
            public const string Output = "_OutputTexture";
            public const string AmbientProbeOutputBuffer = "_AmbientProbeOutputBuffer";

            public static readonly int TransmittanceId = Shader.PropertyToID(Transmittance);
            public static readonly int MultiScatteringId = Shader.PropertyToID(MultiScattering);
            public static readonly int GroundIrradianceId = Shader.PropertyToID(GroundIrradiance);
            public static readonly int SkyViewId = Shader.PropertyToID(SkyView);
            public static readonly int AtmosphericScatteringId = Shader.PropertyToID(AtmosphericScattering);
            public static readonly int SpaceCubemapId = Shader.PropertyToID(SpaceCubemap);
            public static readonly int OutputId = Shader.PropertyToID(Output);
            public static readonly int AmbientProbeOutputBufferId = Shader.PropertyToID(AmbientProbeOutputBuffer);
        }

        internal static class Atmosphere
        {
            public static readonly int BottomRadius = Shader.PropertyToID("_BottomRadius");
            public static readonly int TopRadius = Shader.PropertyToID("_TopRadius");
            public static readonly int RayleighDensityExpScale = Shader.PropertyToID("_RayleighDensityExpScale");
            public static readonly int RayleighScattering = Shader.PropertyToID("_RayleighScattering");
            public static readonly int MieDensityExpScale = Shader.PropertyToID("_MieDensityExpScale");
            public static readonly int MieScattering = Shader.PropertyToID("_MieScattering");
            public static readonly int MieAbsorption = Shader.PropertyToID("_MieAbsorption");
            public static readonly int MiePhaseG = Shader.PropertyToID("_MiePhaseG");
            public static readonly int OzoneAbsorptionExtinction = Shader.PropertyToID("_OzoneAbsorptionExtinction");
            public static readonly int GroundAlbedo = Shader.PropertyToID("_GroundAlbedo");
            public static readonly int CameraPosition = Shader.PropertyToID("_CameraPosForAtmosphere");
            public static readonly int PerPixelRaymarchSamples = Shader.PropertyToID("_PerPixelRaymarchSamples");
            public static readonly int SpaceRaymarchSamples = Shader.PropertyToID("_SpaceRaymarchSamples");

            public static readonly int ScatteringMaxDistance =
                Shader.PropertyToID("_AtmosphericScatteringMaxDistance");

            public static readonly int IntensityMultiplier = Shader.PropertyToID("_IntensityMultiplier");
            public static readonly int PlanetCenterWs = Shader.PropertyToID("_PlanetCenterWS");
            public static readonly int SpaceParams0 = Shader.PropertyToID("_SpaceParams0");
            public static readonly int SpaceParams1 = Shader.PropertyToID("_SpaceParams1");
            public static readonly int SpaceTint = Shader.PropertyToID("_SpaceTint");
            public static readonly int ArtisticParams = Shader.PropertyToID("_SkyArtisticParams");
            public static readonly int ArtisticPhaseParams = Shader.PropertyToID("_SkyArtisticPhaseParams");
            public static readonly int SkyHueColor = Shader.PropertyToID("_SkyHueColor");
            public static readonly int HorizonHueColor = Shader.PropertyToID("_HorizonHueColor");
            public static readonly int SunsetParams = Shader.PropertyToID("_SkySunsetParams");
            public static readonly int PatternParams = Shader.PropertyToID("_SkyPatternParams");
            public static readonly int PatternParams2 = Shader.PropertyToID("_SkyPatternParams2");
            public static readonly int ArtisticAffectEnvironment = Shader.PropertyToID("_SkyArtisticAffectEnvironment");

            public static readonly int MultiScatterActiveSampleCount =
                Shader.PropertyToID("_MultiScatterActiveSampleCount");

            public static readonly int MultiScatterRaymarchSampleCount =
                Shader.PropertyToID("_MultiScatterRaymarchSampleCount");

            public static readonly int AerialPerspectiveInvViewProjection =
                Shader.PropertyToID("_AerialPerspectiveInvViewProjection");

            public static readonly int AerialPerspectiveViewForward =
                Shader.PropertyToID("_AerialPerspectiveViewForwardWS");
        }

        internal static class Celestial
        {
            public const string CookieAtlas = "_CelestialBodyCookieAtlas";

            public static readonly int ColorRadius = Shader.PropertyToID("_CelestialColorRadius");
            public static readonly int ForwardDistance = Shader.PropertyToID("_CelestialForwardDistance");
            public static readonly int RightAngularRadius = Shader.PropertyToID("_CelestialRightAngularRadius");
            public static readonly int UpType = Shader.PropertyToID("_CelestialUpType");
            public static readonly int SurfaceEarthshine = Shader.PropertyToID("_CelestialSurfaceEarthshine");

            public static readonly int CookieAtlasScaleOffset =
                Shader.PropertyToID("_CelestialCookieAtlasScaleOffset");

            public static readonly int SunDirectionLimbDarkening =
                Shader.PropertyToID("_CelestialSunDirectionLimbDarkening");

            public static readonly int CookieParams = Shader.PropertyToID("_CelestialCookieParams");
            public static readonly int FlareColor = Shader.PropertyToID("_CelestialFlareColor");
            public static readonly int FlareParams = Shader.PropertyToID("_CelestialFlareParams");
            public static readonly int BodyCount = Shader.PropertyToID("_CelestialBodyCount");
            public static readonly int LightCount = Shader.PropertyToID("_CelestialLightCount");
            public static readonly int CookieAtlasId = Shader.PropertyToID(CookieAtlas);
        }
        internal static class Clouds
        {
            public static readonly int HighestAltitude = Shader.PropertyToID("_HighestAltitude");
            public static readonly int LowestAltitude = Shader.PropertyToID("_LowestAltitude");
            public static readonly int AltitudeSquaredParams = Shader.PropertyToID("_CloudAltitudeSquaredParams");
            public static readonly int PlanetCenterWS = Shader.PropertyToID("_CloudPlanetCenterWS");
            public static readonly int PlanetRadius = Shader.PropertyToID("_CloudPlanetRadius");
            public static readonly int CameraSpace = Shader.PropertyToID("_CameraSpace");

            public static readonly int MaxStepSize = Shader.PropertyToID("_MaxStepSize");
            public static readonly int NumPrimarySteps = Shader.PropertyToID("_NumPrimarySteps");
            public static readonly int NumLightSteps = Shader.PropertyToID("_NumLightSteps");
            public static readonly int ShapeNoiseOffset = Shader.PropertyToID("_ShapeNoiseOffset");
            public static readonly int VerticalShapeNoiseOffset = Shader.PropertyToID("_VerticalShapeNoiseOffset");
            public static readonly int DensityMultiplier = Shader.PropertyToID("_DensityMultiplier");
            public static readonly int ShapeFactor = Shader.PropertyToID("_ShapeFactor");
            public static readonly int ErosionFactor = Shader.PropertyToID("_ErosionFactor");
            public static readonly int MicroErosionFactor = Shader.PropertyToID("_MicroErosionFactor");
            public static readonly int ErosionOcclusion = Shader.PropertyToID("_ErosionOcclusion");
            public static readonly int NoiseScales = Shader.PropertyToID("_CloudNoiseScales");
            public static readonly int DistanceReciprocals =
                Shader.PropertyToID("_CloudDistanceReciprocals");

            public static readonly int ScatteringTint = Shader.PropertyToID("_ScatteringTint");
            public static readonly int PowderEffectIntensity = Shader.PropertyToID("_PowderEffectIntensity");
            public static readonly int MultiScattering = Shader.PropertyToID("_MultiScattering");
            public static readonly int WindDirection = Shader.PropertyToID("_WindDirection");
            public static readonly int WindVector = Shader.PropertyToID("_WindVector");
            public static readonly int MediumWindSpeed = Shader.PropertyToID("_MediumWindSpeed");
            public static readonly int SmallWindSpeed = Shader.PropertyToID("_SmallWindSpeed");
            public static readonly int AltitudeDistortion = Shader.PropertyToID("_AltitudeDistortion");

            public static readonly int VerticalShapeDisplacement =
                Shader.PropertyToID("_VerticalShapeWindDisplacement");

            public static readonly int VerticalErosionDisplacement =
                Shader.PropertyToID("_VerticalErosionWindDisplacement");

            public static readonly int FadeInStart = Shader.PropertyToID("_FadeInStart");

            public static readonly int CurveLut = Shader.PropertyToID("_CloudLutTexture");
            public static readonly int SunColor = Shader.PropertyToID("_SunColor");
            public static readonly int SunDirection = Shader.PropertyToID("_CloudSunDirectionWS");

            public static readonly int AmbientTop = Shader.PropertyToID("_CloudAmbientTop");
            public static readonly int AmbientBottom = Shader.PropertyToID("_CloudAmbientBottom");
            public static readonly int AmbientSHAr = Shader.PropertyToID("_CloudAmbientSHAr");
            public static readonly int AmbientSHAg = Shader.PropertyToID("_CloudAmbientSHAg");
            public static readonly int AmbientSHAb = Shader.PropertyToID("_CloudAmbientSHAb");
            public static readonly int AmbientSHBr = Shader.PropertyToID("_CloudAmbientSHBr");
            public static readonly int AmbientSHBg = Shader.PropertyToID("_CloudAmbientSHBg");
            public static readonly int AmbientSHBb = Shader.PropertyToID("_CloudAmbientSHBb");
            public static readonly int AmbientSHC = Shader.PropertyToID("_CloudAmbientSHC");

            public static readonly int IntermediateTexture =
                Shader.PropertyToID("_CloudIntermediateTexture");

            public static readonly int TraceDataTexture = Shader.PropertyToID("_CloudTraceDataTexture");
            public static readonly int TraceSize = Shader.PropertyToID("_CloudTraceSize");
            public static readonly int IntermediateSize = Shader.PropertyToID("_CloudIntermediateSize");
            public static readonly int FinalSize = Shader.PropertyToID("_CloudFinalSize");
            public static readonly int IntermediateToFinalScale =
                Shader.PropertyToID("_CloudIntermediateToFinalScale");

            public static readonly int NearPlane = Shader.PropertyToID("_CloudNearPlane");

            public static readonly int ViewDirOriginWS = Shader.PropertyToID("_CloudViewDirOriginWS");
            public static readonly int ViewDirStepXWS = Shader.PropertyToID("_CloudViewDirStepXWS");
            public static readonly int ViewDirStepYWS = Shader.PropertyToID("_CloudViewDirStepYWS");

            public static readonly int AccumulationFrameIndex = Shader.PropertyToID("_AccumulationFrameIndex");
            public static readonly int SubPixelIndex = Shader.PropertyToID("_SubPixelIndex");
            public static readonly int WorldSpaceCameraPos = Shader.PropertyToID("_WorldSpaceCameraPos");
            public static readonly int IntegrationNoise =
                Shader.PropertyToID("_CloudIntegrationNoise");
            public static readonly int CloudShapeNoise = Shader.PropertyToID("_CloudShapeNoise");
            public static readonly int CloudDetailNoise = Shader.PropertyToID("_CloudDetailNoise");

            public static readonly int TransmittanceLut = Textures.TransmittanceId;
        }

        internal static class VolumetricResolve
        {
            public static readonly int SourceLightingTexture =
                Shader.PropertyToID("_SourceLightingTexture");

            public static readonly int SourceDataTexture =
                Shader.PropertyToID("_SourceDataTexture");

            public static readonly int HistoryLightingTexture =
                Shader.PropertyToID("_HistoryLightingTexture");

            public static readonly int HistoryDataTexture =
                Shader.PropertyToID("_HistoryDataTexture");

            public static readonly int OutputLightingTexture =
                Shader.PropertyToID("_OutputLightingTexture");

            public static readonly int OutputDataTexture =
                Shader.PropertyToID("_OutputDataTexture");

            public static readonly int SourceSize = Shader.PropertyToID("_SourceSize");
            public static readonly int OutputSize = Shader.PropertyToID("_OutputSize");
            public static readonly int FullResolutionSize = Shader.PropertyToID("_FullResolutionSize");
            public static readonly int OutputToFullResolutionScale =
                Shader.PropertyToID("_OutputToFullResolutionScale");

            public static readonly int HistoryViewportScale =
                Shader.PropertyToID("_HistoryViewportScale");

            public static readonly int PrevViewProjection =
                Shader.PropertyToID("_PrevViewProjection");

            public static readonly int CurrentViewProjection =
                Shader.PropertyToID("_CurrentViewProjection");

            public static readonly int CurrentInvViewProjection =
                Shader.PropertyToID("_CurrentInvViewProjection");

            public static readonly int NearPlaneReprojection =
                Shader.PropertyToID("_NearPlaneReprojection");

            public static readonly int NearPlane = Shader.PropertyToID("_NearPlane");

            public static readonly int HistoryInvalidation =
                Shader.PropertyToID("_HistoryInvalidation");

            public static readonly int TemporalAccumulationFactor =
                Shader.PropertyToID("_TemporalAccumulationFactor");

            public static readonly int SubPixelIndex = Shader.PropertyToID("_SubPixelIndex");
            public static readonly int CloudHistoryPositionOffsetWS =
                Shader.PropertyToID("_CloudHistoryPositionOffsetWS");
        }

        internal static class Fog
        {
            public static readonly int Enabled = Shader.PropertyToID("_FogEnabled");
            public static readonly int ColorMode = Shader.PropertyToID("_FogColorMode");
            public static readonly int SkyColorEnabled = Shader.PropertyToID("_FogSkyColorEnabled");
            public static readonly int Color = Shader.PropertyToID("_FogColor");
            public static readonly int MipParameters = Shader.PropertyToID("_FogMipParameters");
            public static readonly int ConvolutionMipCount =
                Shader.PropertyToID("_FogConvolutionMipCount");
            public static readonly int MaxDistance = Shader.PropertyToID("_FogMaxDistance");
            public static readonly int BaseExtinction = Shader.PropertyToID("_HeightFogBaseExtinction");
            public static readonly int BaseScattering = Shader.PropertyToID("_HeightFogBaseScattering");
            public static readonly int BaseHeight = Shader.PropertyToID("_HeightFogBaseHeight");
            public static readonly int HeightExponents = Shader.PropertyToID("_HeightFogExponents");
            public static readonly int PlanetUpAltitude = Shader.PropertyToID("_PlanetUpAltitude");
            public static readonly int FilteredSky = Shader.PropertyToID("_FogFilteredSky");
        }

        internal static class Composite
        {
            public static readonly int SceneColorTexture =
                Shader.PropertyToID("_SceneColorTexture");
            public static readonly int SceneDepthTexture =
                Shader.PropertyToID("_CameraDepthTexture");
            public static readonly int OutputTexture =
                Shader.PropertyToID("_CompositeOutputTexture");
            public static readonly int OutputSize =
                Shader.PropertyToID("_CompositeOutputSize");
            public static readonly int WaterSurfaceDepth =
                Shader.PropertyToID("_WaterSurfaceDepthTexture");
            public static readonly int WaterStencil =
                Shader.PropertyToID("_WaterStencilTexture");
            public static readonly int BeforeRefraction =
                Shader.PropertyToID("_BeforeRefractionTexture");
            public static readonly int PerceptualEnabled =
                Shader.PropertyToID("_CompositePerceptualEnabled");
            public static readonly int FeatureFlags =
                Shader.PropertyToID("_CompositeFeatureFlags");
            public static readonly int ImprovedTransmittanceBlend =
                Shader.PropertyToID("_ImprovedTransmittanceBlend");
            public static readonly int CloudGuideDepthTexture =
                Shader.PropertyToID("_CloudGuideDepthTexture");
            public static readonly int LightShaftsMask = Shader.PropertyToID("_LightShaftsMask");
            public static readonly int LightShaftsLightDirection = Shader.PropertyToID("_LightShaftsLightDirectionWS");
            public static readonly int LightShaftsLightColor = Shader.PropertyToID("_LightShaftsLightColor");
            public static readonly int LightShaftsTint = Shader.PropertyToID("_LightShaftsTint");
            public static readonly int LightShaftsIntensity = Shader.PropertyToID("_LightShaftsIntensity");
            public static readonly int LightShaftsAnisotropy = Shader.PropertyToID("_LightShaftsAnisotropy");
            public static readonly int LightShaftsOcclusionMaskDarkness = Shader.PropertyToID("_LightShaftsOcclusionMaskDarkness");
            public static readonly int LightShaftsOcclusionDepthRange = Shader.PropertyToID("_LightShaftsOcclusionDepthRange");
            public static readonly int LightShaftsSunViewportPadding = Shader.PropertyToID("_LightShaftsSunViewportPadding");
        }

        internal static class CloudLayer
        {
            public static readonly int TextureId = Shader.PropertyToID("_CloudLayerTexture");
            public static readonly int BaseTexture = Shader.PropertyToID("_CloudLayerBaseTexture");
            public static readonly int BaseTextureRW = Shader.PropertyToID("_CloudLayerBaseTextureRW");
            public static readonly int LightingTexture = Shader.PropertyToID("_CloudLayerLightingTexture");
            public static readonly int LightingTextureRW = Shader.PropertyToID("_CloudLayerLightingTextureRW");
            public static readonly int SourceTextureA = Shader.PropertyToID("_CloudMapA");
            public static readonly int SourceTextureB = Shader.PropertyToID("_CloudMapB");
            public static readonly int GlobalParams = Shader.PropertyToID("_CloudLayerGlobalParams");
            public static readonly int SunDirection = Shader.PropertyToID("_CloudLayerSunDirection");
            public static readonly int LightParam = Shader.PropertyToID("_CloudLayerLightParamA");
            public static readonly int LightParamB = Shader.PropertyToID("_CloudLayerLightParamB");
            public static readonly int AmbientColor = Shader.PropertyToID("_CloudLayerAmbientColor");
            public static readonly int AmbientColorB = Shader.PropertyToID("_CloudLayerAmbientColorB");
            public static readonly int AmbientProbeDimmers =
                Shader.PropertyToID("_CloudLayerAmbientProbeDimmers");
            public static readonly int AmbientProbeBuffer =
                Shader.PropertyToID("_CloudLayerAmbientProbeBuffer");
            public static readonly int UseAmbientProbeBuffer =
                Shader.PropertyToID("_CloudLayerUseAmbientProbeBuffer");
            public static readonly int Parameters = Shader.PropertyToID("_CloudLayerBakeParams");
            public static readonly int LayerOpacities = Shader.PropertyToID("_CloudLayerLayerOpacities");
            public static readonly int LayerParameters = Shader.PropertyToID("_CloudLayerLayerBakeParams");
            public static readonly int BaseSize = Shader.PropertyToID("_CloudLayerBaseSize");
            public static readonly int LightingSize = Shader.PropertyToID("_CloudLayerLightingSize");
            public static readonly int LightingRowOffset = Shader.PropertyToID("_CloudLayerLightingRowOffset");
            public static readonly int LightingRowCount = Shader.PropertyToID("_CloudLayerLightingRowCount");
            public static readonly int AnimationParamA = Shader.PropertyToID("_CloudLayerAnimationParamA");
            public static readonly int AnimationParamB = Shader.PropertyToID("_CloudLayerAnimationParamB");
            public static readonly int ViewForward = Shader.PropertyToID("_CloudLayerViewForwardWS");
            public static readonly int PixelCoordToViewDirWS =
                Shader.PropertyToID("_CloudLayerPixelCoordToViewDirWS");
            public static readonly int PlanetaryRadius = Shader.PropertyToID("_CloudLayerPlanetaryRadius");
        }
    }
}
