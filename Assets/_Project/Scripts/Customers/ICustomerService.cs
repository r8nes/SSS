namespace ShooterShop.Customers
{
    public interface ICustomerService
    {
        bool TrySell(Customer customer);
        void Dismiss(Customer customer);
    }
}
