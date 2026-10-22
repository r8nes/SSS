using System;
using Akila.FPSFramework;
using Cysharp.Threading.Tasks;
using Mirror;
using ShooterShop.Customers;
using ShooterShop.Feedback;
using ShooterShop.Rentals;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.AI;
using VContainer;

namespace ShooterShop.Raids
{
    public sealed class Raider : NetworkBehaviour, IOnHitInChildren
    {
        [Required]
        [SerializeField] private NavMeshAgent _agent;

        [SyncVar]
        private int _appearanceIndex = -1;

        [SyncVar(hook = nameof(OnWeaponSynced))]
        private int _weapon = -1;

        [SyncVar]
        private Vector3 _aimPoint;

        [SyncVar(hook = nameof(OnAimingSynced))]
        private bool _isAiming;

        [SyncVar]
        private bool _isDead;

        [SyncVar]
        private bool _isMarked;

        private CustomerAppearanceCatalog _appearances;
        private RentalWeaponCatalog _weapons;
        private ShotEffects _shotEffects;
        private RaidConfig _config;
        private RaiderRegistry _registry;
        private IRaidDamage _damage;
        private IScreenLabels _labels;
        private ScreenLabel _marker;
        private AppearanceBody _body;
        private GameObject _handWeapon;
        private int _hitsTaken;

        public RaidRole Role { get; private set; }
        public bool IsDead => _isDead;
        public bool IsArmed => _weapon >= 0;
        public float NextShotTime { get; set; }
        public float BusyUntil { get; set; }
        public bool IsAmbusher { get; private set; }
        public bool IsDashing { get; set; }
        public Vector3 GuardPoint { get; private set; }

        [Inject]
        public void Construct(CustomerAppearanceCatalog appearances, RentalWeaponCatalog weapons, ShotEffects shotEffects, RaidConfig config, RaiderRegistry registry, IRaidDamage damage, IScreenLabels labels)
        {
            _labels = labels;
            _appearances = appearances;
            _weapons = weapons;
            _shotEffects = shotEffects;
            _config = config;
            _registry = registry;
            _damage = damage;
        }

        [Server]
        public void ServerInitialize(RaidRole role, int appearanceIndex, int weapon)
        {
            Role = role;
            _appearanceIndex = appearanceIndex;
            _weapon = weapon;
            _agent.speed = _config.RunSpeed;
        }

        [Server]
        public void ServerMakeAmbusher(Vector3 guardPoint)
        {
            IsAmbusher = true;
            GuardPoint = guardPoint;
            _isMarked = true;
        }

        public override void OnStartClient()
        {
            _agent.enabled = isServer;
            _body = AppearanceBody.Create(_appearances, _appearanceIndex, transform);
            _registry.Add(this);
            RefreshWeapon();

            if (_isMarked && !_isDead)
            {
                _marker = _labels.Create(transform, _config.MarkerOffset, _config.MarkerStyle);
                _marker.SetText(_config.MarkerText);
            }
        }

        public override void OnStopClient()
        {
            _registry.Remove(this);
            ReleaseMarker();
        }

        private void ReleaseMarker()
        {
            if (_marker == null)
                return;

            _labels.Release(_marker);
            _marker = null;
        }

        public void ClientTick(float deltaTime) =>
            _body?.TickLocomotion(deltaTime, _config.WalkBlendValue, _config.AnimationSmoothing);

        public void ClientPostAnimation()
        {
            if (_body != null && _handWeapon != null && _weapons.TryGet(_weapon, out var weapon))
                _body.AlignWeapon(_handWeapon.transform, weapon, _isAiming ? _aimPoint : null);
        }

        [Server]
        public void ServerWarp(Vector3 position)
        {
            _agent.Warp(position);
            transform.position = position + Vector3.up * _agent.baseOffset;
        }

        [Server]
        public void ServerMoveTo(Vector3 destination)
        {
            if (!_agent.isOnNavMesh)
                return;

            _agent.isStopped = false;
            _agent.SetDestination(destination);
        }

