using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using System;
using System.Collections.Generic;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using static Unity.Mathematics.math;

#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

namespace Aetheria.Runtime.Water
{

    public enum WaterSurfaceType
    {
        [InspectorName("Ocean, Sea or Lake")] OceanSeaLake,
        Pool,
    }

    public enum WaterGeometryType
    {
        Quad,
        CustomMesh,
        InstancedQuads,
        Infinite
    }

    [DisallowMultipleComponent]
    [ExecuteInEditMode]
    public sealed class WaterSurface : MonoBehaviour
    {
        public WaterSurfaceType surfaceType = WaterSurfaceType.OceanSeaLake;
        public WaterGeometryType geometryType = WaterGeometryType.Infinite;

        public List<MeshRenderer> meshRenderers = new();

        public Vector2 lodScaleRange = new(4.0f, 256.0f);

        [Range(0.0f, 1.0f)] public float lodWaveHeightDetailBias = 0.2f;

        [Range(2, 15)] public int lodLevels = 7;

        [Range(80, 1024), Tooltip("Resolution used to derive the water LOD geometry density.")]
        public int lodResolution = 384;

        [Min(1), Tooltip("Number of LOD data texels covered by one geometry quad.")]
        public int lodGeometryDownsampleFactor = 2;

        [Tooltip("Multiplier applied to the outermost LOD skirt so it reaches the horizon.")]
        public float lodHorizonExtensionMultiplier = 100.0f;

        [Tooltip("Enables the animated water simulation.")]
        public bool simulationEnabled = true;

        [Range(0, 10)] public float simulationTimeScale = 1.0f;

        public Material materialOverride;

        [Range(0.0f, 1.0f), Tooltip("Controls the smoothness of the water surface.")]
        public float nearSmoothness = 0.95f;

        [Range(0.0f, 1.0f), Tooltip("Controls the smoothness reached by the water surface in the distance.")]
        public float farSmoothness = 0.85f;

        [Min(0.0f), Tooltip("Distance in meters at which smoothness starts fading toward the distant value.")]
        public float smoothnessFadeStart = 150.0f;

        [Min(0.001f), Tooltip("Distance in meters over which smoothness fades toward the distant value.")]
        public float smoothnessFadeDistance = 500.0f;

        public Texture2D normalMap;

        [Range(0.01f, 100.0f)] public float normalMapScale = 3.0f;

        [Range(0.0f, 10.0f)] public float normalMapScrollSpeed = 1.0f;

        [Range(0.0f, 1.0f)] public float normalMapStrength = 0.15f;


        [Tooltip("Sets the color used to tint water refraction.")] [ColorUsage(false)]
        public Color refractionColor = new(0.00f, 0.45f, 0.65f);

        [Range(0.0f, 3.5f),
         Tooltip(
             "Controls the maximum distance in meters used to clamp refraction depth. Higher values increase the distortion amount.")]
        public float maxRefractionDistance = 1.0f;

        [Range(0.001f, 100.0f),
         Tooltip(
             "Controls the approximative distance in meters that the camera can perceive through a water surface. This distance can vary widely depending on the intensity of the light the object receives.")]
        public float absorptionDistance = 5.0f;

        internal Vector3 extinction => (-Mathf.Log(0.02f) / Mathf.Max(absorptionDistance, 0.001f)) *
                                       new Vector3(Mathf.Max(1.0f - refractionColor.r, 0.01f),
                                           Mathf.Max(1.0f - refractionColor.g, 0.01f),
                                           Mathf.Max(1.0f - refractionColor.b, 0.01f));

