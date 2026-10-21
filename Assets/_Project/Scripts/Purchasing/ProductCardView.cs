using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ShooterShop.Purchasing
{
    public sealed class ProductCardView : MonoBehaviour
    {
        [BoxGroup("Info"), Required]
        [SerializeField] private Image _icon;

        [BoxGroup("Info"), Required]
        [SerializeField] private TMP_Text _name;

        [BoxGroup("Info"), Required]
        [SerializeField] private TMP_Text _price;

        [BoxGroup("Info"), Required]
        [SerializeField] private TMP_Text _stock;

        [BoxGroup("Amount"), Required]
        [SerializeField] private TMP_Text _amount;

        [BoxGroup("Amount"), Required]
        [SerializeField] private Button _decreaseButton;

        [BoxGroup("Amount"), Required]
        [SerializeField] private Button _increaseButton;

        [BoxGroup("Amount"), Required]
        [SerializeField] private Button _buyButton;

        [BoxGroup("Amount"), Required]
        [SerializeField] private TMP_Text _buyLabel;

        public Button DecreaseButton => _decreaseButton;
        public Button IncreaseButton => _increaseButton;
        public Button BuyButton => _buyButton;

        public void SetInfo(string displayName, Sprite icon)
        {
            _name.text = displayName;
            _icon.sprite = icon;
            _icon.enabled = icon != null;
        }

        public void SetPrice(string price) => _price.text = price;

        public void SetVisible(bool isVisible) => gameObject.SetActive(isVisible);

        public void SetStock(string stock) => _stock.text = stock;

        public void SetLocked(bool isLocked)
        {
            _buyButton.interactable = !isLocked;
            _increaseButton.interactable = !isLocked;
            _decreaseButton.interactable = !isLocked;
        }

        public void SetAmount(int amount, string total)
        {
            _amount.text = amount.ToString();
            _buyLabel.text = total;
        }
    }
}
