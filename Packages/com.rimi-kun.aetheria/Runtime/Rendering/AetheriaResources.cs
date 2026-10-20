using UnityEngine;

namespace Aetheria.Runtime.Rendering
{
    [CreateAssetMenu(menuName = "Aetheria/Runtime Resources", fileName = "AetheriaResources")]
    public sealed class AetheriaResources : ScriptableObject
    {
        [Header("Atmosphere")]
        public ComputeShader MultiScattering;
        public ComputeShader AtmosphereLutGenerator;
        public ComputeShader AerialPerspective;
        public ComputeShader AmbientProbeConvolution;
        public ComputeShader EnvironmentCloudTrace;
        public ComputeShader EnvironmentComposite;
        public ComputeShader EnvironmentTemporalBlend;

        [Header("Shaders")]
        public ComputeShader Composite;
        public ComputeShader ScreenSpaceLightShafts;
        public Shader PhysicallyBasedSky;
        public Shader HDRISky;
        public Shader CelestialDisks;

        [Header("Volumetric Fog")]
        public ComputeShader VolumetricFogMediaClustering;
        public ComputeShader VolumetricFogIntegration;
        public ComputeShader VolumetricFogFiltering;

        [Header("Lighting Sky")]
        public ComputeShader EnvironmentGgxConvolve;

        [Header("Cloud Layer")]
        public ComputeShader CloudLayerBake;
        public Texture2D CloudLayerDefaultMap;

        [Header("Volumetric Clouds")]
        public ComputeShader VolumetricCloudsTrace;
        public ComputeShader VolumetricCloudsDenoising;

        [Header("Cloud Noise")]
        public Texture2D CloudIntegrationNoise;
        public Texture3D CloudShapeNoise;
        public Texture3D CloudDetailNoise;

        [Header("Water")]
        public ComputeShader WaterSpectralSimulation;
        public ComputeShader WaterFoamSimulation;
        public Texture2D WaterFoam;
        public Texture2D WaterPhysicalFoam;
        public Texture2D WaterCaustics;
        public Texture2D WaterCausticsDistortion;
        public ComputeShader WaterFourierTransformSharedMemory;
        public ComputeShader WaterFourierTransformWaveOperations;
        public Shader WaterSurface;
        public ComputeShader WaterDeferredLighting;
        public Shader WaterUnderwaterEffect;
        public ComputeShader WaterUnderwaterLine;
        public ComputeShader WaterEvaluation;