        internal Vector3 EvaluateStylizedScatteringAlbedo(float opticalDepth)
        {
            var baseAlbedo = new Vector3(
                Mathf.Clamp01(scatteringColor.r),
                Mathf.Clamp01(scatteringColor.g),
                Mathf.Clamp01(scatteringColor.b));
            float strength = Mathf.Clamp01(depthTintStrength);
            if (strength <= 0.0f)
                return baseAlbedo;

            float depthFactor = Mathf.Clamp01(opticalDepth / Mathf.Max(depthTintDistance, 0.001f));
            float depthBlend = Mathf.Pow(depthFactor, Mathf.Max(depthTintFalloff, 0.001f));
            var shallow = new Vector3(
                Mathf.Clamp01(shallowDepthTint.r),
                Mathf.Clamp01(shallowDepthTint.g),
                Mathf.Clamp01(shallowDepthTint.b));
            var deep = new Vector3(
                Mathf.Clamp01(deepDepthTint.r),
                Mathf.Clamp01(deepDepthTint.g),
                Mathf.Clamp01(deepDepthTint.b));
            var target = Vector3.Lerp(shallow, deep, depthBlend);

            float baseLuminance = ColorUtils.Luminance(
                new Color(baseAlbedo.x, baseAlbedo.y, baseAlbedo.z));
            float targetLuminance = Mathf.Max(
                ColorUtils.Luminance(new Color(target.x, target.y, target.z)),
                0.0001f);
            var matchedTarget = target * (baseLuminance / targetLuminance);
            matchedTarget.x = Mathf.Clamp01(matchedTarget.x);
            matchedTarget.y = Mathf.Clamp01(matchedTarget.y);
            matchedTarget.z = Mathf.Clamp01(matchedTarget.z);
            return Vector3.Lerp(baseAlbedo, matchedTarget, strength);
        }

        [Tooltip("Sets the color that is used to simulate the water light scattering.")] [ColorUsage(false)]
        public Color scatteringColor = new(0.0f, 0.27f, 0.23f);

        [Range(0.0f, 1.0f),
         Tooltip("Controls the intensity of the ambient scattering term. This can be adjusted for artistic purposes.")]
        public float ambientScattering = 0.1f;

        [Min(0.0f),
         Tooltip("Distance in meters from the water surface center where probe lighting starts fading to the ambient probe.")]
        public float probeLightingFadeStart;

        [Min(0.0f),
         Tooltip("Distance in meters from the water surface center where probe lighting is fully replaced by the ambient probe. Values not greater than Fade Start disable the fade.")]
        public float probeLightingFadeEnd;

        [Range(0.0f, 1.0f),
         Tooltip(
             "Controls the intensity of height-based scattering. Higher vertical displacement receives more scattering.")]
        public float heightScattering = 0.1f;

        [Range(0.0f, 1.0f),
         Tooltip(
             "Controls the intensity of displacement-based scattering. Larger horizontal displacement receives more scattering.")]
        public float displacementScattering = 0.3f;

        [Range(0.0f, 1.0f),
         Tooltip(
             "Controls additional directional scattering through thin wave tips, most visible around grazing and forward-scattering directions.")]
        public float directLightTipScattering = 0.6f;

        [Range(0.0f, 1.0f),
         Tooltip(
             "Controls additional directional scattering through thicker wave bodies, most visible around grazing and forward-scattering directions.")]
        public float directLightBodyScattering = 0.4f;

        [Range(0.0f, 1.0f),
         Tooltip(
             "Blends the physical water body color toward the artistic shallow/deep palette without changing extinction or fog density.")]
        public float depthTintStrength;

        [ColorUsage(false),
         Tooltip("Artistic target color for shallow water. This affects surface body color, not extinction.")]
        public Color shallowDepthTint = new(0.01960785f, 0.5843138f, 0.420484f, 1.0f);

        [ColorUsage(false),
         Tooltip("Artistic target color for deep water. This affects surface body color, not extinction.")]
        public Color deepDepthTint = new(0.04491666f, 0.3109616f, 0.539f, 1.0f);

        [Min(0.001f),
         Tooltip("Optical depth in meters over which the artistic surface color transitions from shallow to deep.")]
        public float depthTintDistance = 10.0f;

        [Min(0.001f), Tooltip("Shapes the shallow-to-deep artistic color transition.")]
        public float depthTintFalloff = 2.0f;

        [Range(0.0f, 1.0f), Tooltip("Smoothness used at the shoreline when Shoreline Influence is greater than zero.")]
        public float shorelineSmoothness = 1.0f;

        [Range(0.0f, 1.0f), Tooltip("Controls how strongly shoreline depth modifies canonical water smoothness.")]
        public float shorelineSmoothnessInfluence;

        [Min(0.001f), Tooltip("Optical depth in meters over which shoreline smoothness fades to canonical smoothness.")]
        public float shorelineSmoothnessDepthRange = 1.0f;

        [Tooltip("Enables screen space reflections for this water surface.")]
        public bool screenSpaceReflections = true;

        [Tooltip("Enables animated caustics for refracted and underwater scene lighting.")]
        public bool causticsEnabled = true;

