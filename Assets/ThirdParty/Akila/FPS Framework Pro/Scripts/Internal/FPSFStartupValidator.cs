#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace Akila.FPSFramework.Internal
{
    [InitializeOnLoad]
    internal static class FPSFDependencyValidator
    {
        static FPSFDependencyValidator()
        {
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.ExitingEditMode)
                return;

            if (IsMirrorInstalled())
                return;

            EditorApplication.ExitPlaymode();

            bool openInstaller = EditorUtility.DisplayDialog(
                "Mirror Required",
                "FPS Framework Pro requires Mirror Networking to enter Play Mode.\n\n" +
                "Please install Mirror before continuing.",
                "Open Mirror Download Page",
                "Cancel"
            );

            Debug.LogError(
                "[FPS Framework Pro] Play Mode blocked.\n" +
                "Mirror Networking is required to use FPS Framework Pro.\n\n" +
                "Install Mirror from:\n" +
                "Asset Store: https://assetstore.unity.com/packages/tools/network/mirror-129321\n" +
                "GitHub: https://github.com/MirrorNetworking/Mirror"
            );

            if (openInstaller)
            {
                Application.OpenURL("https://assetstore.unity.com/packages/tools/network/mirror-129321");
            }

            UnityEditor.Compilation.CompilationPipeline.RequestScriptCompilation();
        }

        private static bool IsMirrorInstalled()
        {
#if MIRROR
            return true;
#else
        return false;
#endif
        }
    }
}
#endif