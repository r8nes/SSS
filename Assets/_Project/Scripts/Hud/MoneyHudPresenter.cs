using System;
using ShooterShop.Network;
using VContainer.Unity;

namespace ShooterShop.Hud
{
    public sealed class MoneyHudPresenter : IStartable, IDisposable
    {
        private readonly MoneyHudView _view;
        private readonly IShopState _state;

        public MoneyHudPresenter(MoneyHudView view, IShopState state)
        {
            _view = view;
            _state = state;
        }

        public void Start()
        {
            _state.MoneyChanged += Refresh;
            Refresh();
        }

        public void Dispose() => _state.MoneyChanged -= Refresh;

        private void Refresh() => _view.SetMoney($"${_state.Money:N0}");
    }
}
