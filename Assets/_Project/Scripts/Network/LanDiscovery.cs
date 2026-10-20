using System;
using System.Collections.Generic;
using Mirror.Discovery;

namespace ShooterShop.Network
{
    public sealed class LanDiscovery : ILanDiscovery, IDisposable
    {
        private readonly NetworkDiscovery _discovery;
        private readonly Dictionary<long, DiscoveredServer> _servers = new();
        private bool _isSearching;

        public event Action ServersChanged;

        public IReadOnlyCollection<DiscoveredServer> Servers => _servers.Values;

        public LanDiscovery(NetworkDiscovery discovery)
        {
            _discovery = discovery;
            _discovery.OnServerFound.AddListener(OnServerFound);
        }

        public void StartSearch()
        {
            _servers.Clear();
            ServersChanged?.Invoke();
            _discovery.StartDiscovery();
            _isSearching = true;
        }

        public void StopSearch()
        {
            if (!_isSearching)
                return;

            _discovery.StopDiscovery();
            _isSearching = false;
        }

        public void Advertise() => _discovery.AdvertiseServer();

        public void Dispose()
        {
            _discovery.OnServerFound.RemoveListener(OnServerFound);
            StopSearch();
        }

        private void OnServerFound(ServerResponse response)
        {
            if (_servers.ContainsKey(response.serverId))
                return;

            var address = response.EndPoint?.Address.ToString() ?? response.uri.Host;
            _servers[response.serverId] = new DiscoveredServer(response.serverId, response.uri, address);
            ServersChanged?.Invoke();
        }
    }
}
