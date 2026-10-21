using System;
using ShooterShop.Catalog;

namespace ShooterShop.Network
{
    public interface ISupplierPricing
    {
        event Action PricesChanged;
        int GetPriceChangePercent(ProductConfig product);
        void ServerRollPrices();
    }
}
