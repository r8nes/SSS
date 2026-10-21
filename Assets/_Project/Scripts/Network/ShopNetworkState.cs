using System;
using Mirror;
using ShooterShop.Catalog;
using ShooterShop.Customers;
using ShooterShop.Days;
using ShooterShop.Economy;
using ShooterShop.Feedback;
using ShooterShop.Storage;
using ShooterShop.Store;
using ShooterShop.Upgrades;
using VContainer;

namespace ShooterShop.Network
{
    public sealed class ShopNetworkState : NetworkBehaviour, IShopState, IShopCommands, ISupplierPricing
    {
        [SyncVar(hook = nameof(OnMoneySynced))]
        private int _money;

        [SyncVar(hook = nameof(OnStorageSynced))]
        private int _usedSpace;

        [SyncVar(hook = nameof(OnStorageSynced))]
        private int _capacity;

        private readonly SyncDictionary<int, int> _stock = new();
        private readonly SyncDictionary<int, int> _priceChanges = new();

        private IWallet _wallet;
        private IWarehouse _warehouse;
        private ProductCatalog _catalog;
        private ICustomerService _customers;
        private IDayStatistics _statistics;
        private IUnlocks _unlocks;
        private IDeliveryService _delivery;
        private IDayState _day;
        private SupplierPricingConfig _pricing;
        private IShopFeed _feed;

        public event Action MoneyChanged;
        public event Action StorageChanged;
        public event Action<ProductConfig> StockChanged;
        public event Action PricesChanged;

        public int Money => _money;
        public int UsedSpace => _usedSpace;
        public int Capacity => _capacity;
        public int FreeSpace => _capacity - _usedSpace;

        [Inject]
        public void Construct(IWallet wallet, IWarehouse warehouse, ProductCatalog catalog, ICustomerService customers, IDayStatistics statistics, IUnlocks unlocks, IDeliveryService delivery, IDayState day, SupplierPricingConfig pricing, IShopFeed feed)
        {
            _pricing = pricing;
            _feed = feed;
            _day = day;
            _wallet = wallet;
            _warehouse = warehouse;
            _catalog = catalog;
            _customers = customers;
            _statistics = statistics;
            _unlocks = unlocks;
            _delivery = delivery;
        }

        public int GetStock(ProductConfig product) =>
            _stock.TryGetValue(_catalog.IndexOf(product), out var amount) ? amount : 0;

        public int GetPurchaseCost(ProductConfig product, int amount) =>
            UnityEngine.Mathf.RoundToInt(product.BuyPrice * amount
                * (1f + GetPriceChangePercent(product) / 100f)
                * (1f - (_day?.CurrentEvent?.PurchaseDiscount ?? 0f)));

        public int GetPriceChangePercent(ProductConfig product) =>
            _priceChanges.TryGetValue(_catalog.IndexOf(product), out var percent) ? percent : 0;

        [Server]
        public void ServerRollPrices()
        {
            _priceChanges.Clear();
            ProductConfig bestSale = null;
            var bestPercent = 0;

            foreach (var product in _catalog.Products)
            {
                var percent = _pricing.RollChangePercent();

                if (percent == 0)
                    continue;

                _priceChanges[_catalog.IndexOf(product)] = percent;

                if (percent < bestPercent)
                {
                    bestPercent = percent;
                    bestSale = product;
                }
            }

            PricesChanged?.Invoke();

            if (bestSale != null)
                _feed.Notify($"Supplier sale today: {bestSale.DisplayName} {bestPercent}%", NotificationKind.Good);
        }

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
            _warehouse.CapacityChanged += OnCapacityChanged;
        }

        public override void OnStopServer()
        {
            _wallet.BalanceChanged -= OnWalletChanged;
            _warehouse.StockChanged -= OnWarehouseChanged;
            _warehouse.CapacityChanged -= OnCapacityChanged;
        }

        public override void OnStartClient()
        {
            _stock.OnChange += OnStockSynced;
            _priceChanges.OnChange += OnPricesSynced;
        }

        public override void OnStopClient()
        {
            _stock.OnChange -= OnStockSynced;
            _priceChanges.OnChange -= OnPricesSynced;
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
            if (!_unlocks.IsUnlocked(product.RequiredUpgrade) || !_warehouse.CanStore(product, amount))
                return;

            var cost = GetPurchaseCost(product, amount);

            if (!_wallet.TrySpend(cost))
                return;

            _warehouse.TryAdd(product, amount);
            _statistics.RecordExpense(cost);
            _delivery.Deliver(product, amount);
        }

        private void OnWalletChanged(int balance) => _money = balance;

        private void OnCapacityChanged() => _capacity = _warehouse.Capacity;

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

        private void OnPricesSynced(SyncIDictionary<int, int>.Operation _, int __, int ___)
        {
            if (!isServer)
                PricesChanged?.Invoke();
        }

        private void OnStorageSynced(int _, int __) => StorageChanged?.Invoke();

        private void OnStockSynced(SyncIDictionary<int, int>.Operation _, int index, int __)
        {
            if (_catalog.TryGet(index, out var product))
                StockChanged?.Invoke(product);
        }
    }
}
