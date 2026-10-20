using System;
using System.Collections.Generic;
using System.Linq;
using Mirror;
using ShooterShop.Days;
using ShooterShop.Economy;
using ShooterShop.Saves;
using ShooterShop.Storage;
using VContainer;

namespace ShooterShop.Upgrades
{
    public sealed class UpgradeNetworkState : NetworkBehaviour, IUnlocks, IUpgradeCommands
    {
        private readonly SyncList<int> _owned = new();

        private UpgradeCatalog _catalog;
        private IWallet _wallet;
        private IWarehouse _warehouse;
        private IDayStatistics _statistics;
        private StartingState _startingState;

        public event Action Changed;

        [Inject]
        public void Construct(UpgradeCatalog catalog, IWallet wallet, IWarehouse warehouse, IDayStatistics statistics, StartingState startingState)
        {
            _catalog = catalog;
            _wallet = wallet;
            _warehouse = warehouse;
            _statistics = statistics;
            _startingState = startingState;
        }

        public IEnumerable<UpgradeConfig> Owned =>
            _owned.Select(index => _catalog.TryGet(index, out var upgrade) ? upgrade : null).Where(u => u != null);

        public override void OnStartServer()
        {
            foreach (var upgrade in _startingState.Upgrades)
                _owned.Add(_catalog.IndexOf(upgrade));
        }

        public override void OnStartClient() => _owned.OnChange += OnOwnedChanged;

        public override void OnStopClient() => _owned.OnChange -= OnOwnedChanged;

        public bool IsOwned(UpgradeConfig upgrade) => upgrade != null && _owned.Contains(_catalog.IndexOf(upgrade));

        public bool IsUnlocked(UpgradeConfig requirement) => requirement == null || IsOwned(requirement);

        public void BuyUpgrade(UpgradeConfig upgrade)
        {
            if (isServer)
                ServerBuy(upgrade);
            else
                CmdBuy(_catalog.IndexOf(upgrade));
        }

        [Command(requiresAuthority = false)]
        private void CmdBuy(int index)
        {
            if (_catalog.TryGet(index, out var upgrade))
                ServerBuy(upgrade);
        }

        [Server]
        private void ServerBuy(UpgradeConfig upgrade)
        {
            if (IsOwned(upgrade) || !IsUnlocked(upgrade.Requires) || !_wallet.TrySpend(upgrade.Price))
                return;

            _statistics.RecordExpense(upgrade.Price);

            if (upgrade.StorageBonus > 0)
                _warehouse.IncreaseCapacity(upgrade.StorageBonus);

            _owned.Add(_catalog.IndexOf(upgrade));
        }

        private void OnOwnedChanged(SyncList<int>.Operation _, int __, int ___) => Changed?.Invoke();
    }
}
