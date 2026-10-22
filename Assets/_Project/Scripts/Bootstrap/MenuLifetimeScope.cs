using ShooterShop.Audio;
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

        [Required, SceneObjectsOnly]
        [SerializeField] private MenuCameraTour _cameraTour;

        [Required, SceneObjectsOnly]
        [SerializeField] private ScreenFadeView _screenFade;

        [Required, InlineEditor]
        [SerializeField] private MenuCameraTourConfig _cameraTourConfig;

        protected override void Configure(IContainerBuilder builder)
        {
            builder.RegisterComponent(_mainMenuView);
            builder.RegisterEntryPoint<MainMenuPresenter>();

            builder.RegisterComponent(_cameraTour);
            builder.RegisterComponent(_screenFade);
            builder.RegisterInstance(_cameraTourConfig);
            builder.RegisterEntryPoint<MenuCameraTourPlayer>();
            builder.RegisterEntryPoint<MusicCue>().WithParameter(MusicPlaylist.Menu);
        }
    }
}
