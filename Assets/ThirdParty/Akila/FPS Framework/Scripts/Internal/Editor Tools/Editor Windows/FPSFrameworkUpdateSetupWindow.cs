#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace Akila.FPSFramework.Internal
{
    internal class FPSFrameworkUpdateSetupWindow : EditorWindow
    {
        public static void ShowWindow()
        {
            FPSFrameworkUpdateSetupWindow window = GetWindow<FPSFrameworkUpdateSetupWindow>(true, "FPS Framework Update", true);
            window.minSize = new Vector2(460, 220);
            window.maxSize = new Vector2(460, 220);
            window.ShowUtility();
        }

        private void OnGUI()
        {
            // --- 1. HEADER BANNER ---
            DrawHeaderBanner();

            GUILayout.Space(25);

            // --- 2. MAIN CONTENT AREA ---
            using (new GUILayout.HorizontalScope())
            {
                GUILayout.Space(22);
                using (new GUILayout.VerticalScope())
                {
                    // Simple informational update subtext
                    GUIStyle subtextStyle = new GUIStyle(EditorStyles.label)
                    {
                        alignment = TextAnchor.MiddleCenter,
                        wordWrap = true
                    };
                    GUILayout.Label($"FPS Framework has been successfully updated to version {FPSFrameworkCore.version}", subtextStyle);

                    // Pushes the button cleanly to the bottom
                    GUILayout.FlexibleSpace();

                    // --- 3. CALL TO ACTION BUTTON ---
                    if (GUILayout.Button("Get Started & Initialize Setup", GUILayout.Height(38)))
                    {
                        FPSFrameworkSettings.preset.wasPhysicsSetup = false;
                        FPSFrameworkSettings.preset.wasPlayerSetup = false;
                        FPSFrameworkSettings.preset.wasRPSetup = false;
                        FPSFrameworkSettings.preset.wasSceneManagerSetup = false;
                        FPSFrameworkSettings.preset.wasTagsManagerSetup = false;

                        FPSFrameworkSetupWindow.ShowWindow();

                        Close();
                    }
                }
                GUILayout.Space(22);
            }
            GUILayout.Space(25);
        }

        private void DrawHeaderBanner()
        {
            Rect headerRect = GUILayoutUtility.GetRect(position.width, 75);
            EditorGUI.DrawRect(headerRect, new Color(0.14f, 0.14f, 0.14f, 1.0f));

            GUIStyle titleStyle = new GUIStyle(EditorStyles.whiteLargeLabel)
            {
                fontSize = 19,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            EditorGUI.LabelField(headerRect, "FPS Framework Updated", titleStyle);
        }
    }
}
#endif