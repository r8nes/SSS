using System;
using Mirror;

namespace ShooterShop.Network
{
    public sealed class NetworkSession : INetworkSession
    {
        private readonly NetworkManager _manager;
        private readonly ILanDiscovery _discovery;

        public SessionMode Mode { get; private set; }
        public bool IsActive => _manager.isNetworkActive;

        public NetworkSession(NetworkManager manager, ILanDiscovery discovery)
        {
            _manager = manager;
            _discovery = discovery;
        }

        public void StartSolo() => StartHosting(SessionMode.Solo, false);

        public void StartHost()
        {
            StartHosting(SessionMode.Host, true);
            _discovery.Advertise();
        }

        public void Join(Uri uri)
        {
            PrepareStart();
            _manager.StartClient(uri);
            Mode = SessionMode.Client;
        }

        public void Join(string address)
        {
            PrepareStart();
            _manager.networkAddress = address;
            _manager.StartClient();
            Mode = SessionMode.Client;
        }

        public void Leave()
        {
            if (NetworkServer.active)
                _manager.StopHost();
            else if (NetworkClient.active)
                _manager.StopClient();

            Mode = SessionMode.None;
        }

        private void StartHosting(SessionMode mode, bool acceptConnections)
        {
            PrepareStart();
            NetworkServer.listen = acceptConnections;
            _manager.StartHost();
            Mode = mode;
        }

        private void PrepareStart()
        {
            if (IsActive)
                throw new InvalidOperationException($"Network session is already running in {Mode} mode");

            _discovery.StopSearch();
        }
    }
}