        [Range(0.0f, 10.0f)] public float causticsStrength = 3.2f;

        [Tooltip("Minimum caustics intensity in a fully shadowed area.")]
        [Range(0.0f, 1.0f)] public float causticsDirectionalShadowDimmer = 0.2f;

        [Range(0.01f, 100.0f)] public float causticsScale = 50.0f;

        [Range(0.0f, 10.0f)] public float causticsScrollSpeed = 1.0f;

        [Range(0.0f, 1.0f)] public float causticsTextureAverage = 0.07f;

        [Range(0.0f, 25.0f)] public float causticsFocalDepth = 2.0f;

        [Range(0.01f, 10.0f)] public float causticsDepthOfField = 6.0f;

        [Range(0.0f, 0.25f)] public float causticsDistortionStrength = 0.16f;

        [Range(0.01f, 1000.0f)] public float causticsDistortionScale = 250.0f;

        [Tooltip("Enables the underwater fullscreen volume effect for this surface.")]
        public bool underwaterEnabled = true;

        [Range(0.0001f, 1.0f)] public float underwaterFarPlaneMultiplier = 0.68f;

        public bool underwaterDitheringEnabled = true;

        [Range(0.0f, 4.0f)] public float underwaterDitheringIntensity = 1.0f;

        [Min(0.0f)] public float underwaterExtinctionMultiplier = 1.0f;

        [Min(0.0f)] public float underwaterSunBoost = 2.0f;

        [Range(0.0f, 1.0f)] public float underwaterOutScatteringFactor = 0.2f;

        [Range(0.0f, 1.0f)] public float underwaterOutScatteringExtinctionFactor = 0.2f;

        [Range(0.31f, 0.99f)] public float underwaterAnisotropy = 0.92f;

        [Min(0.0f)] public float underwaterDirectLightingMultiplier = 1.0f;

        [Min(0.0f)] public float underwaterAmbientLightingMultiplier = 1.0f;

        [Min(0.0f)] public float underwaterAmbientShadowInfluence = 0.5f;

        public WaterDebugMode debugMode = WaterDebugMode.None;

        public WaterSimulationMaskDebugMode simulationMaskDebugMode = WaterSimulationMaskDebugMode.RedChannel;


        public float currentDebugArrowDensity = 1.0f;


        internal MaterialPropertyBlock propertyBlock;
        internal GraphicsBuffer simulationConstantBuffer;
        internal GraphicsBuffer spectrumConstantBuffer;
        internal GraphicsBuffer surfaceConstantBuffer;
        internal int surfaceIndex;
        private static void BindWaveCascadeTextures(MaterialPropertyBlock block, WaterGpuSimulationResources buffers)
        {
            block.SetTexture(WaterShaderIDs._WaterWaveFieldCascade0, buffers.waveCascades[0].waveField);
            block.SetTexture(WaterShaderIDs._WaterWaveFieldCascade1, buffers.waveCascades[1].waveField);
            block.SetTexture(WaterShaderIDs._WaterWaveFieldCascade2, buffers.waveCascades[2].waveField);
        }

        internal void CreatePropertyBlock(Texture foamTexture, Texture physicalFoamTexture)
        {
            propertyBlock = new MaterialPropertyBlock();
            FillMaterialPropertyBlock(foamTexture, physicalFoamTexture);
        }

        private void ConfigureMaterialPropertyBlock(
            MaterialPropertyBlock block,
            Texture foamTexture,
            Texture physicalFoamTexture,
            Texture simulationMask,
            Texture currentMap)
        {
            block.Clear();

            block.SetConstantBuffer(WaterShaderIDs._WaterSimulationShaderVariables, simulationConstantBuffer, 0,
                simulationConstantBuffer.stride);
            block.SetConstantBuffer(WaterShaderIDs._WaterSurfaceShaderVariables, surfaceConstantBuffer, 0,
                surfaceConstantBuffer.stride);
            BindWaveCascadeTextures(block, simulation.gpuResources);
            block.SetTexture(WaterShaderIDs._WaterFoamTexture,
                this.foamTexture != null ? this.foamTexture : foamTexture);
            block.SetTexture(WaterShaderIDs._WaterPhysicalFoamTexture, physicalFoamTexture);
            block.SetTexture(WaterShaderIDs._WaterNormalMap, normalMap != null ? normalMap : Texture2D.blackTexture);
            block.SetTexture(WaterShaderIDs._WaterShorelineFoamNoiseMap,
                shorelineFoamNoiseMap != null ? shorelineFoamNoiseMap : Texture2D.whiteTexture);
            block.SetVector(WaterShaderIDs._WaterNormalMapParameters, new Vector4(
                Mathf.Max(normalMapScale, 0.01f),
                Mathf.Max(normalMapScrollSpeed, 0.0f),
                Mathf.Clamp01(normalMapStrength),
                normalMap ? 1.0f : 0.0f));

            var gpuResources = simulation.gpuResources;
            block.SetBuffer(WaterShaderIDs._WaterFoamCascades, gpuResources.foamCascadesBuffer);
            var foamResources = gpuResources.foam;
            if (foamResources?.IsValid() == true)
                block.SetTexture(WaterShaderIDs._WaterFoamField, foamResources.current.rt);

            block.SetTexture(WaterShaderIDs._WaterSimulationMask, simulationMask);
            block.SetTexture(WaterShaderIDs._WaterCurrentMap, currentMap);
        }

