using System;

namespace ShooterShop.Upgrades
{
    public interface IUnlocks
    {
        event Action Changed;
        bool IsOwned(UpgradeConfig upgrade);
        bool IsUnlocked(UpgradeConfig requirement);
    }
}
