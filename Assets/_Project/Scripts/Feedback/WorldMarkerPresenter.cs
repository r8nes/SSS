using System;
using ShooterShop.Days;
using ShooterShop.Store;
using UnityEngine;
using VContainer.Unity;

namespace ShooterShop.Feedback
{
    public sealed class WorldMarkerPresenter : IStartable, ITickable, IDisposable
    {
        private readonly WorldMarkerSet _markers;
        private readonly IDayState _day;
        private readonly IDayCommands _dayCommands;
        private readonly CarryService _carry;
        private readonly StoreLayout _store;
        private readonly FeedbackConfig _config;
        private readonly IScreenLabels _labels;
        private readonly IViewCamera _camera;

        private ScreenLabel _terminal;
        private ScreenLabel _openSign;
        private ScreenLabel _delivery;

        public WorldMarkerPresenter(
            WorldMarkerSet markers,
            IDayState day,
            IDayCommands dayCommands,
            CarryService carry,
            StoreLayout store,
            FeedbackConfig config,
            IScreenLabels labels,
            IViewCamera camera)
        {
            _markers = markers;
            _day = day;
            _dayCommands = dayCommands;
            _carry = carry;
            _store = store;
            _config = config;
            _labels = labels;
            _camera = camera;
        }

        public void Start()
        {
            _terminal = Create(_markers.Terminal);
            _openSign = Create(_markers.OpenSign);
            _delivery = Create(_markers.Delivery);
        }

        public void Dispose()
        {
            _labels.Release(_terminal);
            _labels.Release(_openSign);
            _labels.Release(_delivery);
        }

        public void Tick()
        {
            var camera = _camera.Current;

            if (_terminal == null || camera == null || _store == null)
                return;

            var isPreparing = _day.Phase == DayPhase.Preparing && _day.Day > 0;
            var hasDelivery = _carry.HasLooseBoxesNear(_store.DeliveryPoint.position, _config.DeliveryMarkerRadius);
            var bob = Vector3.up * (Mathf.Sin(Time.time * _config.MarkerBobSpeed) * _config.MarkerBobHeight);

            Show(_terminal, _markers.Terminal, isPreparing, camera, bob);
            Show(_openSign, _markers.OpenSign, isPreparing && _dayCommands.CanOpenShop, camera, bob);
            Show(_delivery, _markers.Delivery, hasDelivery, camera, bob);
        }

        private ScreenLabel Create(WorldMarker marker)
        {
            marker.HidePreview();

            var label = _labels.Create(marker.transform, _config.MarkerOffset, _config.MarkerStyle);
            label.SetColor(marker.Color);
            label.IsVisible = false;
            return label;
        }

        private void Show(ScreenLabel label, WorldMarker marker, bool isVisible, Camera camera, Vector3 bob)
        {
            label.IsVisible = isVisible;

            if (!isVisible)
                return;

            label.Offset = _config.MarkerOffset + bob;

            var distance = Mathf.RoundToInt(Vector3.Distance(camera.transform.position, label.WorldPosition));
            label.SetText($"▼ {marker.Title}\n<size=70%>{distance} m</size>");
        }
    }
}
