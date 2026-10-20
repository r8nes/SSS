using System;
using Mirror;

namespace ShooterShop.Days
{
    public sealed class DayNetworkState : NetworkBehaviour, IDayState, IDayStatistics, IDayCommands
    {
        [SyncVar(hook = nameof(OnValueSynced))] private int _day;
        [SyncVar(hook = nameof(OnPhaseSynced))] private DayPhase _phase;
        [SyncVar] private double _closesAt;
        [SyncVar(hook = nameof(OnValueSynced))] private int _income;
        [SyncVar(hook = nameof(OnValueSynced))] private int _expenses;
        [SyncVar(hook = nameof(OnValueSynced))] private int _sales;
        [SyncVar(hook = nameof(OnValueSynced))] private int _laneRentals;

        public event Action Changed;
        public event Action NextDayRequested;

        public int Day => _day;
        public DayPhase Phase => _phase;
        public bool IsOpen => _phase == DayPhase.Open;
        public double TimeLeft => _phase == DayPhase.Open ? Math.Max(0, _closesAt - NetworkTime.time) : 0;
        public int Income => _income;
        public int Expenses => _expenses;
        public int Profit => _income - _expenses;
        public int Sales => _sales;
        public int LaneRentals => _laneRentals;
        public bool CanStartNextDay => isServer && _phase == DayPhase.Summary;

        [Server]
        public void ServerOpen(int day, double closesAt)
        {
            _day = day;
            _income = 0;
            _expenses = 0;
            _sales = 0;
            _laneRentals = 0;
            _closesAt = closesAt;
            _phase = DayPhase.Open;
        }

        [Server]
        public void ServerSetPhase(DayPhase phase) => _phase = phase;

        public void RecordSale(int income, bool isLaneRental)
        {
            if (!isServer)
                return;

            _income += income;

            if (isLaneRental)
                _laneRentals++;
            else
                _sales++;
        }

        public void RecordExpense(int cost)
        {
            if (isServer)
                _expenses += cost;
        }

        public void StartNextDay()
        {
            if (CanStartNextDay)
                NextDayRequested?.Invoke();
        }

        private void OnValueSynced(int _, int __) => Changed?.Invoke();

        private void OnPhaseSynced(DayPhase _, DayPhase __) => Changed?.Invoke();
    }
}