        internal void FillMaterialPropertyBlock(Texture foamTexture, Texture physicalFoamTexture)
        {
            if (propertyBlock == null || simulation?.gpuResources == null || simulationConstantBuffer == null ||
                surfaceConstantBuffer == null || surfaceIndex < 0)
                return;

            var simulationMask = GetSimulationMaskTexture(Texture2D.whiteTexture);
            var currentMap = GetCurrentMapTexture(Texture2D.blackTexture);
            ConfigureMaterialPropertyBlock(propertyBlock, foamTexture, physicalFoamTexture, simulationMask, currentMap);
        }

        private void OnValidate()
        {
            lodScaleRange.x = Mathf.Pow(2.0f, Mathf.Round(Mathf.Log(Mathf.Max(lodScaleRange.x, 0.25f), 2.0f)));

            if (lodScaleRange.y < Mathf.Infinity)
                lodScaleRange.y = Mathf.Pow(2.0f,
                    Mathf.Round(Mathf.Log(Mathf.Max(lodScaleRange.y, lodScaleRange.x), 2.0f)));

            lodResolution = Mathf.Clamp(lodResolution, 80, 1024);
            lodGeometryDownsampleFactor = Mathf.ClosestPowerOfTwo(Mathf.Max(lodGeometryDownsampleFactor, 1));

            int maximumDownSample = Mathf.Max(lodResolution / 32, 1);
            int maximumDownSamplePowerOfTwo = 1;
            while (maximumDownSamplePowerOfTwo <= maximumDownSample / 2)
                maximumDownSamplePowerOfTwo *= 2;
            lodGeometryDownsampleFactor = Mathf.Min(lodGeometryDownsampleFactor, maximumDownSamplePowerOfTwo);

            int resolutionStep = Mathf.Max(16, 8 * lodGeometryDownsampleFactor);
            int minimumResolution = CoreUtils.DivRoundUp(80, resolutionStep) * resolutionStep;
            int maximumResolution = 1024 / resolutionStep * resolutionStep;
            lodResolution = Mathf.Clamp(
                Mathf.RoundToInt(lodResolution / (float)resolutionStep) * resolutionStep,
                minimumResolution,
                maximumResolution);
        }

        private void OnEnable()
        {
            WaterSurfaceRegistry.Register(this);
            WaterBuoyancyScheduler.InvalidateLayout();

#if UNITY_EDITOR
            m_LastPrefabStageVisibility = true;
            PrefabStage.prefabStageOpened += RegisterPrefabStageVisibilityCallback;

            if (PrefabStageUtility.GetCurrentPrefabStage() != null)
                RegisterPrefabStageVisibilityCallback();
#endif
        }

        private void OnDisable()
        {
            WaterSurfaceRegistry.Unregister(this);
            WaterBuoyancyScheduler.InvalidateLayout();
            ReleaseResources();

#if UNITY_EDITOR
            UnregisterPrefabStageVisibilityCallback();
            PrefabStage.prefabStageOpened -= RegisterPrefabStageVisibilityCallback;
#endif
        }

        private void FixedUpdate()
        {
            if (Application.isPlaying)
                WaterBuoyancyScheduler.ExecuteFixedStep();
        }

#if UNITY_EDITOR
        private void RegisterPrefabStageVisibilityCallback(PrefabStage _ = null)
        {
            SceneView.duringSceneGui -= UpdatePrefabStageVisibility;
            SceneView.duringSceneGui += UpdatePrefabStageVisibility;
            UpdatePrefabStageVisibility(null);
        }

