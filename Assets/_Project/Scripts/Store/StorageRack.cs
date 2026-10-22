using System;
using System.Collections.Generic;
using Akila.FPSFramework;
using ShooterShop.Feedback;
using ShooterShop.Upgrades;
using Sirenix.OdinInspector;
using UnityEngine;
using VContainer;

namespace ShooterShop.Store
{
    [Serializable]
    public sealed class StorageSlot
    {
        [Required]
        [SerializeField] private Transform _point;

        [AssetsOnly]
        [SerializeField] private UpgradeConfig _requiredUpgrade;

        public Transform Point => _point;
        public UpgradeConfig RequiredUpgrade => _requiredUpgrade;
    }

    public sealed class StorageRack : MonoBehaviour, IInteractable
    {
        [ListDrawerSettings(ShowFoldout = false)]
        [InfoBox("One box per slot, the slot is where the box centre lands. A slot with an upgrade opens once it is bought")]
        [SerializeField] private List<StorageSlot> _slots = new();

        [Required, ChildGameObjectsOnly]
        [SerializeField] private InteractableOutline _outline;

        [AssetsOnly]
        [InfoBox("Empty means the rack is available from the start")]
        [SerializeField] private UpgradeConfig _requiredUpgrade;

        private CarryService _carry;
        private IUnlocks _unlocks;
        private StoreConfig _config;
        private bool _isHighlighted;

        public bool requireHoldToInteract => false;
        public bool isHighlighted
        {
            get => _isHighlighted;
            set
            {
                _isHighlighted = value;
                _outline.SetVisible(value && _carry.IsLocalPlayerCarrying);
            }
        }

        public bool IsUnlocked => _unlocks.IsUnlocked(_requiredUpgrade);

        [Inject]
        public void Construct(CarryService carry, IUnlocks unlocks, StoreConfig config)
        {
            _carry = carry;
            _unlocks = unlocks;
            _config = config;
        }

        public Transform FindFreeSlot()
        {
            foreach (var slot in _slots)
                if (slot.Point != null && _unlocks.IsUnlocked(slot.RequiredUpgrade) && !_carry.IsRestingNear(slot.Point.position, _config.StorageSlotRadius))
                    return slot.Point;

            return null;
        }

        public string GetInteractionName()
        {
            var box = _carry.LocalCarriedBox;

            if (box == null)
                return string.Empty;

            if (!IsUnlocked)
                return string.Format(_config.StorageLockedPrompt, _requiredUpgrade.DisplayName);

            return FindFreeSlot() == null ? _config.StorageFullPrompt : string.Format(_config.StoragePrompt, box.Product?.DisplayName);
        }

        public void Interact(InteractionsManager source)
        {
            var box = _carry.LocalCarriedBox;

            if (box != null && IsUnlocked && FindFreeSlot() != null)
                box.RequestStore(this);
        }
    }
}
