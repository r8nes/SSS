using System;
using Mirror;

namespace ShooterShop.Network
{
    public sealed class ShopNetworkManager : NetworkManager
    {
        public event Action ClientStopped;

        public override void OnStopClient()
        {
            base.OnStopClient();
            ClientStopped?.Invoke();
        }
    }
}
