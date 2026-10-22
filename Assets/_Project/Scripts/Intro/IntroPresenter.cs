using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using PrimeTween;
using ShooterShop.Input;
using UnityEngine;
using VContainer.Unity;
using Random = UnityEngine.Random;

namespace ShooterShop.Intro
{
    public sealed class IntroPresenter : IStartable, ITickable, IBossDialog, IDisposable
    {
        private readonly IntroConfig _config;
        private readonly IntroLayout _layout;
        private readonly IIntroState _state;
        private readonly DialogView _view;
        private readonly IPlayerInputLock _inputLock;
        private readonly List<float> _intensities = new();
        private readonly CancellationTokenSource _lifetime = new();
        private ShopBoss _boss;
        private int _line = -1;
        private IReadOnlyList<DialogLine> _lines;
        private bool _isPoweredOn;

        public IntroPresenter(IntroConfig config, IntroLayout layout, IIntroState state, DialogView view, IPlayerInputLock inputLock)
        {
            _config = config;
            _layout = layout;
            _state = state;
            _view = view;
            _inputLock = inputLock;
        }

        public void Start()
        {
            foreach (var light in _layout.Lights)
                _intensities.Add(light != null ? light.intensity : 0f);

            _view.SetOpen(false);
            _view.NextButton.onClick.AddListener(Next);
            _state.Changed += Refresh;
            SetLights(false);
            Refresh();
        }

        public void Dispose()
        {
            _lifetime.Cancel();
            _lifetime.Dispose();
            _view.NextButton.onClick.RemoveListener(Next);
            _state.Changed -= Refresh;
            _inputLock.Release(this);
        }

        public void Tick()
        {
            if (_layout.Boss != null)
                _layout.Boss.ClientTick(Time.deltaTime);
        }

        public void Open(ShopBoss boss)
        {
            if (_line >= 0 || !_config.TryGetVisit(_state.PendingVisit, out var visit) || visit.Lines.Count == 0)
                return;

            _lines = visit.Lines;
            _boss = boss;
            _line = 0;
            _inputLock.Acquire(this);
            _view.SetOpen(true);
            ShowLine();
        }

        private void Next()
        {
            if (_line < 0)
                return;

            _line++;

            if (_line < _lines.Count)
            {
                ShowLine();
                return;
            }

            Close();

            if (_boss != null)
                _boss.Finish();
        }

        private void ShowLine()
        {
            var isLast = _line == _lines.Count - 1;
            _view.Show(_config.BossName, _lines[_line].Text, isLast ? _config.FinishLabel : _config.NextLabel);
        }

        private void Close()
        {
            _line = -1;
            _view.SetOpen(false);
            _inputLock.Release(this);
        }

        private void Refresh()
        {
            if (_line >= 0 && !_state.HasPendingVisit)
                Close();

            if (_isPoweredOn || !_state.IsCompleted)
                return;

            _isPoweredOn = true;

            if (_state.Phase == IntroPhase.Skipped)
            {
                SetLights(true);
                OpenDoors(0f);
                return;
            }

            PowerUpAsync(_lifetime.Token).Forget();
        }

        private async UniTaskVoid PowerUpAsync(CancellationToken cancellation)
        {
            if (await UniTask.Delay(TimeSpan.FromSeconds(_config.LightsDelay), cancellationToken: cancellation).SuppressCancellationThrow())
                return;

            var flickers = Random.Range(_config.Flickers.x, _config.Flickers.y + 1);

            for (var i = 0; i < flickers; i++)
            {
                SetLights(true);

                if (await Step(cancellation))
                    return;

                SetLights(false);

                if (await Step(cancellation))
                    return;
            }

            SetLights(true);

            if (await UniTask.Delay(TimeSpan.FromSeconds(_config.DoorDelay), cancellationToken: cancellation).SuppressCancellationThrow())
                return;

            OpenDoors(_config.DoorOpenDuration);
        }

        private UniTask<bool> Step(CancellationToken cancellation) =>
            UniTask.Delay(TimeSpan.FromSeconds(Random.Range(_config.FlickerStep.x, _config.FlickerStep.y)), cancellationToken: cancellation).SuppressCancellationThrow();

        private void SetLights(bool isOn)
        {
            for (var i = 0; i < _layout.Lights.Count; i++)
                if (_layout.Lights[i] != null)
                    _layout.Lights[i].intensity = isOn ? _intensities[i] : 0f;
        }

        private void OpenDoors(float duration)
        {
            foreach (var door in _layout.Doors)
            {
                if (door.Pivot == null)
                    continue;

                var open = door.Pivot.localRotation * Quaternion.Euler(door.OpenRotation);

                if (duration <= 0f)
                    door.Pivot.localRotation = open;
                else
                    Tween.LocalRotation(door.Pivot, open, duration, Ease.InOutQuad);
            }
        }
    }
}
