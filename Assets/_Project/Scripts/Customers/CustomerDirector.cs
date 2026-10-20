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
        private readonly ProductCatalog _catalog;
        private readonly IWarehouse _warehouse;
        private readonly IWallet _wallet;
        private readonly IShootingRange _range;
        private readonly IDayState _day;
        private readonly IDayStatistics _statistics;
        private readonly IUnlocks _unlocks;
        private readonly IObjectResolver _resolver;
        private readonly List<Customer> _customers = new();
        private readonly Dictionary<Customer, ShootingLane> _lanes = new();
        private readonly Customer[] _slots;

        public CustomerDirector(
            CustomerConfig config,
            RangeConfig rangeConfig,
            ShopLayout layout,
            ProductCatalog catalog,
            IWarehouse warehouse,
            IWallet wallet,
            IShootingRange range,
            IDayState day,
            IDayStatistics statistics,
            IUnlocks unlocks,
            IObjectResolver resolver)
        {
            _config = config;
            _rangeConfig = rangeConfig;
            _layout = layout;
            _catalog = catalog;
            _warehouse = warehouse;
            _wallet = wallet;
            _range = range;
            _day = day;
            _statistics = statistics;
            _unlocks = unlocks;
            _resolver = resolver;
            _slots = new Customer[layout.CounterSlots.Count];
        }

        public int ActiveCount => _customers.Count;

        public async UniTask StartAsync(CancellationToken cancellation)
        {
            _range.SessionFinished += OnSessionFinished;
            await UniTask.WaitUntil(() => NetworkServer.active, cancellationToken: cancellation);

            while (!cancellation.IsCancellationRequested && NetworkServer.active)
            {
                var interval = Random.Range(_config.SpawnInterval.x, _config.SpawnInterval.y);
                await UniTask.Delay(TimeSpan.FromSeconds(interval), cancellationToken: cancellation);

                if (_day.IsOpen && _customers.Count < _config.MaxCustomers && TryGetFreeSlot(out var slot))
                    Spawn(slot);
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
            if (customer.State != CustomerState.Waiting || !customer.HasRequest)
                return false;

            var request = customer.Request;

            if (!_warehouse.TryRemove(request.Product, request.Amount))
                return false;

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

                if (customer != null && customer.State is CustomerState.Arriving or CustomerState.Waiting)
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
                case CustomerState.Arriving when customer.ServerHasArrived():
                    customer.ServerFace(_layout.CounterSlots[customer.CounterSlot].rotation);
                    customer.ServerSetState(CustomerState.Waiting);
                    break;
                case CustomerState.Waiting when Time.time - customer.WaitingSince > _config.Patience:
                    Leave(customer);
                    break;
                case CustomerState.GoingToLane when customer.ServerHasArrived():
                    StartShooting(customer);
                    break;
                case CustomerState.Leaving when customer.ServerHasArrived():
                    _customers.Remove(customer);
                    NetworkServer.Destroy(customer.gameObject);
                    break;
            }
        }

        private void Spawn(int slot)
        {
            var entrance = _layout.Entrance;
            var customer = _resolver.Instantiate(_config.Prefab, entrance.position, entrance.rotation);
            var request = PickRequest(customer);

            customer.ServerInitialize(request, _config.WalkSpeed);
            NetworkServer.Spawn(customer.gameObject);

            customer.CounterSlot = slot;
            _slots[slot] = customer;
            _customers.Add(customer);
            customer.ServerMoveTo(_layout.CounterSlots[slot].position);
        }

        private void SendToLane(Customer customer, ShootingLane lane)
        {
            FreeSlot(customer);
            customer.ServerSetState(CustomerState.GoingToLane);
            customer.ServerMoveTo(lane.StandPoint.position);
        }

        private void StartShooting(Customer customer)
        {
            var lane = _lanes[customer];
            var toTarget = Vector3.ProjectOnPlane(lane.Target.position - customer.transform.position, Vector3.up);

            customer.ServerFace(Quaternion.LookRotation(toTarget));
            customer.ServerSetState(CustomerState.Shooting);
            _range.StartSession(lane, customer);
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
            FreeSlot(customer);

            if (_lanes.Remove(customer, out var lane))
                _range.Release(lane);

            customer.ServerSetState(CustomerState.Leaving);
            customer.ServerMoveTo(_layout.Exit.position);
        }

        private void FreeSlot(Customer customer)
        {
            if (customer.CounterSlot >= 0 && _slots[customer.CounterSlot] == customer)
                _slots[customer.CounterSlot] = null;

            customer.CounterSlot = -1;
        }

        private bool TryGetFreeSlot(out int slot)
        {
            slot = Array.FindIndex(_slots, s => s == null);
            return slot >= 0;
        }

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
            var stock = _warehouse.GetAmount(shopProduct);

            if (stock > 0)
                maxAmount = Mathf.Min(maxAmount, stock);

            return new CustomerRequest(shopProduct, Random.Range(1, maxAmount + 1));
        }

        private ProductConfig PickProduct(IReadOnlyList<ProductConfig> candidates)
        {
            var pool = candidates.Where(p => _unlocks.IsUnlocked(p.RequiredUpgrade)).ToList();
            var inStock = pool.Where(p => _warehouse.GetAmount(p) > 0).ToList();
            var source = inStock.Count > 0 && Random.value < _config.InStockRequestChance ? inStock : pool;
            return source[Random.Range(0, source.Count)];
        }
    }
}
