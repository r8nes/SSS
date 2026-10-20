using Akila.FPSFramework;
using VContainer.Unity;

namespace ShooterShop.Loading
{
    public sealed class LoadingScreenRegistrar : IInitializable
    {
        private readonly ILoadingScreenHost _host;
        private readonly LoadingScreen _screen;

        public LoadingScreenRegistrar(ILoadingScreenHost host, LoadingScreen screen)
        {
            _host = host;
            _screen = screen;
        }

        public void Initialize() => _host.Attach(_screen);
    }
}
