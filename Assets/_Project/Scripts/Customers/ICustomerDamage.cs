namespace ShooterShop.Customers
{
    public interface ICustomerDamage
    {
        void ServerHit(Customer customer, UnityEngine.Vector3 direction, UnityEngine.Vector3 point);
    }
}
