using System;
using Mirror;
using ShooterShop.Catalog;
using ShooterShop.Customers;
using ShooterShop.Days;
using ShooterShop.Economy;
using ShooterShop.Storage;
using VContainer;

namespace ShooterShop.Network
{
    public sealed class ShopNetworkState : NetworkBehaviour, IShopState, IShopCommands
    {
        [SyncVar(hook = nameof(OnMoneySynced))]
        private int _money;

        [SyncVar(hook = nameof(OnStorageSynced))]
        private int _usedSpace;

        [SyncVar(hook = nameof(OnStorageSynced))]
        private int _capacity;

        private readonly SyncDictionary<int, int> _stock = new();

        private IWallet _wallet;
        private IWarehouse _warehouse;
        private ProductCatalog _catalog;
        private ICustomerService _customers;
        private IDayStatistics _statistics;

        public event Action MoneyChanged;
        public event Action StorageChanged;
        public event Action<ProductConfig> StockChanged;

        public int Money => _money;
        public int UsedSpace => _usedSpace;
        public int Capacity => _capacity;
        public int FreeSpace => _capacity - _usedSpace;

        [Inject]
        public void Construct(IWallet wallet, IWarehouse warehouse, ProductCatalog catalog, ICustomerService customers, IDayStatistics statistics)
        {
            _customers = customers;
            _statistics = statistics;
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

        public void Sell(Customer customer)
        {
            if (isServer)
                _customers.TrySell(customer);
            else
                CmdSell(customer);
        }

        public void Dismiss(Customer customer)
        {
            if (isServer)
                _customers.Dismiss(customer);
            else
                CmdDismiss(customer);
        }

        public override void OnStartServer()
        {
            _money = _wallet.Balance;
            _capacity = _warehouse.Capacity;
            _usedSpace = _warehouse.UsedSpace;

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

        [Command(requiresAuthority = false)]
        private void CmdSell(Customer customer)
        {
            if (customer != null)
                _customers.TrySell(customer);
        }

        [Command(requiresAuthority = false)]
        private void CmdDismiss(Customer customer)
        {
            if (customer != null)
                _customers.Dismiss(customer);
        }

        [Server]
        private void ServerPurchase(ProductConfig product, int amount)
        {
            if (!_warehouse.CanStore(product, amount))
                return;

            var cost = product.BuyPrice * amount;

            if (!_wallet.TrySpend(cost))
                return;

            _warehouse.TryAdd(product, amount);
            _statistics.RecordExpense(cost);
        }

        private void OnWalletChanged(int balance) => _money = balance;

        private void OnWarehouseChanged(ProductConfig product, int amount)
        {
            _usedSpace = _warehouse.UsedSpace;
            var index = _catalog.IndexOf(product);

            if (amount == 0)
                _stock.Remove(index);
            else
                _stock[index] = amount;
        }

        private void OnMoneySynced(int _, int __) => MoneyChanged?.Invoke();

        private void OnStorageSynced(int _, int __) => StorageChanged?.Invoke();

        private void OnStockSynced(SyncIDictionary<int, int>.Operation _, int index, int __)
        {
            if (_catalog.TryGet(index, out var product))
                StockChanged?.Invoke(product);
        }
    }
}
