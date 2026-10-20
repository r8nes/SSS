using System;
using System.Linq;
using Mirror;
using ShooterShop.Days;
using ShooterShop.Economy;
using ShooterShop.Storage;
using ShooterShop.Upgrades;
using VContainer.Unity;

namespace ShooterShop.Saves
{
    public sealed class ProgressSaver : IStartable, IDisposable
    {
        private readonly IDayState _day;
        private readonly IWallet _wallet;
        private readonly IWarehouse _warehouse;
        private readonly IUnlocks _unlocks;
        private readonly ISaveStorage _storage;
        private int _savedDay;

        public ProgressSaver(IDayState day, IWallet wallet, IWarehouse warehouse, IUnlocks unlocks, ISaveStorage storage)
        {
            _day = day;
            _wallet = wallet;
            _warehouse = warehouse;
            _unlocks = unlocks;
            _storage = storage;
        }

        public void Start() => _day.Changed += OnDayChanged;

        public void Dispose() => _day.Changed -= OnDayChanged;

        private void OnDayChanged()
        {
            if (!NetworkServer.active || _day.Phase != DayPhase.Summary || _savedDay == _day.Day)
                return;

            _savedDay = _day.Day;
            _storage.Save(new SaveData
            {
                CompletedDays = _day.Day,
                Money = _wallet.Balance,
                Stock = _warehouse.Stock.Select(s => new StockSave { Product = s.Key.name, Amount = s.Value }).ToList(),
                Upgrades = _unlocks.Owned.Select(u => u.name).ToList()
            });
        }
    }
}
