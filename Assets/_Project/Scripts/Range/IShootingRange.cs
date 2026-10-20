using System;

namespace ShooterShop.Range
{
    public interface IShootingRange
    {
        event Action<ShootingLane> SessionFinished;
        bool TryReserveLane(out ShootingLane lane);
        void StartSession(ShootingLane lane, IRangeShooter shooter);
        void Release(ShootingLane lane);
    }
}
