using ShooterShop.Catalog;
using ShooterShop.Customers;
using ShooterShop.Days;
using ShooterShop.Economy;
using ShooterShop.Hud;
using ShooterShop.Input;
using ShooterShop.Loading;
using ShooterShop.Network;
using ShooterShop.Purchasing;
using ShooterShop.Range;
using ShooterShop.Sales;
using ShooterShop.Storage;
using ShooterShop.Upgrades;
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

        [BoxGroup("Configs"), Required, InlineEditor]
        [SerializeField] private CustomerConfig _customerConfig;

        [BoxGroup("Configs"), Required, InlineEditor]
        [SerializeField] private RangeConfig _rangeConfig;

        [BoxGroup("Configs"), Required, InlineEditor]
        [SerializeField] private DayConfig _dayConfig;

        [BoxGroup("Network"), Required, SceneObjectsOnly]
        [SerializeField] private ShopNetworkState _shopNetworkState;

        [BoxGroup("Network"), Required, SceneObjectsOnly]
        [SerializeField] private DayNetworkState _dayNetworkState;

        [BoxGroup("Network"), Required, SceneObjectsOnly]
        [SerializeField] private UpgradeNetworkState _upgradeNetworkState;

        [BoxGroup("Configs"), Required, InlineEditor]
        [SerializeField] private UpgradeCatalog _upgradeCatalog;

        [BoxGroup("Purchasing"), Required, SceneObjectsOnly]
        [SerializeField] private PurchaseTerminal _purchaseTerminal;

        [BoxGroup("Purchasing"), Required, SceneObjectsOnly]
        [SerializeField] private PurchaseTerminalView _purchaseTerminalView;

        [BoxGroup("Customers"), Required, SceneObjectsOnly]
        [SerializeField] private ShopLayout _shopLayout;

        [BoxGroup("Customers"), Required, SceneObjectsOnly]
        [SerializeField] private SaleDialogView _saleDialogView;

        [BoxGroup("Range"), Required, SceneObjectsOnly]
        [SerializeField] private RangeLayout _rangeLayout;

        [BoxGroup("Hud"), Required, SceneObjectsOnly]
        [SerializeField] private MoneyHudView _moneyHudView;

        [BoxGroup("Hud"), Required, SceneObjectsOnly]
        [SerializeField] private DayHudView _dayHudView;

        [BoxGroup("Hud"), Required, SceneObjectsOnly]
        [SerializeField] private DaySummaryView _daySummaryView;

        protected override void Configure(IContainerBuilder builder)
        {
            builder.RegisterInstance(_economyConfig);
            builder.RegisterInstance(_productCatalog);

            builder.Register<Wallet>(Lifetime.Singleton).As<IWallet>();
            builder.Register<Warehouse>(Lifetime.Singleton).As<IWarehouse>();
            builder.RegisterComponent(_shopNetworkState).As<IShopState, IShopCommands>();

            builder.RegisterInstance(_dayConfig);
            builder.RegisterComponent(_dayNetworkState).AsSelf().As<IDayState, IDayStatistics, IDayCommands>();
            builder.RegisterEntryPoint<DayCycle>();

            builder.RegisterInstance(_upgradeCatalog);
            builder.RegisterComponent(_upgradeNetworkState).As<IUnlocks, IUpgradeCommands>();

            builder.Register<AkilaPlayerInputLock>(Lifetime.Singleton).AsImplementedInterfaces();

            builder.RegisterComponent(_purchaseTerminal);
            builder.RegisterComponent(_purchaseTerminalView);
            builder.RegisterEntryPoint<PurchaseTerminalPresenter>();
            builder.RegisterEntryPoint<UpgradesPresenter>();

            builder.RegisterInstance(_rangeConfig);
            builder.RegisterComponent(_rangeLayout);
            builder.Register<ShootingRange>(Lifetime.Singleton).AsImplementedInterfaces();

            builder.RegisterInstance(_customerConfig);
            builder.RegisterComponent(_shopLayout);
            builder.Register<CustomerDirector>(Lifetime.Singleton).AsImplementedInterfaces();
            builder.RegisterEntryPoint<CustomerSpawnRegistrar>();
            builder.Register<CustomerRegistry>(Lifetime.Singleton).AsImplementedInterfaces().AsSelf();

            builder.RegisterComponent(_saleDialogView);
            builder.Register<SalePresenter>(Lifetime.Singleton).AsImplementedInterfaces();

            builder.RegisterComponent(_moneyHudView);
            builder.RegisterEntryPoint<MoneyHudPresenter>();

            builder.RegisterComponent(_dayHudView);
            builder.RegisterEntryPoint<DayHudPresenter>();
            builder.RegisterComponent(_daySummaryView);
            builder.RegisterEntryPoint<DaySummaryPresenter>();

            builder.RegisterEntryPoint<OfflineSessionStarter>();
            builder.RegisterEntryPoint<GameLoadingCloser>();
        }
    }
}
