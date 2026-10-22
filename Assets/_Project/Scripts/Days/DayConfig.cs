using Sirenix.OdinInspector;
using UnityEngine;

namespace ShooterShop.Days
{
    [CreateAssetMenu(menuName = "Shooter Shop/Day Config", fileName = "DayConfig")]
    public sealed class DayConfig : ScriptableObject
    {
        [MinValue(30), SuffixLabel("sec")]
        [InfoBox("Real time the in-game clock needs to go from opening to closing hour")]
        [SerializeField] private float _openDuration = 240;

        [BoxGroup("Clock"), PropertyRange(0, 23)]
        [SerializeField] private int _openingHour = 10;

        [BoxGroup("Clock"), PropertyRange(1, 24)]
        [InfoBox("After this hour no new customers come, the shop stays open until a player flips the sign")]
        [SerializeField] private int _closingHour = 16;

        [BoxGroup("Clock"), Required]
        [SerializeField] private string _closingTimeMessage = "It's {0}. No more customers today, flip the sign to close the shop";

        [BoxGroup("Summary"), MinValue(0.1f), SuffixLabel("sec")]
        [SerializeField] private float _summaryFadeDuration = 1.2f;

        [BoxGroup("Summary"), MinValue(0f), SuffixLabel("sec")]
        [InfoBox("Pause on the black screen after Next day, while players return to their spawn points")]
        [SerializeField] private float _summaryBlackHold = 0.5f;

        [BoxGroup("Summary"), MinValue(0f), SuffixLabel("sec")]
        [SerializeField] private float _summaryRevealDelay = 0.2f;

        [BoxGroup("Summary"), MinValue(0f), SuffixLabel("sec")]
        [SerializeField] private float _summaryStagger = 0.18f;

        [BoxGroup("Summary"), MinValue(0.05f), SuffixLabel("sec")]
        [SerializeField] private float _summaryItemDuration = 0.45f;

        [BoxGroup("Summary"), MinValue(0.05f), SuffixLabel("sec")]
        [SerializeField] private float _summaryCountDuration = 1.1f;

        public float SummaryFadeDuration => _summaryFadeDuration;
        public float SummaryBlackHold => _summaryBlackHold;
        public float SummaryRevealDelay => _summaryRevealDelay;
        public float SummaryStagger => _summaryStagger;
        public float SummaryItemDuration => _summaryItemDuration;
        public float SummaryCountDuration => _summaryCountDuration;
        public float OpenDuration => _openDuration;
        public int OpeningHour => _openingHour;
        public int ClosingHour => _closingHour;
        public string ClosingTimeMessage => _closingTimeMessage;

        public string ClockAt(double timeLeft)
        {
            var share = _openDuration <= 0f ? 1.0 : 1.0 - System.Math.Clamp(timeLeft / _openDuration, 0.0, 1.0);
            var minutes = (int)(_openingHour * 60 + share * (_closingHour - _openingHour) * 60);
            minutes -= minutes % 10;
            return $"{minutes / 60:00}:{minutes % 60:00}";
        }
    }
}
