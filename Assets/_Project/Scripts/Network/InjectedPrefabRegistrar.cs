using System;
using Mirror;
using UnityEngine;
using VContainer;
using VContainer.Unity;
using Object = UnityEngine.Object;

namespace ShooterShop.Network
{
    public sealed class InjectedPrefabRegistrar : IInitializable, IDisposable
    {
        private readonly InjectedPrefabs _prefabs;
        private readonly IObjectResolver _resolver;

        public InjectedPrefabRegistrar(InjectedPrefabs prefabs, IObjectResolver resolver)
        {
            _prefabs = prefabs;
            _resolver = resolver;
        }

        public void Initialize()
        {
            foreach (var prefab in _prefabs.Prefabs)
                NetworkClient.RegisterPrefab(prefab, message => Spawn(prefab, message), Despawn);
        }

        public void Dispose()
        {
            foreach (var prefab in _prefabs.Prefabs)
                NetworkClient.UnregisterPrefab(prefab);
        }

        private GameObject Spawn(GameObject prefab, SpawnMessage message) =>
            _resolver.Instantiate(prefab, message.position, message.rotation);

        private static void Despawn(GameObject spawned) => Object.Destroy(spawned);
    }
}
