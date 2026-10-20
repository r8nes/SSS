#if UNITY_EDITOR
using Akila.FPSFramework;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using static Akila.FPSFramework.RenderPipelineDetector;

namespace Akila.FPSFramework.Internal
{
    internal class FPSFrameworkSetupWindow : EditorWindow
    {
        private Vector2 scroll;
        private bool tryingToInstal;

        public int setupCount
        {
            get
            {
                int count = 0;

                if (FPSFrameworkSettings.wasTagsManagerSetup)
                    count++;

                if (FPSFrameworkSettings.wasSceneManagerSetup)
                    count++;

                if (FPSFrameworkSettings.wasPhysicsSetup)
                    count++;

                if (FPSFrameworkSettings.wasPlayerSetup)
                    count++;

                if (FPSFrameworkSettings.wasRPSetup)
                    count++;

                return count;
            }
        }

        [InitializeOnLoadMethod]
        public static void WarnAboutSetup()
        {
            EditorApplication.delayCall += () =>
            {
                if (!FPSFrameworkCore.IsDeveloperMode && FPSFrameworkSettings.wasSetup == false)
                {
                    bool open = EditorUtility.DisplayDialog("Setup Required",
                    "It looks like this is your first time using FPS Framework. Please run the setup wizard to configure the framework for your project.",
                    "Open Setup Wizard");

                    if (open)
                    {
                        ShowWindow();
                    }
                }
            };
        }

        [MenuItem(MenuPaths.QuickSetup, false, 0)]
        public static void ShowWindow()
        {
            var window = GetWindow<FPSFrameworkSetupWindow>(true, "FPS Framework Setup", true);
            window.minSize = new Vector2(600, 500);

            if (CurrentPipeline == PipelineType.BuiltIn)
                FPSFrameworkSettings.preset.renderPipelineType = CoreConfig.RenderPipelineType.BuiltIn;

            if (CurrentPipeline == PipelineType.UniversalRP)
                FPSFrameworkSettings.preset.renderPipelineType = CoreConfig.RenderPipelineType.UniversalRP;

            if (CurrentPipeline == PipelineType.HighDefinitionRP)
                FPSFrameworkSettings.preset.renderPipelineType = CoreConfig.RenderPipelineType.HighDefinitionRP;
        }

        [MenuItem(MenuPaths.QuickSetup, true, 0)]
        public static bool ValidateMenuItem()
        {
            return !FPSFrameworkSettings.wasSetup;
        }

        static Texture2D LoadIconByGUID(string guid)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (string.IsNullOrEmpty(path))
                return null;

            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }
        private void OnGUI()
        {
            scroll = EditorGUILayout.BeginScrollView(scroll);

            DrawHeader();

            DrawWelcomeSection();

            GUILayout.Space(10);

            DrawSetupSteps();

            GUILayout.Space(10);

            DrawUtilitiesSteps();

            GUILayout.Space(30);

            EditorGUILayout.EndScrollView();

            DrawBottomBar();
        }

        void DrawHeader()
        {
            EditorGUILayout.Space(30);

            var title = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 18,
                alignment = TextAnchor.MiddleCenter
            };

            GUILayout.Label("FPS Framework", title);
            GUILayout.Label("Initial Setup", EditorStyles.centeredGreyMiniLabel);

