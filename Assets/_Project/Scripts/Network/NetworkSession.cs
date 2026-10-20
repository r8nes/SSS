using System;
using Cysharp.Threading.Tasks;
using Mirror;
using ShooterShop.Loading;

namespace ShooterShop.Network
{
    public sealed class NetworkSession : INetworkSession, IDisposable
    {
        private readonly ShopNetworkManager _manager;
        private readonly ILanDiscovery _discovery;
        private readonly ILoadingScreen _loadingScreen;

        public event Action Stopped;

        public SessionMode Mode { get; private set; }
        public bool IsActive => _manager.isNetworkActive;

        public NetworkSession(ShopNetworkManager manager, ILanDiscovery discovery, ILoadingScreen loadingScreen)
        {
            _manager = manager;
            _discovery = discovery;
            _loadingScreen = loadingScreen;
            _manager.ClientStopped += OnClientStopped;
        }

        public UniTask StartSoloAsync() => StartHostingAsync(SessionMode.Solo, false);

        public async UniTask StartHostAsync()
        {
            await StartHostingAsync(SessionMode.Host, true);
            _discovery.Advertise();
        }

        public async UniTask JoinAsync(Uri uri)
        {
            await PrepareStartAsync();
            Mode = SessionMode.Client;
            _manager.StartClient(uri);
        }

        public async UniTask JoinAsync(string address)
        {
            await PrepareStartAsync();
            Mode = SessionMode.Client;
            _manager.networkAddress = address;
            _manager.StartClient();
        }

        public async UniTask LeaveAsync()
        {
            if (!IsActive)
                return;

            await _loadingScreen.ShowAsync();

            if (NetworkServer.active)
                _manager.StopHost();
            else
                _manager.StopClient();
        }

        public void Dispose() => _manager.ClientStopped -= OnClientStopped;

        private async UniTask StartHostingAsync(SessionMode mode, bool acceptConnections)
        {
            await PrepareStartAsync();
            Mode = mode;
            NetworkServer.listen = acceptConnections;
            _manager.StartHost();
        }

        private async UniTask PrepareStartAsync()
        {
            if (IsActive)
                throw new InvalidOperationException($"Network session is already running in {Mode} mode");

            _discovery.StopSearch();
            await _loadingScreen.ShowAsync();
        }

        private void OnClientStopped()
        {
            Mode = SessionMode.None;
            Stopped?.Invoke();
        }
    }
}
