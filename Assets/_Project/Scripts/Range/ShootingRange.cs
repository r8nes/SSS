using System;
using System.Collections.Generic;
using Mirror;
using ShooterShop.Feedback;
using ShooterShop.Upgrades;
using UnityEngine;
using VContainer.Unity;
using Random = UnityEngine.Random;

namespace ShooterShop.Range
{
    public sealed class ShootingRange : IShootingRange, IStartable, ITickable, IDisposable
    {
        private readonly RangeConfig _config;
        private readonly RangeLayout _layout;
        private readonly IUnlocks _unlocks;
        private readonly IShopFeed _feed;
        private readonly Dictionary<ShootingLane, IRangeShooter> _shooters = new();
        private readonly Dictionary<ShootingLane, float> _nextShotTime = new();

        public event Action<ShootingLane> SessionFinished;

        public ShootingRange(RangeConfig config, RangeLayout layout, IUnlocks unlocks, IShopFeed feed)
        {
            _feed = feed;
            _config = config;
            _layout = layout;
            _unlocks = unlocks;
        }

        public void Start()
        {
            foreach (var lane in _layout.Lanes)
            {
                lane.Configure(_config.ChallengeDuration);
                lane.SessionFinished += OnSessionFinished;
                lane.RecordSet += OnRecordSet;
            }
        }

        public void Dispose()
        {
            foreach (var lane in _layout.Lanes)
                if (lane != null)
                {
                    lane.SessionFinished -= OnSessionFinished;
                    lane.RecordSet -= OnRecordSet;
                }
        }

        public bool TryReserveLane(out ShootingLane lane)
        {
            foreach (var candidate in _layout.Lanes)
            {
                if (!candidate.IsAvailable || !_unlocks.IsUnlocked(candidate.RequiredUpgrade))
                    continue;

                candidate.ServerReserve();
                lane = candidate;
                return true;
            }

            lane = null;
            return false;
        }

        public void StartSession(ShootingLane lane, IRangeShooter shooter)
        {
            _shooters[lane] = shooter;
            _nextShotTime[lane] = Time.time + NextShotDelay();
            lane.ServerStartSession(_config.SessionDuration);
        }

        public void Release(ShootingLane lane)
        {
            _shooters.Remove(lane);
            lane.ServerRelease();
        }

        public void Tick()
        {
            if (_layout == null)
                return;

            foreach (var lane in _layout.Lanes)
            {
                if (lane == null)
                    continue;

                lane.TickDisplay(_unlocks.IsUnlocked(lane.RequiredUpgrade));

                if (NetworkServer.active)
                    TickServer(lane);
            }
        }

        private void TickServer(ShootingLane lane)
        {
            lane.ServerTick();

            if (!lane.IsOccupied || !_shooters.TryGetValue(lane, out var shooter) || Time.time < _nextShotTime[lane])
                return;

            _nextShotTime[lane] = Time.time + NextShotDelay();
            shooter.ServerFire();
            lane.ServerShoot(Random.value < _config.VisitorAccuracy);
        }

        private void OnSessionFinished(ShootingLane lane)
        {
            _shooters.Remove(lane);
            SessionFinished?.Invoke(lane);
        }

        private void OnRecordSet(ShootingLane lane, int hits)
        {
            _feed.Notify($"New record on lane {lane.Number}: {hits} hits!", NotificationKind.Good);
            _feed.Popup(lane.Target.position + Vector3.up * 0.6f, $"Record {hits}!");
        }

        private float NextShotDelay() => Random.Range(_config.ShotInterval.x, _config.ShotInterval.y);
    }
}