        internal bool TryBuildSnapshot(out ResourceSnapshot snapshot, out string error)
        {
            string missing = null;

            Require(MultiScattering, nameof(MultiScattering), ref missing);
            Require(AtmosphereLutGenerator, nameof(AtmosphereLutGenerator), ref missing);
            Require(AerialPerspective, nameof(AerialPerspective), ref missing);
            Require(AmbientProbeConvolution, nameof(AmbientProbeConvolution), ref missing);
            Require(EnvironmentCloudTrace, nameof(EnvironmentCloudTrace), ref missing);
            Require(EnvironmentComposite, nameof(EnvironmentComposite), ref missing);
            Require(EnvironmentTemporalBlend, nameof(EnvironmentTemporalBlend), ref missing);
            Require(Composite, nameof(Composite), ref missing);
            Require(ScreenSpaceLightShafts, nameof(ScreenSpaceLightShafts), ref missing);
            Require(PhysicallyBasedSky, nameof(PhysicallyBasedSky), ref missing);
            Require(HDRISky, nameof(HDRISky), ref missing);
            Require(CelestialDisks, nameof(CelestialDisks), ref missing);
            Require(VolumetricFogMediaClustering, nameof(VolumetricFogMediaClustering), ref missing);
            Require(VolumetricFogIntegration, nameof(VolumetricFogIntegration), ref missing);
            Require(VolumetricFogFiltering, nameof(VolumetricFogFiltering), ref missing);
            Require(EnvironmentGgxConvolve, nameof(EnvironmentGgxConvolve), ref missing);
            Require(CloudLayerBake, nameof(CloudLayerBake), ref missing);
            Require(CloudLayerDefaultMap, nameof(CloudLayerDefaultMap), ref missing);
            Require(VolumetricCloudsTrace, nameof(VolumetricCloudsTrace), ref missing);
            Require(VolumetricCloudsDenoising, nameof(VolumetricCloudsDenoising), ref missing);
            Require(CloudIntegrationNoise, nameof(CloudIntegrationNoise), ref missing);
            Require(CloudShapeNoise, nameof(CloudShapeNoise), ref missing);
            Require(CloudDetailNoise, nameof(CloudDetailNoise), ref missing);
            Require(WaterSpectralSimulation, nameof(WaterSpectralSimulation), ref missing);
            Require(WaterFoamSimulation, nameof(WaterFoamSimulation), ref missing);
            Require(WaterFoam, nameof(WaterFoam), ref missing);
            Require(WaterPhysicalFoam, nameof(WaterPhysicalFoam), ref missing);
            Require(WaterCaustics, nameof(WaterCaustics), ref missing);
            Require(WaterCausticsDistortion, nameof(WaterCausticsDistortion), ref missing);
            Require(WaterFourierTransformSharedMemory, nameof(WaterFourierTransformSharedMemory), ref missing);
            Require(WaterFourierTransformWaveOperations, nameof(WaterFourierTransformWaveOperations), ref missing);
            Require(WaterSurface, nameof(WaterSurface), ref missing);
            Require(WaterDeferredLighting, nameof(WaterDeferredLighting), ref missing);
            Require(WaterUnderwaterEffect, nameof(WaterUnderwaterEffect), ref missing);
            Require(WaterUnderwaterLine, nameof(WaterUnderwaterLine), ref missing);
            Require(WaterEvaluation, nameof(WaterEvaluation), ref missing);

            if (missing != null)
            {
                snapshot = default;
                error = $"Missing required Aetheria resources: {missing}.";
                return false;
            }

            snapshot = new ResourceSnapshot(
                new AtmosphereResources(
                    MultiScattering,
                    AtmosphereLutGenerator,
                    AerialPerspective),
                new SceneResources(
                    Composite,
                    ScreenSpaceLightShafts,
                    PhysicallyBasedSky,
                    HDRISky,
                    CelestialDisks),
                new VolumetricFogResources(
                    VolumetricFogMediaClustering,
                    VolumetricFogIntegration,
                    VolumetricFogFiltering),
                new EnvironmentLightingResources(
                    AmbientProbeConvolution,
                    EnvironmentCloudTrace,
                    EnvironmentComposite,
                    EnvironmentTemporalBlend,
                    EnvironmentGgxConvolve),
                new CloudLayerResources(
                    CloudLayerBake,
                    CloudLayerDefaultMap),
                new VolumetricCloudResources(
                    VolumetricCloudsTrace,
                    VolumetricCloudsDenoising,
                    CloudIntegrationNoise,
                    CloudShapeNoise,
                    CloudDetailNoise),
                new WaterResources(
                    WaterSpectralSimulation,
                    WaterFoamSimulation,
                    WaterFoam,
                    WaterPhysicalFoam,
                    WaterCaustics,
                    WaterCausticsDistortion,
                    WaterFourierTransformSharedMemory,
                    WaterFourierTransformWaveOperations,
                    WaterSurface,
                    WaterDeferredLighting,
                    WaterUnderwaterEffect,
                    WaterUnderwaterLine,
                    WaterEvaluation));
            error = null;
            return true;
        }

        private static void Require(Object resource, string name, ref string missing)
        {
            if (resource)
                return;

            missing = missing == null ? name : $"{missing}, {name}";
        }
    }

    internal readonly struct ResourceSnapshot
    {
        public readonly AtmosphereResources Atmosphere;
        public readonly SceneResources Scene;
        public readonly VolumetricFogResources VolumetricFog;
        public readonly EnvironmentLightingResources EnvironmentLighting;
        public readonly CloudLayerResources CloudLayer;
        public readonly VolumetricCloudResources VolumetricClouds;
        public readonly WaterResources Water;

        public ResourceSnapshot(
            AtmosphereResources atmosphere,
            SceneResources scene,
            VolumetricFogResources volumetricFog,
            EnvironmentLightingResources environmentLighting,
            CloudLayerResources cloudLayer,
            VolumetricCloudResources volumetricClouds,
            WaterResources water)
        {
            Atmosphere = atmosphere;
            Scene = scene;
            VolumetricFog = volumetricFog;
            EnvironmentLighting = environmentLighting;
            CloudLayer = cloudLayer;
            VolumetricClouds = volumetricClouds;
            Water = water;
        }
    }

    internal sealed class WaterResources
    {
        internal readonly ComputeShader SpectralSimulation;
        internal readonly ComputeShader FoamSimulation;
        internal readonly Texture2D Foam;
        internal readonly Texture2D PhysicalFoam;
        internal readonly Texture2D Caustics;
        internal readonly Texture2D CausticsDistortion;
        internal readonly ComputeShader FourierTransformSharedMemory;
        internal readonly ComputeShader FourierTransformWaveOperations;
        internal readonly Shader Surface;
        internal readonly ComputeShader DeferredLighting;
        internal readonly Shader UnderwaterEffect;
        internal readonly ComputeShader UnderwaterLine;
        internal readonly ComputeShader Evaluation;

