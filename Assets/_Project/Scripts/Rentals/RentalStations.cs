using System;
using Sirenix.OdinInspector;
using UnityEngine;

namespace ShooterShop.Rentals
{
    [Serializable]
    public sealed class RentalStations
    {
        [Required, SceneObjectsOnly]
        [InfoBox("Back rack where rental weapons are stored")]
        [SerializeField] private RentalWeaponHolder _rack;

        [Required, SceneObjectsOnly]
        [InfoBox("Counter spot where visitors pick up and return rental weapons")]
        [SerializeField] private RentalWeaponHolder _handout;

        public RentalWeaponHolder Rack => _rack;
        public RentalWeaponHolder Handout => _handout;
    }
}
