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
        private readonly IScreenLabels _labels;

        public WorldPopupSpawner(IShopFeedEvents feed, FeedbackConfig config, IScreenLabels labels)
        {
            _feed = feed;
            _config = config;
            _labels = labels;
        }

        public void Start() => _feed.PoppedUp += OnPoppedUp;

        public void Dispose() => _feed.PoppedUp -= OnPoppedUp;

        private void OnPoppedUp(Vector3 position, string text, NotificationKind kind)
        {
            var anchor = new GameObject("Popup Anchor").transform;
            anchor.position = position;

            var label = _labels.Create(anchor, Vector3.zero, _config.PopupStyle);
            var color = _config.ColorOf(kind);
            label.SetText(text);
            label.SetColor(color);

            var duration = _config.PopupDuration;
            Tween.Position(anchor, position, position + Vector3.up * _config.PopupRise, duration, Ease.OutCubic);
            Tween.Custom(label, 1f, 0f, duration * 0.4f, (target, alpha) => target.SetAlpha(alpha), startDelay: duration * 0.6f)
                .OnComplete(() =>
                {
                    _labels.Release(label);
                    Object.Destroy(anchor.gameObject);
                });
        }
    }
}
