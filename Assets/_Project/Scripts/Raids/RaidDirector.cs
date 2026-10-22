using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Akila.FPSFramework;
using Cysharp.Threading.Tasks;
using Mirror;
using ShooterShop.Catalog;
using ShooterShop.Customers;
using ShooterShop.Days;
using ShooterShop.Economy;
using ShooterShop.Feedback;
using ShooterShop.Rentals;
using ShooterShop.Storage;
using ShooterShop.Store;
using UnityEngine;
using UnityEngine.AI;
using VContainer;
using VContainer.Unity;
using Random = UnityEngine.Random;

namespace ShooterShop.Raids
{
    public sealed class RaidDirector : IRaid, IRaidDamage, ITickable, IDisposable
    {
        private readonly RaidConfig _config;
        private readonly ShopLayout _shop;
        private readonly StoreLayout _store;
        private readonly ProductCatalog _catalog;
        private readonly IDayState _day;
        private readonly IWarehouse _warehouse;
        private readonly IWallet _wallet;
        private readonly IDeliveryService _delivery;
        private readonly IShopFeed _feed;
        private readonly ICustomerService _customers;
        private readonly CustomerAppearanceCatalog _appearances;
        private readonly RentalWeaponCatalog _weapons;
        private readonly IObjectResolver _resolver;
        private readonly AmbushLayout _ambushLayout;
        private readonly AmbushNetworkState _ambush;
        private readonly List<Raider> _raiders = new();
        private readonly Dictionary<Raider, Dictionary<ProductConfig, int>> _loot = new();
        private readonly Dictionary<Raider, ProductStand> _targets = new();
        private readonly HashSet<Raider> _escaping = new();
        private readonly Dictionary<NetworkIdentity, IServerDamageable> _players = new();
        private readonly CancellationTokenSource _lifetime = new();
        private int _scheduledDay;
        private float _startAt = -1f;
        private int _stolen;

        public RaidDirector(
            RaidConfig config,
            ShopLayout shop,
            StoreLayout store,
            ProductCatalog catalog,
            IDayState day,
            IWarehouse warehouse,
            IWallet wallet,
            IDeliveryService delivery,
            IShopFeed feed,
            ICustomerService customers,
            CustomerAppearanceCatalog appearances,
            RentalWeaponCatalog weapons,
            IObjectResolver resolver,
            AmbushLayout ambushLayout,
            AmbushNetworkState ambush)
        {
            _ambushLayout = ambushLayout;
            _ambush = ambush;
            _config = config;
            _shop = shop;
            _store = store;
            _catalog = catalog;
            _day = day;
            _warehouse = warehouse;
            _wallet = wallet;
            _delivery = delivery;
            _feed = feed;
            _customers = customers;
            _appearances = appearances;
            _weapons = weapons;
            _resolver = resolver;
        }

        public bool IsActive => _raiders.Count > 0 || _startAt >= 0f;
        public bool IsAmbushActive => _ambush.IsActive;

        public void ServerStartAmbush()
        {
            if (_ambush.IsActive || _ambushLayout.Points.Count == 0)
                return;

            var count = Random.Range(_config.AmbushCount.x, _config.AmbushCount.y + 1);

            for (var i = 0; i < count; i++)
            {
                var point = _ambushLayout.Points[i % _ambushLayout.Points.Count];
                Spawn(RaidRole.Gunman, true, point.position, i >= _ambushLayout.Points.Count ? 1.2f : 0.2f, point.rotation, true);
            }

            _ambush.ServerSet(true, 0, count);
        }

        public void Dispose()
        {
            _lifetime.Cancel();
            _lifetime.Dispose();
        }

        public void Tick()
        {
            if (!NetworkServer.active)
                return;

            TickSchedule();

            if (_raiders.Count == 0)
                return;

            var gangLeaves = !_day.IsOpen || !_raiders.Any(r => r.Role == RaidRole.Thief && !r.IsAmbusher && !_escaping.Contains(r));

            foreach (var raider in _raiders.ToList())
            {
                if (raider.IsAmbusher)
                {
                    TickAmbusher(raider);
                    continue;
                }

                if (gangLeaves && !_escaping.Contains(raider))
                    Escape(raider);

                if (_escaping.Contains(raider))
                    TickEscape(raider);
                else if (raider.Role == RaidRole.Thief)
                    TickThief(raider);
                else
                    TickGunman(raider);
            }
        }

