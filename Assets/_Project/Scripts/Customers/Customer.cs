using System;
using Akila.FPSFramework;
using Mirror;
using PrimeTween;
using System.Collections.Generic;
using ShooterShop.Catalog;
using ShooterShop.Feedback;
using ShooterShop.Range;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;
using UnityEngine.AI;
using VContainer;

namespace ShooterShop.Customers
{
    public sealed class Customer : NetworkBehaviour, IInteractable, IRangeShooter, IOnHitInChildren
    {
        [Required]
        [SerializeField] private NavMeshAgent _agent;

        [Required]
        [SerializeField] private TMP_Text _label;

        [Required]
        [SerializeField] private InteractableOutline _outline;

        [BoxGroup("Animation"), Required]
        [SerializeField] private Animator _animator;

        [BoxGroup("Animation"), MinValue(1)]
        [SerializeField] private float _animationSmoothing = 10f;

        [BoxGroup("Animation"), MinValue(0.1f)]
        [InfoBox("Blend tree MoveY value of the walk clip")]
        [SerializeField] private float _walkBlendValue = 5f;

        [BoxGroup("Navigation"), MinMaxSlider(0, 99, true)]
        [InfoBox("Lower is more important: walking customers push through, standing ones step aside")]
        [SerializeField] private Vector2Int _movingPriority = new(30, 60);

        [BoxGroup("Navigation"), Range(0, 99)]
        [SerializeField] private int _standingPriority = 90;

        [BoxGroup("Patience"), Required, ChildGameObjectsOnly]
        [SerializeField] private GameObject _patienceBar;

        [BoxGroup("Patience"), Required, ChildGameObjectsOnly]
        [SerializeField] private Transform _patienceFill;

        [BoxGroup("Patience"), Required, ChildGameObjectsOnly]
        [SerializeField] private Renderer _patienceFillRenderer;

        [BoxGroup("Patience")]
        [SerializeField] private Gradient _patienceColors = new();

        [BoxGroup("Request"), Required, ChildGameObjectsOnly]
        [SerializeField] private SpriteRenderer _requestIcon;

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

        [SyncVar]
        private int _queuePosition = -1;

        [SyncVar]
        private float _walkSpeed = 1f;

        [SyncVar(hook = nameof(OnRequestSynced))]
        private int _markupPercent;

        [SyncVar(hook = nameof(OnRentalSynced))]
        private int _rentalWeapon = -1;

        [SyncVar]
        private double _patienceStartedAt;

        [SyncVar]
        private float _patienceDuration;

        private ProductCatalog _catalog;
        private ICustomerInteraction _interaction;
        private ICustomerDamage _damage;
        private int _hitsTaken;
        private CustomerRegistry _registry;
        private IScreenLabels _screenLabels;
        private IViewCamera _viewCamera;
        private FeedbackConfig _feedback;
        private ScreenLabel _screenLabel;
        private Vector3 _lastPosition;
        private Vector2 _move;
        private Quaternion? _arrivalRotation;
        private bool _isHighlighted;
        private MaterialPropertyBlock _patienceBlock;

        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");

        private static readonly int MoveX = Animator.StringToHash("MoveX");
        private static readonly int MoveY = Animator.StringToHash("MoveY");
        private static readonly int IsGrounded = Animator.StringToHash("IsGrounded");

        public event Action<Customer> StateChanged;

        public CustomerState State => _state;
        public NavMeshAgent Agent => _agent;
        public float WaitingSince { get; private set; }
        public float PatienceLeft => _patienceDuration <= 0f ? float.MaxValue : _patienceDuration - (float)(NetworkTime.time - _patienceStartedAt);
        public int QueuePosition => _queuePosition;
        public bool IsInQueue => _queuePosition >= 0;
        public int RentalWeapon => _rentalWeapon;
        public bool CanBeServed => _state == CustomerState.Waiting && _queuePosition == 0 && HasRequest;

        public bool HasRequest => _catalog != null && _catalog.TryGet(_productIndex, out _);

