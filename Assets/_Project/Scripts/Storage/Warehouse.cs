using System;
using System.Collections.Generic;
using ShooterShop.Catalog;
using ShooterShop.Saves;

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

        public Warehouse(StartingState state)
        {
            Capacity = state.Capacity;

            foreach (var (product, amount) in state.Stock)
                TryAdd(product, amount);
        }

        public int GetAmount(ProductConfig product) =>
            _stock.TryGetValue(product, out var amount) ? amount : 0;

        public bool CanStore(ProductConfig product, int amount) =>
            product != null && amount > 0 && ExtraBoxes(product, amount) <= FreeSpace;

        public int ExtraBoxes(ProductConfig product, int amount) =>
            product.BoxesFor(GetAmount(product) + amount) - product.BoxesFor(GetAmount(product));

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
            UsedSpace += product.BoxesFor(amount) - product.BoxesFor(GetAmount(product));

            if (amount == 0)
                _stock.Remove(product);
            else
                _stock[product] = amount;

            StockChanged?.Invoke(product, amount);
        }
    }
}
