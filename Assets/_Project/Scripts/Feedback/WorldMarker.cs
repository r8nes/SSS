using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;

namespace ShooterShop.Feedback
{
    public sealed class WorldMarker : MonoBehaviour
    {
        [Required, ChildGameObjectsOnly]
        [SerializeField] private GameObject _root;

        [Required, ChildGameObjectsOnly]
        [SerializeField] private TMP_Text _label;

        [Required]
        [SerializeField] private string _title = "Marker";

        private Vector3 _basePosition;
        private string _shownText;
        private bool _isInitialized;

        public void SetVisible(bool isVisible)
        {
            if (_root.activeSelf != isVisible)
                _root.SetActive(isVisible);
        }

        public void Present(Transform viewer, float bobOffset)
        {
            if (!_isInitialized)
            {
                _basePosition = _root.transform.localPosition;
                _isInitialized = true;
            }

            var rootTransform = _root.transform;
            rootTransform.localPosition = _basePosition + Vector3.up * bobOffset;
            rootTransform.rotation = viewer.rotation;

            var distance = Mathf.RoundToInt(Vector3.Distance(viewer.position, rootTransform.position));
            var text = $"{_title}\n<size=60%>{distance} m</size>";

            if (text == _shownText)
                return;

            _shownText = text;
            _label.text = text;
        }
    }
}
