using Akila.FPSFramework;
using Mirror;
using ShooterShop.Catalog;
using ShooterShop.Feedback;
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

        [Required]
        [SerializeField] private InteractableOutline _outline;

        [SyncVar(hook = nameof(OnContentSynced))]
        private int _productIndex = -1;

        [SyncVar(hook = nameof(OnContentSynced))]
        private int _units;

        [SyncVar(hook = nameof(OnCarrierSynced))]
        private uint _carrierNetId;

        [SyncVar]
        private bool _wasThrown;

        [SyncVar]
        private Vector3 _throwOrigin;

        [SyncVar]
        private float _throwDuration;

        [SyncVar(hook = nameof(OnRestPositionSynced))]
        private Vector3 _restPosition;

        [SyncVar(hook = nameof(OnRestRotationSynced))]
        private Quaternion _restRotation;

        private ProductCatalog _catalog;
        private CarryService _carry;
        private ItemAnimator _animator;
        private bool _isHighlighted;

        public uint CarrierNetId => _carrierNetId;
        public bool IsCarried => _carrierNetId != 0;
        public int Units => _units;
        public ProductConfig Product => _catalog != null && _catalog.TryGet(_productIndex, out var product) ? product : null;

        public bool requireHoldToInteract => false;
        public bool isHighlighted
        {
            get => _isHighlighted;
            set
            {
                _isHighlighted = value;
                _outline.SetVisible(value && !IsCarried);
            }
        }

        [Inject]
        public void Construct(ProductCatalog catalog, CarryService carry, ItemAnimator animator)
        {
            _catalog = catalog;
            _carry = carry;
            _animator = animator;
        }

        public override void OnStartClient()
        {
            _carry.Register(this);
            ApplyRest();
            RefreshCarried();
            RefreshLabel();
            _animator.PopIn(transform, transform.localScale);
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

        public void RequestThrow(Vector3 direction) => CmdThrow(direction);

        [Command(requiresAuthority = false)]
        private void CmdThrow(Vector3 direction, NetworkConnectionToClient sender = null)
        {
            if (sender?.identity == null || sender.identity.netId != _carrierNetId)
                return;

            var (position, rotation, duration) = _carry.FindThrowPose(this, direction);
            _wasThrown = true;
            _throwOrigin = transform.position;
            _throwDuration = duration;
            _carrierNetId = 0;
            _restRotation = rotation;
            _restPosition = position;
        }

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
            _wasThrown = false;
            _carrierNetId = 0;
            _restRotation = rotation;
            _restPosition = position;
        }

        private void OnContentSynced(int _, int __) => RefreshLabel();

        private void OnCarrierSynced(uint _, uint __)
        {
            RefreshCarried();

            if (IsCarried)
                _animator.Punch(transform);
        }

        private void OnRestPositionSynced(Vector3 _, Vector3 position)
        {
            if (IsCarried)
                return;

            if (_wasThrown)
                _animator.Throw(transform, _throwOrigin, position, _throwDuration);
            else
                _animator.Land(transform, transform.position, position);
        }

        private void OnRestRotationSynced(Quaternion _, Quaternion rotation)
        {
            if (!IsCarried)
                transform.rotation = rotation;
        }

        private void RefreshCarried()
        {
            _collider.enabled = !IsCarried;

            if (!IsCarried)
                _carry.Detach(this);

            if (IsCarried)
                _outline.SetVisible(false);
        }

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
