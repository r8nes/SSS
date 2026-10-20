using System.Collections.Generic;
using System.Linq;
using Mirror;
using ShooterShop.Catalog;
using ShooterShop.Economy;
using ShooterShop.Upgrades;

namespace ShooterShop.Saves
{
    public sealed class StartingState
    {
        public int CompletedDays { get; }
        public int Money { get; }
        public int Capacity { get; }
        public IReadOnlyList<(ProductConfig Product, int Amount)> Stock { get; }
        public IReadOnlyList<UpgradeConfig> Upgrades { get; }

        public StartingState(EconomyConfig economy, ProductCatalog products, UpgradeCatalog upgrades, ISaveStorage storage)
        {
            if (NetworkServer.active && storage.TryLoad(out var save))
            {
                CompletedDays = save.CompletedDays;
                Money = save.Money;
                Stock = save.Stock
                    .Select(s => (Product: products.Products.FirstOrDefault(p => p.name == s.Product), s.Amount))
                    .Where(s => s.Product != null && s.Amount > 0)
                    .ToList();
                Upgrades = upgrades.Upgrades.Where(u => save.Upgrades.Contains(u.name)).ToList();
            }
            else
            {
                Money = economy.StartingMoney;
                Stock = economy.StartingStock.Select(s => (s.Product, s.Amount)).ToList();
                Upgrades = new List<UpgradeConfig>();
            }

            Capacity = economy.WarehouseCapacity + Upgrades.Sum(u => u.StorageBonus);
        }
    }
}
