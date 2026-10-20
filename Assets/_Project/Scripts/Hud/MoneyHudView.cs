using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;

namespace ShooterShop.Hud
{
    public sealed class MoneyHudView : MonoBehaviour
    {
        [Required]
        [SerializeField] private TMP_Text _money;

        public void SetMoney(string money) => _money.text = money;
    }
}
