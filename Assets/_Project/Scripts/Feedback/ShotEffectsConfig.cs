using Sirenix.OdinInspector;
using UnityEngine;

namespace ShooterShop.Feedback
{
    [CreateAssetMenu(menuName = "Shooter Shop/Feedback/Shot Effects", fileName = "ShotEffectsConfig")]
    public sealed class ShotEffectsConfig : ScriptableObject
    {
        [BoxGroup("Muzzle"), AssetsOnly]
        [SerializeField] private GameObject _muzzleFlash;

        [BoxGroup("Muzzle"), AssetsOnly]
        [SerializeField] private GameObject _muzzleSmoke;

        [BoxGroup("Muzzle"), MinValue(0.1f), SuffixLabel("sec")]
        [SerializeField] private float _effectLifetime = 2f;

        [BoxGroup("Tracer"), Required, AssetsOnly]
        [SerializeField] private Material _tracerMaterial;

        [BoxGroup("Tracer")]
        [SerializeField] private Color _tracerColor = new(1f, 0.85f, 0.45f, 0.9f);

        [BoxGroup("Tracer"), MinValue(0.001f), SuffixLabel("m")]
        [SerializeField] private float _tracerWidth = 0.02f;

        [BoxGroup("Tracer"), MinValue(0.01f), SuffixLabel("sec")]
        [SerializeField] private float _tracerDuration = 0.08f;

        [BoxGroup("Tracer"), MinValue(0f), SuffixLabel("m")]
        [InfoBox("Random spread around the target point")]
        [SerializeField] private float _scatter = 0.25f;

        [BoxGroup("Audio"), MinValue(1f), SuffixLabel("m")]
        [SerializeField] private float _audioMaxDistance = 22f;

        [BoxGroup("Audio"), Range(0f, 1f)]
        [InfoBox("Overall loudness of visitor shots compared to the player's own weapon")]
        [SerializeField] private float _volumeScale = 0.55f;

        [BoxGroup("Audio"), Range(0f, 1f)]
        [InfoBox("Extra volume multiplier when level geometry is between the shot and the listener")]
        [SerializeField] private float _muffledVolume = 0.35f;

        [BoxGroup("Audio"), MinValue(10f), SuffixLabel("Hz")]
        [SerializeField] private float _muffledCutoff = 900f;

        [BoxGroup("Audio")]
        [SerializeField] private LayerMask _occlusionMask = 1;

        public GameObject MuzzleFlash => _muzzleFlash;
        public GameObject MuzzleSmoke => _muzzleSmoke;
        public float EffectLifetime => _effectLifetime;
        public Material TracerMaterial => _tracerMaterial;
        public Color TracerColor => _tracerColor;
        public float TracerWidth => _tracerWidth;
        public float TracerDuration => _tracerDuration;
        public float Scatter => _scatter;
        public float AudioMaxDistance => _audioMaxDistance;
        public float VolumeScale => _volumeScale;
        public float MuffledVolume => _muffledVolume;
        public float MuffledCutoff => _muffledCutoff;
        public LayerMask OcclusionMask => _occlusionMask;
    }
}
