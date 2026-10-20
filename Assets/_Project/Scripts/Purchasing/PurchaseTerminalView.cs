using ShooterShop.Upgrades;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ShooterShop.Purchasing
{
    public sealed class PurchaseTerminalView : MonoBehaviour
    {
        [BoxGroup("Window"), Required]
        [SerializeField] private GameObject _window;

        [BoxGroup("Window"), Required]
        [SerializeField] private Button _closeButton;

        [BoxGroup("Status"), Required]
        [SerializeField] private TMP_Text _balance;

        [BoxGroup("Status"), Required]
        [SerializeField] private TMP_Text _storage;

        [BoxGroup("Status"), Required]
        [SerializeField] private TMP_Text _message;

        [BoxGroup("Tabs"), Required]
        [SerializeField] private Button _stockTabButton;

        [BoxGroup("Tabs"), Required]
        [SerializeField] private Button _upgradesTabButton;

        [BoxGroup("Tabs"), Required]
        [SerializeField] private GameObject _stockPanel;

        [BoxGroup("Tabs"), Required]
        [SerializeField] private GameObject _upgradesPanel;

        [BoxGroup("Products"), Required]
        [SerializeField] private Transform _cardsRoot;

        [BoxGroup("Products"), Required, AssetsOnly]
        [SerializeField] private ProductCardView _cardPrefab;

        [BoxGroup("Upgrades"), Required]
        [SerializeField] private Transform _upgradeCardsRoot;

        [BoxGroup("Upgrades"), Required, AssetsOnly]
        [SerializeField] private UpgradeCardView _upgradeCardPrefab;

        public Button CloseButton => _closeButton;
        public Button StockTabButton => _stockTabButton;
        public Button UpgradesTabButton => _upgradesTabButton;
        public bool IsOpen => _window.activeSelf;

        public void SetOpen(bool open) => _window.SetActive(open);

        public void SetBalance(string balance) => _balance.text = balance;

        public void SetStorage(string storage) => _storage.text = storage;

        public void SetMessage(string message) => _message.text = message;

        public ProductCardView AddCard() => Instantiate(_cardPrefab, _cardsRoot);

        public UpgradeCardView AddUpgradeCard() => Instantiate(_upgradeCardPrefab, _upgradeCardsRoot);

        public void ShowStockTab(bool showStock)
        {
            _stockPanel.SetActive(showStock);
            _upgradesPanel.SetActive(!showStock);
            _stockTabButton.interactable = !showStock;
            _upgradesTabButton.interactable = showStock;
        }
    }
}
