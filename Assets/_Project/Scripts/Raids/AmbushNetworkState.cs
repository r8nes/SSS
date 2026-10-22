using System;
using Mirror;

namespace ShooterShop.Raids
{
    public sealed class AmbushNetworkState : NetworkBehaviour, IAmbushState
    {
        [SyncVar(hook = nameof(OnBoolSynced))]
        private bool _isActive;

        [SyncVar(hook = nameof(OnIntSynced))]
        private int _killed;

        [SyncVar(hook = nameof(OnIntSynced))]
        private int _total;

        public event Action Changed;

        public bool IsActive => _isActive;
        public int Killed => _killed;
        public int Total => _total;

        [Server]
        public void ServerSet(bool isActive, int killed, int total)
        {
            _isActive = isActive;
            _killed = killed;
            _total = total;

            if (!isClient)
                Changed?.Invoke();
        }

        private void OnBoolSynced(bool _, bool __) => Changed?.Invoke();

        private void OnIntSynced(int _, int __) => Changed?.Invoke();
    }
}
