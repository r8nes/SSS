using System;
using System.Collections.Generic;
using ShooterShop.Catalog;
using ShooterShop.Input;
using ShooterShop.Network;
using ShooterShop.Upgrades;
using UnityEngine.InputSystem;
using VContainer.Unity;

namespace ShooterShop.Purchasing
{
    public sealed class PurchaseTerminalPresenter : IStartable, ITickable, IDisposable
    {
        private const int MinAmount = 1;
        private const int MaxAmount = 99;

        private readonly PurchaseTerminal _terminal;
        private readonly PurchaseTerminalView _view;
        private readonly ProductCatalog _catalog;
        private readonly IShopState _state;
        private readonly IShopCommands _commands;
        private readonly IPlayerInputLock _inputLock;
        private readonly IUnlocks _unlocks;
        private readonly ISupplierPricing _pricing;
        private CatalogTab _tab = CatalogTab.Weapons;
        private readonly Dictionary<ProductConfig, ProductCardView> _cards = new();
        private readonly Dictionary<ProductConfig, int> _amounts = new();

        public PurchaseTerminalPresenter(
            PurchaseTerminal terminal,
            PurchaseTerminalView view,
            ProductCatalog catalog,
            IShopState state,
            IShopCommands commands,
            IPlayerInputLock inputLock,
            IUnlocks unlocks,
            ISupplierPricing pricing)
        {
            _pricing = pricing;
            _unlocks = unlocks;
            _terminal = terminal;
            _view = view;
            _catalog = catalog;
            _state = state;
            _commands = commands;
            _inputLock = inputLock;
        }

        public void Start()
        {
            foreach (var product in _catalog.Products)
                CreateCard(product);

            _terminal.Interacted += Toggle;
            _view.CloseButton.onClick.AddListener(Close);
            _view.WeaponsTabButton.onClick.AddListener(ShowWeapons);
            _view.AmmoTabButton.onClick.AddListener(ShowAmmo);
            _view.UpgradesTabButton.onClick.AddListener(ShowUpgrades);
            _pricing.PricesChanged += RefreshAllPrices;
            _unlocks.Changed += RefreshAllStock;
            _state.MoneyChanged += RefreshStatus;
            _state.StorageChanged += RefreshStatus;
            _state.StockChanged += RefreshStock;

            _view.SetOpen(false);
        }

