using System.Collections.Generic;
using Mirror;
using ShooterShop.Feedback;
using Sirenix.OdinInspector;
using UnityEngine;
using VContainer;

namespace ShooterShop.Rentals
{
    public sealed class RentalWeaponHolder : NetworkBehaviour
    {
        [BoxGroup("Holder"), Required]
        [SerializeField] private string _displayName = "Rental rack";

        [BoxGroup("Holder"), AssetsOnly, ListDrawerSettings(ShowFoldout = false)]
        [SerializeField] private List<RentalWeaponConfig> _startingWeapons = new();

        [BoxGroup("Layout"), ChildGameObjectsOnly]
        [InfoBox("Where visitors stand to pick up or return a rental weapon, can be empty for the back rack")]
        [SerializeField] private Transform _customerPoint;

        [BoxGroup("Layout"), Required, ChildGameObjectsOnly, ListDrawerSettings(ShowFoldout = false)]
        [InfoBox("Each slot needs a RentalSlot with a collider on the Iteractable layer")]
        [SerializeField] private List<Transform> _slots = new();

        private readonly SyncList<int> _slotWeapons = new();
        private GameObject[] _visuals;
        private Renderer[][] _visualRenderers;

        private RentalWeaponCatalog _catalog;
        private IPlayerHands _hands;
        private ItemAnimator _animator;
        private FeedbackConfig _feedback;

        private int _highlightedSlot = -1;
        private int _outlinedSlot = -1;
        private int _ghostSlot = -1;
        private RentalWeaponConfig _ghostWeapon;
        private GameObject _ghost;

        public Transform CustomerPoint => _customerPoint;

        [Inject]
        public void Construct(RentalWeaponCatalog catalog, IPlayerHands hands, ItemAnimator animator, FeedbackConfig feedback)
        {
            _catalog = catalog;
            _hands = hands;
            _animator = animator;
            _feedback = feedback;
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
            _visualRenderers = new Renderer[_slots.Count][];
            _slotWeapons.OnChange += OnSlotChanged;

            for (var i = 0; i < _slotWeapons.Count; i++)
                RefreshSlot(i, false);
        }

        public override void OnStopClient()
        {
            _slotWeapons.OnChange -= OnSlotChanged;
            HideGhost();
        }

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
        public bool ServerPut(int weapon) => ServerPutAt(_slotWeapons.IndexOf(-1), weapon);

        public string GetSlotPrompt(int slot)
        {
            if (_hands == null || !IsValidSlot(slot))
                return string.Empty;

            RefreshHighlight();

            if (_catalog.TryGet(_slotWeapons[slot], out var weapon))
                return $"Take {weapon.DisplayName}";

            var held = _hands.HeldRental;
            return held != null ? $"Put {held.DisplayName} on the {_displayName}" : string.Empty;
        }

        public void InteractSlot(int slot)
        {
            if (!IsValidSlot(slot))
                return;

            if (_slotWeapons[slot] >= 0)
            {
                CmdTake(slot);
                return;
            }

            var held = _hands.HeldRental;

            if (held == null)
                return;

            _hands.RemoveHeld();
            HideGhost();
            CmdPut(slot, _catalog.IndexOf(held));
        }

        public void SetSlotHighlighted(int slot, bool isHighlighted)
        {
            if (isHighlighted)
                _highlightedSlot = slot;
            else if (_highlightedSlot == slot)
                _highlightedSlot = -1;

            RefreshHighlight();
        }

        [Command(requiresAuthority = false)]
        private void CmdPut(int slot, int weapon)
        {
            if (!ServerPutAt(slot, weapon))
                ServerPut(weapon);
        }

        [Command(requiresAuthority = false)]
        private void CmdTake(int slot, NetworkConnectionToClient sender = null)
        {
            if (sender == null || !IsValidSlot(slot) || _slotWeapons[slot] < 0)
                return;

            var weapon = _slotWeapons[slot];
            _slotWeapons[slot] = -1;
            TargetGive(sender, weapon);
        }

        [TargetRpc]
        private void TargetGive(NetworkConnectionToClient _, int weapon)
        {
            if (_catalog.TryGet(weapon, out var config))
                _hands.Give(config);
        }

        [Server]
        private bool ServerPutAt(int slot, int weapon)
        {
            if (!IsValidSlot(slot) || _slotWeapons[slot] >= 0 || !_catalog.TryGet(weapon, out _))
                return false;

            _slotWeapons[slot] = weapon;
            return true;
        }

        private bool IsValidSlot(int slot) => slot >= 0 && slot < _slotWeapons.Count && slot < _slots.Count;

        private void RefreshHighlight()
        {
            var slot = _highlightedSlot;
            var isOccupied = IsValidSlot(slot) && _slotWeapons[slot] >= 0;
            var held = _hands?.HeldRental;

            var outlineTarget = isOccupied ? slot : -1;

            if (outlineTarget != _outlinedSlot)
            {
                SetSlotOutline(_outlinedSlot, false);
                _outlinedSlot = outlineTarget;
                SetSlotOutline(outlineTarget, true);
            }

            var ghostSlot = IsValidSlot(slot) && !isOccupied && held != null ? slot : -1;

            if (ghostSlot == _ghostSlot && held == _ghostWeapon)
                return;

            HideGhost();

            if (ghostSlot >= 0)
                ShowGhost(ghostSlot, held);
        }

        private void ShowGhost(int slot, RentalWeaponConfig weapon)
        {
            _ghostSlot = slot;
            _ghostWeapon = weapon;
            _ghost = Instantiate(weapon.DisplayModel, _slots[slot], false);

            foreach (var target in _ghost.GetComponentsInChildren<Renderer>())
            {
                var materials = new Material[target.sharedMaterials.Length];

                for (var i = 0; i < materials.Length; i++)
                    materials[i] = _feedback.GhostMaterial;

                target.sharedMaterials = materials;
                target.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
        }

        private void HideGhost()
        {
            if (_ghost != null)
                Destroy(_ghost);

            _ghost = null;
            _ghostSlot = -1;
            _ghostWeapon = null;
        }

        private void SetSlotOutline(int slot, bool visible)
        {
            if (_visualRenderers == null || slot < 0 || slot >= _visualRenderers.Length || _visualRenderers[slot] == null)
                return;

            MaterialOutline.Apply(_visualRenderers[slot], _feedback.OutlineMaterial, visible);
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
            _visualRenderers[index] = null;

            if (_outlinedSlot == index)
                _outlinedSlot = -1;

            if (index < _slotWeapons.Count && _catalog.TryGet(_slotWeapons[index], out var weapon))
            {
                var visual = Instantiate(weapon.DisplayModel, _slots[index], false);
                _visuals[index] = visual;
                _visualRenderers[index] = visual.GetComponentsInChildren<Renderer>();

                if (animate)
                    _animator.PopIn(visual.transform, visual.transform.localScale);
            }

            RefreshHighlight();
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(1f, 0.5f, 0.2f);

            foreach (var slot in _slots)
                if (slot != null)
                    Gizmos.DrawWireCube(slot.position, new Vector3(0.15f, 0.15f, 0.6f));

            if (_customerPoint != null)
                Gizmos.DrawWireSphere(_customerPoint.position + Vector3.up * 0.1f, 0.3f);

            UnityEditor.Handles.Label(transform.position + Vector3.up * 0.5f, _displayName);
        }
#endif
    }
}
