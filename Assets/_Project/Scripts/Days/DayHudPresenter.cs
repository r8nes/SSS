using System;
using VContainer.Unity;

namespace ShooterShop.Days
{
    public sealed class DayHudPresenter : ITickable
    {
        private readonly DayHudView _view;
        private readonly IDayState _state;

        public DayHudPresenter(DayHudView view, IDayState state)
        {
            _view = view;
            _state = state;
        }

        public void Tick()
        {
            if (_view != null)
                _view.SetText(Format());
        }

        private string Format() => _state.Phase switch
        {
            DayPhase.Open => $"Day {_state.Day}  {FormatTime(_state.TimeLeft)}",
            DayPhase.Closing => $"Day {_state.Day}  Closing...",
            DayPhase.Summary => $"Day {_state.Day}  Closed",
            _ => string.Empty
        };

        private static string FormatTime(double seconds)
        {
            var total = Math.Max(0, (int)Math.Ceiling(seconds));
            return $"{total / 60}:{total % 60:00}";
        }
    }
}
