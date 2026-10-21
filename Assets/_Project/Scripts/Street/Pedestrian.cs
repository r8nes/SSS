using System;
using Akila.FPSFramework;
using Cysharp.Threading.Tasks;
using Mirror;
using ShooterShop.Customers;
using ShooterShop.Feedback;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.AI;
using VContainer;
using Random = UnityEngine.Random;

namespace ShooterShop.Street
{
    public sealed class Pedestrian : NetworkBehaviour, IInteractable, IOnHitInChildren
    {
        [Required]
        [SerializeField] private NavMeshAgent _agent;

        [Required]
        [SerializeField] private InteractableOutline _outline;

        [SyncVar]
        private int _appearanceIndex = -1;

        [SyncVar]
        private float _walkSpeed = 1f;

        private CustomerAppearanceCatalog _appearances;
        private PedestrianConfig _config;
        private IScreenLabels _labels;
        private FeedbackConfig _feedback;
        private PedestrianRegistry _registry;
        private IPedestrianDamage _damage;
        private int _hitsTaken;
        private AppearanceBody _body;
        private ScreenLabel _reply;
        private float _replyHideTime;
        private bool _isHighlighted;

        public float PausedUntil { get; private set; }
        public bool IsDead { get; private set; }
        public NavMeshAgent Agent => _agent;
        public bool requireHoldToInteract => false;
        public bool isHighlighted
        {
            get => _isHighlighted;
            set
            {
                _isHighlighted = value;
                _outline.SetVisible(value);
            }
        }

        [Inject]
        public void Construct(CustomerAppearanceCatalog appearances, PedestrianConfig config, IScreenLabels labels, FeedbackConfig feedback, PedestrianRegistry registry, IPedestrianDamage damage)
        {
            _damage = damage;
            _appearances = appearances;
            _config = config;
            _labels = labels;
            _feedback = feedback;
            _registry = registry;
        }

        [Server]
        public void ServerInitialize(float walkSpeed)
        {
            _appearanceIndex = _appearances.PickIndex();
            _walkSpeed = walkSpeed;
            _agent.speed = walkSpeed;
        }

        public override void OnStartClient()
        {
            _agent.enabled = isServer;
            _body = AppearanceBody.Create(_appearances, _appearanceIndex, transform);

            if (_body != null)
                _outline.SetRenderers(_body.Appearance.Renderers);

            _reply = _labels.Create(transform, _config.ReplyOffset, _feedback.CustomerStyle);
            _reply.IsVisible = false;
            _registry.Add(this);
        }

        public override void OnStopClient()
        {
            _registry.Remove(this);
            _labels.Release(_reply);
            _reply = null;
        }

        public void ClientTick(float deltaTime)
        {
            _body?.TickLocomotion(deltaTime, _config.WalkBlendValue, _config.AnimationSmoothing);

            if (_reply != null && _reply.IsVisible && Time.time >= _replyHideTime)
                _reply.IsVisible = false;
        }

        [Server]
        public void ServerMoveTo(Vector3 destination) => _agent.SetDestination(destination);

        [Server]
        public bool ServerHasArrived() =>
            _agent.isOnNavMesh && !_agent.pathPending && _agent.remainingDistance <= _agent.stoppingDistance + 0.1f;

        [Server]
        public int ServerRegisterHit() => ++_hitsTaken;

        [Server]
        public void ServerPanic(bool isPanicking)
        {
            _agent.speed = isPanicking ? _walkSpeed * _config.PanicSpeedMultiplier : _walkSpeed;
            PausedUntil = 0f;

            if (_agent.isOnNavMesh)
                _agent.isStopped = false;

            if (isPanicking && _config.PanicReplies.Count > 0 && Random.value < 0.6f)
                RpcShout(Random.Range(0, _config.PanicReplies.Count));
        }

        [Server]
        public void ServerDie(Vector3 impulse, Vector3 hitPoint)
        {
            IsDead = true;
            _agent.enabled = false;
            RpcDie(impulse, hitPoint);
        }

        public void OnHitInChildren(HitInfo hitInfo)
        {
            var source = hitInfo.sourcePlayer;

            if (IsDead || source == null || NetworkClient.localPlayer == null || source.transform.root != NetworkClient.localPlayer.transform)
                return;

            if (isServer)
                _damage.ServerHit(this, hitInfo.hitDirection, hitInfo.hitPoint);
            else
                CmdHit(hitInfo.hitDirection, hitInfo.hitPoint);
        }

        [Command(requiresAuthority = false)]
        private void CmdHit(Vector3 direction, Vector3 point) => _damage.ServerHit(this, direction, point);

        [ClientRpc]
        private void RpcDie(Vector3 impulse, Vector3 hitPoint)
        {
            IsDead = true;

            if (_reply != null)
                _reply.IsVisible = false;

            if (_body == null)
                return;

            _body.Ragdoll.Activate(impulse, hitPoint);
            SinkLaterAsync().Forget();
        }

        [ClientRpc]
        private void RpcShout(int replyIndex) => ShowReply(_config.PanicReplies, replyIndex);

        private async UniTaskVoid SinkLaterAsync()
        {
            var token = this.GetCancellationTokenOnDestroy();

            if (await UniTask.Delay(TimeSpan.FromSeconds(_config.CorpseSinkDelay), cancellationToken: token).SuppressCancellationThrow())
                return;

            _body.Ragdoll.Sink(_config.CorpseSinkDepth, _config.CorpseSinkDuration);
        }

        public string GetInteractionName() => IsDead ? string.Empty : _config.InteractionName;

        public void Interact(InteractionsManager source) => CmdTalk(source != null ? source.transform.position : transform.position);

        [Command(requiresAuthority = false)]
        private void CmdTalk(Vector3 speakerPosition)
        {
            if (_config.Replies.Count == 0 || IsDead)
                return;

            PausedUntil = Time.time + _config.TalkPause;

            if (_agent.isOnNavMesh)
                _agent.isStopped = true;

            var look = speakerPosition - transform.position;
            look.y = 0f;

            if (look.sqrMagnitude > 0.01f)
                transform.rotation = Quaternion.LookRotation(look);

            RpcReply(Random.Range(0, _config.Replies.Count));
            ResumeLaterAsync().Forget();
        }

        [ClientRpc]
        private void RpcReply(int replyIndex) => ShowReply(_config.Replies, replyIndex);

        private void ShowReply(System.Collections.Generic.IReadOnlyList<string> replies, int replyIndex)
        {
            if (_reply == null || IsDead || replyIndex < 0 || replyIndex >= replies.Count)
                return;

            _reply.SetText(replies[replyIndex]);
            _reply.IsVisible = true;
            _replyHideTime = Time.time + _config.ReplyDuration;
        }

        private async UniTaskVoid ResumeLaterAsync()
        {
            var token = this.GetCancellationTokenOnDestroy();

            if (await UniTask.Delay(TimeSpan.FromSeconds(_config.TalkPause), cancellationToken: token).SuppressCancellationThrow())
                return;

            if (Time.time >= PausedUntil && _agent.isOnNavMesh)
                _agent.isStopped = false;
        }
    }
}
