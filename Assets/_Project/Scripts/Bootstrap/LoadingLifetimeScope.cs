using Akila.FPSFramework;
using ShooterShop.Loading;
using Sirenix.OdinInspector;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace ShooterShop.Bootstrap
{
    public sealed class LoadingLifetimeScope : LifetimeScope
    {
        [Required, SceneObjectsOnly]
        [SerializeField] private LoadingScreen _loadingScreen;

        protected override void Configure(IContainerBuilder builder)
        {
            builder.RegisterComponent(_loadingScreen);
            builder.RegisterEntryPoint<LoadingScreenRegistrar>();
        }
    }
}
