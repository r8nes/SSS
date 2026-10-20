using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Toolbars;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ShooterShop.Editor.SceneSwitcher
{
    [InitializeOnLoad]
    public static class SceneSwitcherToolbar
    {
        private const string ElementPath = "Shooter Shop/Scene Switcher";
        private static readonly string[] ExcludedFolders = { "Assets/Mirror/" };

        static SceneSwitcherToolbar()
        {
            EditorSceneManager.activeSceneChangedInEditMode += (_, _) => MainToolbar.Refresh(ElementPath);
        }

        [MainToolbarElement(ElementPath, defaultDockPosition = MainToolbarDockPosition.Middle)]
        public static MainToolbarElement Create()
        {
            var icon = EditorGUIUtility.IconContent("SceneAsset Icon").image as Texture2D;
            var content = new MainToolbarContent(SceneManager.GetActiveScene().name, icon, "Switch scene");
            return new MainToolbarDropdown(content, ShowMenu);
        }

        private static void ShowMenu(Rect rect)
        {
            var menu = new GenericMenu();
            var activePath = SceneManager.GetActiveScene().path;
            var buildScenes = EditorBuildSettings.scenes.Select(s => s.path).Where(File.Exists).ToArray();

            foreach (var path in buildScenes)
                AddScene(menu, Path.GetFileNameWithoutExtension(path), path, activePath);

            var otherScenes = AssetDatabase.FindAssets("t:Scene", new[] { "Assets" })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(p => !buildScenes.Contains(p) && !ExcludedFolders.Any(p.StartsWith))
                .OrderBy(p => p);

            if (otherScenes.Any())
                menu.AddSeparator(string.Empty);

            foreach (var path in otherScenes)
                AddScene(menu, "Other/" + Path.GetFileNameWithoutExtension(path), path, activePath);

            menu.DropDown(rect);
        }

        private static void AddScene(GenericMenu menu, string label, string path, string activePath)
        {
            menu.AddItem(new GUIContent(label), path == activePath, () => Open(path));
        }

        private static void Open(string path)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return;

            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                EditorSceneManager.OpenScene(path);
        }
    }
}