        public CustomerRequest Request =>
            _catalog.TryGet(_productIndex, out var product) ? new CustomerRequest(product, _amount, _laneFee, _markupPercent) : default;

        public bool requireHoldToInteract => false;
        public bool isHighlighted
        {
            get => _isHighlighted;
            set
            {
                _isHighlighted = value;
                _outline.SetVisible(value && CanBeServed);
            }
        }

        [Inject]
        public void Construct(ProductCatalog catalog, ICustomerInteraction interaction, CustomerRegistry registry, IScreenLabels screenLabels, IViewCamera viewCamera, FeedbackConfig feedback, ICustomerDamage damage)
        {
            _damage = damage;
            _screenLabels = screenLabels;
            _viewCamera = viewCamera;
            _feedback = feedback;
            _catalog = catalog;
            _interaction = interaction;
            _registry = registry;
        }

        public override void OnStartClient()
        {
            _screenLabel = _screenLabels.Create(transform, _label.transform.localPosition, _feedback.CustomerStyle);
            _label.gameObject.SetActive(false);
            _agent.enabled = isServer;
            _lastPosition = transform.position;
            _animator.SetBool(IsGrounded, true);

            for (var layer = 1; layer < _animator.layerCount; layer++)
                _animator.SetLayerWeight(layer, 0f);

            _registry.Add(this);
            RefreshWeapon();
            RefreshLabel();
            RefreshPatience();
        }

        public override void OnStopClient()
        {
            _registry.Remove(this);
            _screenLabels.Release(_screenLabel);
            _screenLabel = null;
        }

        public void ClientTick(float deltaTime)
        {
            if (deltaTime <= 0f)
                return;

            TickPatience();

            var velocity = (transform.position - _lastPosition) / deltaTime;
            _lastPosition = transform.position;

            var local = transform.InverseTransformDirection(velocity) * (_walkBlendValue / Mathf.Max(0.1f, _walkSpeed));
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
            _walkSpeed = speed;
            ServerSetState(CustomerState.Arriving);
        }

        [Server]
        public void ServerInitializeCourier(float speed)
        {
            _productIndex = -1;
            _agent.speed = speed;
            _walkSpeed = speed;
            ServerSetState(CustomerState.Delivering);
        }

        [Server]
        public void ServerSetState(CustomerState state)
        {
            _state = state;

            if (state == CustomerState.Waiting)
                WaitingSince = Time.time;

            _patienceDuration = 0f;
            RefreshPatience();
            RefreshLabel();
            RefreshWeapon();
            StateChanged?.Invoke(this);
        }

        [Server]
        public void ServerFire() => RpcFire();

        [Server]
        public void ServerSetQueuePosition(int position) => _queuePosition = position;

        [Server]
        public void ServerSetAmount(int amount) => _amount = amount;

        [Server]
        public void ServerSetMarkup(int markupPercent) => _markupPercent = markupPercent;

        [Server]
        public int ServerRegisterHit() => ++_hitsTaken;

        [Server]
        public void ServerSetSpeedMultiplier(float multiplier) => _agent.speed *= multiplier;

        [Server]
        public void ServerDie(float fallDuration, float sinkDelay, float sinkDuration, float sinkDepth)
        {
            ServerSetState(CustomerState.Dead);
            _agent.enabled = false;

            var fallen = transform.rotation * Quaternion.Euler(-85f, 0f, 0f);
            var position = transform.position;
            Tween.Rotation(transform, fallen, fallDuration, Ease.OutBounce);
            Tween.Position(transform, position, position + Vector3.down * sinkDepth, sinkDuration, Ease.InSine, startDelay: fallDuration + sinkDelay);
        }

        public void OnHitInChildren(HitInfo hitInfo)
        {
            var source = hitInfo.sourcePlayer;

            if (_state == CustomerState.Dead || source == null || NetworkClient.localPlayer == null || source.transform.root != NetworkClient.localPlayer.transform)
                return;

            if (isServer)
                _damage.ServerHit(this);
            else
                CmdHit();
        }

        [Command(requiresAuthority = false)]
        private void CmdHit() => _damage.ServerHit(this);

