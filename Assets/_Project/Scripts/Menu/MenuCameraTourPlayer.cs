using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using PrimeTween;
using UnityEngine;
using VContainer.Unity;
using Random = UnityEngine.Random;

namespace ShooterShop.Menu
{
    public sealed class MenuCameraTourPlayer : IAsyncStartable
    {
        private readonly MenuCameraTour _tour;
        private readonly ScreenFadeView _fade;
        private readonly MenuCameraTourConfig _config;

        public MenuCameraTourPlayer(MenuCameraTour tour, ScreenFadeView fade, MenuCameraTourConfig config)
        {
            _tour = tour;
            _fade = fade;
            _config = config;
        }

        public async UniTask StartAsync(CancellationToken cancellation)
        {
            var shots = _tour.Shots.Where(shot => shot.From != null && shot.To != null).ToList();

            if (shots.Count == 0)
            {
                _fade.SetOpacity(0f);
                return;
            }

            _fade.SetOpacity(1f);

            while (!cancellation.IsCancellationRequested)
            {
                foreach (var shot in Order(shots))
                    if (await PlayAsync(shot, cancellation).SuppressCancellationThrow())
                        return;
            }
        }

        private IEnumerable<MenuCameraShot> Order(List<MenuCameraShot> shots) =>
            _config.Shuffle ? shots.OrderBy(_ => Random.value) : shots;

        private async UniTask PlayAsync(MenuCameraShot shot, CancellationToken cancellation)
        {
            var camera = _tour.Camera.transform;
            var duration = shot.Duration > 0f ? shot.Duration : _config.DefaultShotDuration;
            var fade = Mathf.Min(_config.FadeDuration, duration * 0.5f);

            camera.SetPositionAndRotation(shot.From.position, shot.From.rotation);

            var move = Tween.Custom(camera, 0f, 1f, duration, (target, t) => target.SetPositionAndRotation(
                Vector3.Lerp(shot.From.position, shot.To.position, t),
                Quaternion.Slerp(shot.From.rotation, shot.To.rotation, t)), _config.MoveEase);

            try
            {
                _fade.FadeTo(0f, fade);
                await UniTask.Delay(TimeSpan.FromSeconds(Mathf.Max(fade, duration - fade)), cancellationToken: cancellation);
                _fade.FadeTo(1f, fade);
                await UniTask.Delay(TimeSpan.FromSeconds(fade + _config.BlackHold), cancellationToken: cancellation);
            }
            finally
            {
                move.Stop();
            }
        }
    }
}
