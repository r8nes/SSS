using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Mirror;
using ShooterShop.Catalog;
using ShooterShop.Customers;
using ShooterShop.Feedback;
using UnityEngine;
using UnityEngine.AI;
using VContainer;
using VContainer.Unity;

namespace ShooterShop.Store
{
    public sealed class DeliveryService : IDeliveryService, ITickable, IDisposable
    {
        private readonly StoreConfig _config;
        private readonly StoreLayout _layout;
        private readonly ShopLayout _street;
        private readonly IShopFeed _feed;
        private readonly IObjectResolver _resolver;
        private readonly CancellationTokenSource _lifetime = new();
        private readonly Dictionary<Customer, (ProductConfig Product, int Amount)> _couriers = new();
        private readonly List<Customer> _leaving = new();
        private int _nextSpot;

        public DeliveryService(StoreConfig config, StoreLayout layout, ShopLayout street, IShopFeed feed, IObjectResolver resolver)
        {
            _config = config;
            _layout = layout;
            _street = street;
            _feed = feed;
            _resolver = resolver;
        }

        public void Deliver(ProductConfig product, int amount) => DeliverAsync(product, amount).Forget();

        public void Dispose()
        {
            _lifetime.Cancel();
            _lifetime.Dispose();
        }

        public void Tick()
        {
            if (!NetworkServer.active)
                return;

            if (_couriers.Count > 0)
                TickCouriers();

            for (var i = _leaving.Count - 1; i >= 0; i--)
            {
                var courier = _leaving[i];

                if (courier != null && !courier.ServerHasArrived())
                    continue;

                _leaving.RemoveAt(i);

                if (courier != null)
                    NetworkServer.Destroy(courier.gameObject);
            }
        }

        public void DeliverNow(ProductConfig product, int amount)
        {
            while (amount > 0)
            {
                var units = Mathf.Min(amount, product.UnitsPerBox);
                SpawnBox(product, units);
                amount -= units;
            }
        }

        private async UniTaskVoid DeliverAsync(ProductConfig product, int amount)
        {
            var delay = TimeSpan.FromSeconds(_config.DeliveryDelay);

            if (await UniTask.Delay(delay, cancellationToken: _lifetime.Token).SuppressCancellationThrow() || !NetworkServer.active)
                return;

            SendCourier(product, amount);
        }

        private void SendCourier(ProductConfig product, int amount)
        {
            var entrance = _street.Entrance;
            var courier = _resolver.Instantiate(_config.CourierPrefab, entrance.position, entrance.rotation);

            courier.ServerInitializeCourier(_config.CourierSpeed);
            NetworkServer.Spawn(courier.gameObject);
            courier.ServerMoveTo(NavigablePoint(_layout.DeliveryPoint.position));
            _couriers[courier] = (product, amount);
            _feed.Notify($"Courier is on the way: {product.DisplayName} x{amount}");
        }

        private void TickCouriers()
        {
            foreach (var courier in new List<Customer>(_couriers.Keys))
            {
                if (courier == null)
                {
                    _couriers.Remove(courier);
                    continue;
                }

                if (!courier.ServerTickArrival())
                    continue;

                var (product, amount) = _couriers[courier];
                _couriers.Remove(courier);
                DeliverNow(product, amount);
                _feed.Notify($"Delivery arrived: {product.DisplayName} x{amount}", NotificationKind.Good);

                courier.ServerSetState(CustomerState.Leaving);
                courier.ServerMoveTo(_street.Exit.position);
                _leaving.Add(courier);
            }
        }

        private static Vector3 NavigablePoint(Vector3 position) =>
            NavMesh.SamplePosition(position, out var hit, 3f, NavMesh.AllAreas) ? hit.position : position;

        private void SpawnBox(ProductConfig product, int units)
        {
            var point = _layout.DeliveryPoint;
            var row = _nextSpot % 4;
            var column = _nextSpot / 4 % 3;
            _nextSpot++;

            var position = point.position + point.right * (row * _config.BoxSpacing) + point.forward * (column * _config.BoxSpacing);
            var box = _resolver.Instantiate(_config.BoxPrefab, position, point.rotation);
            var halfHeight = box.transform.lossyScale.y * 0.5f;

            box.ServerInitialize(product, units, position + Vector3.up * halfHeight, point.rotation);
            NetworkServer.Spawn(box.gameObject);
        }
    }
}
