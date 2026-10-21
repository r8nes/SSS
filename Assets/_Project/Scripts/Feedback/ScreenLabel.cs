using TMPro;
using UnityEngine;

namespace ShooterShop.Feedback
{
    public sealed class ScreenLabel
    {
        private string _shownText;

        internal ScreenLabel(TMP_Text text, Transform anchor, Vector3 offset, ScreenLabelStyle style)
        {
            View = text;
            Anchor = anchor;
            Offset = offset;
            Style = style;
            View.fontSize = style.FontSize;
            View.color = style.Color;
        }

        internal TMP_Text View { get; }
        internal ScreenLabelStyle Style { get; }

        public Transform Anchor { get; }
        public Vector3 Offset { get; set; }
        public bool IsVisible { get; set; } = true;

        public Vector3 WorldPosition => Anchor.position + Offset;

        public void SetText(string text)
        {
            if (text == _shownText)
                return;

            _shownText = text;
            View.text = text;
        }

        public void SetColor(Color color) => View.color = color;

        public void SetAlpha(float alpha) => View.alpha = alpha;
    }
}
