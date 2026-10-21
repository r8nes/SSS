using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Mirror;
using ShooterShop.Customers;
using ShooterShop.Feedback;
using ShooterShop.Saves;
using VContainer.Unity;

namespace ShooterShop.Days
{
    public sealed class DayCycle : IAsyncStartable, ITickable, IDisposable
    {
        private readonly DayConfig _config;
        private readonly DayNetworkState _state;
        private readonly ICustomerService _customers;
        private readonly StartingState _startingState;
        private readonly DayEventCatalog _events;
        private readonly IShopFeed _feed;
        private bool _warnedClosing;

        public DayCycle(DayConfig config, DayNetworkState state, ICustomerService customers, StartingState startingState, DayEventCatalog events, IShopFeed feed)
        {
            _config = config;
            _state = state;
            _customers = customers;
            _startingState = startingState;
            _events = events;
            _feed = feed;
        }

        public async UniTask StartAsync(CancellationToken cancellation)
        {
            _state.NextDayRequested += OnNextDayRequested;
            _state.OpenShopRequested += OnOpenShopRequested;

            if (await UniTask.WaitUntil(() => NetworkServer.active && _state.isServer, cancellationToken: cancellation).SuppressCancellationThrow())
                return;

            if (_state.Day == 0)
                PrepareDay(_startingState.CompletedDays + 1);
        }

        public void Dispose()
        {
            _state.NextDayRequested -= OnNextDayRequested;
            _state.OpenShopRequested -= OnOpenShopRequested;
        }

        public void Tick()
        {
            if (_state == null || !NetworkServer.active || !_state.isServer)
                return;

            switch (_state.Phase)
            {
                case DayPhase.Open when !_warnedClosing && _state.TimeLeft <= _config.ClosingWarning:
                    _warnedClosing = true;
                    _feed.Notify($"{(int)_config.ClosingWarning} seconds until closing", NotificationKind.Warning);
                    break;
                case DayPhase.Open when _state.TimeLeft <= 0:
                    _state.ServerSetPhase(DayPhase.Closing);
                    _customers.CloseShop();
                    break;
                case DayPhase.Closing when _customers.ActiveCount == 0:
                    _state.ServerSetPhase(DayPhase.Summary);
                    _feed.Notify($"Day {_state.Day} is over", NotificationKind.Info);
                    break;
            }
        }

        private void OnNextDayRequested() => PrepareDay(_state.Day + 1);

        private void OnOpenShopRequested()
        {
            _warnedClosing = false;
            _state.ServerOpen(NetworkTime.time + _config.OpenDuration);
            _feed.Notify("The shop is open!", NotificationKind.Good);
        }

        private void PrepareDay(int day)
        {
            _state.ServerPrepare(day, _events.PickIndex(day));
            PrepareAnnouncementAsync().Forget();
        }

        private async UniTaskVoid PrepareAnnouncementAsync()
        {
            await UniTask.Yield();

            var dayEvent = _state.CurrentEvent;
            _feed.Notify($"Day {_state.Day}: stock up and open the shop with the OPEN sign", NotificationKind.Info);

            if (dayEvent != null)
                _feed.Notify($"Today: {dayEvent.Title}. {dayEvent.Description}", NotificationKind.Good);
        }
    }
}
