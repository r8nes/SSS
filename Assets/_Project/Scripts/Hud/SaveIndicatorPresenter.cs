using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using ShooterShop.Saves;
using UnityEngine;
using VContainer.Unity;

namespace ShooterShop.Hud
{
    public sealed class SaveIndicatorPresenter : IStartable, ITickable, IDisposable
    {
        private readonly SaveIndicatorView _view;
        private readonly HudConfig _config;
        private readonly ISaveEvents _saves;
        private readonly CancellationTokenSource _lifetime = new();
        private bool _isSpinning;

        public SaveIndicatorPresenter(SaveIndicatorView view, HudConfig config, ISaveEvents saves)
        {
            _view = view;
            _config = config;
            _saves = saves;
        }

        public void Start()
        {
            _view.SetVisible(false, 0f);
            _saves.Saved += OnSaved;
        }

        public void Dispose()
        {
            _saves.Saved -= OnSaved;
            _lifetime.Cancel();
            _lifetime.Dispose();
        }

        public void Tick()
        {
            if (_isSpinning)
                _view.Spin(_config.IconSpinSpeed * Time.unscaledDeltaTime);
        }

        private void OnSaved() => ShowAsync(_lifetime.Token).Forget();

        private async UniTaskVoid ShowAsync(CancellationToken cancellation)
        {
            _isSpinning = true;
            _view.SetIconVisible(true);
            _view.SetText(_config.SavingText);
            _view.SetVisible(true, _config.SaveFade);

            if (await UniTask.Delay(TimeSpan.FromSeconds(_config.SavingDuration), true, cancellationToken: cancellation).SuppressCancellationThrow())
                return;

            _isSpinning = false;
            _view.SetIconVisible(false);
            _view.SetText(_config.SavedText);

            if (await UniTask.Delay(TimeSpan.FromSeconds(_config.SavedDuration), true, cancellationToken: cancellation).SuppressCancellationThrow())
                return;

            _view.SetVisible(false, _config.SaveFade);
        }
    }
}
