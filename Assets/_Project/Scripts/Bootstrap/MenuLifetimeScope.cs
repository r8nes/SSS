using ShooterShop.Menu;
using Sirenix.OdinInspector;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace ShooterShop.Bootstrap
{
    public sealed class MenuLifetimeScope : LifetimeScope
    {
        [Required, SceneObjectsOnly]
        [SerializeField] private MainMenuView _mainMenuView;

        protected override void Configure(IContainerBuilder builder)
        {
            builder.RegisterComponent(_mainMenuView);
            builder.RegisterEntryPoint<MainMenuPresenter>();
        }
    }
}
