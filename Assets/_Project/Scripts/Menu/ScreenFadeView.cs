using PrimeTween;
using Sirenix.OdinInspector;
using UnityEngine;

namespace ShooterShop.Menu
{
    public sealed class ScreenFadeView : MonoBehaviour
    {
        [Required, ChildGameObjectsOnly]
        [SerializeField] private CanvasGroup _group;

        public void SetOpacity(float opacity) => _group.alpha = opacity;

        public Tween FadeTo(float opacity, float duration) => Tween.Alpha(_group, opacity, duration, Ease.InOutSine);
    }
}
