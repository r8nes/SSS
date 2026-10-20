using ShooterShop.Catalog;

namespace ShooterShop.Network
{
    public interface IShopCommands
    {
        void Purchase(ProductConfig product, int amount);
    }
}
