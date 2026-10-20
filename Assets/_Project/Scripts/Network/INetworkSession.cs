using System;

namespace ShooterShop.Network
{
    public interface INetworkSession
    {
        SessionMode Mode { get; }
        bool IsActive { get; }
        void StartSolo();
        void StartHost();
        void Join(Uri uri);
        void Join(string address);
        void Leave();
    }
}
