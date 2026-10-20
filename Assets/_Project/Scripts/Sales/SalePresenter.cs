using System;
using ShooterShop.Catalog;
using ShooterShop.Customers;
using ShooterShop.Input;
using ShooterShop.Network;
using UnityEngine.InputSystem;
using VContainer.Unity;

namespace ShooterShop.Sales
{
    public sealed class SalePresenter : ICustomerInteraction, IStartable, ITickable, IDisposable
    {
        private readonly SaleDialogView _view;
        private readonly IShopState _state;
        private readonly IShopCommands _commands;
        private readonly IPlayerInputLock _inputLock;
        private Customer _customer;

        public SalePresenter(SaleDialogView view, IShopState state, IShopCommands commands, IPlayerInputLock inputLock)
        {
            _view = view;
            _state = state;
            _commands = commands;
            _inputLock = inputLock;
        }

        public void Start()
        {
            _view.SellButton.onClick.AddListener(Sell);
            _view.DeclineButton.onClick.AddListener(Decline);
            _state.StockChanged += OnStockChanged;
            _view.SetOpen(false);
        }

        public void Tick()
        {
            if (_view.IsOpen && Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
                Close();
        }

        public void Dispose()
        {
            _view.SellButton.onClick.RemoveListener(Sell);
            _view.DeclineButton.onClick.RemoveListener(Decline);
            _state.StockChanged -= OnStockChanged;
            Close();
        }

        public void Open(Customer customer)
        {
            if (_customer == customer)
            {
                Close();
                return;
            }

            Close();
            _customer = customer;
            _customer.StateChanged += OnCustomerStateChanged;

            var request = customer.Request;
            _view.SetRequest(request.Describe(), $"Pays ${request.Price:N0}");
            _view.SetMessage(string.Empty);
            RefreshStock();

            _inputLock.Acquire(this);
            _view.SetOpen(true);
        }

        private void Sell()
        {
            if (_customer == null)
                return;

            _commands.Sell(_customer);
            _view.SetMessage("Sold!");
        }

        private void Decline()
        {
            if (_customer != null)
                _commands.Dismiss(_customer);

            Close();
        }

        private void Close()
        {
            if (_customer != null)
                _customer.StateChanged -= OnCustomerStateChanged;

            _customer = null;
            _view.SetOpen(false);
            _inputLock.Release(this);
        }

        private void OnCustomerStateChanged(Customer customer)
        {
            if (customer.State != CustomerState.Waiting)
                Close();
        }

        private void OnStockChanged(ProductConfig product)
        {
            if (_customer != null && _customer.Request.Product == product)
                RefreshStock();
        }

        private void RefreshStock()
        {
            var request = _customer.Request;
            var stock = _state.GetStock(request.Product);
            _view.SetStock($"In stock: {stock}", stock >= request.Amount);
        }
    }
}
