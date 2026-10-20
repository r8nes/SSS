using System;
using System.Collections.Generic;
using UnityEngine;

namespace Aetheria.Runtime.Rendering
{
    [ExecuteAlways]
    internal sealed class CameraEventHelper : MonoBehaviour
    {
        public Action<Camera> OnCameraDisable;

        public void OnDisable() => OnCameraDisable?.Invoke(GetComponent<Camera>());
    }

    internal sealed class PerCameraDataManager<T> where T : IDisposable
    {
        private readonly Dictionary<Camera, T> _dataDic = new();
        private readonly Func<T> _factory;

        public PerCameraDataManager(Func<T> factory) =>
            _factory = factory ?? throw new ArgumentNullException(nameof(factory));

        public bool TryGetData(Camera camera, out T data)
        {
            CleanupDeadCameras();

            if (camera)
                return _dataDic.TryGetValue(camera, out data);
            
            data = default;
            return false;
        }

        public T GetOrAddData(Camera camera)
        {
            CleanupDeadCameras();

            if (_dataDic.TryGetValue(camera, out var data)) return data;
            
            data = _factory();

            _dataDic.Add(camera, data);

            if (!ShouldTrackCameraDisable(camera))
                return data;

            var cameraEvent = camera.GetComponent<CameraEventHelper>();

            if (!cameraEvent)
                cameraEvent = camera.gameObject.AddComponent<CameraEventHelper>();

            cameraEvent.OnCameraDisable -= OnCameraDisable;
            cameraEvent.OnCameraDisable += OnCameraDisable;

            cameraEvent.hideFlags = HideFlags.HideInInspector | HideFlags.HideAndDontSave;

            return data;
        }

        public void Dispose()
        {
            foreach (var (camera, data) in _dataDic)
            {
                if (camera && camera.TryGetComponent<CameraEventHelper>(out var cameraEvent))
                    cameraEvent.OnCameraDisable -= OnCameraDisable;

                data?.Dispose();
            }

            _dataDic.Clear();
        }

        public void ForEach(Action<T> action)
        {
            if (action == null) return;

            CleanupDeadCameras();
            
            foreach (var data in _dataDic.Values)
                action(data);
        }

        private void OnCameraDisable(Camera camera)
        {
            if (camera && camera.TryGetComponent<CameraEventHelper>(out var cameraEvent))
                cameraEvent.OnCameraDisable -= OnCameraDisable;

            if (!_dataDic.TryGetValue(camera, out var data)) return;

            data.Dispose();
            _dataDic.Remove(camera);
        }

        private void CleanupDeadCameras()
        {
            if (_dataDic.Count == 0) return;

            List<Camera> deadCameras = null;

            foreach (var (camera, data) in _dataDic)
            {
                if (camera) continue;

                data?.Dispose();
                deadCameras ??= new List<Camera>();
                deadCameras.Add(camera);
            }

            if (deadCameras == null) return;

            for (int i = 0; i < deadCameras.Count; i++)
                _dataDic.Remove(deadCameras[i]);
        }

        private static bool ShouldTrackCameraDisable(Camera camera)
        {
            if (!camera)
                return false;

            if (Application.isPlaying)
                return true;

            return camera.cameraType == CameraType.Game;
        }
    }
}