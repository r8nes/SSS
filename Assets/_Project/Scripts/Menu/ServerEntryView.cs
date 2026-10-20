using Akila.FPSFramework;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;

namespace ShooterShop.Menu
{
    public sealed class ServerEntryView : MonoBehaviour
    {
        [Required]
        [SerializeField] private InteractiveButton _button;

        [Required]
        [SerializeField] private TMP_Text _label;

        public InteractiveButton Button => _button;

        public void SetLabel(string text) => _label.text = text;
    }
}
