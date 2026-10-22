using System;
using Mirror;

namespace ShooterShop.Intro
{
    public sealed class IntroNetworkState : NetworkBehaviour, IIntroState
    {
        [SyncVar(hook = nameof(OnPhaseSynced))]
        private IntroPhase _phase;

        [SyncVar(hook = nameof(OnVisitSynced))]
        private int _pendingVisit = -1;

        public event Action Changed;

        public int PendingVisit => _pendingVisit;
        public bool HasPendingVisit => _pendingVisit >= 0;

        [Server]
        public void ServerSetVisit(int visit)
        {
            if (_pendingVisit == visit)
                return;

            _pendingVisit = visit;

            if (!isClient)
                Changed?.Invoke();
        }

        private void OnVisitSynced(int _, int __) => Changed?.Invoke();

        public IntroPhase Phase => _phase;
        public bool IsCompleted => _phase != IntroPhase.Pending;

        [Server]
        public void ServerSetPhase(IntroPhase phase)
        {
            if (_phase == phase)
                return;

            _phase = phase;

            if (!isClient)
                Changed?.Invoke();
        }

        private void OnPhaseSynced(IntroPhase _, IntroPhase __) => Changed?.Invoke();
    }
}
