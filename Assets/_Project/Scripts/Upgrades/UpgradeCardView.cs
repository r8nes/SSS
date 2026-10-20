using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ShooterShop.Upgrades
{
    public sealed class UpgradeCardView : MonoBehaviour
    {
        [Required]
        [SerializeField] private TMP_Text _name;

        [Required]
        [SerializeField] private TMP_Text _description;

        [Required]
        [SerializeField] private TMP_Text _status;

        [Required]
        [SerializeField] private Button _buyButton;

        [Required]
        [SerializeField] private TMP_Text _buyLabel;

        public Button BuyButton => _buyButton;

        public void SetInfo(string displayName, string description)
        {
            _name.text = displayName;
            _description.text = description;
        }

        public void SetState(string status, string buyLabel, bool canBuy, bool isOwned)
        {
            _status.text = status;
            _buyLabel.text = buyLabel;
            _buyButton.interactable = canBuy;
            _buyButton.gameObject.SetActive(!isOwned);
        }
    }
}
