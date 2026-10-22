using System;
using Sirenix.OdinInspector;
using UnityEngine;

namespace ShooterShop.Feedback
{
    [Serializable]
    public sealed class WorldMarkerSet
    {
        [Required, SceneObjectsOnly]
        [SerializeField] private WorldMarker _terminal;

        [Required, SceneObjectsOnly]
        [SerializeField] private WorldMarker _openSign;

        [Required, SceneObjectsOnly]
        [SerializeField] private WorldMarker _delivery;

        [Required, SceneObjectsOnly]
        [SerializeField] private WorldMarker _storage;

        public WorldMarker Storage => _storage;
        public WorldMarker Terminal => _terminal;
        public WorldMarker OpenSign => _openSign;
        public WorldMarker Delivery => _delivery;
    }
}
