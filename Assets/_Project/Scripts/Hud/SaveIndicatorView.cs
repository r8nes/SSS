using PrimeTween;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;

namespace ShooterShop.Hud
{
    public sealed class SaveIndicatorView : MonoBehaviour
    {
        [Required, ChildGameObjectsOnly]
        [SerializeField] private CanvasGroup _group;

        [Required, ChildGameObjectsOnly]
        [SerializeField] private TMP_Text _text;

        [Required, ChildGameObjectsOnly]
        [SerializeField] private RectTransform _icon;

        public void SetVisible(bool isVisible, float fade)
        {
            Tween.StopAll(_group);
            Tween.Alpha(_group, isVisible ? 1f : 0f, fade);
        }

        public void SetText(string text) => _text.text = text;

        public void SetIconVisible(bool isVisible) => _icon.gameObject.SetActive(isVisible);

        public void Spin(float degrees) => _icon.Rotate(0f, 0f, degrees);
    }
}
