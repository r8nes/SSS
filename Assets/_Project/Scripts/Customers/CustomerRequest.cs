using ShooterShop.Catalog;

namespace ShooterShop.Customers
{
    public readonly struct CustomerRequest
    {
        public ProductConfig Product { get; }
        public int Amount { get; }
        public int Price => Product.SellPrice * Amount;

        public CustomerRequest(ProductConfig product, int amount)
        {
            Product = product;
            Amount = amount;
        }
    }
}
