using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Audio;

namespace ShooterShop.Audio
{
    [CreateAssetMenu(menuName = "Shooter Shop/Audio/Music Config", fileName = "MusicConfig")]
    public sealed class MusicConfig : ScriptableObject
    {
        [BoxGroup("Playlists"), AssetsOnly, ListDrawerSettings(ShowFoldout = false)]
        [SerializeField] private List<AudioClip> _menuTracks = new();

        [BoxGroup("Playlists"), AssetsOnly, ListDrawerSettings(ShowFoldout = false)]
        [SerializeField] private List<AudioClip> _gameplayTracks = new();

        [BoxGroup("Playlists")]
        [SerializeField] private bool _shuffle = true;

        [BoxGroup("Playback"), Range(0f, 1f)]
        [SerializeField] private float _volume = 0.5f;

        [BoxGroup("Playback"), MinValue(0f), SuffixLabel("sec")]
        [SerializeField] private float _crossfadeDuration = 2f;

        [BoxGroup("Playback")]
        [SerializeField] private AudioMixerGroup _output;

        public bool Shuffle => _shuffle;
        public float Volume => _volume;
        public float CrossfadeDuration => _crossfadeDuration;
        public AudioMixerGroup Output => _output;

        public IReadOnlyList<AudioClip> TracksOf(MusicPlaylist playlist) => playlist switch
        {
            MusicPlaylist.Menu => _menuTracks,
            MusicPlaylist.Gameplay => _gameplayTracks,
            _ => System.Array.Empty<AudioClip>()
        };
    }
}
