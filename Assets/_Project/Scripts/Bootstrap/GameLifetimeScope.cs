using ShooterShop.Catalog;
using ShooterShop.Economy;
using ShooterShop.Loading;
using ShooterShop.Network;
using ShooterShop.Storage;
using Sirenix.OdinInspector;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace ShooterShop.Bootstrap
{
    public sealed class GameLifetimeScope : LifetimeScope
    {
        [BoxGroup("Configs"), Required, InlineEditor]
        [SerializeField] private EconomyConfig _economyConfig;

        [BoxGroup("Configs"), Required, InlineEditor]
        [SerializeField] private ProductCatalog _productCatalog;

        [BoxGroup("Network"), Required, SceneObjectsOnly]
        [SerializeField] private ShopNetworkState _shopNetworkState;

        protected override void Configure(IContainerBuilder builder)
        {
            builder.RegisterInstance(_economyConfig);
            builder.RegisterInstance(_productCatalog);

            builder.Register<Wallet>(Lifetime.Singleton).As<IWallet>();
            builder.Register<Warehouse>(Lifetime.Singleton).As<IWarehouse>();
            builder.RegisterComponent(_shopNetworkState).As<IShopState, IShopCommands>();

            builder.RegisterEntryPoint<OfflineSessionStarter>();
            builder.RegisterEntryPoint<GameLoadingCloser>();
        }
    }
}
