using Akila.FPSFramework.Animation;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Akila.FPSFramework
{
    /// <summary>
    /// Component that controls and plays sounds based on an assigned AudioProfile.
    /// Can optionally play automatically on start.
    /// </summary>
    public class AudioEmitter : MonoBehaviour
    {
        [Tooltip("Audio profile defining the sound clips and settings.")]
        public Audio _audio = new Audio();

        [Tooltip("Automatically play the assigned audio on Awake.")]
        public bool playOnAwake = false;
        public bool playOnEnabled = false;
        public bool useOneShot = true;


        private void Start()
        {
            if (_audio.audioProfile == null)
            {
                Debug.LogWarning("AudioEmitter started without an AudioProfile assigned.", this);
                return;
            }

            _audio.Setup(gameObject);

            if (playOnAwake)
            {
                _audio.Play(useOneShot);
            }
        }

        private void OnEnable()
        {
            if (_audio.audioProfile == null)
            {
                Debug.LogWarning("AudioEmitter started without an AudioProfile assigned.", this);
                return;
            }
            _audio.Setup(gameObject);

            if (playOnEnabled)
                _audio.Play(useOneShot);
        }

        /// <summary>
        /// Plays the audio using the default clip/settings.
        /// </summary>
        public void Play()
        {
            if (ValidateAudio()) _audio.Play();
        }

        /// <summary>
        /// Plays a one-shot version of the audio (good for overlapping effects).
        /// </summary>
        public void PlayOneShot()
        {
            if (ValidateAudio()) _audio.Play(true);
        }

        /// <summary>
        /// Pauses the audio playback.
        /// </summary>
        public void Pause()
        {
            if (ValidateAudio()) _audio.Pause();
        }

        /// <summary>
        /// Stops the audio playback.
        /// </summary>
        public void Stop()
        {
            if (ValidateAudio()) _audio.Stop();
        }

        /// <summary>
        /// Ensures the _audio instance is set up and ready.
        /// </summary>
        /// <returns>True if audio is initialized and valid.</returns>
        private bool ValidateAudio()
        {
            if (_audio == null)
            {
                Debug.LogWarning("AudioEmitter attempted to play, but Audio is not initialized.", this);
                return false;
            }

            return true;
        }
    }
}
