using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;

[assembly: InternalsVisibleTo("Aetheria.Editor")]

namespace Aetheria.Runtime.Components
{
    [AddComponentMenu("Aetheria/Atmosphere/Celestial Body Controller")]
    [DisallowMultipleComponent]
    [ExecuteAlways]
    public sealed class CelestialBody : MonoBehaviour
    {
        private static readonly List<CelestialBody> ActiveBodies = new();

        internal static void CollectActiveInstances(List<CelestialBody> results)
        {
            results.Clear();

            for (int i = ActiveBodies.Count - 1; i >= 0; i--)
            {
                var body = ActiveBodies[i];

                if (!body || !IsRegistrable(body))
                {
                    ActiveBodies.RemoveAt(i);
                    continue;
                }

                if (!results.Contains(body))
                    results.Add(body);
            }
        }

        private void Awake() => Register(this);
        private void OnEnable() => Register(this);

        private void OnValidate()
        {
            ClampSerializedValues();

            if (IsRegistrable(this))
                Register(this);
            else
                Unregister(this);
        }

        private void OnDisable() => Unregister(this);

        private void OnDestroy() => Unregister(this);

        private static void Register(CelestialBody body)
        {
            if (!IsRegistrable(body) || ActiveBodies.Contains(body))
                return;

            ActiveBodies.Add(body);
        }

        private static void Unregister(CelestialBody body) => ActiveBodies.Remove(body);
        internal static bool IsRegistrable(CelestialBody body) =>
            body && body.isActiveAndEnabled && body.gameObject.scene.IsValid();

        private void ClampSerializedValues()
        {
            _diameterMultiplier = Mathf.Max(0f, _diameterMultiplier);
            _diameterOverride = Mathf.Max(0f, _diameterOverride);
            _cookieRotation = Mathf.Repeat(_cookieRotation, 360f);
            _limbDarkening = Mathf.Clamp(_limbDarkening, 0f, 2f);
            _flareSize = Mathf.Max(0f, _flareSize);
            _flareFalloff = Mathf.Max(0f, _flareFalloff);
            _sunIntensity = Mathf.Max(0f, _sunIntensity);
            _moonLunarLambertWeight = Mathf.Clamp01(_moonLunarLambertWeight);
            _moonPhase = Mathf.Clamp01(_moonPhase);
            _moonPhaseRotation = Mathf.Repeat(_moonPhaseRotation, 360f);
            _earthshine = Mathf.Clamp01(_earthshine);
        }

        [Header("Sky Interaction")]
        [SerializeField] internal bool _interactsWithSky = true;

        [Header("Body Size")]
        [SerializeField] internal bool _diameterMultiplierMode;
        [SerializeField, Min(0f)] internal float _diameterMultiplier = 1.0f;
        [SerializeField, Min(0f)] internal float _diameterOverride = 0.53f;
        [SerializeField] internal float _angularDiameter = 0.53f;

        [Header("Shading")]
        [SerializeField] internal CelestialBodyShadingSource _shadingSource =
            CelestialBodyShadingSource.Emission;

        [Header("Surface")]
        [SerializeField] internal Color _surfaceTint = Color.white;
        [SerializeField] internal Texture2D _surfaceCookieTexture;
        [SerializeField, Range(0f, 360f)] internal float _cookieRotation;
        [SerializeField, Range(0f, 2f)] internal float _limbDarkening = 0.5442f;
        [SerializeField, Min(0f)] internal float _flareSize = 2.0f;
        [SerializeField, Min(0f)] internal float _flareFalloff = 4.0f;

        [Header("Moon Settings")]
        [SerializeField] internal Light _sunLightOverride;
        [SerializeField] internal Color _sunColor = Color.white;
        [SerializeField, Min(0f)] internal float _sunIntensity = 1.0f;
        [SerializeField, Range(0f, 1f)] internal float _moonLunarLambertWeight = 0.85f;
        [SerializeField, Range(0f, 1f)] internal float _moonPhase = 0.2f;
        [SerializeField, Range(0f, 360f)] internal float _moonPhaseRotation;
        [SerializeField, Range(0f, 1f)] internal float _earthshine = 0.05f;

        [Header("Distance")]
        [SerializeField] internal float _distance = 1.496e11f;

        public bool InteractsWithSky
        {
            get => _interactsWithSky;
            set => _interactsWithSky = value;
        }

