using ShooterShop.Catalog;
using ShooterShop.Customers;
using ShooterShop.Days;
using ShooterShop.Economy;
using ShooterShop.Feedback;
using ShooterShop.Hud;
using ShooterShop.Input;
using ShooterShop.Loading;
using ShooterShop.Network;
using ShooterShop.Purchasing;
using ShooterShop.Range;
using ShooterShop.Rentals;
using ShooterShop.Sales;
using ShooterShop.Saves;
using ShooterShop.Storage;
using ShooterShop.Street;
using ShooterShop.Store;
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
        [SerializeField] private SupplierPricingConfig _supplierPricing;

        [BoxGroup("Configs"), Required, InlineEditor]
        [SerializeField] private ProductCatalog _productCatalog;

        [BoxGroup("Configs"), Required, InlineEditor]
        [SerializeField] private CustomerConfig _customerConfig;

        [BoxGroup("Configs"), Required]
        [SerializeField] private CustomerAppearanceCatalog _customerAppearances;

        [BoxGroup("Configs"), Required, InlineEditor]
        [SerializeField] private PedestrianConfig _pedestrianConfig;

        [BoxGroup("Street"), Required, SceneObjectsOnly]
        [SerializeField] private StreetLayout _streetLayout;

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

        [BoxGroup("Configs"), Required, InlineEditor]
        [SerializeField] private StoreConfig _storeConfig;

        [BoxGroup("Configs"), Required, InlineEditor]
        [SerializeField] private FeedbackConfig _feedbackConfig;

        [BoxGroup("Store"), Required, SceneObjectsOnly]
        [SerializeField] private StoreLayout _storeLayout;

        [BoxGroup("Days"), Required, SceneObjectsOnly]
        [SerializeField] private ShopOpenSign _shopOpenSign;

        [BoxGroup("Configs"), Required, InlineEditor]
        [SerializeField] private DayEventCatalog _dayEventCatalog;

        [BoxGroup("Network"), Required, SceneObjectsOnly]
        [SerializeField] private ShopFeed _shopFeed;

        [BoxGroup("Hud"), Required, SceneObjectsOnly]
        [SerializeField] private NotificationView _notificationView;

        [BoxGroup("Hud")]
        [SerializeField] private WorldMarkerSet _worldMarkers = new();

        [BoxGroup("Configs"), Required, InlineEditor]
        [SerializeField] private RentalWeaponCatalog _rentalWeaponCatalog;

        [BoxGroup("Range")]
        [SerializeField] private RentalStations _rentalStations = new();

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

            builder.Register<StartingState>(Lifetime.Singleton);
            builder.RegisterEntryPoint<ProgressSaver>();

            builder.Register<Wallet>(Lifetime.Singleton).As<IWallet>();
            builder.Register<Warehouse>(Lifetime.Singleton).As<IWarehouse>();
            builder.RegisterComponent(_shopNetworkState).As<IShopState, IShopCommands, ISupplierPricing>();
            builder.RegisterInstance(_supplierPricing);

            builder.RegisterComponent(_shopFeed).As<IShopFeed, IShopFeedEvents>();
            builder.RegisterComponent(_notificationView);
            builder.RegisterEntryPoint<NotificationPresenter>();
            builder.RegisterEntryPoint<WorldPopupSpawner>();
            builder.Register<ViewCamera>(Lifetime.Singleton).As<IViewCamera>();
            builder.Register<ScreenLabelLayer>(Lifetime.Singleton).AsImplementedInterfaces();
            builder.RegisterInstance(_worldMarkers);
            builder.RegisterEntryPoint<WorldMarkerPresenter>();

            builder.RegisterInstance(_dayEventCatalog);
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

            builder.RegisterInstance(_rentalWeaponCatalog);
            builder.RegisterInstance(_rentalStations);
            builder.Register<AkilaPlayerHands>(Lifetime.Singleton).As<IPlayerHands>();

            builder.RegisterInstance(_rangeConfig);
            builder.RegisterComponent(_rangeLayout);
            builder.Register<ShootingRange>(Lifetime.Singleton).AsImplementedInterfaces();

            builder.RegisterInstance(_customerConfig);
            builder.RegisterInstance(_customerAppearances);
            builder.RegisterComponent(_shopLayout);
            builder.Register<CustomerDirector>(Lifetime.Singleton).AsImplementedInterfaces();
            builder.RegisterInstance(new InjectedPrefabs(_customerConfig.Prefab.gameObject, _storeConfig.BoxPrefab.gameObject, _pedestrianConfig.Prefab.gameObject));

            builder.RegisterInstance(_pedestrianConfig);
            builder.RegisterComponent(_streetLayout);
            builder.Register<PedestrianRegistry>(Lifetime.Singleton).AsImplementedInterfaces().AsSelf();
            builder.Register<PedestrianDirector>(Lifetime.Singleton).AsImplementedInterfaces();
            builder.RegisterEntryPoint<InjectedPrefabRegistrar>();

            builder.RegisterInstance(_feedbackConfig);
            builder.Register<ItemAnimator>(Lifetime.Singleton);

            builder.RegisterInstance(_storeConfig);
            builder.RegisterComponent(_storeLayout);
            builder.Register<CarryService>(Lifetime.Singleton).AsImplementedInterfaces().AsSelf();
            builder.Register<DeliveryService>(Lifetime.Singleton).AsImplementedInterfaces();
            builder.RegisterEntryPoint<StartingStockDelivery>();
            builder.RegisterEntryPoint<StandHoldPlacer>();
            builder.RegisterBuildCallback(container =>
            {
                foreach (var stand in _storeLayout.Stands)
                    container.Inject(stand);

                container.Inject(_shopOpenSign);
                container.Inject(_rentalStations.Rack);
                container.Inject(_rentalStations.Handout);
            });
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