        private void UnregisterPrefabStageVisibilityCallback()
            => SceneView.duringSceneGui -= UpdatePrefabStageVisibility;

        private bool m_LastPrefabStageVisibility = true;

        private void UpdatePrefabStageVisibility(SceneView _)
        {
            bool isVisible = true;
            var stage = PrefabStageUtility.GetCurrentPrefabStage();

            if (stage)
            {
                bool isInPrefabStage = gameObject.scene == stage.scene;

                if (!isInPrefabStage && stage.mode == PrefabStage.Mode.InIsolation)
                    isVisible = false;
                if (!isInPrefabStage && CoreUtils.IsSceneViewPrefabStageContextHidden())
                    isVisible = false;
            }

            if (m_LastPrefabStageVisibility == isVisible)
                return;

            if (isVisible)
                WaterSurfaceRegistry.Register(this);
            else
                WaterSurfaceRegistry.Unregister(this);

            m_LastPrefabStageVisibility = isVisible;
        }
#endif

        internal bool IsInstancedQuads() => IsInfinite() || geometryType == WaterGeometryType.InstancedQuads;

        internal bool IsInfinite() =>
            surfaceType == WaterSurfaceType.OceanSeaLake && geometryType == WaterGeometryType.Infinite;

        internal bool IsProceduralGeometry() => !IsCustomMesh();
        internal bool IsCustomMesh() => geometryType == WaterGeometryType.CustomMesh && meshRenderers.Count != 0;
        internal float3 GetUpVector() => transform.up;

        internal void ReleaseResources()
        {

            WaterSurfaceRegistry.RetireResources(simulation, simulationConstantBuffer, spectrumConstantBuffer, surfaceConstantBuffer);
            simulation = null;
            simulationConstantBuffer = null;
            spectrumConstantBuffer = null;
            surfaceConstantBuffer = null;
            propertyBlock = null;
            surfaceIndex = -1;
            m_LastSimulationFrame = -1;
        }

        public float currentSpeed;
        [Range(-180.0f, 180.0f)] public float currentDirection;
        public Texture currentMap;
        public Vector2 currentRegionExtent = new(100.0f, 100.0f);
        public Vector2 currentRegionOffset = new(0.0f, 0.0f);
        [Range(0, 1)] public float currentMapInfluence = 1.0f;

        internal Texture GetCurrentMapTexture(Texture defaultValue = null) => currentMap ? currentMap : defaultValue;

        public Texture simulationMask;
        public Vector2 simulationMaskRemap = new(0.0f, 1.0f);
        public Vector2 simulationMaskExtent = new(100.0f, 100.0f);
        public Vector2 simulationMaskOffset = new(0.0f, 0.0f);

        internal Texture GetSimulationMaskTexture(Texture defaultValue = null) => simulationMask ? simulationMask : defaultValue;

        public enum FoamResolution
        {
            [InspectorName("Low 256")] Resolution256 = 256,
            [InspectorName("Medium 512")] Resolution512 = 512,
            [InspectorName("High 1024")] Resolution1024 = 1024,
            [InspectorName("Very High 2048")] Resolution2048 = 2048,
        }

        public bool foamEnabled = true;
        public FoamResolution foamResolution = FoamResolution.Resolution1024;
        [Range(15, 200)] public int foamSimulationFrequency = 30;
        public bool foamPrewarmingEnabled = true;
        public Texture2D foamTexture;
        [Range(0.0f, 1.0f)] public float foamCurrentInfluence = 0.6f;
        [Min(0.001f)] public float foamTextureTiling = 0.2f;
        [Range(0.001f, 1.0f)] public float foamFeather = 0.4f;
        [Range(0.0f, 12.0f)] public float foamNormalStrength = 3.5f;
        public Color foamColor = Color.white;
        [Range(0.0f, 1.0f)] public float foamSmoothness = 0.15f;
        [Range(0.0f, 1.0f)] public float shorelineFoamStrength;
        [Min(0.001f)] public float shorelineFoamDistance = 100.0f;
        [Min(0.0f)] public float shorelineFoamWaves = 5.0f;
        [Min(0.0f)] public float shorelineFoamSpeed = 1.0f;
        [Min(0.0f)] public float shorelineFoamNoiseIntensity = 0.5f;
        [Min(0.001f)] public float shorelineFoamNoiseScale = 10.0f;
        public Texture2D shorelineFoamNoiseMap;
        [Range(0, WaterLimits.MaxLodCount - 2)] public int waveFoamMinimumLod = 2;
        public Color deepFoamColor = new(0.28f, 0.38f, 0.38f, 1.0f);
        [Range(0.0f, 4.0f)] public float deepFoamStrength = 1.0f;
        public Color bubbleFoamColor = new(0.64f, 0.83f, 0.82f, 1.0f);
        [Range(0.0f, 5.0f)] public float bubbleFoamStrength = 1.68f;
        [Range(0.0f, 0.5f)] public float bubbleFoamParallax = 0.14f;

