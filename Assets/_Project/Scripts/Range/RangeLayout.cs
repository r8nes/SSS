using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

namespace ShooterShop.Range
{
    public sealed class RangeLayout : MonoBehaviour
    {
        [Required, ChildGameObjectsOnly, ListDrawerSettings(ShowFoldout = false)]
        [SerializeField] private List<ShootingLane> _lanes = new();

        public IReadOnlyList<ShootingLane> Lanes => _lanes;
    }
}
