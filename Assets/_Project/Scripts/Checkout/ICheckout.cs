using Mirror;
using ShooterShop.Customers;

namespace ShooterShop.Checkout
{
    public interface ICheckout
    {
        void ServerOffer(Customer customer);
        void ServerTake(NetworkIdentity collector);
    }
}
