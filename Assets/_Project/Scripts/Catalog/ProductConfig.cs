using ShooterShop.Upgrades;
using Sirenix.OdinInspector;
using UnityEngine;

namespace ShooterShop.Catalog
{
    [CreateAssetMenu(menuName = "Shooter Shop/Catalog/Product", fileName = "Product")]
    public sealed class ProductConfig : ScriptableObject
    {
        [HorizontalGroup("Header", 80), PreviewField(80), HideLabel]
        [SerializeField] private Sprite _icon;

        [VerticalGroup("Header/Info"), Required]
        [SerializeField] private string _displayName;

        [VerticalGroup("Header/Info"), EnumToggleButtons]
        [SerializeField] private ProductCategory _category;

        [VerticalGroup("Header/Info"), ShowIf(nameof(IsAmmo)), MinValue(1), SuffixLabel("rounds")]
        [SerializeField] private int _packSize = 30;

        [BoxGroup("Price"), MinValue(0), SuffixLabel("$")]
        [SerializeField] private int _buyPrice;

        [BoxGroup("Price"), MinValue(0), SuffixLabel("$")]
        [InfoBox("Sell price is not higher than buy price", InfoMessageType.Warning, nameof(IsUnprofitable))]
        [SerializeField] private int _sellPrice;

        [BoxGroup("Storage"), MinValue(1), SuffixLabel("slots")]
        [SerializeField] private int _storageSize = 1;

        [BoxGroup("Unlock"), AssetsOnly]
        [InfoBox("Leave empty if the product is available from the start")]
        [SerializeField] private UpgradeConfig _requiredUpgrade;

        [BoxGroup("Visual"), AssetsOnly]
        [SerializeField] private GameObject _displayPrefab;

        public string DisplayName => _displayName;
        public ProductCategory Category => _category;
        public Sprite Icon => _icon;
        public int PackSize => _packSize;
        public int BuyPrice => _buyPrice;
        public int SellPrice => _sellPrice;
        public int StorageSize => _storageSize;
        public GameObject DisplayPrefab => _displayPrefab;
        public UpgradeConfig RequiredUpgrade => _requiredUpgrade;
        public bool IsAmmo => _category == ProductCategory.Ammo;

        private bool IsUnprofitable => _sellPrice <= _buyPrice;
    }
}
