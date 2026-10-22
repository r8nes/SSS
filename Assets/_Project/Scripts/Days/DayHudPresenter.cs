using VContainer.Unity;

namespace ShooterShop.Days
{
    public sealed class DayHudPresenter : ITickable
    {
        private readonly DayHudView _view;
        private readonly IDayState _state;
        private readonly DayConfig _config;

        public DayHudPresenter(DayHudView view, IDayState state, DayConfig config)
        {
            _config = config;
            _view = view;
            _state = state;
        }

        public void Tick()
        {
            if (_view == null)
                return;

            var dayEvent = _state.CurrentEvent;
            _view.SetText(dayEvent != null && _state.Day > 0 ? $"{Format()}\n<size=70%>{dayEvent.Title}</size>" : Format());
        }

        private string Format() => _state.Phase switch
        {
            DayPhase.Preparing when _state.Day > 0 => $"Day {_state.Day}  Preparing",
            DayPhase.Open when _state.IsAcceptingCustomers => $"Day {_state.Day}  {_config.ClockAt(_state.TimeLeft)}",
            DayPhase.Open => $"Day {_state.Day}  {_config.ClockAt(0)}  <size=70%>Flip the sign to close</size>",
            DayPhase.Closing => $"Day {_state.Day}  Closing...",
            DayPhase.Summary => $"Day {_state.Day}  Closed",
            _ => string.Empty
        };
    }
}
