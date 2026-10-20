using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

namespace ShooterShop.Customers
{
    public sealed class ShopLayout : MonoBehaviour
    {
        [BoxGroup("Path"), Required, ChildGameObjectsOnly]
        [SerializeField] private Transform _entrance;

        [BoxGroup("Path"), Required, ChildGameObjectsOnly]
        [SerializeField] private Transform _exit;

        [BoxGroup("Counter"), Required, ChildGameObjectsOnly, ListDrawerSettings(ShowFoldout = false)]
        [SerializeField] private List<Transform> _counterSlots = new();

        public Transform Entrance => _entrance;
        public Transform Exit => _exit;
        public IReadOnlyList<Transform> CounterSlots => _counterSlots;
    }
}
