using System;
using VContainer.Unity;

namespace ShooterShop.Feedback
{
    public sealed class NotificationPresenter : IStartable, IDisposable
    {
        private readonly NotificationView _view;
        private readonly IShopFeedEvents _feed;
        private readonly FeedbackConfig _config;

        public NotificationPresenter(NotificationView view, IShopFeedEvents feed, FeedbackConfig config)
        {
            _view = view;
            _feed = feed;
            _config = config;
        }

        public void Start() => _feed.Notified += OnNotified;

        public void Dispose() => _feed.Notified -= OnNotified;

        private void OnNotified(string message, NotificationKind kind)
        {
            if (_view != null)
                _view.Show(message, _config.ColorOf(kind), _config.DurationOf(kind), _config.NotificationFade, _config.MaxNotifications, kind == NotificationKind.Danger);
        }
    }
}
