using Akila.FPSFramework;
using Sirenix.OdinInspector;
using UnityEngine;

namespace ShooterShop.Rentals
{
    [CreateAssetMenu(menuName = "Shooter Shop/Rentals/Rental Weapon", fileName = "RentalWeapon")]
    public sealed class RentalWeaponConfig : ScriptableObject
    {
        [Required]
        [SerializeField] private string _displayName;

        [Required, AssetsOnly]
        [InfoBox("Networked Akila inventory item the player gets in hands")]
        [SerializeField] private InventoryItem _item;

        [Required, AssetsOnly]
        [InfoBox("Visual-only model shown on the rack and the counter")]
        [SerializeField] private GameObject _displayModel;

        public string DisplayName => _displayName;
        public InventoryItem Item => _item;
        public GameObject DisplayModel => _displayModel;
    }
}