        [Server]
        public void ServerHold(bool isHolding)
        {
            if (_agent.isOnNavMesh)
                _agent.isStopped = isHolding;
        }

        [Server]
        public bool ServerHasArrived() =>
            _agent.isOnNavMesh && !_agent.pathPending && _agent.remainingDistance <= _agent.stoppingDistance + 0.3f;

        [Server]
        public void ServerAimAt(Vector3 point, bool faceTarget)
        {
            _aimPoint = point;
            _isAiming = true;

            if (!faceTarget)
                return;

            var look = point - transform.position;
            look.y = 0f;

            if (look.sqrMagnitude > 0.01f)
                transform.rotation = Quaternion.LookRotation(look);
        }

        [Server]
        public void ServerStopAiming() => _isAiming = false;

        [Server]
        public void ServerFire(Vector3 target) => RpcFire(target);

        [Server]
        public int ServerRegisterHit() => ++_hitsTaken;

        [Server]
        public void ServerDie(Vector3 impulse, Vector3 hitPoint)
        {
            _isDead = true;
            _isAiming = false;
            _agent.enabled = false;
            RpcDie(impulse, hitPoint);
        }

        public void OnHitInChildren(HitInfo hitInfo)
        {
            var source = hitInfo.sourcePlayer;

            if (_isDead || source == null || NetworkClient.localPlayer == null || source.transform.root != NetworkClient.localPlayer.transform)
                return;

            if (isServer)
                _damage.ServerHit(this, hitInfo.hitDirection, hitInfo.hitPoint);
            else
                CmdHit(hitInfo.hitDirection, hitInfo.hitPoint);
        }

        [Command(requiresAuthority = false)]
        private void CmdHit(Vector3 direction, Vector3 point) => _damage.ServerHit(this, direction, point);

        [ClientRpc]
        private void RpcFire(Vector3 target)
        {
            if (_body == null || _handWeapon == null || !_weapons.TryGet(_weapon, out var weapon))
                return;

            _body.PlayFire(weapon.Pose);

            var muzzle = _handWeapon.transform.position + _handWeapon.transform.forward * weapon.MuzzleDistance;
            _shotEffects.Play(muzzle, target, weapon.FireSound);
        }

        [ClientRpc]
        private void RpcDie(Vector3 impulse, Vector3 hitPoint)
        {
            if (_handWeapon != null)
                Destroy(_handWeapon);

            _handWeapon = null;
            ReleaseMarker();

            if (_body == null)
                return;

            _body.Ragdoll.Activate(impulse, hitPoint);
            SinkLaterAsync().Forget();
        }

        private async UniTaskVoid SinkLaterAsync()
        {
            var token = this.GetCancellationTokenOnDestroy();

            if (await UniTask.Delay(TimeSpan.FromSeconds(_config.CorpseSinkDelay), cancellationToken: token).SuppressCancellationThrow())
                return;

            _body.Ragdoll.Sink(_config.CorpseSinkDepth, _config.CorpseSinkDuration);
        }

        private void OnWeaponSynced(int _, int __) => RefreshWeapon();

        private void OnAimingSynced(bool _, bool __) => RefreshPose();

        private void RefreshWeapon()
        {
            if (_body == null || _isDead)
                return;

            if (_handWeapon != null)
                Destroy(_handWeapon);

            _handWeapon = _weapons.TryGet(_weapon, out var weapon) ? Instantiate(weapon.DisplayModel, _body.Appearance.RightHand, false) : null;
            RefreshPose();
        }

        private void RefreshPose()
        {
            if (_body == null)
                return;

            var pose = _weapons.TryGet(_weapon, out var weapon) ? weapon.Pose : RentalWeaponPose.Pistol;
            _body.PlayWeaponPose(pose, _isAiming && _handWeapon != null);
        }
    }
}
