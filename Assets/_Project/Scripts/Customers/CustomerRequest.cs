using ShooterShop.Catalog;

namespace ShooterShop.Customers
{
    public readonly struct CustomerRequest
    {
        public ProductConfig Product { get; }
        public int Amount { get; }
        public int LaneFee { get; }
        public int MarkupPercent { get; }
        public bool IsRangeVisit => LaneFee > 0;
        public int GoodsPrice => UnityEngine.Mathf.RoundToInt(Product.SellPrice * Amount * (1f + MarkupPercent / 100f));
        public int Price => GoodsPrice + LaneFee;

        public CustomerRequest(ProductConfig product, int amount, int laneFee = 0, int markupPercent = 0)
        {
            Product = product;
            Amount = amount;
            LaneFee = laneFee;
            MarkupPercent = markupPercent;
        }

        public string Describe() =>
            IsRangeVisit ? $"Lane + {Amount} x {Product.DisplayName}" : $"{Amount} x {Product.DisplayName}";
    }
}
