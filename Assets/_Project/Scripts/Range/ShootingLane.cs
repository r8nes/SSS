using System;
using Akila.FPSFramework;
using Mirror;
using ShooterShop.Upgrades;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;

namespace ShooterShop.Range
{
    public sealed class ShootingLane : NetworkBehaviour
    {
        [BoxGroup("Layout"), MinValue(1)]
        [SerializeField] private int _number = 1;

        [BoxGroup("Layout"), AssetsOnly]
        [SerializeField] private UpgradeConfig _requiredUpgrade;

        [BoxGroup("Layout"), Required, ChildGameObjectsOnly]
        [SerializeField] private Transform _standPoint;

        [BoxGroup("Layout"), Required, ChildGameObjectsOnly]
        [SerializeField] private ShootingTarget _target;

        [BoxGroup("Display"), Required, ChildGameObjectsOnly]
        [SerializeField] private TMP_Text _display;

        [BoxGroup("Display"), Required, ChildGameObjectsOnly]
        [SerializeField] private Renderer _targetRenderer;

        [BoxGroup("Display")]
        [SerializeField] private Color _hitColor = new(1f, 0.85f, 0.2f);

        [BoxGroup("Display"), MinValue(0.05f), SuffixLabel("sec")]
        [SerializeField] private float _hitFlashDuration = 0.15f;

        [SyncVar]
        private bool _isOccupied;

        [SyncVar]
        private double _sessionEndTime;

        [SyncVar]
        private int _hits;

        [SyncVar]
        private double _challengeEndTime;

        [SyncVar]
        private int _record;

        private float _challengeDuration = 20f;

        private MaterialPropertyBlock _propertyBlock;
        private float _flashUntil = -1f;
        private string _shownText;

        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");

        public event Action<ShootingLane> SessionFinished;
        public event Action<ShootingLane, int> RecordSet;

        public Transform StandPoint => _standPoint;
        public Transform Target => _target.transform;
        public bool IsOccupied => _isOccupied;
        public bool IsReserved { get; private set; }
        public bool IsAvailable => !_isOccupied && !IsReserved;
        public int Number => _number;
        public UpgradeConfig RequiredUpgrade => _requiredUpgrade;
        private bool IsChallengeRunning => _challengeEndTime > 0;

        public void Configure(float challengeDuration) => _challengeDuration = challengeDuration;

        public override void OnStartClient() => _target.Hit += OnTargetHit;

        public override void OnStopClient() => _target.Hit -= OnTargetHit;

        [Server]
        public void ServerReserve() => IsReserved = true;

        [Server]
        public void ServerRelease()
        {
            IsReserved = false;
            _isOccupied = false;
        }

        [Server]
        public void ServerStartSession(float duration)
        {
            IsReserved = false;
            _isOccupied = true;
            _challengeEndTime = 0;
            _hits = 0;
            _sessionEndTime = NetworkTime.time + duration;
        }

        [Server]
        public void ServerTick()
        {
            if (_isOccupied && NetworkTime.time >= _sessionEndTime)
            {
                _isOccupied = false;
                SessionFinished?.Invoke(this);
            }

            if (IsChallengeRunning && NetworkTime.time >= _challengeEndTime)
                FinishChallenge();
        }

        [Server]
        public void ServerShoot(bool isHit)
        {
            if (!isHit)
                return;

            _hits++;
            RpcFlash();
        }

        public void TickDisplay(bool isUnlocked)
        {
            var record = _record > 0 ? $"  Best {_record}" : string.Empty;
            var text = !isUnlocked ? $"LANE {Number}\nLOCKED"
                : _isOccupied ? $"LANE {Number}\n{FormatTime(_sessionEndTime - NetworkTime.time)}  Hits {_hits}"
                : IsChallengeRunning ? $"LANE {Number}  GO!\n{FormatTime(_challengeEndTime - NetworkTime.time)}  Hits {_hits}{record}"
                : $"LANE {Number}\nShoot to start{record}";

            if (text != _shownText)
            {
                _shownText = text;
                _display.text = text;
            }

            if (_flashUntil > 0f && Time.time >= _flashUntil)
            {
                _flashUntil = -1f;
                _targetRenderer.SetPropertyBlock(null);
            }
        }

        private void OnTargetHit(HitInfo hitInfo)
        {
            var source = hitInfo.sourcePlayer;

            if (source == null || NetworkClient.localPlayer == null || source.transform.root != NetworkClient.localPlayer.transform)
                return;

            Flash();

            if (isServer)
                ServerRegisterPlayerHit();
            else
                CmdReportHit();
        }

        [Command(requiresAuthority = false)]
        private void CmdReportHit() => ServerRegisterPlayerHit();

        [Server]
        private void ServerRegisterPlayerHit()
        {
            if (!_isOccupied && !IsChallengeRunning)
            {
                _hits = 0;
                _challengeEndTime = NetworkTime.time + _challengeDuration;
            }

            _hits++;
        }

        [Server]
        private void FinishChallenge()
        {
            _challengeEndTime = 0;

            if (_hits <= _record)
                return;

            _record = _hits;
            RecordSet?.Invoke(this, _record);
        }

        [ClientRpc]
        private void RpcFlash() => Flash();

        private void Flash()
        {
            _propertyBlock ??= new MaterialPropertyBlock();
            _propertyBlock.SetColor(BaseColor, _hitColor);
            _targetRenderer.SetPropertyBlock(_propertyBlock);
            _flashUntil = Time.time + _hitFlashDuration;
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            if (_standPoint == null || _target == null)
                return;

            var stand = _standPoint.position + Vector3.up * 0.1f;
            Gizmos.color = new Color(1f, 0.6f, 0.1f);
            Gizmos.DrawWireSphere(stand, 0.3f);
            Gizmos.DrawLine(stand + Vector3.up * 1.4f, _target.transform.position);
            UnityEditor.Handles.Label(stand + Vector3.up * 0.6f, $"Lane {_number}");
        }
#endif

        private static string FormatTime(double seconds)
        {
            var total = Math.Max(0, (int)Math.Ceiling(seconds));
            return $"{total / 60}:{total % 60:00}";
        }
    }
}
