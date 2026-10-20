using Mirror;
using Mirror.Discovery;
using ShooterShop.Network;
using Sirenix.OdinInspector;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace ShooterShop.Bootstrap
{
    public sealed class ProjectLifetimeScope : LifetimeScope
    {
        [BoxGroup("Network"), Required, ChildGameObjectsOnly]
        [SerializeField] private NetworkManager _networkManager;

        [BoxGroup("Network"), Required, ChildGameObjectsOnly]
        [SerializeField] private NetworkDiscovery _networkDiscovery;

        protected override void Configure(IContainerBuilder builder)
        {
            builder.RegisterComponent(_networkManager);
            builder.RegisterComponent(_networkDiscovery);

            builder.Register<LanDiscovery>(Lifetime.Singleton).AsImplementedInterfaces();
            builder.Register<NetworkSession>(Lifetime.Singleton).As<INetworkSession>();
        }
    }
}
