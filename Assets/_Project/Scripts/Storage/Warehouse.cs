using System;
using System.Collections.Generic;
using ShooterShop.Catalog;
using ShooterShop.Economy;

namespace ShooterShop.Storage
{
    public sealed class Warehouse : IWarehouse
    {
        private readonly Dictionary<ProductConfig, int> _stock = new();

        public event Action<ProductConfig, int> StockChanged;
        public event Action CapacityChanged;

        public int Capacity { get; private set; }
        public int UsedSpace { get; private set; }
        public int FreeSpace => Capacity - UsedSpace;
        public IReadOnlyDictionary<ProductConfig, int> Stock => _stock;

        public Warehouse(EconomyConfig config)
        {
            Capacity = config.WarehouseCapacity;

            foreach (var entry in config.StartingStock)
                TryAdd(entry.Product, entry.Amount);
        }

        public int GetAmount(ProductConfig product) =>
            _stock.TryGetValue(product, out var amount) ? amount : 0;

        public bool CanStore(ProductConfig product, int amount) =>
            product != null && amount > 0 && product.StorageSize * amount <= FreeSpace;

        public bool TryAdd(ProductConfig product, int amount)
        {
            if (!CanStore(product, amount))
                return false;

            SetAmount(product, GetAmount(product) + amount);
            return true;
        }

        public bool TryRemove(ProductConfig product, int amount)
        {
            if (product == null || amount <= 0 || GetAmount(product) < amount)
                return false;

            SetAmount(product, GetAmount(product) - amount);
            return true;
        }

        public void IncreaseCapacity(int amount)
        {
            if (amount <= 0)
                return;

            Capacity += amount;
            CapacityChanged?.Invoke();
        }

        private void SetAmount(ProductConfig product, int amount)
        {
            UsedSpace += (amount - GetAmount(product)) * product.StorageSize;

            if (amount == 0)
                _stock.Remove(product);
            else
                _stock[product] = amount;

            StockChanged?.Invoke(product, amount);
        }
    }
}
