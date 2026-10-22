using System.Collections.Generic;
using ShooterShop.Customers;
using ShooterShop.Feedback;
using ShooterShop.Rentals;
using Sirenix.OdinInspector;
using UnityEngine;

namespace ShooterShop.Raids
{
    [CreateAssetMenu(menuName = "Shooter Shop/Raids/Raid Config", fileName = "RaidConfig")]
    public sealed class RaidConfig : ScriptableObject
    {
        [BoxGroup("Gang"), Required, AssetsOnly]
        [SerializeField] private Raider _prefab;

        [BoxGroup("Gang"), MinValue(0)]
        [SerializeField] private int _thieves = 2;

        [BoxGroup("Gang"), MinValue(0)]
        [SerializeField] private int _gunmen = 3;

        [BoxGroup("Gang"), AssetsOnly, ListDrawerSettings(ShowFoldout = false)]
        [InfoBox("Looks the gang members are picked from, empty means any customer look")]
        [SerializeField] private List<CustomerAppearance> _appearances = new();

        [BoxGroup("Gang"), Required, AssetsOnly, ListDrawerSettings(ShowFoldout = false)]
        [SerializeField] private List<RentalWeaponConfig> _weapons = new();

        [BoxGroup("Gang"), Required]
        [SerializeField] private string _gangName = "The Italian mafia";

        [BoxGroup("Timing"), MinMaxSlider(0f, 180f, true), SuffixLabel("sec")]
        [InfoBox("Delay after the shop opens before the gang bursts in")]
        [SerializeField] private Vector2 _startDelay = new(25f, 50f);

        [BoxGroup("Timing"), MinValue(1f), SuffixLabel("sec")]
        [InfoBox("Customers stay away for this long once the raid starts")]
        [SerializeField] private float _customerPause = 60f;

        [BoxGroup("Movement"), MinValue(0.5f), SuffixLabel("m/s")]
        [SerializeField] private float _runSpeed = 3.2f;

        [BoxGroup("Movement"), MinValue(0.1f)]
        [InfoBox("Blend tree MoveY value of the walk clip")]
        [SerializeField] private float _walkBlendValue = 5f;

        [BoxGroup("Movement"), MinValue(1)]
        [SerializeField] private float _animationSmoothing = 10f;

        [BoxGroup("Stealing"), MinValue(1)]
        [SerializeField] private int _carryCapacity = 12;

        [BoxGroup("Stealing"), MinValue(0f), SuffixLabel("sec")]
        [SerializeField] private float _stealDuration = 2.5f;

        [BoxGroup("Shooting"), MinValue(1f), SuffixLabel("m")]
        [SerializeField] private float _attackRange = 18f;

        [BoxGroup("Shooting"), MinMaxSlider(0.1f, 5f, true), SuffixLabel("sec")]
        [SerializeField] private Vector2 _fireInterval = new(0.6f, 1.6f);

        [BoxGroup("Shooting"), PropertyRange(0f, 1f)]
        [SerializeField] private float _hitChance = 0.3f;

        [BoxGroup("Shooting"), PropertyRange(0f, 1f)]
        [InfoBox("Share of the hit chance left at the edge of the attack range, it falls off linearly with distance")]
        [SerializeField] private float _hitChanceAtRange = 0.35f;

        [BoxGroup("Shooting"), MinMaxSlider(0f, 3f, true), SuffixLabel("m")]
        [InfoBox("How far a missed shot lands from the player, so the tracer visibly misses")]
        [SerializeField] private Vector2 _missOffset = new(0.5f, 1.4f);

        [BoxGroup("Shooting"), MinValue(0f)]
        [SerializeField] private float _damage = 9f;

        [BoxGroup("Shooting"), SuffixLabel("m")]
        [InfoBox("Height above the player root the gang aims at")]
        [SerializeField] private float _aimHeight = 1.3f;

        [BoxGroup("Shooting"), SuffixLabel("m")]
        [InfoBox("Height above the raider root the line of sight starts from")]
        [SerializeField] private float _eyeHeight = 0.6f;

        [BoxGroup("Shooting")]
        [SerializeField] private bool _thievesShoot = true;

        [BoxGroup("Shooting")]
        [SerializeField] private LayerMask _sightMask = 1;

