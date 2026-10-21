using System;
using Sirenix.OdinInspector;
using UnityEngine;

namespace ShooterShop.Feedback
{
    [Serializable]
    public sealed class ScreenLabelStyle
    {
        [MinValue(6)]
        [SerializeField] private float _fontSize = 22f;

        [SerializeField] private Color _color = Color.white;

        [MinValue(0), SuffixLabel("m")]
        [InfoBox("0 means no distance limit")]
        [SerializeField] private float _maxDistance;

        [InfoBox("Keeps the label on the screen edge when its target is off screen or behind the camera")]
        [SerializeField] private bool _pinToScreenEdge;

        public float FontSize => _fontSize;
        public Color Color => _color;
        public float MaxDistance => _maxDistance;
        public bool PinToScreenEdge => _pinToScreenEdge;

        public ScreenLabelStyle(float fontSize, Color color, float maxDistance, bool pinToScreenEdge)
        {
            _fontSize = fontSize;
            _color = color;
            _maxDistance = maxDistance;
            _pinToScreenEdge = pinToScreenEdge;
        }
    }
}
