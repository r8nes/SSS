using System;
using Cysharp.Threading.Tasks;
using ShooterShop.Loading;
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
        private readonly ILoadingScreen _loadingScreen;

        public MainMenuPresenter(MainMenuView view, INetworkSession session, ILanDiscovery discovery, ILoadingScreen loadingScreen)
        {
            _view = view;
            _session = session;
            _discovery = discovery;
            _loadingScreen = loadingScreen;
        }

        public void Start()
        {
            _view.SoloButton.onClick.AddListener(OnSoloClicked);
            _view.HostButton.onClick.AddListener(OnHostClicked);
            _view.JoinButton.onClick.AddListener(OnJoinClicked);
            _discovery.ServersChanged += RefreshServers;
            _session.Stopped += OnSessionStopped;

            ResetMenu();
        }

        public void Dispose()
        {
            _view.SoloButton.onClick.RemoveListener(OnSoloClicked);
            _view.HostButton.onClick.RemoveListener(OnHostClicked);
            _view.JoinButton.onClick.RemoveListener(OnJoinClicked);
            _discovery.ServersChanged -= RefreshServers;
            _session.Stopped -= OnSessionStopped;
        }

        private void OnSoloClicked() => Launch(_session.StartSoloAsync);

        private void OnHostClicked() => Launch(_session.StartHostAsync);

        private void OnJoinClicked()
        {
            var address = string.IsNullOrEmpty(_view.Address) ? DefaultAddress : _view.Address;
            Launch(() => _session.JoinAsync(address));
        }

        private void OnServerClicked(DiscoveredServer server) => Launch(() => _session.JoinAsync(server.Uri));

        private void Launch(Func<UniTask> start)
        {
            _view.SetInteractable(false);
            start().Forget();
        }

        private void OnSessionStopped() => ResetMenu();

        private void ResetMenu()
        {
            _loadingScreen.Hide();
            _view.SetInteractable(true);
            _discovery.StartSearch();
            RefreshServers();
        }

        private void RefreshServers()
        {
            _view.ClearServers();

            foreach (var server in _discovery.Servers)
                _view.AddServer(server.Address).Button.onClick.AddListener(() => OnServerClicked(server));
        }
    }
}
