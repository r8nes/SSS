using System;

namespace ShooterShop.Days
{
    public interface IDayState
    {
        event Action Changed;
        int Day { get; }
        DayPhase Phase { get; }
        bool IsOpen { get; }
        bool IsAcceptingCustomers { get; }
        double TimeLeft { get; }
        int Income { get; }
        int Expenses { get; }
        int Profit { get; }
        int Sales { get; }
        int LaneRentals { get; }
        DayEventConfig CurrentEvent { get; }
    }
}
