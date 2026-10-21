using ShooterShop.Upgrades;
using Sirenix.OdinInspector;
using UnityEngine.Serialization;
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
        [FormerlySerializedAs("_stockTabButton")]
        [SerializeField] private Button _weaponsTabButton;

        [BoxGroup("Tabs"), Required]
        [SerializeField] private Button _ammoTabButton;

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
        public Button WeaponsTabButton => _weaponsTabButton;
        public Button AmmoTabButton => _ammoTabButton;
        public Button UpgradesTabButton => _upgradesTabButton;
        public bool IsOpen => _window.activeSelf;

        public void SetOpen(bool open) => _window.SetActive(open);

        public void SetBalance(string balance) => _balance.text = balance;

        public void SetStorage(string storage) => _storage.text = storage;

        public void SetMessage(string message) => _message.text = message;

        public ProductCardView AddCard() => Instantiate(_cardPrefab, _cardsRoot);

        public UpgradeCardView AddUpgradeCard() => Instantiate(_upgradeCardPrefab, _upgradeCardsRoot);

        public void ShowTab(CatalogTab tab)
        {
            _stockPanel.SetActive(tab != CatalogTab.Upgrades);
            _upgradesPanel.SetActive(tab == CatalogTab.Upgrades);
            _weaponsTabButton.interactable = tab != CatalogTab.Weapons;
            _ammoTabButton.interactable = tab != CatalogTab.Ammo;
            _upgradesTabButton.interactable = tab != CatalogTab.Upgrades;
        }
    }
}
