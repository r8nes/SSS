using System;

namespace ShooterShop.Raids
{
    public interface IAmbushState
    {
        event Action Changed;
        bool IsActive { get; }
        int Killed { get; }
        int Total { get; }
    }
}