        internal bool HasSimulationFoam() => simulationEnabled && foamEnabled;

        public WaterCascadeParameters cascade0 = WaterCascadeParameters.Create(
            new Vector2(88.0f, 88.0f), 1.0f, 1.0f, 10.0f, 20.0f, 150.0f, 0.8f, 0.2f, 1.0f, 0.5f, 8.0f);
        public WaterCascadeParameters cascade1 = WaterCascadeParameters.Create(
            new Vector2(57.0f, 57.0f), 0.75f, 1.0f, 5.0f, 15.0f, 150.0f, 0.8f, 0.4f, 1.0f, 0.5f, 0.0f);
        public WaterCascadeParameters cascade2 = WaterCascadeParameters.Create(
            new Vector2(16.0f, 16.0f), 0.0f, 0.25f, 20.0f, 20.0f, 550.0f, 0.8f, 0.4f, 1.0f, 0.25f, 3.0f);


        public float simulationTime
        {
            get => simulation?.simulationTime ?? 0.0f;
            set => simulation?.ResetSimulationTime(value);
        }

        internal int activeCascadeCount => WaterSimulationLayout.CascadeCount;
        internal WaterSimulationState simulation;

        [NonSerialized] private WaterSpectrumParameters m_CachedSpectrumParameters;
        [NonSerialized] private Vector3 m_CachedCascadeVariance;
        [NonSerialized] private Vector3 m_CachedFoamWhitecap = new(float.NaN, float.NaN, float.NaN);
        [NonSerialized] private int m_CachedSpectrumHash = int.MinValue;
        [NonSerialized] private bool m_TransitionNextSpectrumUpdate;
        [NonSerialized] private int m_LastSimulationFrame = -1;

        public void TransitionSpectrumToCurrentState()
        {
            m_TransitionNextSpectrumUpdate = true;
            m_CachedSpectrumHash = int.MinValue;
        }

        internal void EnsureSimulationResources(int simulationResolution, Texture foamTexture, Texture physicalFoamTexture)
        {
            simulationConstantBuffer ??= new GraphicsBuffer(GraphicsBuffer.Target.Constant, 1,
                UnsafeUtility.SizeOf<WaterSimulationShaderVariables>());
            spectrumConstantBuffer ??= new GraphicsBuffer(GraphicsBuffer.Target.Constant, 1,
                UnsafeUtility.SizeOf<WaterSpectrumShaderVariables>());
            surfaceConstantBuffer ??= new GraphicsBuffer(GraphicsBuffer.Target.Constant, 1,
                UnsafeUtility.SizeOf<WaterSurfaceShaderVariables>());

            int cascadeCount = activeCascadeCount;
            int foamResolutionValue = (int)foamResolution;
            bool enableFoamResources = HasSimulationFoam();

            if (simulation != null && !simulation.MatchesConfiguration(simulationResolution, cascadeCount))
            {
                simulation.Release();
                simulation = null;
            }

            bool simulationCreated = false;
            if (simulation == null)
            {
                simulation = new WaterSimulationState();
                simulation.Initialize(simulationResolution, cascadeCount);
                simulationCreated = true;
            }

            simulation.ConfigureFoam(foamResolutionValue, lodLevels, enableFoamResources);
            if (simulationCreated || propertyBlock == null)
                CreatePropertyBlock(foamTexture, physicalFoamTexture);

            WaterSpectrumParameters spectrum = EvaluateSpectrumParameters(simulationResolution);
            if (simulation.spectrum != spectrum)
            {
                if (m_TransitionNextSpectrumUpdate)
                    simulation.SetSpectrum(spectrum);
                else
                    simulation.SetSpectrumImmediate(spectrum);
            }
            m_TransitionNextSpectrumUpdate = false;
        }

