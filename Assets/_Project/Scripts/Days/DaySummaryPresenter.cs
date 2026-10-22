using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using ShooterShop.Input;
using ShooterShop.Menu;
using VContainer.Unity;

namespace ShooterShop.Days
{
    public sealed class DaySummaryPresenter : IStartable, IDisposable
    {
        private readonly DaySummaryView _view;
        private readonly ScreenFadeView _fade;
        private readonly DayConfig _config;
        private readonly IDayState _state;
        private readonly IDayCommands _commands;
        private readonly IPlayerInputLock _inputLock;
        private readonly ILocalPlayerSpawner _spawner;
        private readonly CancellationTokenSource _lifetime = new();
        private bool _isShowing;

        public DaySummaryPresenter(
            DaySummaryView view,
            ScreenFadeView fade,
            DayConfig config,
            IDayState state,
            IDayCommands commands,
            IPlayerInputLock inputLock,
            ILocalPlayerSpawner spawner)
        {
            _view = view;
            _fade = fade;
            _config = config;
            _state = state;
            _commands = commands;
            _inputLock = inputLock;
            _spawner = spawner;
        }

        public void Start()
        {
            _view.NextDayButton.onClick.AddListener(_commands.StartNextDay);
            _state.Changed += Refresh;
            _view.SetOpen(false);
            _fade.SetOpacity(0f);
            Refresh();
        }

        public void Dispose()
        {
            _lifetime.Cancel();
            _lifetime.Dispose();
            _view.NextDayButton.onClick.RemoveListener(_commands.StartNextDay);
            _state.Changed -= Refresh;
            _inputLock.Release(this);
        }

        private void Refresh()
        {
            var isSummary = _state.Phase == DayPhase.Summary;

            if (isSummary && !_isShowing)
                ShowAsync(_lifetime.Token).Forget();
            else if (!isSummary && _isShowing)
                HideAsync(_lifetime.Token).Forget();
            else if (isSummary)
                _view.SetCanContinue(_commands.CanStartNextDay);
        }

        private async UniTaskVoid ShowAsync(CancellationToken cancellation)
        {
            _isShowing = true;
            _inputLock.Acquire(this);
            _fade.FadeTo(1f, _config.SummaryFadeDuration);

            if (await UniTask.Delay(TimeSpan.FromSeconds(_config.SummaryFadeDuration), cancellationToken: cancellation).SuppressCancellationThrow() || !_isShowing)
                return;

            _view.SetCanContinue(_commands.CanStartNextDay);
            _view.SetOpen(true);
            _view.Reveal(new DaySummaryData(
                $"Day {_state.Day} complete",
                _state.Income,
                _state.Expenses,
                _state.Profit,
                $"Sales: {_state.Sales}    Lane rentals: {_state.LaneRentals}"), _config);
        }

        private async UniTaskVoid HideAsync(CancellationToken cancellation)
        {
            _isShowing = false;
            _view.SetOpen(false);
            _fade.SetOpacity(1f);
            _spawner.ReturnToSpawn();

            if (await UniTask.Delay(TimeSpan.FromSeconds(_config.SummaryBlackHold), cancellationToken: cancellation).SuppressCancellationThrow())
                return;

            _fade.FadeTo(0f, _config.SummaryFadeDuration);
            _inputLock.Release(this);
        }
    }
}
