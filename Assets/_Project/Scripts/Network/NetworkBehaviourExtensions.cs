using Mirror;

namespace ShooterShop.Network
{
    public static class NetworkBehaviourExtensions
    {
        public static bool IsSpawnedOnServer(this NetworkBehaviour behaviour) =>
            NetworkServer.active && behaviour != null && behaviour.netIdentity != null && behaviour.isServer;
    }
}