        internal void PrepareSimulationFrame(int simulationResolution, Texture foamTexture, Texture physicalFoamTexture)
        {
            if (Application.isPlaying && m_LastSimulationFrame == Time.frameCount)
                return;
            EnsureSimulationResources(simulationResolution, foamTexture, physicalFoamTexture);
            simulation?.foam.SetSimulationFrequency(foamSimulationFrequency);
            UpdateSimulation(simulationEnabled ? simulationTimeScale : 0.0f);
            if (Application.isPlaying)
                m_LastSimulationFrame = Time.frameCount;
        }

        private void UpdateSimulation(float simulationTimeScale)
        {
            if (simulation == null)
                return;
            simulation.Update(simulationTimeScale);
            WaterSimulationRenderingParameters previousRendering = simulation.renderingParameters;
            WaterSimulationRenderingParameters rendering = EvaluateSimulationRenderingParameters();
            if (previousRendering.cascade0.displacementScale != rendering.cascade0.displacementScale
                || previousRendering.cascade1.displacementScale != rendering.cascade1.displacementScale
                || previousRendering.cascade2.displacementScale != rendering.cascade2.displacementScale)
                simulation.waveFieldUpdateRequired = true;
            Vector3 foamWhitecap = new(cascade0.whitecap, cascade1.whitecap, cascade2.whitecap);
            if (m_CachedFoamWhitecap != foamWhitecap)
            {
                m_CachedFoamWhitecap = foamWhitecap;
                simulation.waveFieldUpdateRequired = true;
            }
            simulation.renderingParameters = rendering;
            if (simulationEnabled)
                return;
            for (int cascadeIndex = 0; cascadeIndex < WaterLimits.CascadeCount; ++cascadeIndex)
            {
                WaterCascadeRenderingParameters cascade = rendering.GetCascade(cascadeIndex);
                cascade.displacementScale = 0.0f;
                cascade.normalScale = 0.0f;
                rendering.SetCascade(cascadeIndex, cascade);
            }
            rendering.maxWaveHeight = 0.0f;
            rendering.maxWaveDisplacement = 0.0f;
            simulation.renderingParameters = rendering;
        }

        internal static void EvaluateTransformMatrices(bool usesCustomMesh, Vector3 position, Quaternion rotation,
            ref float4x4 waterToWorld, ref float4x4 worldToWater)
        {
            if (usesCustomMesh)
            {
                waterToWorld = Matrix4x4.identity;
                worldToWater = Matrix4x4.identity;
            }
            else
            {
                waterToWorld = Matrix4x4.TRS(position, rotation, Vector3.one);
                worldToWater = inverse(waterToWorld);
            }
        }

        private static int HashFloat(int hash, float value)
        {
            unchecked
            {
                return hash * 31 + value.GetHashCode();
            }
        }

        private static int HashCascadeSpectrum(int hash, WaterCascadeParameters cascade)
        {
            unchecked
            {
                hash = HashFloat(hash, cascade.tileLength.x);
                hash = HashFloat(hash, cascade.tileLength.y);
                hash = HashFloat(hash, cascade.windSpeed);
                hash = HashFloat(hash, cascade.windDirection);
                hash = HashFloat(hash, cascade.fetchLength);
                hash = HashFloat(hash, cascade.swell);
                hash = HashFloat(hash, cascade.spread);
                return HashFloat(hash, cascade.detail);
            }
        }

        private int ComputeSpectrumInputHash(int resolution)
        {
            unchecked
            {
                int hash = 17 * 31 + resolution;
                hash = HashCascadeSpectrum(hash, cascade0);
                hash = HashCascadeSpectrum(hash, cascade1);
                return HashCascadeSpectrum(hash, cascade2);
            }
        }

        private WaterSpectrumParameters EvaluateSpectrumParameters(int resolution)
        {
            int inputHash = ComputeSpectrumInputHash(resolution);
            if (inputHash == m_CachedSpectrumHash)
                return m_CachedSpectrumParameters;

            WaterSpectrumParameters spectrum = new WaterSpectrumParameters
            {
                cascade0 = WaterCascadeSpectrumParameters.Create(cascade0, 0),
                cascade1 = WaterCascadeSpectrumParameters.Create(cascade1, 1),
                cascade2 = WaterCascadeSpectrumParameters.Create(cascade2, 2)
            };
            m_CachedSpectrumHash = inputHash;
            m_CachedSpectrumParameters = spectrum;
            m_CachedCascadeVariance = WaterSpectrumMath.EstimateCascadeVariance(spectrum, resolution);
            return spectrum;
        }

