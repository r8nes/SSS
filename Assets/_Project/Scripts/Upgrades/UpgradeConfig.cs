using Sirenix.OdinInspector;
using UnityEngine;

namespace ShooterShop.Upgrades
{
    [CreateAssetMenu(menuName = "Shooter Shop/Upgrades/Upgrade", fileName = "Upgrade")]
    public sealed class UpgradeConfig : ScriptableObject
    {
        [Required]
        [SerializeField] private string _displayName;

        [TextArea(2, 4)]
        [SerializeField] private string _description;

        [MinValue(0), SuffixLabel("$")]
        [SerializeField] private int _price;

        [InfoBox("Must be owned before this upgrade can be bought")]
        [SerializeField] private UpgradeConfig _requires;

        [BoxGroup("Effect"), MinValue(0), SuffixLabel("slots")]
        [InfoBox("Products and lanes that need this upgrade reference it themselves")]
        [SerializeField] private int _storageBonus;

        public string DisplayName => _displayName;
        public string Description => _description;
        public int Price => _price;
        public UpgradeConfig Requires => _requires;
        public int StorageBonus => _storageBonus;
    }
}