        [Server]
        public void ServerSetRentalWeapon(int weapon)
        {
            _rentalWeapon = weapon;
            RefreshWeapon();
        }

        [Server]
        public void ServerStartPatience(float duration)
        {
            _patienceStartedAt = NetworkTime.time;
            _patienceDuration = duration;
            RefreshPatience();
        }

        [Server]
        public void ServerMoveTo(Vector3 destination, Quaternion? arrivalRotation = null)
        {
            _arrivalRotation = arrivalRotation;
            _agent.avoidancePriority = UnityEngine.Random.Range(_movingPriority.x, _movingPriority.y + 1);
            _agent.SetDestination(destination);
        }

        [Server]
        public bool ServerTickArrival()
        {
            if (!ServerHasArrived())
                return false;

            _agent.avoidancePriority = _standingPriority;

            if (_arrivalRotation.HasValue)
            {
                transform.rotation = _arrivalRotation.Value;
                _arrivalRotation = null;
            }

            return true;
        }

        [Server]
        public bool ServerHasArrived() =>
            !_agent.pathPending && _agent.remainingDistance <= _agent.stoppingDistance + 0.05f;

        public string GetInteractionName() =>
            CanBeServed ? "Serve customer" : string.Empty;

        public void Interact(InteractionsManager source)
        {
            if (CanBeServed)
                _interaction.Open(this);
        }

        private void OnRequestSynced(int _, int __) => RefreshLabel();

        private void OnRentalSynced(int _, int __) => RefreshWeapon();

        private void OnStateSynced(CustomerState _, CustomerState __)
        {
            RefreshPatience();
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
            var isHoldingRental = _rentalWeapon >= 0 && _state is CustomerState.GoingToLane or CustomerState.Shooting or CustomerState.ReturningWeapon;
            var isShooting = isHoldingRental && _state == CustomerState.Shooting;
            var layer = _animator.GetLayerIndex(_weaponLayer);

            foreach (var part in _weaponParts)
                part.SetActive(isHoldingRental);

            if (layer >= 0)
                _animator.SetLayerWeight(layer, isShooting ? 1f : 0f);
        }

        private void RefreshPatience() =>
            _patienceBar.SetActive(_state is CustomerState.Waiting or CustomerState.Searching or CustomerState.WaitingForWeapon);

        private void TickPatience()
        {
            if (!_patienceBar.activeSelf)
                return;

            var camera = _viewCamera.Current;

            if (camera != null)
                _patienceBar.transform.rotation = camera.transform.rotation;

            var share = _patienceDuration > 0f ? Mathf.Clamp01(PatienceLeft / _patienceDuration) : 1f;
            var scale = _patienceFill.localScale;
            scale.x = share;
            _patienceFill.localScale = scale;
            _patienceFill.localPosition = new Vector3((share - 1f) * 0.5f, 0f, 0f);

            _patienceBlock ??= new MaterialPropertyBlock();
            _patienceBlock.SetColor(BaseColor, _patienceColors.Evaluate(share));
            _patienceFillRenderer.SetPropertyBlock(_patienceBlock);
        }

        private void RefreshLabel()
        {
            if (_catalog == null)
                return;

            var isSearching = _state == CustomerState.Searching && HasRequest;
            _requestIcon.gameObject.SetActive(isSearching && Request.Product.Icon != null);

            if (isSearching)
                _requestIcon.sprite = Request.Product.Icon;

            _screenLabel?.SetText(_state switch
            {
                CustomerState.Searching when HasRequest => $"Needs {Request.Product.DisplayName}!",
                CustomerState.Delivering => "Delivery",
                CustomerState.GoingToHandout or CustomerState.WaitingForWeapon => "Waiting for a rental gun",
                CustomerState.ReturningWeapon => "Returning the gun",
                CustomerState.Waiting when HasRequest => $"{Request.Describe()}  ${Request.Price:N0}",
                CustomerState.Browsing when HasRequest => $"Looking for {Request.Product.DisplayName}",
                CustomerState.Arriving when HasRequest => Request.Describe(),
                _ => string.Empty
            });
        }
    }
}
