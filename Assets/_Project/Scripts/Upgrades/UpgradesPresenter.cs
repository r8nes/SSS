using System;
using System.Collections.Generic;
using ShooterShop.Network;
using ShooterShop.Purchasing;
using VContainer.Unity;

namespace ShooterShop.Upgrades
{
    public sealed class UpgradesPresenter : IStartable, IDisposable
    {
        private readonly PurchaseTerminalView _view;
        private readonly UpgradeCatalog _catalog;
        private readonly IUnlocks _unlocks;
        private readonly IUpgradeCommands _commands;
        private readonly IShopState _shop;
        private readonly Dictionary<UpgradeConfig, UpgradeCardView> _cards = new();

        public UpgradesPresenter(
            PurchaseTerminalView view,
            UpgradeCatalog catalog,
            IUnlocks unlocks,
            IUpgradeCommands commands,
            IShopState shop)
        {
            _view = view;
            _catalog = catalog;
            _unlocks = unlocks;
            _commands = commands;
            _shop = shop;
        }

        public void Start()
        {
            foreach (var upgrade in _catalog.Upgrades)
            {
                var card = _view.AddUpgradeCard();
                card.SetInfo(upgrade.DisplayName, upgrade.Description);
                card.BuyButton.onClick.AddListener(() => Buy(upgrade));
                _cards[upgrade] = card;
            }

            _unlocks.Changed += Refresh;
            _shop.MoneyChanged += Refresh;
            Refresh();
        }

        public void Dispose()
        {
            _unlocks.Changed -= Refresh;
            _shop.MoneyChanged -= Refresh;
        }

        private void Buy(UpgradeConfig upgrade)
        {
            if (!_unlocks.IsUnlocked(upgrade.Requires))
                _view.SetMessage($"Requires {upgrade.Requires.DisplayName}");
            else if (_shop.Money < upgrade.Price)
                _view.SetMessage($"Not enough money: need ${upgrade.Price:N0}");
            else
            {
                _commands.BuyUpgrade(upgrade);
                _view.SetMessage($"Purchased {upgrade.DisplayName}");
            }
        }

        private void Refresh()
        {
            foreach (var (upgrade, card) in _cards)
            {
                var isOwned = _unlocks.IsOwned(upgrade);
                var isAvailable = _unlocks.IsUnlocked(upgrade.Requires);

                var status = isOwned ? "Owned"
                    : isAvailable ? string.Empty
                    : $"Requires {upgrade.Requires.DisplayName}";

                card.SetState(status, $"Buy ${upgrade.Price:N0}", isAvailable && _shop.Money >= upgrade.Price, isOwned);
            }
        }
    }
}
