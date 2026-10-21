using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Mirror;
using UnityEngine;
using UnityEngine.AI;
using VContainer;
using VContainer.Unity;
using Random = UnityEngine.Random;

namespace ShooterShop.Street
{
    public sealed class PedestrianDirector : IAsyncStartable, ITickable, IPedestrianDamage, IDisposable
    {
        private readonly PedestrianConfig _config;
        private readonly StreetLayout _layout;
        private readonly IObjectResolver _resolver;
        private readonly Dictionary<Pedestrian, float> _idleUntil = new();
        private readonly CancellationTokenSource _lifetime = new();
        private float _panicUntil;
        private bool _isPanicking;

        public PedestrianDirector(PedestrianConfig config, StreetLayout layout, IObjectResolver resolver)
        {
            _config = config;
            _layout = layout;
            _resolver = resolver;
        }

        public async UniTask StartAsync(CancellationToken cancellation)
        {
            if (await UniTask.WaitUntil(() => NetworkServer.active, cancellationToken: cancellation).SuppressCancellationThrow())
                return;

            if (_layout.Waypoints.Count < 2)
                return;

            for (var i = 0; i < _config.Count; i++)
                Spawn();
        }

        public void Dispose()
        {
            _lifetime.Cancel();
            _lifetime.Dispose();
        }

        public void Tick()
        {
            if (!NetworkServer.active || _idleUntil.Count == 0)
                return;

            if (_isPanicking && Time.time >= _panicUntil)
                SetPanic(false);

            foreach (var pedestrian in new List<Pedestrian>(_idleUntil.Keys))
            {
                if (pedestrian == null)
                {
                    _idleUntil.Remove(pedestrian);
                    continue;
                }

                if (Time.time < pedestrian.PausedUntil || !pedestrian.ServerHasArrived())
                    continue;

                if (_idleUntil[pedestrian] < 0f)
                {
                    _idleUntil[pedestrian] = _isPanicking ? Time.time : Time.time + Random.Range(_config.IdleTime.x, _config.IdleTime.y);
                    continue;
                }

                if (Time.time < _idleUntil[pedestrian])
                    continue;

                _idleUntil[pedestrian] = -1f;
                pedestrian.ServerMoveTo(RandomWaypoint());
            }
        }

        public void ServerHit(Pedestrian pedestrian, Vector3 direction, Vector3 point)
        {
            if (pedestrian == null || pedestrian.IsDead || !_idleUntil.ContainsKey(pedestrian))
                return;

            if (pedestrian.ServerRegisterHit() >= _config.HitsToKill)
                Kill(pedestrian, direction, point);

            SetPanic(true);
        }

        private void Kill(Pedestrian pedestrian, Vector3 direction, Vector3 point)
        {
            _idleUntil.Remove(pedestrian);
            pedestrian.ServerDie(direction.normalized * _config.DeathImpulse, point);
            ReplaceLaterAsync(pedestrian).Forget();
        }

        private void SetPanic(bool isPanicking)
        {
            if (isPanicking)
                _panicUntil = Time.time + _config.PanicDuration;

            if (_isPanicking == isPanicking)
                return;

            _isPanicking = isPanicking;

            foreach (var pedestrian in new List<Pedestrian>(_idleUntil.Keys))
            {
                if (pedestrian == null)
                    continue;

                pedestrian.ServerPanic(isPanicking);

                if (!isPanicking)
                    continue;

                _idleUntil[pedestrian] = -1f;
                pedestrian.ServerMoveTo(RandomWaypoint());
            }
        }

        private async UniTaskVoid ReplaceLaterAsync(Pedestrian corpse)
        {
            var corpseLifetime = _config.CorpseSinkDelay + _config.CorpseSinkDuration;

            if (await UniTask.Delay(TimeSpan.FromSeconds(corpseLifetime), cancellationToken: _lifetime.Token).SuppressCancellationThrow())
                return;

            if (corpse != null && NetworkServer.active)
                NetworkServer.Destroy(corpse.gameObject);

            var wait = Mathf.Max(0f, _config.RespawnDelay - corpseLifetime);

            if (await UniTask.Delay(TimeSpan.FromSeconds(wait), cancellationToken: _lifetime.Token).SuppressCancellationThrow())
                return;

            if (NetworkServer.active)
                Spawn();
        }

        private void Spawn()
        {
            var start = RandomWaypoint();
            var pedestrian = _resolver.Instantiate(_config.Prefab, start, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f));

            pedestrian.ServerInitialize(Random.Range(_config.WalkSpeed.x, _config.WalkSpeed.y));
            NetworkServer.Spawn(pedestrian.gameObject);
            pedestrian.Agent.Warp(start);
            pedestrian.ServerPanic(_isPanicking);
            pedestrian.ServerMoveTo(RandomWaypoint());
            _idleUntil[pedestrian] = -1f;
        }

        private Vector3 RandomWaypoint()
        {
            var point = _layout.Waypoints[Random.Range(0, _layout.Waypoints.Count)].position;
            return NavMesh.SamplePosition(point, out var hit, 2f, NavMesh.AllAreas) ? hit.position : point;
        }
    }
}
