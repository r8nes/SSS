using System;
using Akila.FPSFramework;
using Cysharp.Threading.Tasks;
using UnityEngine.SceneManagement;

namespace ShooterShop.Loading
{
    public sealed class SceneLoadingScreen : ILoadingScreen, ILoadingScreenHost
    {
        private readonly string _sceneName;
        private LoadingScreen _screen;
        private bool _isSceneRequested;
        private int _version;

        public SceneLoadingScreen(string sceneName)
        {
            _sceneName = sceneName;
        }

        public void Attach(LoadingScreen screen) => _screen = screen;

        public async UniTask ShowAsync()
        {
            _version++;

            if (_screen == null)
                await LoadScreenAsync();

            _screen.Enable();
            await UniTask.NextFrame();
        }

        public void Hide()
        {
            if (_screen != null)
                HideAfterDelayAsync(_version).Forget();
        }

        private async UniTaskVoid HideAfterDelayAsync(int version)
        {
            await UniTask.Delay(TimeSpan.FromSeconds(_screen.postLoadDelay), DelayType.Realtime);

            if (version == _version && _screen != null)
                _screen.Disable();
        }

        private async UniTask LoadScreenAsync()
        {
            if (!_isSceneRequested)
            {
                _isSceneRequested = true;
                await SceneManager.LoadSceneAsync(_sceneName, LoadSceneMode.Additive).ToUniTask();
            }

            await UniTask.WaitUntil(() => _screen != null);
        }
    }
}
