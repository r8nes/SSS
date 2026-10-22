using System.Collections.Generic;
using PrimeTween;
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

        [BoxGroup("Reveal"), Required, ListDrawerSettings(ShowFoldout = false)]
        [InfoBox("Elements that appear one by one, in this order")]
        [SerializeField] private List<CanvasGroup> _revealOrder = new();

        [BoxGroup("Reveal")]
        [SerializeField] private Color _profitColor = new(0.45f, 0.9f, 0.45f);

        [BoxGroup("Reveal")]
        [SerializeField] private Color _lossColor = new(1f, 0.4f, 0.35f);

        public Button NextDayButton => _nextDayButton;
        public bool IsOpen => _window.activeSelf;

        public void SetOpen(bool open) => _window.SetActive(open);

        public void SetCanContinue(bool canContinue)
        {
            _nextDayButton.gameObject.SetActive(canContinue);
            _waitingLabel.SetActive(!canContinue);
        }

        public void Reveal(DaySummaryData data, DayConfig config)
        {
            _title.text = data.Title;
            _details.text = data.Details;
            _profit.color = data.Profit >= 0 ? _profitColor : _lossColor;
            _income.text = FormatMoney("Income", 0);
            _expenses.text = FormatMoney("Expenses", 0);
            _profit.text = FormatMoney("Profit", 0);

            for (var i = 0; i < _revealOrder.Count; i++)
            {
                var group = _revealOrder[i];
                var delay = config.SummaryRevealDelay + i * config.SummaryStagger;
                var rect = (RectTransform)group.transform;

                Tween.StopAll(group);
                Tween.StopAll(rect);
                group.alpha = 0f;
                Tween.Alpha(group, 0f, 1f, config.SummaryItemDuration, startDelay: delay);
                Tween.Scale(rect, Vector3.one * 0.85f, Vector3.one, config.SummaryItemDuration, Ease.OutBack, startDelay: delay);
            }

            var countDelay = config.SummaryRevealDelay + config.SummaryStagger;
            Count(_income, "Income", data.Income, config, countDelay);
            Count(_expenses, "Expenses", data.Expenses, config, countDelay + config.SummaryStagger);
            Count(_profit, "Profit", data.Profit, config, countDelay + config.SummaryStagger * 2f);
        }

        private static void Count(TMP_Text text, string label, int value, DayConfig config, float delay)
        {
            Tween.StopAll(text);
            Tween.Custom(text, 0f, value, config.SummaryCountDuration, (target, current) => target.text = FormatMoney(label, Mathf.RoundToInt(current)), Ease.OutCubic, startDelay: delay);
        }

        private static string FormatMoney(string label, int value) =>
            $"{label}  {(value < 0 ? "-" : "")}${Mathf.Abs(value):N0}";
    }
}
