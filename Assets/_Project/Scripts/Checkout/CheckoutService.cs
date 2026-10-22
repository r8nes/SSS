using System;
using Mirror;
using ShooterShop.Customers;
using ShooterShop.Feedback;
using ShooterShop.Storage;
using UnityEngine;
using VContainer.Unity;

namespace ShooterShop.Checkout
{
    public sealed class CheckoutService : ICheckout, IStartable, ITickable, IDisposable
    {
        private readonly CheckoutCounter _counter;
        private readonly CheckoutConfig _config;
        private readonly ICustomerService _customers;
        private readonly IWarehouse _warehouse;
        private readonly IShopFeed _feed;
        private Customer _payer;
        private bool _hasPayer;

        public CheckoutService(CheckoutCounter counter, CheckoutConfig config, ICustomerService customers, IWarehouse warehouse, IShopFeed feed)
        {
            _counter = counter;
            _config = config;
            _customers = customers;
            _warehouse = warehouse;
            _feed = feed;
        }

        public void Start() => _counter.TakeRequested += ServerTake;

        public void Dispose() => _counter.TakeRequested -= ServerTake;

        public void ServerOffer(Customer customer)
        {
            if (_hasPayer || customer == null || !customer.CanBeServed)
                return;

            var request = customer.Request;

            if (_warehouse.GetAmount(request.Product) < request.Amount)
            {
                _feed.Notify(string.Format(_config.OutOfStockMessage, request.Product.DisplayName), NotificationKind.Warning);
                return;
            }

            _payer = customer;
            _hasPayer = true;
            _counter.ServerOffer(request.Product, request.Amount, request.Price, customer.transform.position + Vector3.up * 0.3f);
        }

        public void ServerTake(NetworkIdentity collector)
        {
            if (!_hasPayer)
                return;

            var isSold = _payer != null && _customers.TrySell(_payer);
            Release(isSold && collector != null ? collector.netId : 0);
        }

        public void Tick()
        {
            if (_hasPayer && NetworkServer.active && (_payer == null || !_payer.CanBeServed))
                Release(0);
        }

        private void Release(uint collectorNetId)
        {
            _payer = null;
            _hasPayer = false;
            _counter.ServerClear(collectorNetId);
        }
    }
}
