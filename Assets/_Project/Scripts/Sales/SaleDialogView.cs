using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ShooterShop.Sales
{
    public sealed class SaleDialogView : MonoBehaviour
    {
        [BoxGroup("Window"), Required]
        [SerializeField] private GameObject _window;

        [BoxGroup("Info"), Required]
        [SerializeField] private TMP_Text _request;

        [BoxGroup("Info"), Required]
        [SerializeField] private TMP_Text _payout;

        [BoxGroup("Info"), Required]
        [SerializeField] private TMP_Text _stock;

        [BoxGroup("Info"), Required]
        [SerializeField] private TMP_Text _message;

        [BoxGroup("Actions"), Required]
        [SerializeField] private Button _sellButton;

        [BoxGroup("Actions"), Required]
        [SerializeField] private Button _declineButton;

        public Button SellButton => _sellButton;
        public Button DeclineButton => _declineButton;
        public bool IsOpen => _window.activeSelf;

        public void SetOpen(bool open) => _window.SetActive(open);

        public void SetRequest(string request, string payout)
        {
            _request.text = request;
            _payout.text = payout;
        }

        public void SetStock(string stock, bool isEnough)
        {
            _stock.text = stock;
            _stock.color = isEnough ? Color.white : new Color(1f, 0.4f, 0.35f);
            _sellButton.interactable = isEnough;
        }

        public void SetMessage(string message) => _message.text = message;
    }
}
