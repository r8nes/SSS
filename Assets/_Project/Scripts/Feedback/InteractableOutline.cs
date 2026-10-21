using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

namespace ShooterShop.Feedback
{
    public sealed class InteractableOutline : MonoBehaviour
    {
        private const string ShellName = "Outline Shell";

        [Required, AssetsOnly]
        [SerializeField] private Material _outlineMaterial;

        [Required, ListDrawerSettings(ShowFoldout = false)]
        [SerializeField] private List<Renderer> _renderers = new();

        [ReadOnly, ListDrawerSettings(ShowFoldout = false)]
        [InfoBox("Static meshes get a separate shell object with smoothed normals, so the outline follows the silhouette on batched and multi-material renderers. Rebuild after changing renderers.")]
        [SerializeField] private List<GameObject> _shells = new();

        private readonly List<Material> _buffer = new();
        private bool _isVisible;
        private bool _isPrepared;

        public void SetVisible(bool visible)
        {
            if (_isVisible == visible)
                return;

            _isVisible = visible;

            if (visible && !_isPrepared)
                PrepareShells();

            foreach (var shell in _shells)
                if (shell != null)
                    shell.SetActive(visible);

            foreach (var target in _renderers)
                if (target is SkinnedMeshRenderer)
                    AppendMaterial(target, visible);
        }

        private void PrepareShells()
        {
            _isPrepared = true;

            foreach (var shell in _shells)
            {
                if (shell == null || !shell.TryGetComponent<MeshFilter>(out var filter) || filter.sharedMesh == null)
                    continue;

                if (!filter.sharedMesh.name.EndsWith("(Outline)") && OutlineMeshes.TryGetSmoothed(filter.sharedMesh, out var smoothed))
                    filter.sharedMesh = smoothed;
            }
        }

        private void AppendMaterial(Renderer target, bool visible)
        {
            target.GetSharedMaterials(_buffer);
            _buffer.Remove(_outlineMaterial);

            if (visible)
                _buffer.Add(_outlineMaterial);

            target.SetSharedMaterials(_buffer);
        }

#if UNITY_EDITOR
        [Button("Rebuild outline shells")]
        public void RebuildShells()
        {
            foreach (var oldShell in GetComponentsInChildren<Transform>(true))
                if (oldShell != null && oldShell.name == ShellName)
                    DestroyImmediate(oldShell.gameObject);

            _shells.Clear();

            foreach (var target in _renderers)
            {
                if (target is not MeshRenderer || !target.TryGetComponent<MeshFilter>(out var filter) || filter.sharedMesh == null)
                    continue;

                var shell = new GameObject(ShellName) { layer = target.gameObject.layer };
                shell.transform.SetParent(target.transform, false);

                var shellFilter = shell.AddComponent<MeshFilter>();
                shellFilter.sharedMesh = BakeSmoothedMesh(filter.sharedMesh);

                var shellRenderer = shell.AddComponent<MeshRenderer>();
                var materials = new Material[filter.sharedMesh.subMeshCount];

                for (var i = 0; i < materials.Length; i++)
                    materials[i] = _outlineMaterial;

                shellRenderer.sharedMaterials = materials;
                shellRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                shellRenderer.receiveShadows = false;
                UnityEditor.GameObjectUtility.SetStaticEditorFlags(shell, 0);
                shell.SetActive(false);
                _shells.Add(shell);
            }

            UnityEditor.EditorUtility.SetDirty(this);
        }

        private static Mesh BakeSmoothedMesh(Mesh source)
        {
            const string folder = "Assets/_Project/Art/OutlineMeshes";

            if (!UnityEditor.AssetDatabase.IsValidFolder(folder))
                UnityEditor.AssetDatabase.CreateFolder("Assets/_Project/Art", "OutlineMeshes");

            var path = $"{folder}/{source.name}_{source.GetInstanceID()}_Outline.asset";
            var existing = UnityEditor.AssetDatabase.LoadAssetAtPath<Mesh>(path);

            if (existing != null)
                return existing;

            var smoothed = OutlineMeshes.CreateSmoothed(source);
            UnityEditor.AssetDatabase.CreateAsset(smoothed, path);
            return smoothed;
        }
#endif
    }
}
