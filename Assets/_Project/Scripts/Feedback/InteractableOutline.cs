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
        [InfoBox("Static meshes get a separate shell object, so the outline also works on batched and multi-material renderers. Rebuild after changing renderers.")]
        [SerializeField] private List<GameObject> _shells = new();

        private readonly List<Material> _buffer = new();
        private bool _isVisible;

        public void SetVisible(bool visible)
        {
            if (_isVisible == visible)
                return;

            _isVisible = visible;

            foreach (var shell in _shells)
                if (shell != null)
                    shell.SetActive(visible);

            foreach (var target in _renderers)
                if (target is SkinnedMeshRenderer)
                    AppendMaterial(target, visible);
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
            foreach (var shell in _shells)
                if (shell != null)
                    DestroyImmediate(shell);

            _shells.Clear();

            foreach (var target in _renderers)
            {
                if (target is not MeshRenderer || !target.TryGetComponent<MeshFilter>(out var filter) || filter.sharedMesh == null)
                    continue;

                var shell = new GameObject(ShellName) { layer = target.gameObject.layer };
                shell.transform.SetParent(target.transform, false);
                shell.AddComponent<MeshFilter>().sharedMesh = filter.sharedMesh;

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
#endif
    }
}
