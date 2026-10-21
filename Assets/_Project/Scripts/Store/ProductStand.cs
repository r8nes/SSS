using System.Collections.Generic;
using Akila.FPSFramework;
using Mirror;
using ShooterShop.Catalog;
using Sirenix.OdinInspector;
using UnityEngine;
using VContainer;

namespace ShooterShop.Store
{
    public sealed class ProductStand : NetworkBehaviour, IInteractable
    {
        [BoxGroup("Stand"), Required]
        [SerializeField] private string _displayName = "Stand";

        [BoxGroup("Stand"), EnumToggleButtons]
        [SerializeField] private List<ProductCategory> _accepts = new();

        [BoxGroup("Layout"), Required, ChildGameObjectsOnly]
        [InfoBox("Where customers stand while picking items")]
        [SerializeField] private Transform _customerPoint;

        [BoxGroup("Layout"), Required, ChildGameObjectsOnly, ListDrawerSettings(ShowFoldout = false)]
        [SerializeField] private List<Transform> _slots = new();

        private readonly SyncList<int> _slotProducts = new();
        private GameObject[] _visuals;

        private ProductCatalog _catalog;
        private CarryService _carry;

        public Transform CustomerPoint => _customerPoint;
        public bool requireHoldToInteract => false;
        public bool isHighlighted { get; set; }

        [Inject]
        public void Construct(ProductCatalog catalog, CarryService carry)
        {
            _catalog = catalog;
            _carry = carry;
        }

        public override void OnStartServer()
        {
            while (_slotProducts.Count < _slots.Count)
                _slotProducts.Add(-1);
        }

        public override void OnStartClient()
        {
            _visuals = new GameObject[_slots.Count];
            _slotProducts.OnChange += OnSlotChanged;

            for (var i = 0; i < _slotProducts.Count; i++)
                RefreshSlot(i);
        }

        public override void OnStopClient() => _slotProducts.OnChange -= OnSlotChanged;

        public bool Accepts(ProductConfig product) => product != null && _accepts.Contains(product.Category);

        public int CountOf(ProductConfig product)
        {
            var index = _catalog.IndexOf(product);
            var count = 0;

            foreach (var slot in _slotProducts)
                if (slot == index)
                    count++;

            return count;
        }

        public int FreeSlots
        {
            get
            {
                var count = 0;

                foreach (var slot in _slotProducts)
                    if (slot < 0)
                        count++;

                return count;
            }
        }

        [Server]
        public int ServerTake(ProductConfig product, int amount)
        {
            var index = _catalog.IndexOf(product);
            var taken = 0;

            for (var i = _slotProducts.Count - 1; i >= 0 && taken < amount; i--)
            {
                if (_slotProducts[i] != index)
                    continue;

                _slotProducts[i] = -1;
                taken++;
            }

            return taken;
        }

        [Server]
        public int ServerPut(ProductConfig product, int amount)
        {
            var placed = 0;

            for (var i = 0; i < _slotProducts.Count && placed < amount; i++)
            {
                if (_slotProducts[i] >= 0)
                    continue;

                _slotProducts[i] = _catalog.IndexOf(product);
                placed++;
            }

            return placed;
        }

        public string GetInteractionName()
        {
            var box = _carry.LocalCarriedBox;

            if (box == null || box.Product == null)
                return string.Empty;

            if (!Accepts(box.Product))
                return $"{_displayName}: not for {box.Product.DisplayName}";

            return FreeSlots > 0 ? $"Place {box.Product.DisplayName} ({box.Units} left)" : $"{_displayName} is full";
        }

        public void Interact(InteractionsManager source)
        {
            var box = _carry.LocalCarriedBox;

            if (box != null && Accepts(box.Product) && FreeSlots > 0)
                CmdPlaceFrom(box);
        }

        [Command(requiresAuthority = false)]
        private void CmdPlaceFrom(DeliveryBox box, NetworkConnectionToClient sender = null)
        {
            if (box == null || sender?.identity == null || box.CarrierNetId != sender.identity.netId)
                return;

            var product = box.Product;

            if (!Accepts(product) || FreeSlots == 0 || !box.ServerTakeUnit())
                return;

            ServerPut(product, 1);

            if (box.Units == 0)
                NetworkServer.Destroy(box.gameObject);
        }

        private void OnSlotChanged(SyncList<int>.Operation _, int index, int __)
        {
            if (index >= 0 && index < _slots.Count)
                RefreshSlot(index);
            else
                for (var i = 0; i < _slotProducts.Count; i++)
                    RefreshSlot(i);
        }

        private void RefreshSlot(int index)
        {
            if (_visuals[index] != null)
                Destroy(_visuals[index]);

            _visuals[index] = null;

            if (index < _slotProducts.Count && _catalog.TryGet(_slotProducts[index], out var product) && product.ShelfModel != null)
                _visuals[index] = Instantiate(product.ShelfModel, _slots[index], false);
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.4f, 0.9f, 1f);

            foreach (var slot in _slots)
                if (slot != null)
                    Gizmos.DrawWireCube(slot.position, Vector3.one * 0.15f);

            if (_customerPoint == null)
                return;

            Gizmos.color = Color.magenta;
            Gizmos.DrawWireSphere(_customerPoint.position + Vector3.up * 0.1f, 0.3f);
            UnityEditor.Handles.Label(_customerPoint.position + Vector3.up * 0.6f, _displayName);
        }
#endif
    }
}
