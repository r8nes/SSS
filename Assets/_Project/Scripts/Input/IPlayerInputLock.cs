namespace ShooterShop.Input
{
    public interface IPlayerInputLock
    {
        bool IsLocked { get; }
        void Acquire(object owner);
        void Release(object owner);
    }
}
