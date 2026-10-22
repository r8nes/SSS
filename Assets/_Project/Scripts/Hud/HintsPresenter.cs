using Mirror;
using ShooterShop.Input;
using ShooterShop.Rentals;
using ShooterShop.Store;
using VContainer.Unity;

namespace ShooterShop.Hud
{
    public sealed class HintsPresenter : ITickable
    {
        private readonly HintsView _view;
        private readonly HudConfig _config;
        private readonly StoreConfig _storeConfig;
        private readonly CarryService _carry;
        private readonly IPlayerHands _hands;
        private readonly IPlayerInputLock _inputLock;

        public HintsPresenter(HintsView view, HudConfig config, StoreConfig storeConfig, CarryService carry, IPlayerHands hands, IPlayerInputLock inputLock)
        {
            _view = view;
            _config = config;
            _storeConfig = storeConfig;
            _carry = carry;
            _hands = hands;
            _inputLock = inputLock;
        }

        public void Tick()
        {
            if (NetworkClient.localPlayer == null || _inputLock.IsLocked)
            {
                _view.Hide(_config.HintFade);
                return;
            }

            _view.Show(CurrentHint(), _config.HintFade);
        }

        private string CurrentHint()
        {
            if (_carry.IsLocalPlayerCarrying)
                return string.Format(_config.CarryHint, _storeConfig.DropKey, _storeConfig.ThrowKey);

            return _hands.IsHoldingWeapon ? _config.WeaponHint : _config.DefaultHint;
        }
    }
}
