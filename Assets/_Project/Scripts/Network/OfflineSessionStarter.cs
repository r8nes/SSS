using VContainer.Unity;

namespace ShooterShop.Network
{
    public sealed class OfflineSessionStarter : IStartable
    {
        private readonly INetworkSession _session;

        public OfflineSessionStarter(INetworkSession session)
        {
            _session = session;
        }

        public void Start()
        {
            if (!_session.IsActive)
                _session.StartSolo();
        }
    }
}
