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

        [BoxGroup("Products"), Required]
        [SerializeField] private Transform _cardsRoot;

        [BoxGroup("Products"), Required, AssetsOnly]
        [SerializeField] private ProductCardView _cardPrefab;

        public Button CloseButton => _closeButton;
        public bool IsOpen => _window.activeSelf;

        public void SetOpen(bool open) => _window.SetActive(open);

        public void SetBalance(string balance) => _balance.text = balance;

        public void SetStorage(string storage) => _storage.text = storage;

        public void SetMessage(string message) => _message.text = message;

        public ProductCardView AddCard() => Instantiate(_cardPrefab, _cardsRoot);
    }
}