        public void ServerHit(Raider raider, Vector3 direction, Vector3 point)
        {
            if (raider == null || raider.IsDead || !_raiders.Contains(raider))
                return;

            if (raider.ServerRegisterHit() >= _config.HitsToKill)
                Kill(raider, direction, point);
        }

        private void TickSchedule()
        {
            if (_startAt >= 0f)
            {
                if (!_day.IsOpen)
                    _startAt = -1f;
                else if (Time.time >= _startAt)
                    Begin();

                return;
            }

            if (!_day.IsOpen || _day.CurrentEvent == null || !_day.CurrentEvent.HasRaid || _scheduledDay == _day.Day)
                return;

            _scheduledDay = _day.Day;
            _startAt = Time.time + Random.Range(_config.StartDelay.x, _config.StartDelay.y);
        }

        private void Begin()
        {
            _startAt = -1f;
            _stolen = 0;
            _feed.Notify($"{_config.GangName} burst into the shop!", NotificationKind.Danger);
            _feed.Notify("They are stealing your stock. Grab a gun and defend the shop!", NotificationKind.Danger);
            _customers.Evacuate(_config.CustomerPause);

            for (var i = 0; i < _config.Thieves; i++)
                Spawn(RaidRole.Thief, _config.ThievesShoot, _shop.Exit.position, 1.5f, _shop.Exit.rotation);

            for (var i = 0; i < _config.Gunmen; i++)
                Spawn(RaidRole.Gunman, true, _shop.Exit.position, 1.5f, _shop.Exit.rotation);
        }

        private void Spawn(RaidRole role, bool isArmed, Vector3 origin, float spread, Quaternion rotation, bool isAmbusher = false)
        {
            var offset = Random.insideUnitCircle * spread;
            var position = origin + new Vector3(offset.x, 0f, offset.y);

            if (NavMesh.SamplePosition(position, out var hit, 3f, NavMesh.AllAreas))
                position = hit.position;

            var raider = _resolver.Instantiate(_config.Prefab, position, rotation);
            var weapon = isArmed && _config.Weapons.Count > 0 ? _weapons.IndexOf(_config.Weapons[Random.Range(0, _config.Weapons.Count)]) : -1;

            raider.ServerInitialize(role, PickAppearance(), weapon);

            if (isAmbusher)
                raider.ServerMakeAmbusher(position);

            NetworkServer.Spawn(raider.gameObject);
            raider.ServerWarp(position);
            raider.NextShotTime = Time.time + Random.Range(_config.FireInterval.x, _config.FireInterval.y);
            _raiders.Add(raider);
            _loot[raider] = new Dictionary<ProductConfig, int>();

            if (role == RaidRole.Gunman && !isAmbusher)
                raider.ServerMoveTo(_shop.Entrance.position);
        }

        private int PickAppearance()
        {
            var looks = _config.Appearances;

            if (looks.Count == 0)
                return _appearances.PickIndex();

            return _appearances.IndexOf(looks[Random.Range(0, looks.Count)]);
        }

        private void TickThief(Raider raider)
        {
            if (raider.IsArmed)
                TryShoot(raider, false);

            if (Time.time < raider.BusyUntil)
                return;

            if (!_targets.TryGetValue(raider, out var stand) || stand == null)
            {
                PickStand(raider);
                return;
            }

            if (!raider.ServerHasArrived())
                return;

            Steal(raider, stand);
            raider.BusyUntil = Time.time + _config.StealDuration;
            _targets.Remove(raider);
        }

        private void PickStand(Raider raider)
        {
            var claimed = _targets.Values.ToHashSet();
            var stand = _store.Stands
                .Where(s => !s.IsEmpty)
                .OrderBy(s => claimed.Contains(s) ? 1 : 0)
                .ThenBy(s => Vector3.SqrMagnitude(s.CustomerPoint.position - raider.transform.position))
                .FirstOrDefault();

            if (stand == null || CarriedBy(raider) >= _config.CarryCapacity)
            {
                Escape(raider);
                return;
            }

            _targets[raider] = stand;
            raider.ServerMoveTo(stand.CustomerPoint.position);
        }

