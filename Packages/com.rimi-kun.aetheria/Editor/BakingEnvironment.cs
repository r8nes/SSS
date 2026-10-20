using System.Collections.Generic;
using Aetheria.Runtime;
using Aetheria.Runtime.Components;
using Aetheria.Runtime.Overrides;
using Aetheria.Runtime.Sky;
using Aetheria.Runtime.Rendering;
using UnityEditor;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace Aetheria.Editor
{
    [InitializeOnLoad]
    internal static class BakingEnvironment
    {
        private sealed class CameraSkyboxState
        {
            public Skybox Component;
            public Material PreviousMaterial;
            public bool AddedComponent;
        }

        private const string CameraName = "Aetheria Static Lighting Camera";
        private const string TargetName = "Aetheria Static Lighting Camera Target";
        private const string MaterialName = "Aetheria Static Lighting Skybox";

        private static readonly int Tex = Shader.PropertyToID("_Tex");

        private static Material _material;
        private static bool _rendering;
        private static bool _ownsRenderSettings;
        private static Material _previousSkybox;
        private static float _previousAmbientIntensity;
        private static AmbientMode _previousAmbientMode;
        private static SphericalHarmonicsL2 _previousAmbientProbe;
        private static float _previousReflectionIntensity;
        private static Texture _previousCustomReflectionTexture;
        private static readonly Dictionary<int, CameraSkyboxState> CameraSkyboxes = new();
        private static AetheriaFeature _visualSkyboxFeature;
        private static VolumeStack _staticLightingVolumeStack;

        static BakingEnvironment()
        {
            Lightmapping.bakeStarted -= OnBakeStarted;
            Lightmapping.bakeStarted += OnBakeStarted;
            Lightmapping.bakeCompleted -= OnBakeFinished;
            Lightmapping.bakeCompleted += OnBakeFinished;
            Lightmapping.bakeCancelled -= OnBakeFinished;
            Lightmapping.bakeCancelled += OnBakeFinished;
            StandaloneProbeBaking.preBake -= OnStandaloneProbePreBake;
            StandaloneProbeBaking.preBake += OnStandaloneProbePreBake;
            StandaloneProbeBaking.postBake -= OnStandaloneProbePostBake;
            StandaloneProbeBaking.postBake += OnStandaloneProbePostBake;
            AssemblyReloadEvents.beforeAssemblyReload -= Cleanup;
            AssemblyReloadEvents.beforeAssemblyReload += Cleanup;
            EditorApplication.quitting -= Cleanup;
            EditorApplication.quitting += Cleanup;
            RenderPipelineManager.beginCameraRendering -= OnBeginCameraRendering;
            RenderPipelineManager.beginCameraRendering += OnBeginCameraRendering;
            GlobalIlluminationUtils.Install();
        }

        private static void OnBakeStarted() => Prepare(false, true);

        private static bool OnStandaloneProbePreBake(StandaloneProbeBakeType bakeType) =>
            Prepare(bakeType == StandaloneProbeBakeType.ReflectionProbes, false);

        private static void OnStandaloneProbePostBake(StandaloneProbeBakeType _) => OnBakeFinished();

        private static void OnBakeFinished()
        {
            RestoreRenderSettings();
            Runtime.Aetheria.RequestEnvironmentUpdate();
            SceneView.RepaintAll();
        }

        private static bool Prepare(bool reflectionCapture, bool cancelLightmappingOnFailure)
        {
            if (_rendering) return false;

            GlobalIlluminationUtils.Install();

            var activeScene = SceneManager.GetActiveScene();

            if (!StaticLightingSky.TryGet(activeScene, out var staticSky))
            {
                return FailBake(
                    $"Aetheria: scene '{activeScene.name}' has no active Static Lighting Sky. " +
                    "Add Aetheria Static Lighting Sky and assign the Volume profile that must " +
                    "be used for lightmaps, light probes, and APV baking.",
                    cancelLightmappingOnFailure);
            }

            if (!ValidateProfile(staticSky))
            {
                if (cancelLightmappingOnFailure)
                    Lightmapping.Cancel();
                return false;
            }

            if (!TryFindRendererFeature(out var feature, out int rendererIndex))
            {
                return FailBake(
                    "Aetheria: the active URP Asset has no renderer containing an active " +
                    "Aetheria Feature with complete environment-lighting resources.",
                    cancelLightmappingOnFailure);
            }

            SaveRenderSettings();

            EnvironmentSettings.StaticLightingBindingActive = false;
            ReleaseStaticLightingVolumeStack();

            var bakeVolumeStack = BuildVolumeStack(staticSky.Profile);
            var bakeSky = SkyManager.GetSkySetting(bakeVolumeStack);
            bool includeSun = bakeSky != null && bakeSky.includeSunInBaking.value;
            if (reflectionCapture && staticSky.IncludeSunInReflectionBaking)
                includeSun = true;

            EnvironmentSettings.StaticLightingObserverPositionWs = staticSky.ObserverPositionWs;

            if (!TryRenderStaticEnvironment(
                    feature,
                    rendererIndex,
                    staticSky,
                    bakeVolumeStack,
                    includeSun,
                    out var radianceCubemap))
            {
                VolumeManager.instance.DestroyStack(bakeVolumeStack);
                RestoreRenderSettings();
                return FailBake(
                    "Aetheria: static lighting environment preparation failed. The lighting " +
                    "bake was cancelled instead of continuing with an incomplete sky.",
                    cancelLightmappingOnFailure);
            }

            SetStaticLightingVolumeStack(bakeVolumeStack);

            var skyboxMaterial = EnsureSkyboxMaterial();

            if (!skyboxMaterial)
            {
                RestoreRenderSettings();
                if (cancelLightmappingOnFailure)
                    Lightmapping.Cancel();
                return false;
            }

            skyboxMaterial.SetTexture(Tex, radianceCubemap);

            EnvironmentSettings.StaticLightingBindingActive = true;

            RenderSettings.skybox = skyboxMaterial;
            RenderSettings.ambientIntensity = 1.0f;
            RenderSettings.ambientMode = AmbientMode.Skybox;
            RenderSettings.reflectionIntensity = 1.0f;
            RenderSettings.customReflectionTexture = null;
            DynamicGI.UpdateEnvironment();

            _visualSkyboxFeature = feature;
            RefreshVisualCameraSkyboxes();
            SceneView.RepaintAll();
            return true;
        }

        private static bool ValidateProfile(StaticLightingSky staticSky)
        {
            if (!staticSky.Profile)
            {
                Debug.LogError(
                    "Aetheria: Static Lighting Sky has no Volume profile assigned.",
                    staticSky);
                return false;
            }

            if (!staticSky.Profile.TryGet<VisualEnvironment>(out var visualEnvironment) ||
                !visualEnvironment || !visualEnvironment.active)
            {
                Debug.LogError(
                    $"Aetheria: Volume profile '{staticSky.Profile.name}' does not contain " +
                    "an active Visual Environment override.",
                    staticSky);
                return false;
            }

            if (!SkyManager.SkyTypes.TryGetValue(visualEnvironment.skyType.value, out var skyType))
            {
                Debug.LogError(
                    $"Aetheria: Visual Environment in '{staticSky.Profile.name}' selects " +
                    $"an unknown sky type ID {visualEnvironment.skyType.value}.",
                    staticSky);
                return false;
            }

            for (int i = 0; i < staticSky.Profile.components.Count; i++)
            {
                var component = staticSky.Profile.components[i];
                if (component && component.GetType() == skyType &&
                    component is SkySettings sky && sky.IsActive())
                    return true;
            }

            Debug.LogError(
                $"Aetheria: Volume profile '{staticSky.Profile.name}' does not contain " +
                $"an active '{skyType.Name}' override selected by Visual Environment.",
                staticSky);
            return false;
        }

        private static bool TryFindRendererFeature(
            out AetheriaFeature feature,
            out int rendererIndex)
        {
            feature = null;
            rendererIndex = -1;

            if (GraphicsSettings.currentRenderPipeline is not UniversalRenderPipelineAsset asset)
                return false;

            var rendererDataList = asset.rendererDataList;

            for (int index = 0; index < rendererDataList.Length; index++)
            {
                var rendererData = rendererDataList[index];

                if (!rendererData) continue;

                for (int j = 0; j < rendererData.rendererFeatures.Count; j++)
                {
                    var candidate = rendererData.rendererFeatures[j];

                    if (candidate is not AetheriaFeature aetheria ||
                        !aetheria ||
                        !aetheria.isActive ||
                        !aetheria.ResourcesAsset)
                        continue;

                    asset.GetRenderer(index);

                    if (!aetheria.PipelineReady) continue;

                    feature = aetheria;
                    rendererIndex = index;
                    return true;
                }
            }

            return false;
        }

        private static bool TryRenderStaticEnvironment(
            AetheriaFeature feature,
            int rendererIndex,
            StaticLightingSky staticSky,
            VolumeStack volumeStack,
            bool includeSun,
            out RenderTexture radianceCubemap)
        {
            radianceCubemap = null;
            _rendering = true;

            GameObject cameraObject = null;
            RenderTexture cameraTarget = null;

            try
            {
                cameraObject = new GameObject(CameraName)
                {
                    hideFlags = HideFlags.HideAndDontSave
                };

                var camera = cameraObject.AddComponent<Camera>();
                camera.enabled = false;
                camera.cameraType = CameraType.Reflection;
                camera.clearFlags = CameraClearFlags.Nothing;
                camera.cullingMask = ~0;
                camera.allowHDR = true;
                camera.allowMSAA = false;
                camera.nearClipPlane = 0.1f;
                camera.farClipPlane = 1.0f;
                camera.transform.SetPositionAndRotation(
                    staticSky.ObserverPositionWs,
                    Quaternion.identity);
                cameraObject.AddComponent<StaticLightingCaptureCamera>();

                var cameraData = cameraObject.AddComponent<UniversalAdditionalCameraData>();
                cameraData.renderPostProcessing = false;
                cameraData.renderShadows = false;
                cameraData.requiresColorTexture = false;
                cameraData.requiresDepthTexture = false;
                cameraData.SetRenderer(rendererIndex);

                camera.SetVolumeFrameworkUpdateMode(VolumeFrameworkUpdateMode.ViaScripting);
                cameraData.volumeStack = volumeStack;

                cameraTarget = new RenderTexture(new RenderTextureDescriptor(1, 1)
                {
                    graphicsFormat = GraphicsFormat.R16G16B16A16_SFloat,
                    depthStencilFormat = SystemInfo.GetGraphicsFormat(DefaultFormat.DepthStencil),
                    msaaSamples = 1
                })
                {
                    name = TargetName,
                    hideFlags = HideFlags.HideAndDontSave
                };

                if (!cameraTarget.Create())
                    return false;

                camera.targetTexture = cameraTarget;

                if (!RenderAndSynchronizeStaticEnvironment(
                        feature,
                        camera,
                        staticSky,
                        includeSun,
                        out _))
                    return false;


                return RenderAndSynchronizeStaticEnvironment(
                    feature,
                    camera,
                    staticSky,
                    includeSun,
                    out radianceCubemap);
            }
            finally
            {
                if (cameraTarget)
                {
                    cameraTarget.Release();
                    CoreUtils.Destroy(cameraTarget);
                }

                if (cameraObject)
                    CoreUtils.Destroy(cameraObject);

                _rendering = false;
            }
        }

        private static bool RenderAndSynchronizeStaticEnvironment(
            AetheriaFeature feature,
            Camera camera,
            StaticLightingSky staticSky,
            bool includeSun,
            out RenderTexture radianceCubemap)
        {
            radianceCubemap = null;

            ulong previousGeneration = feature.PrepareStaticLightingCapture(
                staticSky.Resolution,
                includeSun,
                staticSky.ObserverPositionWs);

            camera.Render();

            if (!feature.IsStaticLightingSkyPrepared(camera))
            {
                Debug.LogError(
                    "Aetheria: static lighting sky preparation did not complete for the baking camera.");
                return false;
            }

            if (feature.TryGetStaticLightingRadiance(
                    previousGeneration,
                    out radianceCubemap)) return WaitForRadianceGpuCompletion(radianceCubemap);

            Debug.LogError(
                "Aetheria: static lighting environment did not produce a new radiance " +
                "generation for the baking camera.");

            return false;
        }

        private static bool WaitForRadianceGpuCompletion(RenderTexture radianceCubemap)
        {
            if (!radianceCubemap || !radianceCubemap.IsCreated())
                return false;

            if (!SystemInfo.supportsAsyncGPUReadback)
            {
                Debug.LogError(
                    "Aetheria: this graphics device does not support AsyncGPUReadback, " +
                    "which is required to synchronize the static lighting environment before baking.");
                return false;
            }

            if (!SystemInfo.IsFormatSupported(
                    radianceCubemap.graphicsFormat,
                    GraphicsFormatUsage.ReadPixels))
            {
                Debug.LogError(
                    $"Aetheria: static lighting radiance format {radianceCubemap.graphicsFormat} " +
                    "does not support GPU readback on this device.");
                return false;
            }

            var request = AsyncGPUReadback.Request(
                radianceCubemap,
                0,
                0,
                1,
                0,
                1,
                0,
                1,
                static _ => { });
            request.WaitForCompletion();

            if (!request.hasError)
                return true;

            Debug.LogError(
                "Aetheria: GPU synchronization failed while preparing the static lighting " +
                "radiance cubemap.");
            return false;
        }

        private static VolumeStack BuildVolumeStack(VolumeProfile profile)
        {
            var stack = VolumeManager.instance.CreateStack();

            for (int i = 0; i < profile.components.Count; i++)
            {
                var source = profile.components[i];

                if (!source || source is FogVolume)
                    continue;

                var destination = stack.GetComponent(source.GetType());

                if (destination)
                    source.Override(destination, 1.0f);
            }

            return stack;
        }

        private static Material EnsureSkyboxMaterial()
        {
            if (_material)
                return _material;

            var skyboxShader = Shader.Find("Skybox/Cubemap");

            if (!skyboxShader)
            {
                Debug.LogError(
                    "Aetheria: unable to create the static lighting skybox because " +
                    "the built-in Skybox/Cubemap shader was not found.");
                return null;
            }

            _material = CoreUtils.CreateEngineMaterial(skyboxShader);
            _material.name = MaterialName;
            _material.hideFlags = HideFlags.HideAndDontSave;

            return _material;
        }

        private static void OnBeginCameraRendering(
            ScriptableRenderContext _,
            Camera camera)
        {
            if (!EnvironmentSettings.StaticLightingBindingActive ||
                !_visualSkyboxFeature ||
                !IsVisualCamera(camera))
                return;

            EnsureVisualCameraSkybox(camera);
        }

        private static void RefreshVisualCameraSkyboxes()
        {
            if (!_visualSkyboxFeature) return;

            for (int i = 0; i < Camera.allCameras.Length; i++)
            {
                var camera = Camera.allCameras[i];

                if (IsVisualCamera(camera))
                    EnsureVisualCameraSkybox(camera);
            }

            for (int i = 0; i < SceneView.sceneViews.Count; i++)
            {
                var sceneView = (SceneView)SceneView.sceneViews[i];

                if (sceneView && IsVisualCamera(sceneView.camera))
                    EnsureVisualCameraSkybox(sceneView.camera);
            }
        }

        private static bool IsVisualCamera(Camera camera) =>
            camera &&
            camera.cameraType is CameraType.Game or CameraType.SceneView &&
            !camera.TryGetComponent<StaticLightingCaptureCamera>(out _);

        private static void EnsureVisualCameraSkybox(Camera camera)
        {
            var visualMaterial = _visualSkyboxFeature.GetSkyboxMaterial(camera);
            if (!visualMaterial)
                return;

            int cameraId = camera.GetInstanceID();

            if (CameraSkyboxes.TryGetValue(cameraId, out var state))
            {
                if (!state.Component)
                {
                    CameraSkyboxes.Remove(cameraId);
                }
                else
                {
                    if (state.Component.material != visualMaterial)
                        state.Component.material = visualMaterial;
                    return;
                }
            }

            var component = camera.GetComponent<Skybox>();

            if (component && component.material)
                return;

            bool addedComponent = !component;
            var previousMaterial = component ? component.material : null;

            if (!component)
            {
                component = camera.gameObject.AddComponent<Skybox>();
                component.hideFlags = HideFlags.HideAndDontSave;
            }

            component.material = visualMaterial;
            CameraSkyboxes.Add(cameraId, new CameraSkyboxState
            {
                Component = component,
                PreviousMaterial = previousMaterial,
                AddedComponent = addedComponent
            });
        }

        private static void RestoreVisualCameraSkyboxes()
        {
            foreach (var state in CameraSkyboxes.Values)
            {
                if (!state.Component)
                    continue;

                if (state.AddedComponent)
                    CoreUtils.Destroy(state.Component);
                else
                    state.Component.material = state.PreviousMaterial;
            }

            CameraSkyboxes.Clear();
            _visualSkyboxFeature = null;
        }

        private static void SetStaticLightingVolumeStack(VolumeStack stack)
        {
            ReleaseStaticLightingVolumeStack();
            _staticLightingVolumeStack = stack;
            EnvironmentSettings.StaticLightingVolumeStack = stack;
        }

        private static void ReleaseStaticLightingVolumeStack()
        {
            EnvironmentSettings.StaticLightingVolumeStack = null;

            if (_staticLightingVolumeStack == null)
                return;

            VolumeManager.instance.DestroyStack(_staticLightingVolumeStack);
            _staticLightingVolumeStack = null;
        }

        private static void SaveRenderSettings()
        {
            if (_ownsRenderSettings) return;

            _previousSkybox = RenderSettings.skybox;
            _previousAmbientIntensity = RenderSettings.ambientIntensity;
            _previousAmbientMode = RenderSettings.ambientMode;
            _previousAmbientProbe = RenderSettings.ambientProbe;
            _previousReflectionIntensity = RenderSettings.reflectionIntensity;
            _previousCustomReflectionTexture = RenderSettings.customReflectionTexture;
            _ownsRenderSettings = true;
        }

        private static void RestoreRenderSettings()
        {
            RestoreVisualCameraSkyboxes();
            EnvironmentSettings.StaticLightingBindingActive = false;
            EnvironmentSettings.StaticLightingObserverPositionWs = Vector3.zero;
            ReleaseStaticLightingVolumeStack();

            if (!_ownsRenderSettings) return;

            RenderSettings.skybox = _previousSkybox;
            RenderSettings.ambientIntensity = _previousAmbientIntensity;
            RenderSettings.ambientMode = _previousAmbientMode;
            RenderSettings.ambientProbe = _previousAmbientProbe;
            RenderSettings.reflectionIntensity = _previousReflectionIntensity;
            RenderSettings.customReflectionTexture = _previousCustomReflectionTexture;

            _previousSkybox = null;
            _previousCustomReflectionTexture = null;
            _ownsRenderSettings = false;

            DynamicGI.UpdateEnvironment();
        }

        private static bool FailBake(string message, bool cancelLightmapping)
        {
            Debug.LogError(message);
            if (cancelLightmapping)
                Lightmapping.Cancel();
            return false;
        }

        private static void Cleanup()
        {
            RestoreRenderSettings();

            if (!_material) return;

            CoreUtils.Destroy(_material);
            _material = null;
        }
    }
}