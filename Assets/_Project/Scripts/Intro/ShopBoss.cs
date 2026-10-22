using Akila.FPSFramework;
using Mirror;
using ShooterShop.Customers;
using ShooterShop.Feedback;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.AI;
using VContainer;

namespace ShooterShop.Intro
{
    public sealed class ShopBoss : NetworkBehaviour, IInteractable
    {
        [Required]
        [SerializeField] private NavMeshAgent _agent;

        [Required]
        [SerializeField] private InteractableOutline _outline;

        [Required]
        [SerializeField] private Collider _interactionCollider;

        [SyncVar(hook = nameof(OnLeavingSynced))]
        private bool _isLeaving;

        [SyncVar(hook = nameof(OnPresenceSynced))]
        private bool _isPresent = true;

        private IntroConfig _config;
        private CustomerAppearanceCatalog _appearances;
        private IBossDialog _dialog;
        private IIntroCommands _commands;
        private AppearanceBody _body;
        private bool _isHighlighted;

        public bool IsLeaving => _isLeaving;
        public bool requireHoldToInteract => false;
        public bool isHighlighted
        {
            get => _isHighlighted;
            set
            {
                _isHighlighted = value;
                _outline.SetVisible(value && !_isLeaving);
            }
        }

        [Inject]
        public void Construct(IntroConfig config, CustomerAppearanceCatalog appearances, IBossDialog dialog, IIntroCommands commands)
        {
            _config = config;
            _appearances = appearances;
            _dialog = dialog;
            _commands = commands;
        }

        public override void OnStartClient()
        {
            _agent.enabled = isServer;
            _agent.speed = _config.WalkSpeed;
            _body = AppearanceBody.Create(_appearances, _appearances.IndexOf(_config.BossAppearance), transform);

            if (_body != null)
                _outline.SetRenderers(_body.Appearance.Renderers);

            OnLeavingSynced(false, _isLeaving);
            OnPresenceSynced(true, _isPresent);
        }

        [Server]
        public void ServerAppear(Vector3 position, Quaternion rotation)
        {
            _isLeaving = false;
            _isPresent = true;
            _agent.enabled = true;
            _agent.Warp(position);
            transform.rotation = rotation;
            ApplyState();
        }

        [Server]
        public void ServerHide()
        {
            _isLeaving = false;
            _isPresent = false;
            ApplyState();
        }

        public void ClientTick(float deltaTime) =>
            _body?.TickLocomotion(deltaTime, _config.WalkBlendValue, _config.AnimationSmoothing);

        [Server]
        public void ServerLeave(Vector3 exit)
        {
            _isLeaving = true;
            ApplyState();

            if (_agent.isOnNavMesh)
                _agent.SetDestination(exit);
        }

        [Server]
        public bool ServerHasArrived() =>
            !_agent.isOnNavMesh || (!_agent.pathPending && _agent.remainingDistance <= _agent.stoppingDistance + 0.3f);

        public string GetInteractionName() => _isLeaving || !_isPresent ? string.Empty : _config.InteractionName;

        public void Interact(InteractionsManager source)
        {
            if (!_isLeaving && _isPresent)
                _dialog.Open(this);
        }

        public void Finish()
        {
            if (isServer)
                _commands.ServerFinish();
            else
                CmdFinish();
        }

        [Command(requiresAuthority = false)]
        private void CmdFinish() => _commands.ServerFinish();

        private void OnLeavingSynced(bool _, bool __) => ApplyState();

        private void OnPresenceSynced(bool _, bool __) => ApplyState();

        private void ApplyState()
        {
            _interactionCollider.enabled = _isPresent && !_isLeaving;

            if (_isLeaving || !_isPresent)
                _outline.SetVisible(false);

            if (_body != null)
                _body.Appearance.gameObject.SetActive(_isPresent);

            if (isServer && !_isPresent && _agent.enabled)
                _agent.enabled = false;
        }
    }
}
