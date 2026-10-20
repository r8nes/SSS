using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using Mirror;
using ShooterShop.Catalog;
using ShooterShop.Economy;
using ShooterShop.Storage;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace ShooterShop.Customers
{
    public sealed class CustomerDirector : ICustomerService, IAsyncStartable, ITickable
    {
        private readonly CustomerConfig _config;
        private readonly ShopLayout _layout;
        private readonly ProductCatalog _catalog;
        private readonly IWarehouse _warehouse;
        private readonly IWallet _wallet;
        private readonly IObjectResolver _resolver;
        private readonly List<Customer> _customers = new();
        private readonly Customer[] _slots;

        public CustomerDirector(
            CustomerConfig config,
            ShopLayout layout,
            ProductCatalog catalog,
            IWarehouse warehouse,
            IWallet wallet,
            IObjectResolver resolver)
        {
            _config = config;
            _layout = layout;
            _catalog = catalog;
            _warehouse = warehouse;
            _wallet = wallet;
            _resolver = resolver;
            _slots = new Customer[layout.CounterSlots.Count];
        }

        public async UniTask StartAsync(CancellationToken cancellation)
        {
            await UniTask.WaitUntil(() => NetworkServer.active, cancellationToken: cancellation);

            while (!cancellation.IsCancellationRequested && NetworkServer.active)
            {
                var interval = Random.Range(_config.SpawnInterval.x, _config.SpawnInterval.y);
                await UniTask.Delay(System.TimeSpan.FromSeconds(interval), cancellationToken: cancellation);

                if (_customers.Count < _config.MaxCustomers && TryGetFreeSlot(out var slot))
                    Spawn(slot);
            }
        }

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
            Leave(customer);
            return true;
        }

        public void Dismiss(Customer customer)
        {
            if (customer.State == CustomerState.Waiting)
                Leave(customer);
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

            customer.ServerInitialize(PickRequest(), _config.WalkSpeed);
            NetworkServer.Spawn(customer.gameObject);

            customer.CounterSlot = slot;
            _slots[slot] = customer;
            _customers.Add(customer);
            customer.ServerMoveTo(_layout.CounterSlots[slot].position);
        }

        private void Leave(Customer customer)
        {
            if (customer.CounterSlot >= 0 && _slots[customer.CounterSlot] == customer)
                _slots[customer.CounterSlot] = null;

            customer.CounterSlot = -1;
            customer.ServerSetState(CustomerState.Leaving);
            customer.ServerMoveTo(_layout.Exit.position);
        }

        private bool TryGetFreeSlot(out int slot)
        {
            slot = System.Array.FindIndex(_slots, s => s == null);
            return slot >= 0;
        }

        private CustomerRequest PickRequest()
        {
            var inStock = _catalog.Products.Where(p => _warehouse.GetAmount(p) > 0).ToList();
            var fromStock = inStock.Count > 0 && Random.value < _config.InStockRequestChance;
            var pool = fromStock ? inStock : _catalog.Products.ToList();
            var product = pool[Random.Range(0, pool.Count)];

            var maxAmount = product.IsAmmo ? _config.MaxAmmoAmount : _config.MaxWeaponAmount;

            if (fromStock)
                maxAmount = Mathf.Min(maxAmount, _warehouse.GetAmount(product));

            return new CustomerRequest(product, Random.Range(1, maxAmount + 1));
        }
    }
}
