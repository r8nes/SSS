using System;
using ShooterShop.Days;
using ShooterShop.Feedback;
using UnityEngine;
using VContainer.Unity;

namespace ShooterShop.Economy
{
    public sealed class Bank : IFines, IStartable, IDisposable
    {
        private readonly IWallet _wallet;
        private readonly EconomyConfig _config;
        private readonly IDayStatistics _statistics;
        private readonly IShopFeed _feed;

        public Bank(IWallet wallet, EconomyConfig config, IDayStatistics statistics, IShopFeed feed)
        {
            _wallet = wallet;
            _config = config;
            _statistics = statistics;
            _feed = feed;
        }

        public void Start() => _wallet.BalanceChanged += OnBalanceChanged;

        public void Dispose() => _wallet.BalanceChanged -= OnBalanceChanged;

        public void ServerFineForKill(Vector3 position)
        {
            _wallet.Charge(_config.KillFine);
            _statistics.RecordExpense(_config.KillFine);
            _feed.Popup(position + Vector3.up * 2f, $"-${_config.KillFine:N0}", NotificationKind.Danger);
            _feed.Notify(string.Format(_config.KillFineMessage, _config.KillFine), NotificationKind.Danger);
        }

        private void OnBalanceChanged(int balance)
        {
            if (balance > 0)
                return;

            _wallet.Add(_config.LoanAmount - balance);
            _feed.Notify(string.Format(_config.LoanMessage, _config.LoanAmount), NotificationKind.Warning);
        }
    }
}
