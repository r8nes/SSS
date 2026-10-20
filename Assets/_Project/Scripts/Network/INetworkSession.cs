using System;
using Cysharp.Threading.Tasks;

namespace ShooterShop.Network
{
    public interface INetworkSession
    {
        event Action Stopped;
        SessionMode Mode { get; }
        bool IsActive { get; }
        UniTask StartSoloAsync();
        UniTask StartHostAsync();
        UniTask JoinAsync(Uri uri);
        UniTask JoinAsync(string address);
        UniTask LeaveAsync();
    }
}