            EditorGUILayout.Space(30);
        }

        void DrawWelcomeSection()
        {
            EditorGUILayout.BeginVertical("box");

            GUILayout.Label("Welcome", EditorStyles.boldLabel);

            EditorGUILayout.HelpBox(
                "Thank you for using FPS Framework. This window will guide you through the initial setup and provide quick access to common tools and utilities.",
                MessageType.Info
            );

            EditorGUILayout.EndVertical();
        }

        void DrawSetupSteps()
        {
            EditorGUILayout.BeginVertical("box");
            GUILayout.Label("Initial Setup", EditorStyles.boldLabel);

            DrawStep("Setup Tags & Layers", false, FPSFrameworkSettings.wasTagsManagerSetup, SetupTagsAndLayers);

            DrawStep("Setup Scene Manager", false, FPSFrameworkSettings.wasSceneManagerSetup, SetupSceneManager);

            DrawStep("Setup Player Settings", false, FPSFrameworkSettings.wasPlayerSetup, SetupPlayer);

            EditorGUI.BeginDisabledGroup(!FPSFrameworkSettings.wasTagsManagerSetup);

            DrawStep("Setup Physics Settings", false, FPSFrameworkSettings.wasPhysicsSetup, SetupPhysics);

            EditorGUI.EndDisabledGroup();

            if (!FPSFrameworkSettings.wasTagsManagerSetup)
                EditorGUILayout.HelpBox("Note: Make sure to complete 'Setup Tags & Layers' before configuring physics settings.", MessageType.Info);


            EditorGUILayout.Space();

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("RPs Convertor", EditorStyles.boldLabel);

            EditorGUI.BeginDisabledGroup(true);
            EditorGUILayout.TextField("Current RP", RenderPipelineDetector.CurrentPiplineName);
            EditorGUI.EndDisabledGroup();

            FPSFrameworkSettings.preset.renderPipelineType = (CoreConfig.RenderPipelineType)EditorGUILayout.EnumPopup("Target Pipeline Type", FPSFrameworkSettings.preset.renderPipelineType);

            EditorGUILayout.HelpBox(
                "Converting settings will adjust your FPS Framework configuration to match the selected render pipeline. " +
                "Make sure you've selected the correct pipeline before proceeding, as some settings may be overwritten.",
                MessageType.Info
            );

            if (FPSFrameworkSettings.preset.renderPipelineType != CoreConfig.RenderPipelineType.BuiltIn)
            {
                bool canNotInstall = false;

                if (RenderPipelineDetector.CurrentPipeline == PipelineType.BuiltIn && FPSFrameworkSettings.preset.renderPipelineType == CoreConfig.RenderPipelineType.BuiltIn)
                    canNotInstall = true;

                if (RenderPipelineDetector.CurrentPipeline == PipelineType.UniversalRP && FPSFrameworkSettings.preset.renderPipelineType == CoreConfig.RenderPipelineType.UniversalRP)
                    canNotInstall = true;

                if (RenderPipelineDetector.CurrentPipeline == PipelineType.HighDefinitionRP && FPSFrameworkSettings.preset.renderPipelineType == CoreConfig.RenderPipelineType.HighDefinitionRP)
                    canNotInstall = true;

                if (RPConvertor.IsRPInstalled(FPSFrameworkSettings.preset.renderPipelineType))
                    canNotInstall = true;

                if (tryingToInstal)
                    canNotInstall = true;

                EditorGUI.BeginDisabledGroup(canNotInstall);

                if (GUILayout.Button("Install"))
                {
                    tryingToInstal = true;

                    bool confirm = EditorUtility.DisplayDialog(
                        "Confirm Installation",
                        "This process will configure your FPS Framework to work with the selected Render Pipeline.\n\n" +
                        "Some settings and assets may be automatically modified or replaced to ensure compatibility.\n\n" +
                        "Do you want to continue?",
                        "Proceed",
                        "Cancel"
                    );

                    if (confirm)
                    {
                        string currentGUID = "";

                        if (FPSFrameworkSettings.preset.renderPipelineType == CoreConfig.RenderPipelineType.BuiltIn)
                        {
                            currentGUID = WizardGUIDs.birpPackage;
                        }

                        if (FPSFrameworkSettings.preset.renderPipelineType == CoreConfig.RenderPipelineType.UniversalRP)
                        {
                            currentGUID = WizardGUIDs.urpPackage;
                        }

                        RPConvertor.InstallRenderPipline(FPSFrameworkSettings.preset.renderPipelineType);
                    }
                }


                EditorGUI.EndDisabledGroup();
            }

            bool canNotBeSetup = false;

            if (RPConvertor.IsRPInstalled(FPSFrameworkSettings.preset.renderPipelineType) == false)
                canNotBeSetup = true;

            EditorGUI.BeginDisabledGroup(canNotBeSetup);

            DrawStep("Setup Selected RP", false, FPSFrameworkSettings.wasRPSetup, () =>
            {
                bool confirm = EditorUtility.DisplayDialog(
                     "Confirm Setup",
                     "This process will configure your FPS Framework to work with the selected Render Pipeline.\n\n" +
                     "Important:\n" +
                     "All related prefabs, materials, scenes, and configuration files will be overwritten or replaced to match the selected pipeline.\n\n" +
                     "It is strongly recommended to back up your project before continuing.\n\n" +
                     "Do you want to proceed?",
                     "Continue",
                     "Cancel"
                );

                if (confirm)
                {
                    string currentGUID = "";

                    if (FPSFrameworkSettings.preset.renderPipelineType == CoreConfig.RenderPipelineType.BuiltIn)
                    {
                        currentGUID = WizardGUIDs.birpPackage;
                    }

                    if (FPSFrameworkSettings.preset.renderPipelineType == CoreConfig.RenderPipelineType.UniversalRP)
                    {
                        currentGUID = WizardGUIDs.urpPackage;
                    }

                    if (FPSFrameworkSettings.preset.renderPipelineType == CoreConfig.RenderPipelineType.HighDefinitionRP)
                    {
                        currentGUID = WizardGUIDs.hdrpPackage;
                    }

                    RPConvertor.SetupRenderPipline(FPSFrameworkSettings.preset.renderPipelineType, currentGUID);
                }

                FPSFrameworkSettings.wasRPSetup = true;
            });

            EditorGUI.EndDisabledGroup();

            EditorGUILayout.EndVertical();
        }

        void DrawUtilitiesSteps()
        {
            EditorGUILayout.BeginVertical("box");
            GUILayout.Label("Utilities", EditorStyles.boldLabel);

            DrawStep("Open Documentation", true, false, () => { Application.OpenURL(FPSFrameworkSettings.docsUrl); });

            DrawStep("Report an issue", true, false, () => { Application.OpenURL(FPSFrameworkSettings.discordUr); });

            EditorGUILayout.EndVertical();
        }

        void DrawStep(string label, bool optional, bool completed, System.Action action)
        {
            EditorGUILayout.BeginVertical();

            EditorGUILayout.BeginHorizontal();

            using (new EditorGUI.DisabledScope(completed))
            {
                if (GUILayout.Button(label, GUILayout.Height(20)))
                {
                    action.Invoke();
                }
            }

            if (optional == false)
            {
                string guid = !completed
                    ? "140ed67a0a7e38f468bbde6bcc204792" // completed icon GUID
                    : "cfb820d8ad3e6a6489d534362095b2c5"; // required icon GUID

                Texture2D myIconTex = LoadIconByGUID(guid); // your GUID
                GUIContent myIcon = new GUIContent(myIconTex);

                EditorGUIUtility.SetIconSize(new Vector2(16, 16));
                EditorGUILayout.LabelField(myIcon, GUILayout.Width(20));
                EditorGUIUtility.SetIconSize(Vector2.zero);
            }

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();
        }

        void DrawBottomBar()
        {
            EditorGUILayout.BeginHorizontal();

            GUILayout.FlexibleSpace(); // pushes button to the right

            EditorGUILayout.BeginHorizontal();

            GUILayout.Label($"{setupCount} of 5");

            EditorGUI.BeginDisabledGroup(!FPSFrameworkSettings.wasSetup);

            if (GUILayout.Button("Close", GUILayout.Width(85), GUILayout.Height(20)))
            {
                Close();
            }

            EditorGUI.EndDisabledGroup();

            EditorGUILayout.EndHorizontal();
        }

        // ===================== Setup Logic =====================

        void SetupTagsAndLayers()
        {
            SetLayerName(3, "No Player Collision");
            SetLayerName(8, "Player");
            SetLayerName(11, "Post-Procssing");
            SetLayerName(12, "FPS Object");
            SetLayerName(13, "Enviroment");
            SetLayerName(15, "Iteractable");

            FPSFrameworkSettings.wasTagsManagerSetup = true;

            AssetDatabase.SaveAssets();
        }

        void SetupSceneManager()
        {
            AddScene(WizardGUIDs.mainMenuScene);
            AddScene(WizardGUIDs.loadingMenuScene);
            AddScene(WizardGUIDs.singlePlayerDemoScene);

#if FPS_FRAMEWORK_PRO
            AddScene(WizardGUIDs.multiPlayerDemoScene);
#endif

            string demoScenePath = AssetDatabase.GUIDToAssetPath(WizardGUIDs.singlePlayerDemoScene);

            EditorSceneManager.OpenScene(demoScenePath);

            FPSFrameworkSettings.wasSceneManagerSetup = true;

            AssetDatabase.SaveAssets();
        }

        void SetupPlayer()
        {
            PlayerSettings.colorSpace = ColorSpace.Linear;

            FPSFrameworkSettings.wasPlayerSetup = true;

            AssetDatabase.SaveAssets();
        }

        void SetupPhysics()
        {
            Physics.IgnoreLayerCollision(LayerMask.NameToLayer("Player"), LayerMask.NameToLayer("Player"), true);
            Physics.IgnoreLayerCollision(LayerMask.NameToLayer("Player"), LayerMask.NameToLayer("No Player Collision"), true);
            Physics.IgnoreLayerCollision(LayerMask.NameToLayer("No Player Collision"), LayerMask.NameToLayer("No Player Collision"), true);
            Physics.IgnoreLayerCollision(LayerMask.NameToLayer("Player"), LayerMask.NameToLayer("Iteractable"), true);
            Physics.IgnoreLayerCollision(LayerMask.NameToLayer("FPS Object"), LayerMask.NameToLayer("No Player Collision"), true);
            Physics.IgnoreLayerCollision(LayerMask.NameToLayer("FPS Object"), LayerMask.NameToLayer("FPS Object"), true);

            FPSFrameworkSettings.wasPhysicsSetup = true;

            AssetDatabase.SaveAssets();
        }

        static void AddScene(string guid)
        {
            string scenePath = "";

            scenePath = AssetDatabase.GUIDToAssetPath(guid);

            var scenes = EditorBuildSettings.scenes.ToList();

            // Prevent duplicates
            if (!scenes.Any(s => s.path == scenePath))
            {
                scenes.Add(new EditorBuildSettingsScene(scenePath, true));
                EditorBuildSettings.scenes = scenes.ToArray();
            }
            else
            {
                Debug.Log("Scene already exists in Build Settings.");
            }
        }

        public static void SetLayerName(int index, string newName)
        {
            if (index < 0 || index > 31)
            {
                Debug.LogError("Layer index must be between 0 and 31.");
                return;
            }

            SerializedObject tagManager =
                new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);

            SerializedProperty layersProp = tagManager.FindProperty("layers");

            SerializedProperty layer = layersProp.GetArrayElementAtIndex(index);
            layer.stringValue = newName;

            tagManager.ApplyModifiedProperties();
        }

        public static void ApplyCollisionMatrix((string layerA, string layerB, bool ignore)[] rules)
        {
            foreach (var rule in rules)
            {
                int a = LayerMask.NameToLayer(rule.layerA);
                int b = LayerMask.NameToLayer(rule.layerB);

                if (a == -1 || b == -1)
                {
                    Debug.LogWarning(
                        $"CollisionMatrix: Invalid layer name ({rule.layerA}, {rule.layerB})"
                    );
                    continue;
                }

                Physics.IgnoreLayerCollision(a, b, rule.ignore);
            }
        }
    }
}
#endif
