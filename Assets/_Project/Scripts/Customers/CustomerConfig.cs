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

        [BoxGroup("Behaviour"), MinValue(0.5f), SuffixLabel("m/s")]
        [SerializeField] private float _walkSpeed = 2.5f;

        [BoxGroup("Requests"), PropertyRange(0, 1)]
        [InfoBox("Chance that a customer asks for something that is currently in stock")]
        [SerializeField] private float _inStockRequestChance = 0.75f;

        [BoxGroup("Requests"), MinValue(1)]
        [SerializeField] private int _maxWeaponAmount = 1;

        [BoxGroup("Requests"), MinValue(1)]
        [SerializeField] private int _maxAmmoAmount = 4;

        public Customer Prefab => _prefab;
        public Vector2 SpawnInterval => _spawnInterval;
        public int MaxCustomers => _maxCustomers;
        public float Patience => _patience;
        public float WalkSpeed => _walkSpeed;
        public float InStockRequestChance => _inStockRequestChance;
        public int MaxWeaponAmount => _maxWeaponAmount;
        public int MaxAmmoAmount => _maxAmmoAmount;
    }
}