        private static WaterCascadeParameters GetCascade(WaterSurface surface, int index) =>
            index == 0 ? surface.cascade0 : index == 1 ? surface.cascade1 : surface.cascade2;

        private WaterSimulationRenderingParameters EvaluateSimulationRenderingParameters()
        {
            WaterSimulationRenderingParameters rendering = new WaterSimulationRenderingParameters();
            float currentSpeedMetersPerSecond = currentSpeed * WaterConstants.k_KilometerPerHourToMeterPerSecond;
            for (int cascadeIndex = 0; cascadeIndex < WaterLimits.CascadeCount; ++cascadeIndex)
            {
                WaterCascadeParameters source = GetCascade(this, cascadeIndex);
                float2 tileLength = new float2(
                    Mathf.Max(source.tileLength.x, 0.001f),
                    Mathf.Max(source.tileLength.y, 0.001f));
                float worldTileLength = math.max(tileLength.x, tileLength.y);
                float fadeStartMultiplier = cascadeIndex == 1 ? 4.0f : 3.0f;
                float fadeStart = fadeStartMultiplier * worldTileLength;
                float fadeDistance = 4.0f * fadeStart;
                float fadeScale = -1.0f / Mathf.Max(fadeDistance, 0.001f);
                rendering.SetCascade(cascadeIndex, new WaterCascadeRenderingParameters
                {
                    inverseTileLength = 1.0f / tileLength,
                    displacementScale = Mathf.Max(source.displacementScale, 0.0f),
                    normalScale = Mathf.Max(source.normalScale, 0.0f),
                    currentSpeed = currentSpeedMetersPerSecond,
                    distanceFadeScale = fadeScale,
                    distanceFadeBias = 1.0f - fadeStart * fadeScale
                });
            }

            float variance0 = Mathf.Max(m_CachedCascadeVariance.x, 0.0f) * cascade0.displacementScale * cascade0.displacementScale;
            float variance1 = Mathf.Max(m_CachedCascadeVariance.y, 0.0f) * cascade1.displacementScale * cascade1.displacementScale;
            float variance2 = Mathf.Max(m_CachedCascadeVariance.z, 0.0f) * cascade2.displacementScale * cascade2.displacementScale;
            float standardDeviation = Mathf.Sqrt(variance0 + variance1 + variance2);
            rendering.maxWaveHeight = Mathf.Max(0.25f, 6.0f * standardDeviation);
            rendering.maxWaveDisplacement = Mathf.Max(0.25f, 8.5f * standardDeviation);
            EvaluateTransformMatrices(IsCustomMesh(), transform.position, transform.rotation,
                ref rendering.waterToWorldMatrix, ref rendering.worldToWaterMatrix);
            return rendering;
        }

        internal Vector4 EvaluateFoamWhitecapParameters() => new(
            Mathf.Max(cascade0.whitecap, 0.0f),
            Mathf.Max(cascade1.whitecap, 0.0f),
            Mathf.Max(cascade2.whitecap, 0.0f),
            0.0f);

        internal Vector4 EvaluateFoamAmountParameters() => new(
            Mathf.Max(cascade0.foamAmount, 0.0f),
            Mathf.Max(cascade1.foamAmount, 0.0f),
            Mathf.Max(cascade2.foamAmount, 0.0f),
            0.0f);

        internal Vector4 EvaluateFoamAppearanceParameters() => new(
            Mathf.Clamp01(foamSmoothness),
            Mathf.Max(foamFeather, 0.001f),
            Mathf.Max(foamNormalStrength, 0.0f),
            Mathf.Max(foamTextureTiling, 0.001f));

        internal Vector4 EvaluateFoamVolumeAppearanceParameters() => new(
            Mathf.Clamp(bubbleFoamParallax, 0.0f, 0.5f),
            Mathf.Max(deepFoamStrength, 0.0f),
            Mathf.Max(bubbleFoamStrength, 0.0f),
            0.0f);
    }
}
