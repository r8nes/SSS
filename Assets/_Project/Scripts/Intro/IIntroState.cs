using System;

namespace ShooterShop.Intro
{
    public interface IIntroState
    {
        event Action Changed;
        IntroPhase Phase { get; }
        bool IsCompleted { get; }
        int PendingVisit { get; }
        bool HasPendingVisit { get; }
    }
}
