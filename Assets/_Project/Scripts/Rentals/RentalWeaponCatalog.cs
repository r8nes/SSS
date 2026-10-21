using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

namespace ShooterShop.Rentals
{
    [CreateAssetMenu(menuName = "Shooter Shop/Rentals/Rental Weapon Catalog", fileName = "RentalWeaponCatalog")]
    public sealed class RentalWeaponCatalog : ScriptableObject
    {
        [Required, AssetsOnly, ListDrawerSettings(ShowFoldout = false)]
        [SerializeField] private List<RentalWeaponConfig> _weapons = new();

        public int IndexOf(RentalWeaponConfig weapon) => _weapons.IndexOf(weapon);

        public bool TryGet(int index, out RentalWeaponConfig weapon)
        {
            weapon = index >= 0 && index < _weapons.Count ? _weapons[index] : null;
            return weapon != null;
        }

        public RentalWeaponConfig FindByItemName(string itemName)
        {
            foreach (var weapon in _weapons)
                if (weapon != null && weapon.Item != null && weapon.Item.Name == itemName)
                    return weapon;

            return null;
        }
    }
}