        public void Tick()
        {
            if (_view != null && _view.IsOpen && Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
                Close();
        }

        public void Dispose()
        {
            _terminal.Interacted -= Toggle;
            _view.CloseButton.onClick.RemoveListener(Close);
            _view.WeaponsTabButton.onClick.RemoveListener(ShowWeapons);
            _view.AmmoTabButton.onClick.RemoveListener(ShowAmmo);
            _view.UpgradesTabButton.onClick.RemoveListener(ShowUpgrades);
            _pricing.PricesChanged -= RefreshAllPrices;
            _unlocks.Changed -= RefreshAllStock;
            _state.MoneyChanged -= RefreshStatus;
            _state.StorageChanged -= RefreshStatus;
            _state.StockChanged -= RefreshStock;
            _inputLock.Release(this);
        }

        private void Toggle()
        {
            if (_view.IsOpen)
                Close();
            else
                Open();
        }

        private void Open()
        {
            _inputLock.Acquire(this);
            _view.SetMessage(string.Empty);
            RefreshStatus();
            RefreshAllStock();
            RefreshAllPrices();
            ShowTab(_tab);
            _view.SetOpen(true);
        }

        private void Close()
        {
            _view.SetOpen(false);
            _inputLock.Release(this);
        }

        private void ShowWeapons() => ShowTab(CatalogTab.Weapons);

        private void ShowAmmo() => ShowTab(CatalogTab.Ammo);

        private void ShowUpgrades() => ShowTab(CatalogTab.Upgrades);

        private void ShowTab(CatalogTab tab)
        {
            _tab = tab;
            _view.ShowTab(tab);

            foreach (var (product, card) in _cards)
                card.SetVisible(tab == (product.IsAmmo ? CatalogTab.Ammo : CatalogTab.Weapons));
        }

        private void RefreshAllPrices()
        {
            foreach (var product in _catalog.Products)
            {
                if (!_cards.TryGetValue(product, out var card))
                    continue;

                card.SetPrice(FormatPrice(product));
                RefreshAmount(product);
            }
        }

        private void RefreshAllStock()
        {
            foreach (var product in _catalog.Products)
                RefreshStock(product);
        }

        private void CreateCard(ProductConfig product)
        {
            var card = _view.AddCard();
            card.SetInfo(product.DisplayName, product.Icon);
            card.SetPrice(FormatPrice(product));
            card.DecreaseButton.onClick.AddListener(() => ChangeAmount(product, -1));
            card.IncreaseButton.onClick.AddListener(() => ChangeAmount(product, 1));
            card.BuyButton.onClick.AddListener(() => Buy(product));

            _cards[product] = card;
            _amounts[product] = MinAmount;
            RefreshAmount(product);
        }

        private void ChangeAmount(ProductConfig product, int delta)
        {
            _amounts[product] = Math.Clamp(_amounts[product] + delta, MinAmount, MaxAmount);
            RefreshAmount(product);
        }

        private void Buy(ProductConfig product)
        {
            var amount = _amounts[product];
            var cost = _state.GetPurchaseCost(product, amount);

            if (!_unlocks.IsUnlocked(product.RequiredUpgrade))
                _view.SetMessage($"Requires {product.RequiredUpgrade.DisplayName}");
            else if (_state.Money < cost)
                _view.SetMessage($"Not enough money: need {FormatMoney(cost)}");
            else if (_state.FreeSpace < BoxesNeeded(product, amount))
                _view.SetMessage($"Not enough storage: need {BoxesNeeded(product, amount)} box slots");
            else
            {
                _commands.Purchase(product, amount);
                _view.SetMessage($"Ordered {amount} x {product.DisplayName}");
            }
        }

        private int BoxesNeeded(ProductConfig product, int amount) =>
            product.BoxesFor(_state.GetStock(product) + amount) - product.BoxesFor(_state.GetStock(product));

        private void RefreshStatus()
        {
            _view.SetBalance(FormatMoney(_state.Money));
            _view.SetStorage($"Storage {_state.UsedSpace}/{_state.Capacity} boxes");
        }

        private void RefreshStock(ProductConfig product)
        {
            if (!_cards.TryGetValue(product, out var card))
                return;

            var isLocked = !_unlocks.IsUnlocked(product.RequiredUpgrade);
            card.SetLocked(isLocked);
            card.SetStock(isLocked ? $"Requires {product.RequiredUpgrade.DisplayName}" : $"In stock: {_state.GetStock(product)}");
        }

        private void RefreshAmount(ProductConfig product)
        {
            var amount = _amounts[product];
            _cards[product].SetAmount(amount, $"Buy {FormatMoney(_state.GetPurchaseCost(product, amount))}");
        }

        private string FormatPrice(ProductConfig product)
        {
            var change = _pricing.GetPriceChangePercent(product);
            var current = FormatMoney(_state.GetPurchaseCost(product, 1));
            var unit = product.IsAmmo ? $" / {product.PackSize} rds" : string.Empty;

            if (change == 0)
                return current + unit;

            var color = change < 0 ? "#6EE36E" : "#FF6B5E";
            var original = $"<s><color=#9A9A9A>{FormatMoney(product.BuyPrice)}</color></s>";
            return $"{original} <color={color}>{current}</color>{unit}\n<color={color}><b>{change:+0;-0}%</b></color>";
        }

        private static string FormatMoney(int amount) => $"${amount:N0}";
    }
}
