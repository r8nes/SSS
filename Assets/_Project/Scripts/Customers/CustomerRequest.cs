using ShooterShop.Catalog;

namespace ShooterShop.Customers
{
    public readonly struct CustomerRequest
    {
        public ProductConfig Product { get; }
        public int Amount { get; }
        public int LaneFee { get; }
        public bool IsRangeVisit => LaneFee > 0;
        public int Price => Product.SellPrice * Amount + LaneFee;

        public CustomerRequest(ProductConfig product, int amount, int laneFee = 0)
        {
            Product = product;
            Amount = amount;
            LaneFee = laneFee;
        }

        public string Describe() =>
            IsRangeVisit ? $"Lane + {Amount} x {Product.DisplayName}" : $"{Amount} x {Product.DisplayName}";
    }
}
