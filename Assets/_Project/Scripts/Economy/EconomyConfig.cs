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
        [SerializeField] private int _warehouseCapacity = 6;

        [BoxGroup("Storage"), TableList]
        [SerializeField] private List<StockEntry> _startingStock = new();

        public int StartingMoney => _startingMoney;
        [BoxGroup("Penalties"), MinValue(0), SuffixLabel("$")]
        [InfoBox("Charged when a player kills a customer of the shop")]
        [SerializeField] private int _killFine = 1500;

        [BoxGroup("Penalties"), Required]
        [SerializeField] private string _killFineMessage = "You killed a customer. Fine: ${0:N0}";

        [BoxGroup("Penalties"), MinValue(1), SuffixLabel("$")]
        [InfoBox("Given automatically when the balance drops to zero or below, the balance becomes this amount")]
        [SerializeField] private int _loanAmount = 5000;

        [BoxGroup("Penalties"), Required]
        [SerializeField] private string _loanMessage = "Out of money. The bank gave you a loan of ${0:N0}";

        public int KillFine => _killFine;
        public string KillFineMessage => _killFineMessage;
        public int LoanAmount => _loanAmount;
        public string LoanMessage => _loanMessage;
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
