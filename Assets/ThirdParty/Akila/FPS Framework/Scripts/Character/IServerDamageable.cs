namespace Akila.FPSFramework
{
    public interface IServerDamageable
    {
        bool IsAlive { get; }
        void ServerDamage(float amount);
    }
}
