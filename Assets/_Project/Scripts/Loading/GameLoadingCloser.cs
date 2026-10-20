using System.Threading;
using Cysharp.Threading.Tasks;
using Mirror;
using VContainer.Unity;

namespace ShooterShop.Loading
{
    public sealed class GameLoadingCloser : IAsyncStartable
    {
        private readonly ILoadingScreen _loadingScreen;

        public GameLoadingCloser(ILoadingScreen loadingScreen)
        {
            _loadingScreen = loadingScreen;
        }

        public async UniTask StartAsync(CancellationToken cancellation)
        {
            await UniTask.WaitUntil(() => NetworkClient.localPlayer != null, cancellationToken: cancellation);
            _loadingScreen.Hide();
        }
    }
}
