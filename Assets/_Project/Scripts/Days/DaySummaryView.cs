using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ShooterShop.Days
{
    public sealed class DaySummaryView : MonoBehaviour
    {
        [BoxGroup("Window"), Required]
        [SerializeField] private GameObject _window;

        [BoxGroup("Info"), Required]
        [SerializeField] private TMP_Text _title;

        [BoxGroup("Info"), Required]
        [SerializeField] private TMP_Text _income;

        [BoxGroup("Info"), Required]
        [SerializeField] private TMP_Text _expenses;

        [BoxGroup("Info"), Required]
        [SerializeField] private TMP_Text _profit;

        [BoxGroup("Info"), Required]
        [SerializeField] private TMP_Text _details;

        [BoxGroup("Actions"), Required]
        [SerializeField] private Button _nextDayButton;

        [BoxGroup("Actions"), Required]
        [SerializeField] private GameObject _waitingLabel;

        public Button NextDayButton => _nextDayButton;
        public bool IsOpen => _window.activeSelf;

        public void SetOpen(bool open) => _window.SetActive(open);

        public void SetSummary(string title, string income, string expenses, string profit, bool isProfitable, string details)
        {
            _title.text = title;
            _income.text = income;
            _expenses.text = expenses;
            _profit.text = profit;
            _profit.color = isProfitable ? new Color(0.45f, 0.9f, 0.45f) : new Color(1f, 0.4f, 0.35f);
            _details.text = details;
        }

        public void SetCanContinue(bool canContinue)
        {
            _nextDayButton.gameObject.SetActive(canContinue);
            _waitingLabel.SetActive(!canContinue);
        }
    }
}
