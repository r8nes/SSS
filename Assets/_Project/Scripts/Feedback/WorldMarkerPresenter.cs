using ShooterShop.Days;
using ShooterShop.Store;
using UnityEngine;
using VContainer.Unity;

namespace ShooterShop.Feedback
{
    public sealed class WorldMarkerPresenter : ITickable
    {
        private readonly WorldMarkerSet _markers;
        private readonly IDayState _day;
        private readonly IDayCommands _dayCommands;
        private readonly CarryService _carry;
        private readonly StoreLayout _store;
        private readonly FeedbackConfig _config;

        public WorldMarkerPresenter(WorldMarkerSet markers, IDayState day, IDayCommands dayCommands, CarryService carry, StoreLayout store, FeedbackConfig config)
        {
            _markers = markers;
            _day = day;
            _dayCommands = dayCommands;
            _carry = carry;
            _store = store;
            _config = config;
        }

        public void Tick()
        {
            var camera = Camera.main;

            if (camera == null || _markers.Terminal == null)
                return;

            var isPreparing = _day.Phase == DayPhase.Preparing && _day.Day > 0;
            var hasDelivery = _store != null && _carry.HasLooseBoxesNear(_store.DeliveryPoint.position, _config.DeliveryMarkerRadius);
            var bob = Mathf.Sin(Time.time * _config.MarkerBobSpeed) * _config.MarkerBobHeight;

            Show(_markers.Terminal, isPreparing, camera.transform, bob);
            Show(_markers.OpenSign, isPreparing && _dayCommands.CanOpenShop, camera.transform, bob);
            Show(_markers.Delivery, hasDelivery, camera.transform, bob);
        }

        private static void Show(WorldMarker marker, bool isVisible, Transform viewer, float bob)
        {
            marker.SetVisible(isVisible);

            if (isVisible)
                marker.Present(viewer, bob);
        }
    }
}
