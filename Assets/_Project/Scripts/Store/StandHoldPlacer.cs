using ShooterShop.Input;
using UnityEngine;
using UnityEngine.InputSystem;
using VContainer.Unity;

namespace ShooterShop.Store
{
    public sealed class StandHoldPlacer : ITickable
    {
        private readonly StoreLayout _layout;
        private readonly StoreConfig _config;
        private readonly CarryService _carry;
        private readonly IPlayerInputLock _inputLock;

        private ProductStand _stand;
        private float _nextPlaceTime;

        public StandHoldPlacer(StoreLayout layout, StoreConfig config, CarryService carry, IPlayerInputLock inputLock)
        {
            _layout = layout;
            _config = config;
            _carry = carry;
            _inputLock = inputLock;
        }

        public void Tick()
        {
            var keyboard = Keyboard.current;

            if (_layout == null || keyboard == null || _inputLock.IsLocked || !_carry.IsLocalPlayerCarrying)
            {
                _stand = null;
                return;
            }

            var key = keyboard[_config.PlaceKey];

            if (key.wasPressedThisFrame)
            {
                _stand = FindHighlighted();
                _nextPlaceTime = Time.time + _config.HoldRepeatDelay;
                return;
            }

            if (!key.isPressed || _stand == null || !_stand.isHighlighted || Time.time < _nextPlaceTime)
                return;

            _nextPlaceTime = Time.time + _config.HoldRepeatInterval;
            _stand.Interact(null);
        }

        private ProductStand FindHighlighted()
        {
            foreach (var stand in _layout.Stands)
                if (stand != null && stand.isHighlighted)
                    return stand;

            return null;
        }
    }
}
