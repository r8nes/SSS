using System;
using Akila.FPSFramework;
using PrimeTween;
using UnityEngine;
using VContainer.Unity;
using Object = UnityEngine.Object;
using Random = UnityEngine.Random;

namespace ShooterShop.Audio
{
    public sealed class MusicPlayer : IMusicPlayer, ITickable, IDisposable
    {
        private readonly MusicConfig _config;
        private readonly AudioSource[] _sources = new AudioSource[2];
        private readonly float[] _fades = new float[2];
        private readonly Tween[] _fadeTweens = new Tween[2];
        private GameObject _root;
        private int _activeSource;
        private int _trackIndex = -1;

        public MusicPlayer(MusicConfig config)
        {
            _config = config;
        }

        public MusicPlaylist Current { get; private set; }

        public void Play(MusicPlaylist playlist)
        {
            if (playlist == Current)
                return;

            Current = playlist;
            _trackIndex = -1;
            PlayNextTrack();
        }

        public void Tick()
        {
            if (_root == null)
                return;

            var volume = _config.Volume * FPSFrameworkSettings.musicVolume;

            for (var i = 0; i < _sources.Length; i++)
                _sources[i].volume = volume * _fades[i];

            var active = _sources[_activeSource];

            if (Current == MusicPlaylist.None || !active.isPlaying || active.clip == null)
                return;

            if (active.time >= active.clip.length - _config.CrossfadeDuration)
                PlayNextTrack();
        }

        public void Dispose()
        {
            for (var i = 0; i < _fadeTweens.Length; i++)
                _fadeTweens[i].Stop();

            if (_root != null)
                Object.Destroy(_root);
        }

        private void PlayNextTrack()
        {
            EnsureSources();
            Fade(_activeSource, 0f, true);

            var tracks = _config.TracksOf(Current);

            if (tracks.Count == 0)
                return;

            _trackIndex = _config.Shuffle && tracks.Count > 1
                ? (_trackIndex + Random.Range(1, tracks.Count)) % tracks.Count
                : (_trackIndex + 1) % tracks.Count;

            _activeSource = 1 - _activeSource;
            var incoming = _sources[_activeSource];
            incoming.clip = tracks[Math.Max(0, _trackIndex)];
            _fades[_activeSource] = 0f;
            incoming.Play();
            Fade(_activeSource, 1f, false);
        }

        private void Fade(int index, float target, bool stopWhenDone)
        {
            _fadeTweens[index].Stop();

            if (!_sources[index].isPlaying)
            {
                _fades[index] = target;
                return;
            }

            _fadeTweens[index] = Tween.Custom(_fades[index], target, Mathf.Max(0.01f, _config.CrossfadeDuration), value => _fades[index] = value);

            if (stopWhenDone)
                _fadeTweens[index].OnComplete(_sources[index], source => source.Stop());
        }

        private void EnsureSources()
        {
            if (_root != null)
                return;

            _root = new GameObject("Music");
            Object.DontDestroyOnLoad(_root);

            for (var i = 0; i < _sources.Length; i++)
            {
                var source = _root.AddComponent<AudioSource>();
                source.playOnAwake = false;
                source.loop = false;
                source.spatialBlend = 0f;
                source.outputAudioMixerGroup = _config.Output;
                _sources[i] = source;
            }
        }
    }
}
