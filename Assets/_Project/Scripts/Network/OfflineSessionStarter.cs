using System.Threading;
using Cysharp.Threading.Tasks;
using VContainer.Unity;

namespace ShooterShop.Network
{
    public sealed class OfflineSessionStarter : IAsyncStartable
    {
        private readonly INetworkSession _session;

        public OfflineSessionStarter(INetworkSession session)
        {
            _session = session;
        }

        public async UniTask StartAsync(CancellationToken cancellation)
        {
            if (!_session.IsActive)
                await _session.StartSoloAsync();
        }
    }
}
