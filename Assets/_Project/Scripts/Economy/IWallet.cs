using System;

namespace ShooterShop.Economy
{
    public interface IWallet
    {
        event Action<int> BalanceChanged;
        int Balance { get; }
        bool CanAfford(int amount);
        bool TrySpend(int amount);
        void Add(int amount);
        void Charge(int amount);
    }
}
