using ShooterShop.Catalog;

namespace ShooterShop.Store
{
    public interface IDeliveryService
    {
        void Deliver(ProductConfig product, int amount);
        void DeliverNow(ProductConfig product, int amount);
    }
}
