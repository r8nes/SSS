using UnityEditor;
using UnityEditor.Rendering;
using UnityEditor.Rendering.Universal;
using UnityEngine.Rendering;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using Aetheria.Runtime.Water;

namespace Aetheria.Editor.Water
{
    static class WaterSurfaceMenuItems
    {
        [MenuItem("GameObject/Aetheria/Water Surface", false, 10)]
        static void CreateSurface(MenuCommand command)
        {
            var go = new GameObject("Water Surface");
            go.AddComponent<WaterSurface>();
            GameObjectUtility.SetParentAndAlign(go, command.context as GameObject);
            Undo.RegisterCreatedObjectUndo(go, "Create Water Surface");
            Selection.activeObject = go;
        }

    }
}
