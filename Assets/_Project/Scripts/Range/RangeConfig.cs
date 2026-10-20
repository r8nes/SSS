using Sirenix.OdinInspector;
using UnityEngine;

namespace ShooterShop.Range
{
    [CreateAssetMenu(menuName = "Shooter Shop/Range Config", fileName = "RangeConfig")]
    public sealed class RangeConfig : ScriptableObject
    {
        [BoxGroup("Rental"), MinValue(0), SuffixLabel("$")]
        [SerializeField] private int _laneFee = 60;

        [BoxGroup("Rental"), MinValue(10), SuffixLabel("sec")]
        [SerializeField] private float _sessionDuration = 60;

        [BoxGroup("Visitors"), PropertyRange(0, 1)]
        [InfoBox("Chance that a new customer comes to shoot instead of shopping, when a lane is free")]
        [SerializeField] private float _visitorChance = 0.4f;

        [BoxGroup("Visitors"), MinValue(1)]
        [SerializeField] private int _maxAmmoBoxes = 2;

        [BoxGroup("Shooting"), MinMaxSlider(0.2f, 5f, true), SuffixLabel("sec")]
        [SerializeField] private Vector2 _shotInterval = new(0.6f, 1.6f);

        [BoxGroup("Shooting"), PropertyRange(0, 1)]
        [SerializeField] private float _visitorAccuracy = 0.7f;

        public int LaneFee => _laneFee;
        public float SessionDuration => _sessionDuration;
        public float VisitorChance => _visitorChance;
        public int MaxAmmoBoxes => _maxAmmoBoxes;
        public Vector2 ShotInterval => _shotInterval;
        public float VisitorAccuracy => _visitorAccuracy;
    }
}
