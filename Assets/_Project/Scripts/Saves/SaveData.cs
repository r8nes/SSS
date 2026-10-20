using System;
using System.Collections.Generic;

namespace ShooterShop.Saves
{
    [Serializable]
    public sealed class SaveData
    {
        public int CompletedDays;
        public int Money;
        public List<StockSave> Stock = new();
        public List<string> Upgrades = new();
    }

    [Serializable]
    public sealed class StockSave
    {
        public string Product;
        public int Amount;
    }
}
