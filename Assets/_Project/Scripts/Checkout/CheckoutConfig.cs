using System;
using System.Collections.Generic;
using System.Linq;
using Sirenix.OdinInspector;
using UnityEngine;

namespace ShooterShop.Checkout
{
    [Serializable]
    public sealed class MoneyDenomination
    {
        [MinValue(1), SuffixLabel("$")]
        [SerializeField] private int _value = 100;

        [Required, AssetsOnly]
        [SerializeField] private GameObject _prefab;

        [MinValue(0.0001f), SuffixLabel("m")]
        [InfoBox("Height one bill adds to its stack")]
        [SerializeField] private float _stackStep = 0.004f;

        public int Value => _value;
        public GameObject Prefab => _prefab;
        public float StackStep => _stackStep;
    }

    [CreateAssetMenu(menuName = "Shooter Shop/Checkout Config", fileName = "CheckoutConfig")]
    public sealed class CheckoutConfig : ScriptableObject
    {
        [BoxGroup("Money"), ListDrawerSettings(ShowFoldout = false)]
        [InfoBox("Any order, the price is paid with the largest bills first")]
        [SerializeField] private List<MoneyDenomination> _denominations = new();

        [BoxGroup("Money"), MinValue(1)]
        [InfoBox("Bills drawn per stack, larger payments only make it look full")]
        [SerializeField] private int _maxBillsPerStack = 12;

        [BoxGroup("Money"), SuffixLabel("m")]
        [InfoBox("Offset between the stacks of different bills, in the money anchor space")]
        [SerializeField] private Vector3 _stackSpacing = new(0.18f, 0f, 0f);

        [BoxGroup("Money"), Range(0f, 45f), SuffixLabel("°")]
        [SerializeField] private float _billYawJitter = 8f;

        [BoxGroup("Goods"), MinValue(1)]
        [SerializeField] private int _maxGoodsShown = 6;

        [BoxGroup("Goods"), SuffixLabel("m")]
        [SerializeField] private Vector3 _goodsSpacing = new(0f, 0f, 0.25f);

        [BoxGroup("Goods"), SuffixLabel("°")]
        [InfoBox("Ammo boxes are turned so they do not overlap on the counter")]
        [SerializeField] private float _ammoYaw = 90f;

        [BoxGroup("Animation"), MinValue(0f), SuffixLabel("sec")]
        [SerializeField] private float _goodsStagger = 0.12f;

        [BoxGroup("Animation"), MinValue(0f), SuffixLabel("sec")]
        [InfoBox("Pause between the goods and the first bill")]
        [SerializeField] private float _moneyDelay = 0.35f;

        [BoxGroup("Animation"), MinValue(0f), SuffixLabel("sec")]
        [SerializeField] private float _billStagger = 0.05f;

        [BoxGroup("Animation"), MinValue(0.05f), SuffixLabel("sec")]
        [SerializeField] private float _collectDuration = 0.35f;

        [BoxGroup("Animation"), MinValue(0f), SuffixLabel("sec")]
        [SerializeField] private float _collectStagger = 0.02f;

        [BoxGroup("Animation"), SuffixLabel("m")]
        [InfoBox("Where the money flies to, in front of the collecting player's camera")]
        [SerializeField] private Vector3 _collectOffset = new(0f, -0.25f, 0.35f);

        [BoxGroup("Animation"), MinValue(0.05f), SuffixLabel("sec")]
        [SerializeField] private float _goodsTakeDuration = 0.4f;

        [BoxGroup("Text"), Required]
        [SerializeField] private string _takePrompt = "Take ${0:N0}";

        [BoxGroup("Text"), Required]
        [SerializeField] private string _outOfStockMessage = "Not enough {0} in stock";

        public IReadOnlyList<MoneyDenomination> Denominations => _denominations;
        public int MaxBillsPerStack => _maxBillsPerStack;
        public Vector3 StackSpacing => _stackSpacing;
        public float BillYawJitter => _billYawJitter;
        public int MaxGoodsShown => _maxGoodsShown;
        public Vector3 GoodsSpacing => _goodsSpacing;
        public float AmmoYaw => _ammoYaw;
        public float GoodsStagger => _goodsStagger;
        public float MoneyDelay => _moneyDelay;
        public float BillStagger => _billStagger;
        public float CollectDuration => _collectDuration;
        public float CollectStagger => _collectStagger;
        public Vector3 CollectOffset => _collectOffset;
        public float GoodsTakeDuration => _goodsTakeDuration;
        public string TakePrompt => _takePrompt;
        public string OutOfStockMessage => _outOfStockMessage;

        public IEnumerable<(MoneyDenomination Denomination, int Count)> Split(int amount)
        {
            foreach (var denomination in _denominations.Where(d => d.Prefab != null).OrderByDescending(d => d.Value))
            {
                var count = amount / denomination.Value;

                if (count <= 0)
                    continue;

                amount -= count * denomination.Value;
                yield return (denomination, count);
            }
        }
    }
}
