using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Mirror;
using ShooterShop.Customers;
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

        public DayCycle(DayConfig config, DayNetworkState state, ICustomerService customers, StartingState startingState)
        {
            _config = config;
            _state = state;
            _customers = customers;
            _startingState = startingState;
        }

        public async UniTask StartAsync(CancellationToken cancellation)
        {
            _state.NextDayRequested += OnNextDayRequested;

            if (await UniTask.WaitUntil(() => NetworkServer.active && _state.isServer, cancellationToken: cancellation).SuppressCancellationThrow())
                return;

            if (_state.Day == 0)
                OpenDay(_startingState.CompletedDays + 1);
        }

        public void Dispose() => _state.NextDayRequested -= OnNextDayRequested;

        public void Tick()
        {
            if (_state == null || !NetworkServer.active || !_state.isServer)
                return;

            switch (_state.Phase)
            {
                case DayPhase.Open when _state.TimeLeft <= 0:
                    _state.ServerSetPhase(DayPhase.Closing);
                    _customers.CloseShop();
                    break;
                case DayPhase.Closing when _customers.ActiveCount == 0:
                    _state.ServerSetPhase(DayPhase.Summary);
                    break;
            }
        }

        private void OnNextDayRequested() => OpenDay(_state.Day + 1);

        private void OpenDay(int day) => _state.ServerOpen(day, NetworkTime.time + _config.OpenDuration);
    }
}
