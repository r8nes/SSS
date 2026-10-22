using PrimeTween;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;

namespace ShooterShop.Hud
{
    public sealed class HintsView : MonoBehaviour
    {
        [Required, ChildGameObjectsOnly]
        [SerializeField] private CanvasGroup _group;

        [Required, ChildGameObjectsOnly]
        [SerializeField] private TMP_Text _text;

        public void Show(string text, float fade)
        {
            if (_text.text == text && _group.alpha > 0.99f)
                return;

            _text.text = text;
            Tween.StopAll(_group);
            Tween.Alpha(_group, 1f, fade);
        }

        public void Hide(float fade)
        {
            if (_group.alpha <= 0f)
                return;

            Tween.StopAll(_group);
            Tween.Alpha(_group, 0f, fade);
        }
    }
}
