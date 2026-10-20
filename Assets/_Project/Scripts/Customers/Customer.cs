using System;
using Akila.FPSFramework;
using Mirror;
using System.Collections.Generic;
using ShooterShop.Catalog;
using ShooterShop.Range;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;
using UnityEngine.AI;
using VContainer;

namespace ShooterShop.Customers
{
    public sealed class Customer : NetworkBehaviour, IInteractable, IRangeShooter
    {
        [Required]
        [SerializeField] private NavMeshAgent _agent;

        [Required]
        [SerializeField] private TMP_Text _label;

        [BoxGroup("Animation"), Required]
        [SerializeField] private Animator _animator;

        [BoxGroup("Animation"), MinValue(1)]
        [SerializeField] private float _animationSmoothing = 10f;

        [BoxGroup("Range"), ChildGameObjectsOnly]
        [SerializeField] private List<GameObject> _weaponParts = new();

        [BoxGroup("Range")]
        [SerializeField] private string _weaponLayer = "Pistol";

        [BoxGroup("Range")]
        [SerializeField] private string _fireState = "Fire";

        [SyncVar(hook = nameof(OnRequestSynced))]
        private int _productIndex = -1;

        [SyncVar(hook = nameof(OnRequestSynced))]
        private int _amount;

        [SyncVar(hook = nameof(OnRequestSynced))]
        private int _laneFee;

        [SyncVar(hook = nameof(OnStateSynced))]
        private CustomerState _state;

        private ProductCatalog _catalog;
        private ICustomerInteraction _interaction;
        private CustomerRegistry _registry;
        private Vector3 _lastPosition;
        private Vector2 _move;

        private static readonly int MoveX = Animator.StringToHash("MoveX");
        private static readonly int MoveY = Animator.StringToHash("MoveY");
        private static readonly int IsGrounded = Animator.StringToHash("IsGrounded");

        public event Action<Customer> StateChanged;

        public CustomerState State => _state;
        public NavMeshAgent Agent => _agent;
        public float WaitingSince { get; private set; }
        public int CounterSlot { get; set; } = -1;

        public bool HasRequest => _catalog != null && _catalog.TryGet(_productIndex, out _);

        public CustomerRequest Request =>
            _catalog.TryGet(_productIndex, out var product) ? new CustomerRequest(product, _amount, _laneFee) : default;

        public bool requireHoldToInteract => false;
        public bool isHighlighted { get; set; }

        [Inject]
        public void Construct(ProductCatalog catalog, ICustomerInteraction interaction, CustomerRegistry registry)
        {
            _catalog = catalog;
            _interaction = interaction;
            _registry = registry;
        }

        public override void OnStartClient()
        {
            _agent.enabled = isServer;
            _lastPosition = transform.position;
            _animator.SetBool(IsGrounded, true);

            for (var layer = 1; layer < _animator.layerCount; layer++)
                _animator.SetLayerWeight(layer, 0f);

            _registry.Add(this);
            RefreshWeapon();
            RefreshLabel();
        }

        public override void OnStopClient() => _registry.Remove(this);

        public void TickAnimation(float deltaTime)
        {
            if (deltaTime <= 0f)
                return;

            var velocity = (transform.position - _lastPosition) / deltaTime;
            _lastPosition = transform.position;

            var local = transform.InverseTransformDirection(velocity);
            _move = Vector2.Lerp(_move, new Vector2(local.x, local.z), deltaTime * _animationSmoothing);
            _animator.SetFloat(MoveX, _move.x);
            _animator.SetFloat(MoveY, _move.y);
        }

        [Server]
        public void ServerInitialize(CustomerRequest request, float speed)
        {
            _productIndex = _catalog.IndexOf(request.Product);
            _amount = request.Amount;
            _laneFee = request.LaneFee;
            _agent.speed = speed;
            ServerSetState(CustomerState.Arriving);
        }

        [Server]
        public void ServerSetState(CustomerState state)
        {
            _state = state;

            if (state == CustomerState.Waiting)
                WaitingSince = Time.time;

            RefreshLabel();
            RefreshWeapon();
            StateChanged?.Invoke(this);
        }

        [Server]
        public void ServerFire() => RpcFire();

        [Server]
        public void ServerMoveTo(Vector3 destination) => _agent.SetDestination(destination);

        [Server]
        public void ServerFace(Quaternion rotation) => transform.rotation = rotation;

        [Server]
        public bool ServerHasArrived() =>
            !_agent.pathPending && _agent.remainingDistance <= _agent.stoppingDistance + 0.05f;

        public string GetInteractionName() =>
            _state == CustomerState.Waiting && HasRequest ? "Serve customer" : string.Empty;

        public void Interact(InteractionsManager source)
        {
            if (_state == CustomerState.Waiting && HasRequest)
                _interaction.Open(this);
        }

        private void OnRequestSynced(int _, int __) => RefreshLabel();

        private void OnStateSynced(CustomerState _, CustomerState __)
        {
            RefreshLabel();
            RefreshWeapon();

            if (!isServer)
                StateChanged?.Invoke(this);
        }

        [ClientRpc]
        private void RpcFire()
        {
            var layer = _animator.GetLayerIndex(_weaponLayer);

            if (layer >= 0)
                _animator.Play(_fireState, layer, 0f);
        }

        private void RefreshWeapon()
        {
            var isShooting = _state == CustomerState.Shooting;
            var layer = _animator.GetLayerIndex(_weaponLayer);

            foreach (var part in _weaponParts)
                part.SetActive(isShooting);

            if (layer >= 0)
                _animator.SetLayerWeight(layer, isShooting ? 1f : 0f);
        }

        private void RefreshLabel()
        {
            if (_catalog == null)
                return;

            _label.text = _state switch
            {
                CustomerState.Waiting when HasRequest => Request.Describe(),
                CustomerState.Arriving => "...",
                _ => string.Empty
            };
        }
    }
}
