using System;
using Mirror;
using ShooterShop.Catalog;
using ShooterShop.Economy;
using ShooterShop.Storage;
using VContainer;

namespace ShooterShop.Network
{
    public sealed class ShopNetworkState : NetworkBehaviour, IShopState, IShopCommands
    {
        [SyncVar(hook = nameof(OnMoneySynced))]
        private int _money;

        private readonly SyncDictionary<int, int> _stock = new();

        private IWallet _wallet;
        private IWarehouse _warehouse;
        private ProductCatalog _catalog;

        public event Action MoneyChanged;
        public event Action<ProductConfig> StockChanged;

        public int Money => _money;

        [Inject]
        public void Construct(IWallet wallet, IWarehouse warehouse, ProductCatalog catalog)
        {
            _wallet = wallet;
            _warehouse = warehouse;
            _catalog = catalog;
        }

        public int GetStock(ProductConfig product) =>
            _stock.TryGetValue(_catalog.IndexOf(product), out var amount) ? amount : 0;

        public void Purchase(ProductConfig product, int amount)
        {
            if (isServer)
                ServerPurchase(product, amount);
            else
                CmdPurchase(_catalog.IndexOf(product), amount);
        }

        public override void OnStartServer()
        {
            _money = _wallet.Balance;

            foreach (var (product, amount) in _warehouse.Stock)
                _stock[_catalog.IndexOf(product)] = amount;

            _wallet.BalanceChanged += OnWalletChanged;
            _warehouse.StockChanged += OnWarehouseChanged;
        }

        public override void OnStopServer()
        {
            _wallet.BalanceChanged -= OnWalletChanged;
            _warehouse.StockChanged -= OnWarehouseChanged;
        }

        public override void OnStartClient()
        {
            _stock.OnChange += OnStockSynced;
        }

        public override void OnStopClient()
        {
            _stock.OnChange -= OnStockSynced;
        }

        [Command(requiresAuthority = false)]
        private void CmdPurchase(int productIndex, int amount)
        {
            if (_catalog.TryGet(productIndex, out var product))
                ServerPurchase(product, amount);
        }

        [Server]
        private void ServerPurchase(ProductConfig product, int amount)
        {
            if (!_warehouse.CanStore(product, amount))
                return;

            if (_wallet.TrySpend(product.BuyPrice * amount))
                _warehouse.TryAdd(product, amount);
        }

        private void OnWalletChanged(int balance) => _money = balance;

        private void OnWarehouseChanged(ProductConfig product, int amount)
        {
            var index = _catalog.IndexOf(product);

            if (amount == 0)
                _stock.Remove(index);
            else
                _stock[index] = amount;
        }

        private void OnMoneySynced(int _, int __) => MoneyChanged?.Invoke();

        private void OnStockSynced(SyncIDictionary<int, int>.Operation _, int index, int __)
        {
            if (_catalog.TryGet(index, out var product))
                StockChanged?.Invoke(product);
        }
    }
}
