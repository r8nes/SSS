using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using Mirror;
using ShooterShop.Catalog;
using ShooterShop.Days;
using ShooterShop.Economy;
using ShooterShop.Range;
using ShooterShop.Storage;
using ShooterShop.Store;
using ShooterShop.Upgrades;
using UnityEngine;
using VContainer;
using VContainer.Unity;
using Random = UnityEngine.Random;

namespace ShooterShop.Customers
{
    public sealed class CustomerDirector : ICustomerService, IAsyncStartable, ITickable, IDisposable
    {
        private readonly CustomerConfig _config;
        private readonly RangeConfig _rangeConfig;
        private readonly ShopLayout _layout;
        private readonly StoreLayout _store;
        private readonly ProductCatalog _catalog;
        private readonly IWarehouse _warehouse;
        private readonly IWallet _wallet;
        private readonly IShootingRange _range;
        private readonly IDeliveryService _delivery;
        private readonly IDayState _day;
        private readonly IDayStatistics _statistics;
        private readonly IUnlocks _unlocks;
        private readonly IObjectResolver _resolver;
        private readonly List<Customer> _customers = new();
        private readonly Dictionary<Customer, ShootingLane> _lanes = new();
        private readonly Dictionary<Customer, ProductStand> _stands = new();
        private readonly HashSet<Customer> _holdingItems = new();
        private readonly List<Customer> _queue = new();

        public CustomerDirector(
            CustomerConfig config,
            RangeConfig rangeConfig,
            ShopLayout layout,
            StoreLayout store,
            ProductCatalog catalog,
            IWarehouse warehouse,
            IWallet wallet,
            IShootingRange range,
            IDeliveryService delivery,
            IDayState day,
            IDayStatistics statistics,
            IUnlocks unlocks,
            IObjectResolver resolver)
        {
            _config = config;
            _rangeConfig = rangeConfig;
            _layout = layout;
            _store = store;
            _catalog = catalog;
            _warehouse = warehouse;
            _wallet = wallet;
            _range = range;
            _delivery = delivery;
            _day = day;
            _statistics = statistics;
            _unlocks = unlocks;
            _resolver = resolver;
        }

        public int ActiveCount => _customers.Count;

        public async UniTask StartAsync(CancellationToken cancellation)
        {
            _range.SessionFinished += OnSessionFinished;

            if (await UniTask.WaitUntil(() => NetworkServer.active, cancellationToken: cancellation).SuppressCancellationThrow())
                return;

            while (!cancellation.IsCancellationRequested && NetworkServer.active)
            {
                var interval = Random.Range(_config.SpawnInterval.x, _config.SpawnInterval.y);

                if (await UniTask.Delay(TimeSpan.FromSeconds(interval), cancellationToken: cancellation).SuppressCancellationThrow())
                    return;

                if (_day.IsOpen && _customers.Count < _config.MaxCustomers)
                    Spawn();
            }
        }

        public void Dispose() => _range.SessionFinished -= OnSessionFinished;

        public void Tick()
        {
            if (!NetworkServer.active)
                return;

            for (var i = _customers.Count - 1; i >= 0; i--)
                UpdateCustomer(_customers[i]);
        }

        public bool TrySell(Customer customer)
        {
            if (!customer.CanBeServed)
                return false;

            var request = customer.Request;

            if (!_warehouse.TryRemove(request.Product, request.Amount))
                return false;

            _holdingItems.Remove(customer);
            _wallet.Add(request.Price);
            _statistics.RecordSale(request.Price, request.IsRangeVisit);

            if (_lanes.TryGetValue(customer, out var lane))
                SendToLane(customer, lane);
            else
                Leave(customer);

            return true;
        }

        public void Dismiss(Customer customer)
        {
            if (customer.State == CustomerState.Waiting)
                Leave(customer);
        }

        public void CloseShop()
        {
            for (var i = _customers.Count - 1; i >= 0; i--)
            {
                var customer = _customers[i];

                if (customer != null && customer.State is CustomerState.Browsing or CustomerState.Arriving or CustomerState.Waiting)
                    Leave(customer);
            }
        }

        private void UpdateCustomer(Customer customer)
        {
            if (customer == null)
            {
                _customers.Remove(customer);
                return;
            }

            switch (customer.State)
            {
                case CustomerState.Browsing when customer.ServerTickArrival():
                    PickFromStand(customer);
                    break;
                case CustomerState.Arriving when customer.ServerTickArrival():
                    customer.ServerSetState(CustomerState.Waiting);
                    break;
                case CustomerState.Waiting when Time.time - customer.WaitingSince > _config.Patience:
                    Leave(customer);
                    break;
                case CustomerState.Waiting:
                    customer.ServerTickArrival();
                    break;
                case CustomerState.GoingToLane when customer.ServerTickArrival():
                    StartShooting(customer);
                    break;
                case CustomerState.Leaving when customer.ServerHasArrived():
                    _customers.Remove(customer);
                    NetworkServer.Destroy(customer.gameObject);
                    break;
            }
        }

