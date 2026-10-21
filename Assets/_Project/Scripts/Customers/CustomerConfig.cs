using Sirenix.OdinInspector;
using UnityEngine;

namespace ShooterShop.Customers
{
    [CreateAssetMenu(menuName = "Shooter Shop/Customer Config", fileName = "CustomerConfig")]
    public sealed class CustomerConfig : ScriptableObject
    {
        [BoxGroup("Prefab"), Required, AssetsOnly]
        [SerializeField] private Customer _prefab;

        [BoxGroup("Spawning"), MinMaxSlider(1, 60, true), SuffixLabel("sec")]
        [SerializeField] private Vector2 _spawnInterval = new(8, 15);

        [BoxGroup("Spawning"), MinValue(1)]
        [SerializeField] private int _maxCustomers = 3;

        [BoxGroup("Behaviour"), MinValue(5), SuffixLabel("sec")]
        [SerializeField] private float _patience = 45;

        [BoxGroup("Behaviour"), MinValue(5), SuffixLabel("sec")]
        [InfoBox("How long a customer waits at an empty stand for the item to be restocked")]
        [SerializeField] private float _searchPatience = 25;

        [BoxGroup("Behaviour"), MinValue(0.5f), SuffixLabel("m/s")]
        [SerializeField] private float _walkSpeed = 2.5f;

        [BoxGroup("Requests"), PropertyRange(0, 1)]
        [InfoBox("Chance that a customer asks for something that is currently in stock")]
        [SerializeField] private float _inStockRequestChance = 0.75f;

        [BoxGroup("Requests"), MinValue(1)]
        [SerializeField] private int _maxWeaponAmount = 1;

        [BoxGroup("Requests"), MinValue(1)]
        [SerializeField] private int _maxAmmoAmount = 4;

        [BoxGroup("Tips"), MinValue(0), SuffixLabel("sec")]
        [InfoBox("Customers served within this time after reaching the line leave a tip, smaller the longer they waited")]
        [SerializeField] private float _tipWindow = 15;

        [BoxGroup("Tips"), PropertyRange(0, 1)]
        [SerializeField] private float _maxTipShare = 0.2f;

        [BoxGroup("Prices"), PropertyRange(0, 0.05f)]
        [InfoBox("Per 1% of stand markup: chance to refuse when above zero, chance to take one more when below")]
        [SerializeField] private float _priceSensitivity = 0.015f;

        [BoxGroup("Violence"), MinValue(1)]
        [SerializeField] private int _hitsToKill = 2;

        [BoxGroup("Violence"), MinValue(1), SuffixLabel("sec")]
        [InfoBox("After a customer is killed everyone runs out and nobody comes in for this long")]
        [SerializeField] private float _panicDuration = 30;

        [BoxGroup("Violence"), MinValue(1)]
        [SerializeField] private float _panicSpeedMultiplier = 2.2f;

        [BoxGroup("Corpse"), MinValue(0)]
        [InfoBox("Impulse applied to the ragdoll bone closest to the killing shot")]
        [SerializeField] private float _deathImpulse = 120f;

        [BoxGroup("Corpse"), MinValue(0), SuffixLabel("sec")]
        [SerializeField] private float _corpseSinkDelay = 3f;

        [BoxGroup("Corpse"), MinValue(0.5f), SuffixLabel("sec")]
        [SerializeField] private float _corpseSinkDuration = 6f;

        [BoxGroup("Corpse"), MinValue(0.1f), SuffixLabel("m")]
        [SerializeField] private float _corpseSinkDepth = 1.2f;

        public int HitsToKill => _hitsToKill;
        public float PanicDuration => _panicDuration;
        public float PanicSpeedMultiplier => _panicSpeedMultiplier;
        public float DeathImpulse => _deathImpulse;
        public float CorpseSinkDelay => _corpseSinkDelay;
        public float CorpseSinkDuration => _corpseSinkDuration;
        public float CorpseSinkDepth => _corpseSinkDepth;
        public Customer Prefab => _prefab;
        public float SearchPatience => _searchPatience;
        public float TipWindow => _tipWindow;
        public float MaxTipShare => _maxTipShare;
        public float PriceSensitivity => _priceSensitivity;
        public Vector2 SpawnInterval => _spawnInterval;
        public int MaxCustomers => _maxCustomers;
        public float Patience => _patience;
        public float WalkSpeed => _walkSpeed;
        public float InStockRequestChance => _inStockRequestChance;
        public int MaxWeaponAmount => _maxWeaponAmount;
        public int MaxAmmoAmount => _maxAmmoAmount;
    }
}
