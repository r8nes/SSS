using System;
using ShooterShop.Catalog;

namespace ShooterShop.Network
{
    public interface IShopState
    {
        event Action MoneyChanged;
        event Action StorageChanged;
        event Action<ProductConfig> StockChanged;
        int Money { get; }
        int UsedSpace { get; }
        int Capacity { get; }
        int FreeSpace { get; }
        int GetStock(ProductConfig product);
        int GetPurchaseCost(ProductConfig product, int amount);
    }
}
