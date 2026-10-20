namespace ShooterShop.Customers
{
    public interface ICustomerService
    {
        int ActiveCount { get; }
        bool TrySell(Customer customer);
        void Dismiss(Customer customer);
        void CloseShop();
    }
}
