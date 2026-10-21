using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ShooterShop.Feedback
{
    public sealed class NotificationItemView : MonoBehaviour
    {
        [Required]
        [SerializeField] private TMP_Text _text;

        [Required]
        [SerializeField] private Image _accent;

        [Required]
        [SerializeField] private CanvasGroup _canvasGroup;

        public CanvasGroup CanvasGroup => _canvasGroup;

        public void Set(string text, Color color)
        {
            _text.text = text;
            _accent.color = color;
        }
    }
}
