using System;
using Akila.FPSFramework;
using Mirror;
using Cysharp.Threading.Tasks;
using System.Collections.Generic;
using ShooterShop.Catalog;
using ShooterShop.Feedback;
using ShooterShop.Range;
using ShooterShop.Rentals;
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



        [SyncVar]
        private int _appearanceIndex = -1;

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
        private CustomerAppearanceCatalog _appearances;
        private Animator _animator;
        private CustomerRagdoll _ragdoll;
        private GameObject _handWeapon;
        private int _handWeaponIndex = -1;
        private RentalWeaponCatalog _rentalWeapons;
        private AppearanceBody _body;
        private int _hitsTaken;
        private CustomerRegistry _registry;
        private IScreenLabels _screenLabels;
        private IViewCamera _viewCamera;
        private FeedbackConfig _feedback;
        private ScreenLabel _screenLabel;
        private Quaternion? _arrivalRotation;
        private bool _isHighlighted;
        private MaterialPropertyBlock _patienceBlock;

        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");


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
        public void Construct(ProductCatalog catalog, ICustomerInteraction interaction, CustomerRegistry registry, IScreenLabels screenLabels, IViewCamera viewCamera, FeedbackConfig feedback, ICustomerDamage damage, CustomerAppearanceCatalog appearances, RentalWeaponCatalog rentalWeapons)
        {
            _rentalWeapons = rentalWeapons;
            _appearances = appearances;
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
            BuildAppearance();

            _registry.Add(this);
            RefreshWeapon();
            RefreshLabel();
            RefreshPatience();
        }

        public override void OnStopClient()
        {
            _registry.Remove(this);
            _body?.Dispose();
            _screenLabels.Release(_screenLabel);
            _screenLabel = null;
        }

        public void ClientTick(float deltaTime)
        {
            if (deltaTime <= 0f || _animator == null)
                return;

            TickPatience();
            _body.TickLocomotion(deltaTime, _walkBlendValue, _animationSmoothing);
        }

        [Server]
        public void ServerInitialize(CustomerRequest request, float speed)
        {
            _appearanceIndex = _appearances.PickIndex();
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
            _appearanceIndex = _appearances.PickIndex();
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
        public void ServerDie(Vector3 impulse, Vector3 hitPoint, float sinkDelay, float sinkDuration, float sinkDepth)
        {
            ServerSetState(CustomerState.Dead);
            _agent.enabled = false;
            RpcDie(impulse, hitPoint, sinkDelay, sinkDuration, sinkDepth);
        }

        public void OnHitInChildren(HitInfo hitInfo)
        {
            var source = hitInfo.sourcePlayer;

            if (_state == CustomerState.Dead || source == null || NetworkClient.localPlayer == null || source.transform.root != NetworkClient.localPlayer.transform)
                return;

            if (isServer)
                _damage.ServerHit(this, hitInfo.hitDirection, hitInfo.hitPoint);
            else
                CmdHit(hitInfo.hitDirection, hitInfo.hitPoint);
        }

        [Command(requiresAuthority = false)]
        private void CmdHit(Vector3 direction, Vector3 point) => _damage.ServerHit(this, direction, point);

        [ClientRpc]
        private void RpcDie(Vector3 impulse, Vector3 hitPoint, float sinkDelay, float sinkDuration, float sinkDepth)
        {
            if (_ragdoll != null)
                _ragdoll.Activate(impulse, hitPoint);
            SinkLaterAsync(sinkDelay, sinkDuration, sinkDepth).Forget();
        }

        private async UniTaskVoid SinkLaterAsync(float delay, float duration, float depth)
        {
            var token = this.GetCancellationTokenOnDestroy();

            if (await UniTask.Delay(TimeSpan.FromSeconds(delay), cancellationToken: token).SuppressCancellationThrow())
                return;

            if (_ragdoll != null)
                _ragdoll.Sink(depth, duration);
        }

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
        private void RpcFire() => _body?.Kick();

        public void ClientPostAnimation(float deltaTime)
        {
            if (_body != null && _state == CustomerState.Shooting && _rentalWeapon >= 0)
                _body.ApplyAimPose(deltaTime);
        }

        private void RefreshWeapon()
        {
            var isHoldingRental = _rentalWeapon >= 0 && _state is CustomerState.GoingToLane or CustomerState.Shooting or CustomerState.ReturningWeapon;
            if (_animator == null)
                return;

            RefreshHandWeapon(isHoldingRental ? _rentalWeapon : -1);
        }

        private void RefreshHandWeapon(int weapon)
        {
            if (weapon == _handWeaponIndex || _body == null)
                return;

            _handWeaponIndex = weapon;

            if (_handWeapon != null)
                Destroy(_handWeapon);

            _handWeapon = null;

            if (!_rentalWeapons.TryGet(weapon, out var config))
                return;

            _handWeapon = Instantiate(config.DisplayModel, _body.Appearance.RightHand, false);
            _handWeapon.transform.SetLocalPositionAndRotation(_appearances.HandWeaponPosition, _appearances.HandWeaponRotation);
        }

        private void BuildAppearance()
        {
            _body = AppearanceBody.Create(_appearances, _appearanceIndex, transform);

            if (_body == null)
                return;

            _animator = _body.Animator;
            _ragdoll = _body.Ragdoll;
            _outline.SetRenderers(_body.Appearance.Renderers);

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
