using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Mirror;
using ShooterShop.Catalog;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace ShooterShop.Store
{
    public sealed class DeliveryService : IDeliveryService, IDisposable
    {
        private readonly StoreConfig _config;
        private readonly StoreLayout _layout;
        private readonly IObjectResolver _resolver;
        private readonly CancellationTokenSource _lifetime = new();
        private int _nextSpot;

        public DeliveryService(StoreConfig config, StoreLayout layout, IObjectResolver resolver)
        {
            _config = config;
            _layout = layout;
            _resolver = resolver;
        }

        public void Deliver(ProductConfig product, int amount) => DeliverAsync(product, amount).Forget();

        public void Dispose()
        {
            _lifetime.Cancel();
            _lifetime.Dispose();
        }

        private async UniTaskVoid DeliverAsync(ProductConfig product, int amount)
        {
            var delay = TimeSpan.FromSeconds(_config.DeliveryDelay);

            if (await UniTask.Delay(delay, cancellationToken: _lifetime.Token).SuppressCancellationThrow() || !NetworkServer.active)
                return;

            DeliverNow(product, amount);
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
