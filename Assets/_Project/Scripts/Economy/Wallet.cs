using System;
using ShooterShop.Saves;

namespace ShooterShop.Economy
{
    public sealed class Wallet : IWallet
    {
        public event Action<int> BalanceChanged;

        public int Balance { get; private set; }

        public Wallet(StartingState state)
        {
            Balance = state.Money;
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

        public void Charge(int amount)
        {
            if (amount <= 0)
                return;

            Balance -= amount;
            BalanceChanged?.Invoke(Balance);
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
