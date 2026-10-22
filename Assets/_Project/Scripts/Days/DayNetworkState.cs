using System;
using Mirror;
using ShooterShop.Network;
using VContainer;

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
        [SyncVar(hook = nameof(OnValueSynced))] private int _eventIndex = -1;

        private DayEventCatalog _events;

        public event Action Changed;
        public event Action NextDayRequested;
        public event Action OpenShopRequested;
        public event Action CloseShopRequested;

        public int Day => _day;
        public DayPhase Phase => _phase;
        public bool IsOpen => _phase == DayPhase.Open;
        public bool IsAcceptingCustomers => IsOpen && TimeLeft > 0;
        public bool CanCloseShop => IsOpen && TimeLeft <= 0;
        public double TimeLeft => _phase == DayPhase.Open ? Math.Max(0, _closesAt - NetworkTime.time) : 0;
        public int Income => _income;
        public int Expenses => _expenses;
        public int Profit => _income - _expenses;
        public int Sales => _sales;
        public int LaneRentals => _laneRentals;
        public DayEventConfig CurrentEvent => _events != null && _events.TryGet(_eventIndex, out var dayEvent) ? dayEvent : null;
        public bool CanStartNextDay => _phase == DayPhase.Summary && this.IsSpawnedOnServer();
        public bool CanOpenShop => _phase == DayPhase.Preparing && _day > 0;

        [Inject]
        public void Construct(DayEventCatalog events) => _events = events;

        [Server]
        public void ServerPrepare(int day, int eventIndex)
        {
            _eventIndex = eventIndex;
            _day = day;
            _income = 0;
            _expenses = 0;
            _sales = 0;
            _laneRentals = 0;
            _phase = DayPhase.Preparing;
        }

        [Server]
        public void ServerOpen(double closesAt)
        {
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

        public void OpenShop()
        {
            if (!CanOpenShop)
                return;

            if (isServer)
                OpenShopRequested?.Invoke();
            else
                CmdOpenShop();
        }

        public void CloseShop()
        {
            if (!CanCloseShop)
                return;

            if (isServer)
                CloseShopRequested?.Invoke();
            else
                CmdCloseShop();
        }

        [Command(requiresAuthority = false)]
        private void CmdCloseShop()
        {
            if (CanCloseShop)
                CloseShopRequested?.Invoke();
        }

        [Command(requiresAuthority = false)]
        private void CmdOpenShop()
        {
            if (CanOpenShop)
                OpenShopRequested?.Invoke();
        }

        private void OnValueSynced(int _, int __) => Changed?.Invoke();

        private void OnPhaseSynced(DayPhase _, DayPhase __) => Changed?.Invoke();
    }
}
