using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Mirror;
using ShooterShop.Days;
using ShooterShop.Network;
using ShooterShop.Raids;
using ShooterShop.Saves;
using VContainer.Unity;

namespace ShooterShop.Intro
{
    public sealed class IntroDirector : IAsyncStartable, IIntroCommands, ITickable, IDisposable
    {
        private readonly IntroNetworkState _state;
        private readonly IntroLayout _layout;
        private readonly IntroConfig _config;
        private readonly StartingState _startingState;
        private readonly IDayState _day;
        private readonly IRaid _raid;
        private int _visitedDay;
        private bool _isReady;

        public IntroDirector(IntroNetworkState state, IntroLayout layout, IntroConfig config, StartingState startingState, IDayState day, IRaid raid)
        {
            _state = state;
            _layout = layout;
            _config = config;
            _startingState = startingState;
            _day = day;
            _raid = raid;
        }

        public async UniTask StartAsync(CancellationToken cancellation)
        {
            if (await UniTask.WaitUntil(() => _state.IsSpawnedOnServer() && _day.Day > 0, cancellationToken: cancellation).SuppressCancellationThrow())
                return;

            if (_startingState.CompletedDays > 0)
                _state.ServerSetPhase(IntroPhase.Skipped);

            _isReady = true;
            _day.Changed += OnDayChanged;
            OnDayChanged();
        }

        public void Dispose() => _day.Changed -= OnDayChanged;

        public void ServerFinish()
        {
            if (!_config.TryGetVisit(_state.PendingVisit, out var visit))
                return;

            _state.ServerSetVisit(-1);

            if (!_state.IsCompleted)
                _state.ServerSetPhase(IntroPhase.Completed);

            _layout.Boss.ServerLeave(_layout.BossExit.position);

            if (visit.StartsAmbush)
                _raid.ServerStartAmbush();
        }

        public void Tick()
        {
            var boss = _layout.Boss;

            if (_isReady && NetworkServer.active && boss.IsLeaving && boss.ServerHasArrived())
                boss.ServerHide();
        }

        private void OnDayChanged()
        {
            if (_day.Phase != DayPhase.Preparing || _visitedDay == _day.Day)
                return;

            _visitedDay = _day.Day;
            var visit = _config.VisitIndexOf(_day.Day);

            if (visit < 0 || (_day.Day == 1 && _state.Phase == IntroPhase.Skipped))
            {
                if (!_state.HasPendingVisit)
                    _layout.Boss.ServerHide();

                return;
            }

            _state.ServerSetVisit(visit);
            _layout.Boss.ServerAppear(_layout.BossSpawn.position, _layout.BossSpawn.rotation);
        }
    }
}