        private void Steal(Raider raider, ProductStand stand)
        {
            var loot = _loot[raider];
            var taken = 0;

            foreach (var product in _catalog.Products)
            {
                var room = _config.CarryCapacity - CarriedBy(raider);

                if (room <= 0)
                    break;

                var amount = stand.ServerTake(product, room);

                if (amount <= 0)
                    continue;

                loot[product] = loot.TryGetValue(product, out var carried) ? carried + amount : amount;
                taken += amount;
            }

            if (taken > 0)
                _feed.Popup(stand.CustomerPoint.position + Vector3.up * 1.5f, $"-{taken} stolen!", NotificationKind.Warning);
        }

        private int CarriedBy(Raider raider) => _loot.TryGetValue(raider, out var loot) ? loot.Values.Sum() : 0;

        private void TickGunman(Raider raider)
        {
            if (TryShoot(raider, true))
                return;

            var target = NearestPlayer(raider.transform.position);

            if (target != null && Time.time >= raider.BusyUntil)
            {
                raider.BusyUntil = Time.time + 1f;
                raider.ServerMoveTo(target.transform.position);
            }
        }

        private void TickAmbusher(Raider raider)
        {
            if (raider.IsDashing)
            {
                if (Time.time < raider.BusyUntil && !raider.ServerHasArrived())
                    return;

                raider.IsDashing = false;
                raider.BusyUntil = Time.time + Random.Range(_config.AmbushBurstTime.x, _config.AmbushBurstTime.y);
            }

            TryShoot(raider, true);

            if (Time.time < raider.BusyUntil)
                return;

            var offset = Random.insideUnitCircle * _config.AmbushWanderRadius;
            var destination = raider.GuardPoint + new Vector3(offset.x, 0f, offset.y);

            if (!NavMesh.SamplePosition(destination, out var hit, _config.AmbushWanderRadius, NavMesh.AllAreas))
                return;

            raider.IsDashing = true;
            raider.BusyUntil = Time.time + _config.AmbushDashTimeout;
            raider.ServerStopAiming();
            raider.ServerMoveTo(hit.position);
        }

        private bool TryShoot(Raider raider, bool holdPosition)
        {
            var target = VisiblePlayer(raider);

            if (target == null)
            {
                raider.ServerStopAiming();

                if (holdPosition)
                    raider.ServerHold(false);

                return false;
            }

            var aimPoint = target.transform.position + Vector3.up * _config.AimHeight;
            raider.ServerAimAt(aimPoint, holdPosition);

            if (holdPosition)
                raider.ServerHold(true);

            if (Time.time < raider.NextShotTime)
                return true;

            raider.NextShotTime = Time.time + Random.Range(_config.FireInterval.x, _config.FireInterval.y);

            if (Random.value < HitChance(raider, aimPoint) && _players.TryGetValue(target, out var damageable) && damageable != null)
            {
                raider.ServerFire(aimPoint);
                damageable.ServerDamage(_config.Damage);
                return true;
            }

            raider.ServerFire(MissPoint(aimPoint));
            return true;
        }

        private float HitChance(Raider raider, Vector3 aimPoint)
        {
            var range = raider.IsAmbusher ? _config.AmbushAttackRange : _config.AttackRange;
            var distance = Vector3.Distance(raider.transform.position, aimPoint) / range;
            return _config.HitChance * Mathf.Lerp(1f, _config.HitChanceAtRange, Mathf.Clamp01(distance));
        }

        private Vector3 MissPoint(Vector3 aimPoint)
        {
            var direction = Random.onUnitSphere;
            direction.y = Mathf.Abs(direction.y) * 0.5f;
            return aimPoint + direction.normalized * Random.Range(_config.MissOffset.x, _config.MissOffset.y);
        }

