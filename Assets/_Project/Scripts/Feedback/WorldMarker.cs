using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;

namespace ShooterShop.Feedback
{
    public sealed class WorldMarker : MonoBehaviour
    {
        [Required, ChildGameObjectsOnly]
        [InfoBox("Editor preview only, the game draws the marker on the screen label layer")]
        [SerializeField] private GameObject _root;

        [Required, ChildGameObjectsOnly]
        [SerializeField] private TMP_Text _label;

        [Required]
        [SerializeField] private string _title = "Marker";

        public string Title => _title;
        public Color Color => _label.color;

        public void HidePreview()
        {
            if (_root.activeSelf)
                _root.SetActive(false);
        }
    }
}