        private void Spawn()
        {
            var entrance = _layout.Entrance;
            var customer = _resolver.Instantiate(_config.Prefab, entrance.position, entrance.rotation);
            var request = PickRequest(customer);
            var stand = FindStand(request.Product);

            customer.ServerInitialize(request, _config.WalkSpeed);
            NetworkServer.Spawn(customer.gameObject);
            _customers.Add(customer);

            if (stand == null)
            {
                Leave(customer);
                return;
            }

            _stands[customer] = stand;
            customer.ServerSetState(CustomerState.Browsing);
            customer.ServerMoveTo(stand.CustomerPoint.position, stand.CustomerPoint.rotation);
        }

        private void PickFromStand(Customer customer)
        {
            if (_queue.Count >= _layout.QueuePoints.Count)
                return;

            var request = customer.Request;
            var taken = _stands[customer].ServerTake(request.Product, request.Amount);

            if (taken == 0)
            {
                Leave(customer);
                return;
            }

            _holdingItems.Add(customer);
            customer.ServerSetAmount(taken);
            customer.ServerSetState(CustomerState.Arriving);
            _queue.Add(customer);
            MoveToQueuePosition(customer, _queue.Count - 1);
        }

        private void SendToLane(Customer customer, ShootingLane lane)
        {
            RemoveFromQueue(customer);
            customer.ServerSetState(CustomerState.GoingToLane);

            var stand = lane.StandPoint.position;
            var toTarget = Vector3.ProjectOnPlane(lane.Target.position - stand, Vector3.up);
            customer.ServerMoveTo(stand, Quaternion.LookRotation(toTarget));
        }

        private void StartShooting(Customer customer)
        {
            customer.ServerSetState(CustomerState.Shooting);
            _range.StartSession(_lanes[customer], customer);
        }

        private void OnSessionFinished(ShootingLane lane)
        {
            var customer = _lanes.FirstOrDefault(pair => pair.Value == lane).Key;

            if (customer != null)
                Leave(customer);
            else
                _range.Release(lane);
        }

        private void Leave(Customer customer)
        {
            RemoveFromQueue(customer);
            ReturnItems(customer);
            _stands.Remove(customer);

            if (_lanes.Remove(customer, out var lane))
                _range.Release(lane);

            customer.ServerSetState(CustomerState.Leaving);
            customer.ServerMoveTo(_layout.Exit.position);
        }

        private void ReturnItems(Customer customer)
        {
            if (!_holdingItems.Remove(customer) || !customer.HasRequest)
                return;

            var request = customer.Request;
            var left = request.Amount;

            if (_stands.TryGetValue(customer, out var stand))
                left -= stand.ServerPut(request.Product, left);

            if (left > 0)
                _delivery.DeliverNow(request.Product, left);
        }

        private void RemoveFromQueue(Customer customer)
        {
            if (!_queue.Remove(customer))
                return;

            customer.ServerSetQueuePosition(-1);

            for (var i = 0; i < _queue.Count; i++)
                if (_queue[i].QueuePosition != i)
                    MoveToQueuePosition(_queue[i], i);
        }

        private void MoveToQueuePosition(Customer customer, int position)
        {
            var point = _layout.QueuePoints[position];
            customer.ServerSetQueuePosition(position);
            customer.ServerMoveTo(point.position, point.rotation);
        }

        private ProductStand FindStand(ProductConfig product)
        {
            var accepting = _store.Stands.Where(s => s.Accepts(product)).ToList();
            return accepting.FirstOrDefault(s => s.CountOf(product) > 0) ?? accepting.FirstOrDefault();
        }

        private int OnShelves(ProductConfig product) => _store.Stands.Sum(s => s.CountOf(product));

        private CustomerRequest PickRequest(Customer customer)
        {
            var ammo = _catalog.Products.Where(p => p.IsAmmo && _unlocks.IsUnlocked(p.RequiredUpgrade)).ToList();

            if (ammo.Count > 0 && Random.value < _rangeConfig.VisitorChance && _range.TryReserveLane(out var lane))
            {
                _lanes[customer] = lane;
                var product = PickProduct(ammo);
                var boxes = Random.Range(1, _rangeConfig.MaxAmmoBoxes + 1);
                return new CustomerRequest(product, boxes, _rangeConfig.LaneFee);
            }

            var shopProduct = PickProduct(_catalog.Products);
            var maxAmount = shopProduct.IsAmmo ? _config.MaxAmmoAmount : _config.MaxWeaponAmount;
            return new CustomerRequest(shopProduct, Random.Range(1, maxAmount + 1));
        }

        private ProductConfig PickProduct(IReadOnlyList<ProductConfig> candidates)
        {
            var pool = candidates.Where(p => _unlocks.IsUnlocked(p.RequiredUpgrade)).ToList();
            var onShelves = pool.Where(p => OnShelves(p) > 0).ToList();
            var source = onShelves.Count > 0 && Random.value < _config.InStockRequestChance ? onShelves : pool;
            return source[Random.Range(0, source.Count)];
        }
    }
}
