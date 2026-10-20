using Mirror;
using Mirror.Discovery;
using ShooterShop.Loading;
using ShooterShop.Network;
using ShooterShop.Saves;
using Sirenix.OdinInspector;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace ShooterShop.Bootstrap
{
    public sealed class ProjectLifetimeScope : LifetimeScope
    {
        [BoxGroup("Network"), Required, ChildGameObjectsOnly]
        [SerializeField] private ShopNetworkManager _networkManager;

        [BoxGroup("Network"), Required, ChildGameObjectsOnly]
        [SerializeField] private NetworkDiscovery _networkDiscovery;

        [BoxGroup("Loading"), Required]
        [SerializeField] private string _loadingSceneName = "Loading";

        [BoxGroup("Saves"), Required]
        [SerializeField] private string _saveFileName = "save.json";

        protected override void Configure(IContainerBuilder builder)
        {
            builder.RegisterComponent(_networkManager).As<NetworkManager>().AsSelf();
            builder.RegisterComponent(_networkDiscovery);

            builder.Register<SceneLoadingScreen>(Lifetime.Singleton)
                .AsImplementedInterfaces()
                .WithParameter("sceneName", _loadingSceneName);

            builder.Register<JsonSaveStorage>(Lifetime.Singleton)
                .As<ISaveStorage>()
                .WithParameter("fileName", _saveFileName);

            builder.Register<LanDiscovery>(Lifetime.Singleton).AsImplementedInterfaces();
            builder.Register<NetworkSession>(Lifetime.Singleton).AsImplementedInterfaces();
        }
    }
}