        private NetworkIdentity VisiblePlayer(Raider raider)
        {
            var eye = raider.transform.position + Vector3.up * _config.EyeHeight;
            NetworkIdentity best = null;
            var range = raider.IsAmbusher ? _config.AmbushAttackRange : _config.AttackRange;
            var bestDistance = range * range;

            foreach (var player in AlivePlayers())
            {
                var aimPoint = player.transform.position + Vector3.up * _config.AimHeight;
                var distance = Vector3.SqrMagnitude(aimPoint - eye);

                if (distance > bestDistance)
                    continue;

                if (Physics.Linecast(eye, aimPoint, out var hit, _config.SightMask, QueryTriggerInteraction.Ignore) && hit.transform.root != player.transform.root)
                    continue;

                best = player;
                bestDistance = distance;
            }

            return best;
        }

        private NetworkIdentity NearestPlayer(Vector3 position) =>
            AlivePlayers().OrderBy(p => Vector3.SqrMagnitude(p.transform.position - position)).FirstOrDefault();

        private IEnumerable<NetworkIdentity> AlivePlayers()
        {
            foreach (var connection in NetworkServer.connections.Values)
            {
                var player = connection.identity;

                if (player == null)
                    continue;

                if (!_players.TryGetValue(player, out var damageable))
                    _players[player] = damageable = player.TryGetComponent<IServerDamageable>(out var found) ? found : null;

                if (damageable == null || damageable.IsAlive)
                    yield return player;
            }
        }

        private void Escape(Raider raider)
        {
            _escaping.Add(raider);
            _targets.Remove(raider);
            raider.ServerStopAiming();
            raider.ServerMoveTo(_shop.Exit.position);
        }

        private void TickEscape(Raider raider)
        {
            if (raider.IsArmed)
                TryShoot(raider, false);

            if (!raider.ServerHasArrived())
                return;

            foreach (var (product, amount) in _loot[raider])
            {
                _warehouse.TryRemove(product, amount);
                _stolen += amount;
            }

            Forget(raider);
            NetworkServer.Destroy(raider.gameObject);
        }

        private void Kill(Raider raider, Vector3 direction, Vector3 point)
        {
            ReturnLoot(raider);
            Forget(raider);

            if (raider.IsAmbusher)
                CountAmbushKill();
            raider.ServerDie(direction.normalized * _config.DeathImpulse, point);
            _wallet.Add(_config.Bounty);
            _feed.Popup(point + Vector3.up * 0.5f, $"+${_config.Bounty:N0} bounty", NotificationKind.Good);
            DespawnCorpseAsync(raider).Forget();
        }

        private void CountAmbushKill()
        {
            var killed = _ambush.Killed + 1;
            var isCleared = killed >= _ambush.Total;
            _ambush.ServerSet(!isCleared, killed, _ambush.Total);

            if (!isCleared)
                return;

            _wallet.Add(_config.AmbushReward);
            _feed.Notify($"All bandits are dead! The boss pays you ${_config.AmbushReward:N0}", NotificationKind.Good);
        }

        private void ReturnLoot(Raider raider)
        {
            foreach (var (product, amount) in _loot[raider])
            {
                var left = amount;

                foreach (var stand in _store.Stands.Where(s => s.Accepts(product)))
                    left -= stand.ServerPut(product, left);

                if (left > 0)
                    _delivery.DeliverNow(product, left);
            }
        }

        private void Forget(Raider raider)
        {
            _raiders.Remove(raider);
            _loot.Remove(raider);
            _targets.Remove(raider);
            _escaping.Remove(raider);

            if (_raiders.Count == 0 && !raider.IsAmbusher)
                _feed.Notify(_stolen > 0 ? $"The raid is over. The gang got away with {_stolen} items" : "The raid is over. Nothing was stolen!", _stolen > 0 ? NotificationKind.Warning : NotificationKind.Good);
        }

        private async UniTaskVoid DespawnCorpseAsync(Raider raider)
        {
            var lifetime = _config.CorpseSinkDelay + _config.CorpseSinkDuration;

            if (await UniTask.Delay(TimeSpan.FromSeconds(lifetime), cancellationToken: _lifetime.Token).SuppressCancellationThrow())
                return;

            if (raider != null && NetworkServer.active)
                NetworkServer.Destroy(raider.gameObject);
        }
    }
}
