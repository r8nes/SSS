using System;
using VContainer.Unity;

namespace ShooterShop.Raids
{
    public sealed class AmbushHudPresenter : IStartable, IDisposable
    {
        private readonly AmbushHudView _view;
        private readonly IAmbushState _state;

        public AmbushHudPresenter(AmbushHudView view, IAmbushState state)
        {
            _view = view;
            _state = state;
        }

        public void Start()
        {
            _state.Changed += Refresh;
            Refresh();
        }

        public void Dispose() => _state.Changed -= Refresh;

        private void Refresh()
        {
            _view.SetVisible(_state.IsActive);
            _view.SetCounter($"Bandits killed: {_state.Killed} / {_state.Total}");
        }
    }
}
