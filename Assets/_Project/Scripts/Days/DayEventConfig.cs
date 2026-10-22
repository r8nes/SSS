using ShooterShop.Catalog;
using Sirenix.OdinInspector;
using UnityEngine;

namespace ShooterShop.Days
{
    [CreateAssetMenu(menuName = "Shooter Shop/Days/Day Event", fileName = "DayEvent")]
    public sealed class DayEventConfig : ScriptableObject
    {
        [BoxGroup("Text"), Required]
        [SerializeField] private string _title;

        [BoxGroup("Text"), TextArea]
        [SerializeField] private string _description;

        [BoxGroup("Demand")]
        [SerializeField] private bool _hasDemand;

        [BoxGroup("Demand"), ShowIf(nameof(_hasDemand)), EnumToggleButtons]
        [SerializeField] private ProductCategory _demandCategory;

        [BoxGroup("Demand"), ShowIf(nameof(_hasDemand)), MinValue(1)]
        [InfoBox("How many times more likely customers ask for this category")]
        [SerializeField] private float _demandWeight = 3f;

        [BoxGroup("Economy"), PropertyRange(0, 0.9f)]
        [SerializeField] private float _purchaseDiscount;

        [BoxGroup("Range"), MinValue(0)]
        [SerializeField] private float _laneFeeMultiplier = 1f;

        [BoxGroup("Range"), MinValue(0)]
        [SerializeField] private float _visitorChanceMultiplier = 1f;

        [BoxGroup("Customers"), MinValue(0.1f)]
        [InfoBox("Above 1 means customers come more often")]
        [SerializeField] private float _spawnRateMultiplier = 1f;

        [BoxGroup("Raid")]
        [InfoBox("A gang bursts into the shop during this day, see RaidConfig")]
        [SerializeField] private bool _hasRaid;

        public bool HasRaid => _hasRaid;
        public string Title => _title;
        public string Description => _description;
        public float PurchaseDiscount => _purchaseDiscount;
        public float LaneFeeMultiplier => _laneFeeMultiplier;
        public float VisitorChanceMultiplier => _visitorChanceMultiplier;
        public float SpawnRateMultiplier => _spawnRateMultiplier;

        public float DemandWeightOf(ProductCategory category) =>
            _hasDemand && category == _demandCategory ? _demandWeight : 1f;
    }
}
