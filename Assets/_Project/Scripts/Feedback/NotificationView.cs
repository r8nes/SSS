using PrimeTween;
using Sirenix.OdinInspector;
using UnityEngine;

namespace ShooterShop.Feedback
{
    public sealed class NotificationView : MonoBehaviour
    {
        [Required, ChildGameObjectsOnly]
        [SerializeField] private RectTransform _container;

        [Required, AssetsOnly]
        [SerializeField] private NotificationItemView _itemPrefab;

        public void Show(string text, Color color, float duration, float fadeDuration, int maxVisible, bool isAlarming)
        {
            while (_container.childCount >= maxVisible)
                DestroyImmediate(_container.GetChild(0).gameObject);

            var item = Instantiate(_itemPrefab, _container);
            item.Set(text, color);

            var group = item.CanvasGroup;
            Tween.Alpha(group, 0f, 1f, fadeDuration);
            Tween.Scale(item.transform, Vector3.one * 0.85f, Vector3.one, fadeDuration, Ease.OutBack);
            if (isAlarming)
                Tween.ShakeLocalPosition(item.transform, new Vector3(18f, 6f, 0f), fadeDuration * 3f, startDelay: fadeDuration);

            Tween.Alpha(group, 1f, 0f, fadeDuration, startDelay: duration)
                .OnComplete(item.gameObject, Destroy);
        }
    }
}
