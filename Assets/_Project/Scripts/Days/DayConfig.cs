using Sirenix.OdinInspector;
using UnityEngine;

namespace ShooterShop.Days
{
    [CreateAssetMenu(menuName = "Shooter Shop/Day Config", fileName = "DayConfig")]
    public sealed class DayConfig : ScriptableObject
    {
        [MinValue(30), SuffixLabel("sec")]
        [SerializeField] private float _openDuration = 300;

        [MinValue(5), SuffixLabel("sec")]
        [SerializeField] private float _closingWarning = 60;

        public float OpenDuration => _openDuration;
        public float ClosingWarning => _closingWarning;
    }
}
