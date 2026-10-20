using UnityEditor;
using UnityEngine;

namespace Akila.FPSFramework
{
#if UNITY_EDITOR
    [InitializeOnLoad]
#endif
    public class FPSFrameworkSettings
    {
        internal static bool wasSetup
        {
            get
            {
                return wasTagsManagerSetup && wasPhysicsSetup && wasSceneManagerSetup && wasPlayerSetup && wasRPSetup;
            }
        }

        internal static bool wasTagsManagerSetup
        {
            get
            {
                if (preset == null)
                    return false;

                return preset.wasTagsManagerSetup;
            }

            set
            {
                if(preset)
                {
                    preset.wasTagsManagerSetup = value;
#if UNITY_EDITOR
                    EditorUtility.SetDirty(preset);
#endif
                }
            }
        }

        internal static bool wasPhysicsSetup
        {
            get
            {
                if (preset == null)
                    return false;

                return preset.wasPhysicsSetup;
            }

            set
            {
                if(preset)
                {
                    preset.wasPhysicsSetup = value;
#if UNITY_EDITOR
                    EditorUtility.SetDirty(preset);
#endif                
                }
            }
        }

        internal static bool wasSceneManagerSetup
        {
            get
            {
                if (preset == null)
                    return false;

                return preset.wasSceneManagerSetup;
            }

            set
            {
                if(preset)
                {
                    preset.wasSceneManagerSetup = value;
#if UNITY_EDITOR
                    EditorUtility.SetDirty(preset);
#endif
                }
            }
        }

        internal static bool wasPlayerSetup
        {
            get
            {
                if (preset == null)
                    return false;

                return preset.wasPlayerSetup;
            }

            set
            {
                if (preset)
                {
                    preset.wasPlayerSetup = value;
#if UNITY_EDITOR
                    EditorUtility.SetDirty(preset);
#endif                
                }
            }
        }

        internal static bool wasRPSetup
        {
            get
            {
                if (preset == null)
                    return false;

                return preset.wasRPSetup;
            }

            set
            {
                if (preset)
                {
                    preset.wasRPSetup = value;
#if UNITY_EDITOR
                    EditorUtility.SetDirty(preset);
#endif                
                }
            }
        }

        internal static string discordUr
        {
            get
            {
                return "https://discord.gg/aMzVgV66Je";
            }
        }

        internal static string docsUrl
        {
            get
            {
                return "https://akila.gitbook.io/fps-framework/";
            }
        }

        internal static string reviewUrl
        {
            get
            {
                if (preset == null)
                    return "";

                return preset.reviewUrl;
            }
        }

        public static bool shortenMenus
        {
            get
            {
                if(preset != null)
                    return preset.shortenMenus;

                return false;
            }
            set
            {
                if (preset == null) LoadActiveSettings();

                preset.shortenMenus = value;

#if UNITY_EDITOR
                EditorUtility.SetDirty(preset);
#endif
            }
        }

        public static bool automaticUpdatesChecking
        {
            get
            {
                if (preset != null)
                    return preset.automaticUpdatesChecking;

                return false;
            }
            set
            {
                if (preset == null) LoadActiveSettings();

                preset.automaticUpdatesChecking = value;

#if UNITY_EDITOR
                EditorUtility.SetDirty(preset);
#endif
            }
        }

        public static float globalAudioVolume
        {
            get
            {
                if(preset != null)
                    return preset.masterAudioVolume;

                return 1;
            }
            set
            {
                if (preset == null) LoadActiveSettings();

                preset.masterAudioVolume = value;

#if UNITY_EDITOR
                EditorUtility.SetDirty(preset);
#endif
            }
        }

        public static float globalAnimationWeight
        {
            get
            {
                if (preset != null)
                    return preset.globalAnimationWeight;

                return 1;
            }
            set
            {
                if (preset == null) LoadActiveSettings();

                preset.globalAnimationWeight = value;

#if UNITY_EDITOR
                EditorUtility.SetDirty(preset);
#endif
            }
        }

        public static float globalAnimationSpeed
        {
            get
            {
                if(preset != null)
                    return preset.globalAnimationSpeed;

                return 1;
            }
            set
            {
                if (preset == null) LoadActiveSettings();

                preset.globalAnimationSpeed = value;

#if UNITY_EDITOR
                EditorUtility.SetDirty(preset);
#endif
            }
        }

        public static int maxAnimationFramerate
        {
            get
            {
                if(preset != null)
                    return preset.maxAnimationFramerate;

                return 120;
            }
            set
            {
                if (preset == null) LoadActiveSettings();

                preset.maxAnimationFramerate = value;

#if UNITY_EDITOR
                EditorUtility.SetDirty(preset);
#endif
            }
        }

        public static MessageLevel debugLevel
        {
            get
            {
                if (preset != null)
                    return preset.debugLevel;

                return (MessageLevel)0;
            }
            set
            {
                if (preset == null) LoadActiveSettings();

                preset.debugLevel = value;

#if UNITY_EDITOR
                EditorUtility.SetDirty(preset);
#endif
            }
        }

        internal static CoreConfig preset
        {
            get
            {
                if(_preset == null)
                    LoadActiveSettings();

                return _preset;
            }
        }

        private static CoreConfig _preset;

        private static void LoadActiveSettings()
        {
            _preset = Resources.Load<CoreConfig>("CoreConfig");
        }

#if UNITY_EDITOR
        static FPSFrameworkSettings()
        {
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        private static void OnPlayModeChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingEditMode && !IsValidConfiguration())
            {
                Debug.LogError("Play Mode stopped due to a null settings preset in FPS Framework settings. Please assign one and try again.");

                EditorUtility.DisplayDialog(
                    "Play Mode Blocked",
                    "Play Mode was stopped because the settings preset in FPS Framework settings is null. Please reimport 'Data' folder and try again.",
                    "OK"
                );

                EditorApplication.isPlaying = false;
            }
        }

        private static bool IsValidConfiguration()
        {
            return preset != null;
        }

        [CustomEditor(typeof(CoreConfig))]
        protected class FPSFSPresetEditor : Editor
        {
            public override void OnInspectorGUI()
            {
                EditorGUILayout.HelpBox("Modifying 'CoreConfig' directly from the preset is unavailable. Open the FPS Framework Settings window to make changes.", MessageType.Info);

                if (GUILayout.Button("Open Settings"))
                {
                    EditorApplication.ExecuteMenuItem(MenuPaths.Settings);
                }
            }
        }
#endif
    }
}