        [BoxGroup("Violence"), MinValue(1)]
        [SerializeField] private int _hitsToKill = 3;

        [BoxGroup("Violence"), MinValue(0)]
        [SerializeField] private int _bounty = 150;

        [BoxGroup("Violence"), MinValue(0)]
        [SerializeField] private float _deathImpulse = 120f;

        [BoxGroup("Violence"), MinValue(0), SuffixLabel("sec")]
        [SerializeField] private float _corpseSinkDelay = 4f;

        [BoxGroup("Violence"), MinValue(0.5f), SuffixLabel("sec")]
        [SerializeField] private float _corpseSinkDuration = 6f;

        [BoxGroup("Violence"), MinValue(0.1f), SuffixLabel("m")]
        [SerializeField] private float _corpseSinkDepth = 1.2f;

        [BoxGroup("Ambush"), MinMaxSlider(1, 16, true)]
        [SerializeField] private Vector2Int _ambushCount = new(7, 8);

        [BoxGroup("Ambush"), MinValue(1f), SuffixLabel("m")]
        [SerializeField] private float _ambushAttackRange = 32f;

        [BoxGroup("Ambush"), MinValue(0f), SuffixLabel("m")]
        [InfoBox("Ambushers dash to random spots this far around their point between bursts")]
        [SerializeField] private float _ambushWanderRadius = 3f;

        [BoxGroup("Ambush"), MinMaxSlider(0.5f, 10f, true), SuffixLabel("sec")]
        [SerializeField] private Vector2 _ambushBurstTime = new(2.5f, 5f);

        [BoxGroup("Ambush"), MinValue(0.5f), SuffixLabel("sec")]
        [SerializeField] private float _ambushDashTimeout = 3f;

        [BoxGroup("Ambush"), MinValue(0)]
        [InfoBox("Paid once every bandit of the ambush is dead")]
        [SerializeField] private int _ambushReward = 800;

        [BoxGroup("Ambush")]
        [SerializeField] private ScreenLabelStyle _markerStyle = new(26f, new Color(1f, 0.15f, 0.1f), 0f, false);

        [BoxGroup("Ambush"), SuffixLabel("m")]
        [SerializeField] private Vector3 _markerOffset = new(0f, 1.25f, 0f);

        [BoxGroup("Ambush"), Required]
        [SerializeField] private string _markerText = "▼";

        public float HitChanceAtRange => _hitChanceAtRange;
        public Vector2 MissOffset => _missOffset;
        public float AmbushWanderRadius => _ambushWanderRadius;
        public Vector2 AmbushBurstTime => _ambushBurstTime;
        public float AmbushDashTimeout => _ambushDashTimeout;
        public Vector2Int AmbushCount => _ambushCount;
        public float AmbushAttackRange => _ambushAttackRange;
        public int AmbushReward => _ambushReward;
        public ScreenLabelStyle MarkerStyle => _markerStyle;
        public Vector3 MarkerOffset => _markerOffset;
        public string MarkerText => _markerText;
        public Raider Prefab => _prefab;
        public int Thieves => _thieves;
        public int Gunmen => _gunmen;
        public IReadOnlyList<CustomerAppearance> Appearances => _appearances;
        public IReadOnlyList<RentalWeaponConfig> Weapons => _weapons;
        public string GangName => _gangName;
        public Vector2 StartDelay => _startDelay;
        public float CustomerPause => _customerPause;
        public float RunSpeed => _runSpeed;
        public float WalkBlendValue => _walkBlendValue;
        public float AnimationSmoothing => _animationSmoothing;
        public int CarryCapacity => _carryCapacity;
        public float StealDuration => _stealDuration;
        public float AttackRange => _attackRange;
        public Vector2 FireInterval => _fireInterval;
        public float HitChance => _hitChance;
        public float Damage => _damage;
        public float AimHeight => _aimHeight;
        public float EyeHeight => _eyeHeight;
        public bool ThievesShoot => _thievesShoot;
        public LayerMask SightMask => _sightMask;
        public int HitsToKill => _hitsToKill;
        public int Bounty => _bounty;
        public float DeathImpulse => _deathImpulse;
        public float CorpseSinkDelay => _corpseSinkDelay;
        public float CorpseSinkDuration => _corpseSinkDuration;
        public float CorpseSinkDepth => _corpseSinkDepth;
    }
}
