using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

namespace ShooterShop.Upgrades
{
    [CreateAssetMenu(menuName = "Shooter Shop/Upgrades/Upgrade Catalog", fileName = "UpgradeCatalog")]
    public sealed class UpgradeCatalog : ScriptableObject
    {
        [ListDrawerSettings(ShowFoldout = false), Required]
        [SerializeField] private List<UpgradeConfig> _upgrades = new();

        public IReadOnlyList<UpgradeConfig> Upgrades => _upgrades;

        public int IndexOf(UpgradeConfig upgrade) => _upgrades.IndexOf(upgrade);

        public bool TryGet(int index, out UpgradeConfig upgrade)
        {
            var isValid = index >= 0 && index < _upgrades.Count;
            upgrade = isValid ? _upgrades[index] : null;
            return isValid;
        }
    }
}
