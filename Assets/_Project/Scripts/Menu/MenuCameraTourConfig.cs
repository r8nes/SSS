using PrimeTween;
using Sirenix.OdinInspector;
using UnityEngine;

namespace ShooterShop.Menu
{
    [CreateAssetMenu(menuName = "Shooter Shop/Menu/Camera Tour Config", fileName = "MenuCameraTourConfig")]
    public sealed class MenuCameraTourConfig : ScriptableObject
    {
        [MinValue(1f), SuffixLabel("sec")]
        [InfoBox("Used by shots whose own duration is zero")]
        [SerializeField] private float _defaultShotDuration = 12f;

        [MinValue(0.1f), SuffixLabel("sec")]
        [SerializeField] private float _fadeDuration = 1.2f;

        [MinValue(0f), SuffixLabel("sec")]
        [InfoBox("How long the screen stays black between shots")]
        [SerializeField] private float _blackHold = 0.4f;

        [SerializeField] private Ease _moveEase = Ease.InOutSine;

        [SerializeField] private bool _shuffle;

        public float DefaultShotDuration => _defaultShotDuration;
        public float FadeDuration => _fadeDuration;
        public float BlackHold => _blackHold;
        public Ease MoveEase => _moveEase;
        public bool Shuffle => _shuffle;
    }
}
