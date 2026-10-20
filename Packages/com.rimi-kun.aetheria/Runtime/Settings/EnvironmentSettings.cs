using UnityEngine;
using UnityEngine.Rendering;

namespace Aetheria.Runtime.Rendering
{
    internal static class EnvironmentSettings
    {
        private const string DefaultSkyboxMaterialName = "Default-Skybox.mat";

        internal static bool StaticLightingBindingActive { get; set; }
        internal static VolumeStack StaticLightingVolumeStack { get; set; }
        internal static Vector3 StaticLightingObserverPositionWs { get; set; }

#if UNITY_EDITOR
        private static bool _editorShutdownInProgress;
#endif

        private static bool _ownsReflectionOverride;
        private static DefaultReflectionMode _previousReflectionMode;
        private static Texture _previousReflectionTexture;
        private static Texture _activeReflectionTexture;

        internal static void ApplyDefaults()
        {
            if (!CanAccessRenderSettings || StaticLightingBindingActive)
                return;

            ApplyDefaultSkybox();
            ApplyDefaultAmbientSource();
            ApplyDefaultReflections();
        }

        private static void ApplyDefaultSkybox()
        {
            if (!CanAccessRenderSettings)
                return;

            var defaultSkybox = LoadDefaultSkybox();

            if (RenderSettings.skybox == defaultSkybox)
                return;

            RenderSettings.skybox = defaultSkybox;
            RepaintSceneViews();
        }

        internal static void ApplyDefaultAmbientSource()
        {
            if (!CanAccessRenderSettings || StaticLightingBindingActive) return;
            if (RenderSettings.ambientMode == AmbientMode.Skybox) return;

            RenderSettings.ambientMode = AmbientMode.Skybox;
        }

        internal static void ApplyDefaultReflections()
        {
            if (!CanAccessRenderSettings) return;
            if (!_ownsReflectionOverride) return;

            bool stillOwnsCurrentBinding =
                RenderSettings.defaultReflectionMode == DefaultReflectionMode.Custom &&
                RenderSettings.customReflectionTexture == _activeReflectionTexture;
            bool changed = false;

            if (stillOwnsCurrentBinding)
            {
                changed =
                    RenderSettings.defaultReflectionMode != _previousReflectionMode ||
                    RenderSettings.customReflectionTexture != _previousReflectionTexture;
                RenderSettings.defaultReflectionMode = _previousReflectionMode;
                RenderSettings.customReflectionTexture = _previousReflectionTexture;
            }

            _previousReflectionTexture = null;
            _activeReflectionTexture = null;
            _ownsReflectionOverride = false;

            if (changed)
                RepaintSceneViews();
        }

        internal static void ApplySkybox(Material skyboxMaterial)
        {
            if (!CanAccessRenderSettings || StaticLightingBindingActive)
                return;

            if (!skyboxMaterial)
            {
                ApplyDefaultSkybox();
                return;
            }

            if (RenderSettings.skybox == skyboxMaterial)
                return;

            RenderSettings.skybox = skyboxMaterial;
            RepaintSceneViews();
        }

        internal static void ApplyReflection(RenderTexture reflectionTexture)
        {
            if (!CanAccessRenderSettings)
                return;

            if (!reflectionTexture)
            {
                ApplyDefaultReflections();
                return;
            }

            if (!_ownsReflectionOverride)
            {
                _previousReflectionMode = RenderSettings.defaultReflectionMode;
                _previousReflectionTexture = RenderSettings.customReflectionTexture;
                _ownsReflectionOverride = true;
            }

            bool changed =
                RenderSettings.defaultReflectionMode != DefaultReflectionMode.Custom ||
                RenderSettings.customReflectionTexture != reflectionTexture;

            RenderSettings.defaultReflectionMode = DefaultReflectionMode.Custom;
            RenderSettings.customReflectionTexture = reflectionTexture;
            
            _activeReflectionTexture = reflectionTexture;

            if (changed)
                RepaintSceneViews();
        }

        internal static void ApplyAmbientProbe(SphericalHarmonicsL2 ambientProbe)
        {
            if (!CanAccessRenderSettings || StaticLightingBindingActive)
                return;

            RenderSettings.ambientMode = AmbientMode.Custom;
            RenderSettings.ambientProbe = ambientProbe;
        }


#if UNITY_EDITOR
        internal static void PrepareForEditorShutdown()
        {
            if (_editorShutdownInProgress)
                return;

            ApplyDefaults();
            _editorShutdownInProgress = true;
        }
        
        internal static void CancelEditorShutdown() =>
            _editorShutdownInProgress = false;

        internal static void ConfirmEditorShutdown() =>
            _editorShutdownInProgress = true;
#endif

        private static bool CanAccessRenderSettings
        {
            get
            {
#if UNITY_EDITOR
                return !_editorShutdownInProgress;
#else
                return true;
#endif
            }
        }

        private static Material LoadDefaultSkybox()
        {
#if UNITY_EDITOR
            return UnityEditor.AssetDatabase.GetBuiltinExtraResource<Material>(
                DefaultSkyboxMaterialName);
#else
            return null;
#endif
        }

        private static void RepaintSceneViews()
        {
#if UNITY_EDITOR
            if (!Application.isPlaying)
                UnityEditor.SceneView.RepaintAll();
#endif
        }
    }
}