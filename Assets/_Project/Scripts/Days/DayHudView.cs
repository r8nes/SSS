using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;

namespace ShooterShop.Days
{
    public sealed class DayHudView : MonoBehaviour
    {
        [Required]
        [SerializeField] private TMP_Text _label;

        private string _shown;

        public void SetText(string text)
        {
            if (text == _shown)
                return;

            _shown = text;
            _label.text = text;
        }
    }
}
