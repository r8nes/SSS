using System;

namespace ShooterShop.Economy
{
    public sealed class Wallet : IWallet
    {
        public event Action<int> BalanceChanged;

        public int Balance { get; private set; }

        public Wallet(EconomyConfig config)
        {
            Balance = config.StartingMoney;
        }

        public bool CanAfford(int amount) => amount >= 0 && Balance >= amount;

        public bool TrySpend(int amount)
        {
            if (!CanAfford(amount))
                return false;

            Balance -= amount;
            BalanceChanged?.Invoke(Balance);
            return true;
        }

        public void Add(int amount)
        {
            if (amount <= 0)
                throw new ArgumentOutOfRangeException(nameof(amount), amount, "Amount must be positive");

            Balance += amount;
            BalanceChanged?.Invoke(Balance);
        }
    }
}
