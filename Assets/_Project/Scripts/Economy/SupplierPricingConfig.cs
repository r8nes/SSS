using Sirenix.OdinInspector;
using UnityEngine;

namespace ShooterShop.Economy
{
    [CreateAssetMenu(menuName = "Shooter Shop/Economy/Supplier Pricing", fileName = "SupplierPricingConfig")]
    public sealed class SupplierPricingConfig : ScriptableObject
    {
        [PropertyRange(0, 1)]
        [InfoBox("Chance that a product gets a sale or a price rise for the day")]
        [SerializeField] private float _changeChance = 0.45f;

        [PropertyRange(0, 1)]
        [InfoBox("Share of changed products that get cheaper, the rest get more expensive")]
        [SerializeField] private float _saleShare = 0.55f;

        [MinMaxSlider(5, 60, true), SuffixLabel("%")]
        [SerializeField] private Vector2Int _saleRange = new(10, 35);

        [MinMaxSlider(5, 60, true), SuffixLabel("%")]
        [SerializeField] private Vector2Int _riseRange = new(10, 30);

        [MinValue(1), SuffixLabel("%")]
        [SerializeField] private int _step = 5;

        public int RollChangePercent()
        {
            if (Random.value >= _changeChance)
                return 0;

            var isSale = Random.value < _saleShare;
            var range = isSale ? _saleRange : _riseRange;
            var percent = Mathf.RoundToInt(Random.Range(range.x, range.y + 1) / (float)_step) * _step;
            return isSale ? -percent : percent;
        }
    }
}
