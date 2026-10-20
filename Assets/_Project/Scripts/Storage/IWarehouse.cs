using System;
using System.Collections.Generic;
using ShooterShop.Catalog;

namespace ShooterShop.Storage
{
    public interface IWarehouse
    {
        event Action<ProductConfig, int> StockChanged;
        event Action CapacityChanged;
        int Capacity { get; }
        int UsedSpace { get; }
        int FreeSpace { get; }
        IReadOnlyDictionary<ProductConfig, int> Stock { get; }
        int GetAmount(ProductConfig product);
        bool CanStore(ProductConfig product, int amount);
        bool TryAdd(ProductConfig product, int amount);
        bool TryRemove(ProductConfig product, int amount);
        void IncreaseCapacity(int amount);
    }
}
