using System;
using System.Collections.Generic;
using ShooterShop.Catalog;
using ShooterShop.Input;
using ShooterShop.Network;
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
        private readonly Dictionary<ProductConfig, ProductCardView> _cards = new();
        private readonly Dictionary<ProductConfig, int> _amounts = new();

        public PurchaseTerminalPresenter(
            PurchaseTerminal terminal,
            PurchaseTerminalView view,
            ProductCatalog catalog,
            IShopState state,
            IShopCommands commands,
            IPlayerInputLock inputLock)
        {
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
            _state.MoneyChanged += RefreshStatus;
            _state.StorageChanged += RefreshStatus;
            _state.StockChanged += RefreshStock;

            _view.SetOpen(false);
        }

        public void Tick()
        {
            if (_view.IsOpen && Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
                Close();
        }

        public void Dispose()
        {
            _terminal.Interacted -= Toggle;
            _view.CloseButton.onClick.RemoveListener(Close);
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

            foreach (var product in _catalog.Products)
                RefreshStock(product);

            _view.SetOpen(true);
        }

        private void Close()
        {
            _view.SetOpen(false);
            _inputLock.Release(this);
        }

        private void CreateCard(ProductConfig product)
        {
            var card = _view.AddCard();
            card.SetInfo(product.DisplayName, product.Icon, FormatPrice(product));
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
            var cost = product.BuyPrice * amount;

            if (_state.Money < cost)
                _view.SetMessage($"Not enough money: need {FormatMoney(cost)}");
            else if (_state.FreeSpace < product.StorageSize * amount)
                _view.SetMessage($"Not enough storage: need {product.StorageSize * amount} slots");
            else
            {
                _commands.Purchase(product, amount);
                _view.SetMessage($"Ordered {amount} x {product.DisplayName}");
            }
        }

        private void RefreshStatus()
        {
            _view.SetBalance(FormatMoney(_state.Money));
            _view.SetStorage($"Storage {_state.UsedSpace}/{_state.Capacity}");
        }

        private void RefreshStock(ProductConfig product)
        {
            if (_cards.TryGetValue(product, out var card))
                card.SetStock($"In stock: {_state.GetStock(product)}");
        }

        private void RefreshAmount(ProductConfig product)
        {
            var amount = _amounts[product];
            _cards[product].SetAmount(amount, $"Buy {FormatMoney(product.BuyPrice * amount)}");
        }

        private static string FormatPrice(ProductConfig product) =>
            product.IsAmmo ? $"{FormatMoney(product.BuyPrice)} / {product.PackSize} rds" : FormatMoney(product.BuyPrice);

        private static string FormatMoney(int amount) => $"${amount:N0}";
    }
}
