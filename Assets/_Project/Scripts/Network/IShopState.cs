using System;
using ShooterShop.Catalog;

namespace ShooterShop.Network
{
    public interface IShopState
    {
        event Action MoneyChanged;
        event Action<ProductConfig> StockChanged;
        int Money { get; }
        int GetStock(ProductConfig product);
    }
}