        internal WaterResources(
            ComputeShader spectralSimulation,
            ComputeShader foamSimulation,
            Texture2D foam,
            Texture2D physicalFoam,
            Texture2D caustics,
            Texture2D causticsDistortion,
            ComputeShader fourierTransformSharedMemory,
            ComputeShader fourierTransformWaveOperations,
            Shader surface,
            ComputeShader deferredLighting,
            Shader underwaterEffect,
            ComputeShader underwaterLine,
            ComputeShader evaluation)
        {
            SpectralSimulation = spectralSimulation;
            FoamSimulation = foamSimulation;
            Foam = foam;
            PhysicalFoam = physicalFoam;
            Caustics = caustics;
            CausticsDistortion = causticsDistortion;
            FourierTransformSharedMemory = fourierTransformSharedMemory;
            FourierTransformWaveOperations = fourierTransformWaveOperations;
            Surface = surface;
            DeferredLighting = deferredLighting;
            UnderwaterEffect = underwaterEffect;
            UnderwaterLine = underwaterLine;
            Evaluation = evaluation;
        }
    }

    internal readonly struct AtmosphereResources
    {
        public readonly ComputeShader MultiScattering;
        public readonly ComputeShader LutGenerator;
        public readonly ComputeShader AerialPerspective;

        public AtmosphereResources(
            ComputeShader multiScattering,
            ComputeShader lutGenerator,
            ComputeShader aerialPerspective)
        {
            MultiScattering = multiScattering;
            LutGenerator = lutGenerator;
            AerialPerspective = aerialPerspective;
        }
    }

    internal readonly struct SceneResources
    {
        public readonly ComputeShader Composite;
        public readonly ComputeShader ScreenSpaceLightShafts;
        public readonly Shader PhysicallyBasedSky;
        public readonly Shader HDRISky;
        public readonly Shader CelestialDisks;

        public SceneResources(
            ComputeShader composite,
            ComputeShader screenSpaceLightShafts,
            Shader physicallyBasedSky,
            Shader hdriSky,
            Shader celestialDisks)
        {
            Composite = composite;
            ScreenSpaceLightShafts = screenSpaceLightShafts;
            PhysicallyBasedSky = physicallyBasedSky;
            HDRISky = hdriSky;
            CelestialDisks = celestialDisks;
        }
    }

    internal readonly struct VolumetricFogResources
    {
        public readonly ComputeShader MediaClustering;
        public readonly ComputeShader Integration;
        public readonly ComputeShader Filtering;

        public VolumetricFogResources(
            ComputeShader mediaClustering,
            ComputeShader integration,
            ComputeShader filtering)
        {
            MediaClustering = mediaClustering;
            Integration = integration;
            Filtering = filtering;
        }
    }

    internal readonly struct EnvironmentLightingResources
    {
        public readonly ComputeShader AmbientProbeConvolution;
        public readonly ComputeShader CloudTrace;
        public readonly ComputeShader Composite;
        public readonly ComputeShader TemporalBlend;
        public readonly ComputeShader GgxConvolve;

        public EnvironmentLightingResources(
            ComputeShader ambientProbeConvolution,
            ComputeShader cloudTrace,
            ComputeShader composite,
            ComputeShader temporalBlend,
            ComputeShader ggxConvolve)
        {
            AmbientProbeConvolution = ambientProbeConvolution;
            CloudTrace = cloudTrace;
            Composite = composite;
            TemporalBlend = temporalBlend;
            GgxConvolve = ggxConvolve;
        }
    }

    internal readonly struct CloudLayerResources
    {
        public readonly ComputeShader Bake;
        public readonly Texture2D DefaultMap;

        public CloudLayerResources(ComputeShader bake, Texture2D defaultMap)
        {
            Bake = bake;
            DefaultMap = defaultMap;
        }
    }

    internal readonly struct VolumetricCloudResources
    {
        public readonly ComputeShader Trace;
        public readonly ComputeShader Denoising;
        public readonly Texture2D IntegrationNoise;
        public readonly Texture3D ShapeNoise;
        public readonly Texture3D DetailNoise;

        public VolumetricCloudResources(
            ComputeShader trace,
            ComputeShader denoising,
            Texture2D integrationNoise,
            Texture3D shapeNoise,
            Texture3D detailNoise)
        {
            Trace = trace;
            Denoising = denoising;
            IntegrationNoise = integrationNoise;
            ShapeNoise = shapeNoise;
            DetailNoise = detailNoise;
        }
    }
}
