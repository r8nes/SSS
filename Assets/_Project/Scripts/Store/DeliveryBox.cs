using Akila.FPSFramework;
using Mirror;
using ShooterShop.Catalog;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;
using VContainer;

namespace ShooterShop.Store
{
    public sealed class DeliveryBox : NetworkBehaviour, IInteractable
    {
        [Required]
        [SerializeField] private Collider _collider;

        [Required]
        [SerializeField] private TMP_Text _label;

        [SyncVar(hook = nameof(OnContentSynced))]
        private int _productIndex = -1;

        [SyncVar(hook = nameof(OnContentSynced))]
        private int _units;

        [SyncVar(hook = nameof(OnCarrierSynced))]
        private uint _carrierNetId;

        [SyncVar(hook = nameof(OnRestPositionSynced))]
        private Vector3 _restPosition;

        [SyncVar(hook = nameof(OnRestRotationSynced))]
        private Quaternion _restRotation;

        private ProductCatalog _catalog;
        private CarryService _carry;

        public uint CarrierNetId => _carrierNetId;
        public bool IsCarried => _carrierNetId != 0;
        public int Units => _units;
        public ProductConfig Product => _catalog != null && _catalog.TryGet(_productIndex, out var product) ? product : null;

        public bool requireHoldToInteract => false;
        public bool isHighlighted { get; set; }

        [Inject]
        public void Construct(ProductCatalog catalog, CarryService carry)
        {
            _catalog = catalog;
            _carry = carry;
        }

        public override void OnStartClient()
        {
            _carry.Register(this);
            ApplyRest();
            RefreshCarried();
            RefreshLabel();
        }

        public override void OnStopClient() => _carry.Unregister(this);

        [Server]
        public void ServerInitialize(ProductConfig product, int units, Vector3 position, Quaternion rotation)
        {
            _productIndex = _catalog.IndexOf(product);
            _units = units;
            _restPosition = position;
            _restRotation = rotation;
            ApplyRest();
        }

        [Server]
        public bool ServerTakeUnit()
        {
            if (_units <= 0)
                return false;

            _units--;
            return true;
        }

        public string GetInteractionName() =>
            !IsCarried && Product != null ? $"Pick up {Product.DisplayName} x{_units}" : string.Empty;

        public void Interact(InteractionsManager source)
        {
            if (!IsCarried && !_carry.IsLocalPlayerCarrying)
                CmdPickUp();
        }

        public void RequestDrop() => CmdDrop();

        [Command(requiresAuthority = false)]
        private void CmdPickUp(NetworkConnectionToClient sender = null)
        {
            if (IsCarried || sender?.identity == null || _carry.IsCarrying(sender.identity.netId))
                return;

            _carrierNetId = sender.identity.netId;
        }

        [Command(requiresAuthority = false)]
        private void CmdDrop(NetworkConnectionToClient sender = null)
        {
            if (sender?.identity == null || sender.identity.netId != _carrierNetId)
                return;

            var (position, rotation) = _carry.FindDropPose(this);
            _carrierNetId = 0;
            _restPosition = position;
            _restRotation = rotation;
            ApplyRest();
        }

        private void OnContentSynced(int _, int __) => RefreshLabel();

        private void OnCarrierSynced(uint _, uint __) => RefreshCarried();

        private void OnRestPositionSynced(Vector3 _, Vector3 __) => ApplyRest();

        private void OnRestRotationSynced(Quaternion _, Quaternion __) => ApplyRest();

        private void RefreshCarried() => _collider.enabled = !IsCarried;

        private void ApplyRest()
        {
            if (!IsCarried)
                transform.SetPositionAndRotation(_restPosition, _restRotation);
        }

        private void RefreshLabel()
        {
            var product = Product;
            _label.text = product != null ? $"{product.DisplayName}\nx{_units}" : string.Empty;
        }
    }
}
