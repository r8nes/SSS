using ShooterShop.Catalog;
using ShooterShop.Customers;

namespace ShooterShop.Network
{
    public interface IShopCommands
    {
        void Purchase(ProductConfig product, int amount);
        void Sell(Customer customer);
        void Dismiss(Customer customer);
    }
}
