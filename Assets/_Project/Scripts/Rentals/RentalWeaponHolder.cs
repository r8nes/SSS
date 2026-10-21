using System.Collections.Generic;
using Akila.FPSFramework;
using Mirror;
using ShooterShop.Feedback;
using Sirenix.OdinInspector;
using UnityEngine;
using VContainer;

namespace ShooterShop.Rentals
{
    public sealed class RentalWeaponHolder : NetworkBehaviour, IInteractable
    {
        [BoxGroup("Holder"), Required]
        [SerializeField] private string _displayName = "Rental rack";

        [BoxGroup("Holder"), AssetsOnly, ListDrawerSettings(ShowFoldout = false)]
        [SerializeField] private List<RentalWeaponConfig> _startingWeapons = new();

        [BoxGroup("Feedback"), Required]
        [SerializeField] private InteractableOutline _outline;

        [BoxGroup("Layout"), ChildGameObjectsOnly]
        [InfoBox("Where visitors stand to pick up or return a rental weapon, can be empty for the back rack")]
        [SerializeField] private Transform _customerPoint;

        [BoxGroup("Layout"), Required, ChildGameObjectsOnly, ListDrawerSettings(ShowFoldout = false)]
        [SerializeField] private List<Transform> _slots = new();

        private readonly SyncList<int> _slotWeapons = new();
        private GameObject[] _visuals;

        private RentalWeaponCatalog _catalog;
        private IPlayerHands _hands;
        private ItemAnimator _animator;
        private bool _isHighlighted;

        public Transform CustomerPoint => _customerPoint;
        public bool requireHoldToInteract => false;
        public bool isHighlighted
        {
            get => _isHighlighted;
            set
            {
                _isHighlighted = value;
                _outline.SetVisible(value && !string.IsNullOrEmpty(GetInteractionName()));
            }
        }

        public bool HasFreeSlot => _slotWeapons.Contains(-1);

        [Inject]
        public void Construct(RentalWeaponCatalog catalog, IPlayerHands hands, ItemAnimator animator)
        {
            _catalog = catalog;
            _hands = hands;
            _animator = animator;
        }

        public override void OnStartServer()
        {
            while (_slotWeapons.Count < _slots.Count)
                _slotWeapons.Add(-1);

            foreach (var weapon in _startingWeapons)
                ServerPut(_catalog.IndexOf(weapon));
        }

        public override void OnStartClient()
        {
            _visuals = new GameObject[_slots.Count];
            _slotWeapons.OnChange += OnSlotChanged;

            for (var i = 0; i < _slotWeapons.Count; i++)
                RefreshSlot(i, false);
        }

        public override void OnStopClient() => _slotWeapons.OnChange -= OnSlotChanged;

        [Server]
        public int ServerTakeAny()
        {
            for (var i = _slotWeapons.Count - 1; i >= 0; i--)
            {
                var weapon = _slotWeapons[i];

                if (weapon < 0)
                    continue;

                _slotWeapons[i] = -1;
                return weapon;
            }

            return -1;
        }

        [Server]
        public bool ServerPut(int weapon)
        {
            if (!_catalog.TryGet(weapon, out _))
                return false;

            var free = _slotWeapons.IndexOf(-1);

            if (free < 0)
                return false;

            _slotWeapons[free] = weapon;
            return true;
        }

        public string GetInteractionName()
        {
            if (_hands == null)
                return string.Empty;

            var held = _hands.HeldRental;

            if (held != null)
                return HasFreeSlot ? $"Put {held.DisplayName} on the {_displayName}" : $"{_displayName} is full";

            return TryPeek(out var weapon) ? $"Take {weapon.DisplayName}" : string.Empty;
        }

        public void Interact(InteractionsManager source)
        {
            var held = _hands.HeldRental;

            if (held != null)
            {
                if (!HasFreeSlot)
                    return;

                _hands.RemoveHeld();
                CmdPut(_catalog.IndexOf(held));
            }
            else if (TryPeek(out _))
            {
                CmdTake();
            }
        }

        [Command(requiresAuthority = false)]
        private void CmdPut(int weapon) => ServerPut(weapon);

        [Command(requiresAuthority = false)]
        private void CmdTake(NetworkConnectionToClient sender = null)
        {
            if (sender == null)
                return;

            var weapon = ServerTakeAny();

            if (weapon >= 0)
                TargetGive(sender, weapon);
        }

        [TargetRpc]
        private void TargetGive(NetworkConnectionToClient _, int weapon)
        {
            if (_catalog.TryGet(weapon, out var config))
                _hands.Give(config);
        }

        private bool TryPeek(out RentalWeaponConfig weapon)
        {
            for (var i = _slotWeapons.Count - 1; i >= 0; i--)
                if (_catalog.TryGet(_slotWeapons[i], out weapon))
                    return true;

            weapon = null;
            return false;
        }

        private void OnSlotChanged(SyncList<int>.Operation _, int index, int __)
        {
            if (index >= 0 && index < _slots.Count)
                RefreshSlot(index, true);
            else
                for (var i = 0; i < _slotWeapons.Count; i++)
                    RefreshSlot(i, false);
        }

        private void RefreshSlot(int index, bool animate)
        {
            if (_visuals == null || index >= _visuals.Length)
                return;

            if (_visuals[index] != null)
            {
                if (animate)
                    _animator.Vanish(_visuals[index]);
                else
                    Destroy(_visuals[index]);
            }

            _visuals[index] = null;

            if (index >= _slotWeapons.Count || !_catalog.TryGet(_slotWeapons[index], out var weapon))
                return;

            var visual = Instantiate(weapon.DisplayModel, _slots[index], false);
            _visuals[index] = visual;

            if (animate)
                _animator.PopIn(visual.transform, visual.transform.localScale);
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(1f, 0.5f, 0.2f);

            foreach (var slot in _slots)
                if (slot != null)
                    Gizmos.DrawWireCube(slot.position, new Vector3(0.6f, 0.15f, 0.15f));

            if (_customerPoint != null)
                Gizmos.DrawWireSphere(_customerPoint.position + Vector3.up * 0.1f, 0.3f);

            UnityEditor.Handles.Label(transform.position + Vector3.up * 0.5f, _displayName);
        }
#endif
    }
}
