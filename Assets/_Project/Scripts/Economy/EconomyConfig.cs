using System;
using System.Collections.Generic;
using ShooterShop.Catalog;
using Sirenix.OdinInspector;
using UnityEngine;

namespace ShooterShop.Economy
{
    [CreateAssetMenu(menuName = "Shooter Shop/Economy Config", fileName = "EconomyConfig")]
    public sealed class EconomyConfig : ScriptableObject
    {
        [BoxGroup("Wallet"), MinValue(0), SuffixLabel("$")]
        [SerializeField] private int _startingMoney = 5000;

        [BoxGroup("Storage"), MinValue(1), SuffixLabel("slots")]
        [SerializeField] private int _warehouseCapacity = 50;

        [BoxGroup("Storage"), TableList]
        [SerializeField] private List<StockEntry> _startingStock = new();

        public int StartingMoney => _startingMoney;
        public int WarehouseCapacity => _warehouseCapacity;
        public IReadOnlyList<StockEntry> StartingStock => _startingStock;
    }

    [Serializable]
    public struct StockEntry
    {
        [Required]
        public ProductConfig Product;

        [MinValue(1)]
        public int Amount;
    }
}
