using System;
using System.Collections.Generic;
using Akila.FPSFramework;
using PrimeTween;
using UnityEngine;
using Object = UnityEngine.Object;
using Random = UnityEngine.Random;

namespace ShooterShop.Feedback
{
    public sealed class ShotEffects : IDisposable
    {
        private readonly ShotEffectsConfig _config;
        private readonly Dictionary<GameObject, LineRenderer> _tracers = new();
        private readonly Dictionary<GameObject, AudioSource> _audioSources = new();
        private readonly Dictionary<GameObject, AudioLowPassFilter> _lowPassFilters = new();
        private readonly IViewCamera _camera;
        private GameObject _tracerTemplate;
        private GameObject _audioTemplate;

        public ShotEffects(ShotEffectsConfig config, IViewCamera camera)
        {
            _camera = camera;
            _config = config;
        }

        public void Play(Vector3 muzzle, Vector3 target, AudioProfile sound)
        {
            var end = target + Random.insideUnitSphere * _config.Scatter;
            var direction = end - muzzle;
            var rotation = direction.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(direction) : Quaternion.identity;

            SpawnEffect(_config.MuzzleFlash, muzzle, rotation);
            SpawnEffect(_config.MuzzleSmoke, muzzle, rotation);
            DrawTracer(muzzle, end);
            PlaySound(sound, muzzle);
        }

        public void Dispose()
        {
            if (_tracerTemplate != null)
                Object.Destroy(_tracerTemplate);

            if (_audioTemplate != null)
                Object.Destroy(_audioTemplate);
        }

        private void SpawnEffect(GameObject prefab, Vector3 position, Quaternion rotation)
        {
            if (prefab == null)
                return;

            var effect = PoolManager.Get(prefab, _config.EffectLifetime);
            effect.transform.SetPositionAndRotation(position, rotation);
        }

        private void DrawTracer(Vector3 from, Vector3 to)
        {
            var instance = PoolManager.Get(TracerTemplate, _config.TracerDuration);
            var line = Cached(_tracers, instance);

            line.SetPosition(0, from);
            line.SetPosition(1, to);
            Tween.Custom(line, _config.TracerWidth, 0f, _config.TracerDuration, (target, width) => target.widthMultiplier = width);
        }

        private void PlaySound(AudioProfile profile, Vector3 position)
        {
            if (profile == null || profile.audioClip == null)
                return;

            var instance = PoolManager.Get(AudioTemplate, profile.audioClip.length + 0.1f);
            var source = Cached(_audioSources, instance);
            var lowPass = Cached(_lowPassFilters, instance);
            var isMuffled = IsOccluded(position);

            source.transform.position = position;
            source.clip = profile.audioClip;
            source.outputAudioMixerGroup = profile.output;
            source.volume = profile.volume * FPSFrameworkSettings.globalAudioVolume * _config.VolumeScale * (isMuffled ? _config.MuffledVolume : 1f);
            lowPass.enabled = isMuffled;
            lowPass.cutoffFrequency = _config.MuffledCutoff;
            source.pitch = profile.pitch * Random.Range(0.95f, 1.05f);
            source.Play();
        }

        private bool IsOccluded(Vector3 position)
        {
            var listener = _camera.Current;

            return listener != null && Physics.Linecast(position, listener.transform.position, _config.OcclusionMask, QueryTriggerInteraction.Ignore);
        }

        private static T Cached<T>(Dictionary<GameObject, T> cache, GameObject instance) where T : Component
        {
            if (!cache.TryGetValue(instance, out var component))
                cache[instance] = component = instance.GetComponent<T>();

            return component;
        }

        private GameObject TracerTemplate
        {
            get
            {
                if (_tracerTemplate != null)
                    return _tracerTemplate;

                _tracerTemplate = new GameObject("Tracer");
                _tracerTemplate.SetActive(false);

                var line = _tracerTemplate.AddComponent<LineRenderer>();
                line.useWorldSpace = true;
                line.positionCount = 2;
                line.sharedMaterial = _config.TracerMaterial;
                line.startColor = line.endColor = _config.TracerColor;
                line.widthMultiplier = _config.TracerWidth;
                line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                line.receiveShadows = false;
                return _tracerTemplate;
            }
        }

        private GameObject AudioTemplate
        {
            get
            {
                if (_audioTemplate != null)
                    return _audioTemplate;

                _audioTemplate = new GameObject("Shot Audio");
                _audioTemplate.SetActive(false);

                var source = _audioTemplate.AddComponent<AudioSource>();
                source.playOnAwake = false;
                source.spatialBlend = 1f;
                source.rolloffMode = AudioRolloffMode.Linear;
                source.maxDistance = _config.AudioMaxDistance;
                _audioTemplate.AddComponent<AudioLowPassFilter>().enabled = false;
                return _audioTemplate;
            }
        }
    }
}
