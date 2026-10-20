using System;
using Mirror;
using UnityEngine;
using VContainer;
using VContainer.Unity;
using Object = UnityEngine.Object;

namespace ShooterShop.Customers
{
    public sealed class CustomerSpawnRegistrar : IInitializable, IDisposable
    {
        private readonly CustomerConfig _config;
        private readonly IObjectResolver _resolver;

        public CustomerSpawnRegistrar(CustomerConfig config, IObjectResolver resolver)
        {
            _config = config;
            _resolver = resolver;
        }

        public void Initialize() =>
            NetworkClient.RegisterPrefab(_config.Prefab.gameObject, Spawn, Despawn);

        public void Dispose() => NetworkClient.UnregisterPrefab(_config.Prefab.gameObject);

        private GameObject Spawn(SpawnMessage message) =>
            _resolver.Instantiate(_config.Prefab, message.position, message.rotation).gameObject;

        private static void Despawn(GameObject spawned) => Object.Destroy(spawned);
    }
}
