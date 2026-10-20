using System;
using ShooterShop.Network;
using VContainer.Unity;

namespace ShooterShop.Menu
{
    public sealed class MainMenuPresenter : IStartable, IDisposable
    {
        private const string DefaultAddress = "localhost";

        private readonly MainMenuView _view;
        private readonly INetworkSession _session;
        private readonly ILanDiscovery _discovery;

        public MainMenuPresenter(MainMenuView view, INetworkSession session, ILanDiscovery discovery)
        {
            _view = view;
            _session = session;
            _discovery = discovery;
        }

        public void Start()
        {
            _view.SoloButton.onClick.AddListener(OnSoloClicked);
            _view.HostButton.onClick.AddListener(OnHostClicked);
            _view.JoinButton.onClick.AddListener(OnJoinClicked);
            _discovery.ServersChanged += RefreshServers;

            _view.SetInteractable(true);
            _discovery.StartSearch();
            RefreshServers();
        }

        public void Dispose()
        {
            _view.SoloButton.onClick.RemoveListener(OnSoloClicked);
            _view.HostButton.onClick.RemoveListener(OnHostClicked);
            _view.JoinButton.onClick.RemoveListener(OnJoinClicked);
            _discovery.ServersChanged -= RefreshServers;
        }

        private void OnSoloClicked() => Launch(_session.StartSolo);

        private void OnHostClicked() => Launch(_session.StartHost);

        private void OnJoinClicked()
        {
            var address = string.IsNullOrEmpty(_view.Address) ? DefaultAddress : _view.Address;
            Launch(() => _session.Join(address));
        }

        private void OnServerClicked(DiscoveredServer server) => Launch(() => _session.Join(server.Uri));

        private void Launch(Action start)
        {
            _view.SetInteractable(false);
            start();
        }

        private void RefreshServers()
        {
            _view.ClearServers();

            foreach (var server in _discovery.Servers)
                _view.AddServer(server.Address).Button.onClick.AddListener(() => OnServerClicked(server));
        }
    }
}
