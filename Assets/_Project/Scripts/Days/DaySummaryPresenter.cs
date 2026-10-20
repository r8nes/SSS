using System;
using ShooterShop.Input;
using VContainer.Unity;

namespace ShooterShop.Days
{
    public sealed class DaySummaryPresenter : IStartable, IDisposable
    {
        private readonly DaySummaryView _view;
        private readonly IDayState _state;
        private readonly IDayCommands _commands;
        private readonly IPlayerInputLock _inputLock;

        public DaySummaryPresenter(DaySummaryView view, IDayState state, IDayCommands commands, IPlayerInputLock inputLock)
        {
            _view = view;
            _state = state;
            _commands = commands;
            _inputLock = inputLock;
        }

        public void Start()
        {
            _view.NextDayButton.onClick.AddListener(_commands.StartNextDay);
            _state.Changed += Refresh;
            _view.SetOpen(false);
            Refresh();
        }

        public void Dispose()
        {
            _view.NextDayButton.onClick.RemoveListener(_commands.StartNextDay);
            _state.Changed -= Refresh;
            _inputLock.Release(this);
        }

        private void Refresh()
        {
            var isSummary = _state.Phase == DayPhase.Summary;

            if (isSummary)
                Show();
            else if (_view.IsOpen)
                Hide();
        }

        private void Show()
        {
            _view.SetSummary(
                $"Day {_state.Day} complete",
                $"Income  ${_state.Income:N0}",
                $"Expenses  ${_state.Expenses:N0}",
                $"Profit  {(_state.Profit < 0 ? "-" : "")}${Math.Abs(_state.Profit):N0}",
                _state.Profit >= 0,
                $"Sales: {_state.Sales}    Lane rentals: {_state.LaneRentals}");
            _view.SetCanContinue(_commands.CanStartNextDay);

            if (!_view.IsOpen)
            {
                _inputLock.Acquire(this);
                _view.SetOpen(true);
            }
        }

        private void Hide()
        {
            _view.SetOpen(false);
            _inputLock.Release(this);
        }
    }
}