        public bool DiameterMultiplierMode
        {
            get => _diameterMultiplierMode;
            set => _diameterMultiplierMode = value;
        }

        public float DiameterMultiplier
        {
            get => _diameterMultiplier;
            set => _diameterMultiplier = Mathf.Max(0f, value);
        }

        public float DiameterOverride
        {
            get => _diameterOverride;
            set => _diameterOverride = Mathf.Max(0f, value);
        }

        public float AngularDiameter
        {
            get => _angularDiameter;
            set => _angularDiameter = value;
        }

        public CelestialBodyShadingSource ShadingSource
        {
            get => _shadingSource;
            set => _shadingSource = value;
        }

        public Color SurfaceTint
        {
            get => _surfaceTint;
            set => _surfaceTint = value;
        }

        public Texture2D SurfaceCookieTexture
        {
            get => _surfaceCookieTexture;
            set => _surfaceCookieTexture = value;
        }

        public float CookieRotation
        {
            get => _cookieRotation;
            set => _cookieRotation = Mathf.Repeat(value, 360f);
        }

        public float LimbDarkening
        {
            get => _limbDarkening;
            set => _limbDarkening = Mathf.Clamp(value, 0f, 2f);
        }

        public float FlareSize
        {
            get => _flareSize;
            set => _flareSize = Mathf.Max(0f, value);
        }

        public float FlareFalloff
        {
            get => _flareFalloff;
            set => _flareFalloff = Mathf.Max(0f, value);
        }

        public Light SunLightOverride
        {
            get => _sunLightOverride;
            set => _sunLightOverride = value;
        }

        public Color SunColor
        {
            get => _sunColor;
            set => _sunColor = value;
        }

        public float SunIntensity
        {
            get => _sunIntensity;
            set => _sunIntensity = Mathf.Max(0f, value);
        }

        public float MoonLunarLambertWeight
        {
            get => _moonLunarLambertWeight;
            set => _moonLunarLambertWeight = Mathf.Clamp01(value);
        }

        public float MoonPhase
        {
            get => _moonPhase;
            set => _moonPhase = Mathf.Clamp01(value);
        }

        public float MoonPhaseRotation
        {
            get => _moonPhaseRotation;
            set => _moonPhaseRotation = Mathf.Repeat(value, 360f);
        }

        public float Earthshine
        {
            get => _earthshine;
            set => _earthshine = Mathf.Clamp01(value);
        }

        public float Distance
        {
            get => _distance;
            set => _distance = value;
        }

        public CelestialBodySettings Settings
        {
            get => new(
                InteractsWithSky,
                DiameterMultiplierMode,
                DiameterMultiplier,
                DiameterOverride,
                AngularDiameter,
                ShadingSource,
                SurfaceTint,
                SurfaceCookieTexture,
                CookieRotation,
                LimbDarkening,
                FlareSize,
                FlareFalloff,
                SunLightOverride,
                SunColor,
                SunIntensity,
                MoonLunarLambertWeight,
                MoonPhase,
                MoonPhaseRotation,
                Earthshine,
                Distance);
            set => Apply(value);
        }

        public void Apply(in CelestialBodySettings settings)
        {
            InteractsWithSky = settings.InteractsWithSky;
            DiameterMultiplierMode = settings.DiameterMultiplierMode;
            DiameterMultiplier = settings.DiameterMultiplier;
            DiameterOverride = settings.DiameterOverride;
            AngularDiameter = settings.AngularDiameter;
            ShadingSource = settings.ShadingSource;
            SurfaceTint = settings.SurfaceTint;
            SurfaceCookieTexture = settings.SurfaceCookieTexture;
            CookieRotation = settings.CookieRotation;
            LimbDarkening = settings.LimbDarkening;
            FlareSize = settings.FlareSize;
            FlareFalloff = settings.FlareFalloff;
            SunLightOverride = settings.SunLightOverride;
            SunColor = settings.SunColor;
            SunIntensity = settings.SunIntensity;
            MoonLunarLambertWeight = settings.MoonLunarLambertWeight;
            MoonPhase = settings.MoonPhase;
            MoonPhaseRotation = settings.MoonPhaseRotation;
            Earthshine = settings.Earthshine;
            Distance = settings.Distance;
        }
    }

    public enum CelestialBodyShadingSource
    {
        ReflectSunLight = 0,
        Emission = 1,
        Manual = 2
    }
}