using System;
using System.Collections.Generic;

namespace ShooterShop.Upgrades
{
    public interface IUnlocks
    {
        event Action Changed;
        IEnumerable<UpgradeConfig> Owned { get; }
        bool IsOwned(UpgradeConfig upgrade);
        bool IsUnlocked(UpgradeConfig requirement);
    }
}
