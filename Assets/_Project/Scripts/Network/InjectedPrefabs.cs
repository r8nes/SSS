using System.Collections.Generic;
using UnityEngine;

namespace ShooterShop.Network
{
    public sealed class InjectedPrefabs
    {
        public IReadOnlyList<GameObject> Prefabs { get; }

        public InjectedPrefabs(params GameObject[] prefabs)
        {
            Prefabs = prefabs;
        }
    }
}
