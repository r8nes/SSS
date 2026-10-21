using System;
using PrimeTween;
using UnityEngine;
using VContainer.Unity;
using Object = UnityEngine.Object;

namespace ShooterShop.Feedback
{
    public sealed class WorldPopupSpawner : IStartable, IDisposable
    {
        private readonly IShopFeedEvents _feed;
        private readonly FeedbackConfig _config;

        public WorldPopupSpawner(IShopFeedEvents feed, FeedbackConfig config)
        {
            _feed = feed;
            _config = config;
        }

        public void Start() => _feed.PoppedUp += OnPoppedUp;

        public void Dispose() => _feed.PoppedUp -= OnPoppedUp;

        private void OnPoppedUp(Vector3 position, string text, NotificationKind kind)
        {
            var camera = Camera.main;
            var rotation = camera != null ? camera.transform.rotation : Quaternion.identity;
            var popup = Object.Instantiate(_config.PopupPrefab, position, rotation);
            var scale = popup.transform.localScale;

            popup.text = text;
            popup.color = _config.ColorOf(kind);

            var duration = _config.PopupDuration;
            Tween.Position(popup.transform, position, position + Vector3.up * _config.PopupRise, duration, Ease.OutCubic);
            Tween.Scale(popup.transform, Vector3.zero, scale, _config.PopDuration, Ease.OutBack);
            Tween.Custom(popup, 1f, 0f, duration * 0.4f, (target, alpha) => target.alpha = alpha, startDelay: duration * 0.6f)
                .OnComplete(popup.gameObject, Object.Destroy);
        }
    }
}
