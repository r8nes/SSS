using System;
using Aetheria.Runtime.Overrides;
using Aetheria.Runtime.Rendering;
using Aetheria.Runtime.Utils;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;

namespace Aetheria.Runtime.Data
{
    internal sealed class AtmosphereData : IDisposable
    {
        public const int MaxCelestialLights = (int)AtmosphereShaderConfig.MaxCelestialLights;
        
        private LutResource _validMask;

        private readonly SharedLutCache _sharedCache;

        private SharedLutCache.Resource _transmittance;
        private SharedLutCache.Resource _multiScattering;
        private SharedLutCache.Resource _groundIrradiance;

        private int _transmittanceKey;
        private int _multiScatteringKey;
        private int _groundIrradianceKey;
        private int _skyViewKey;

        private const float SkyViewAltitudeThresholdMeters = 25.0f;
        private const float SkyViewLightDirectionThresholdDegrees = 0.05f;

        private bool _hasSkyViewObserverState;
        private bool _skyViewInitialized;
        private float _skyViewObserverAltitudeMeters;
        private bool _hasSkyViewCelestialDirectionState;
        private int _skyViewCanonicalLightCount;
        private readonly Vector3[] _skyViewCanonicalLightDirections = new Vector3[MaxCelestialLights];

        private bool _hasAerialPerspectiveState;
        private Matrix4x4 _aerialPerspectiveViewProjection;
        private Vector3 _aerialPerspectiveCameraPositionWs;
        private int _aerialPerspectiveVolumeHash;
        private int _aerialPerspectiveCelestialStateHash;
        private int _aerialPerspectivePerPixelRaymarchSamples;
        private int _aerialPerspectiveSpaceRaymarchSamples;

        private bool _hasSharedResources;

        public readonly Vector4[] CelestialDirs = new Vector4[MaxCelestialLights];
        public readonly Color[] CelestialColors = new Color[MaxCelestialLights];
        public readonly float[] CelestialAngularRadii = new float[MaxCelestialLights];
        public readonly float[] CelestialDistanceFromCamera = new float[MaxCelestialLights];
        public int CelestialLightCount;
        
        internal readonly Vector4[] CelestialColorRadius = new Vector4[MaxCelestialLights];
        internal readonly Vector4[] CelestialForwardDistance = new Vector4[MaxCelestialLights];
        internal readonly Vector4[] CelestialRightAngularRadius = new Vector4[MaxCelestialLights];
        internal readonly Vector4[] CelestialUpType = new Vector4[MaxCelestialLights];
        internal readonly Vector4[] CelestialSurfaceEarthshine = new Vector4[MaxCelestialLights];
        internal readonly Vector4[] CelestialCookieAtlasScaleOffset = new Vector4[MaxCelestialLights];
        internal readonly Vector4[] CelestialSunDirectionLimbDarkening = new Vector4[MaxCelestialLights];
        internal readonly Vector4[] CelestialCookieParams = new Vector4[MaxCelestialLights];
        internal readonly Vector4[] CelestialFlareColor = new Vector4[MaxCelestialLights];
        internal readonly Vector4[] CelestialFlareParams = new Vector4[MaxCelestialLights];
        
        internal readonly MaterialPropertyBlock CelestialDiskProperties = new();

        public int CelestialStateHash;

        private RenderTexture _skyViewLut;
        private RenderTexture _atmosphericScatteringLut;

        private RenderGraphTextureHandleCache _skyViewCache;
        private RenderGraphTextureHandleCache _atmosphericScatteringCache;

        public AtmosphereData(SharedLutCache sharedCache) =>
            _sharedCache = sharedCache ?? throw new ArgumentNullException(nameof(sharedCache));

        public void BindSharedResources(PhysicallyBasedSky volume, VisualEnvironment environment)
        {
            int transmittanceKey = LutDependencyKeys.Transmittance(volume, environment);
            int multiScatteringKey = LutDependencyKeys.MultiScattering(volume, transmittanceKey);
            int groundIrradianceKey = LutDependencyKeys.GroundIrradiance(multiScatteringKey);

            bool transmittanceChanged = !_hasSharedResources || transmittanceKey != _transmittanceKey;
            bool multiScatteringChanged = !_hasSharedResources || multiScatteringKey != _multiScatteringKey;
            bool groundIrradianceChanged = !_hasSharedResources || groundIrradianceKey != _groundIrradianceKey;

            switch (transmittanceChanged)
            {
                case false when !multiScatteringChanged && !groundIrradianceChanged:
                    return;
                case true:
                {
                    if (_hasSharedResources)
                        _sharedCache.ReleaseTransmittance(_transmittanceKey);

                    _transmittanceKey = transmittanceKey;
                    _transmittance = _sharedCache.AcquireTransmittance(transmittanceKey);
                    break;
                }
            }

            if (multiScatteringChanged)
            {
                if (_hasSharedResources)
                    _sharedCache.ReleaseMultiScattering(_multiScatteringKey);

                _multiScatteringKey = multiScatteringKey;
                _multiScattering = _sharedCache.AcquireMultiScattering(multiScatteringKey);
            }

            if (groundIrradianceChanged)
            {
                if (_hasSharedResources)
                    _sharedCache.ReleaseGroundIrradiance(_groundIrradianceKey);

                _groundIrradianceKey = groundIrradianceKey;
                _groundIrradiance = _sharedCache.AcquireGroundIrradiance(groundIrradianceKey);
            }

            _hasSharedResources = true;
            InvalidateSkyDependentLuts();
        }

        public void PrepareFrame(PhysicallyBasedSky volume, VisualEnvironment environment, Camera camera, int raymarchSamples)
        {
            if (!camera || !environment) return;
            PrepareSkyViewFrame(volume, environment, camera.transform.position, raymarchSamples);
        }

        public void PrepareAerialPerspectiveFrame(
            PhysicallyBasedSky volume,
            VisualEnvironment environment,
            Matrix4x4 viewProjection,
            Vector3 cameraPositionWs,
            int perPixelRaymarchSamples,
            int spaceRaymarchSamples)
        {
            if (!volume || !environment) return;

            int volumeHash = HashCode.Combine(volume.GetHashCode(), environment.GetHashCode());

            if (_hasAerialPerspectiveState &&
                _aerialPerspectiveViewProjection.Equals(viewProjection) &&
                _aerialPerspectiveCameraPositionWs.Equals(cameraPositionWs) &&
                _aerialPerspectiveVolumeHash == volumeHash &&
                _aerialPerspectiveCelestialStateHash == CelestialStateHash &&
                _aerialPerspectivePerPixelRaymarchSamples == perPixelRaymarchSamples &&
                _aerialPerspectiveSpaceRaymarchSamples == spaceRaymarchSamples)
                return;

            _hasAerialPerspectiveState = true;
            _aerialPerspectiveViewProjection = viewProjection;
            _aerialPerspectiveCameraPositionWs = cameraPositionWs;
            _aerialPerspectiveVolumeHash = volumeHash;
            _aerialPerspectiveCelestialStateHash = CelestialStateHash;
            _aerialPerspectivePerPixelRaymarchSamples = perPixelRaymarchSamples;
            _aerialPerspectiveSpaceRaymarchSamples = spaceRaymarchSamples;
            SetValid(LutResource.AtmosphericScattering, false);
        }

        private void PrepareSkyViewFrame(
            PhysicallyBasedSky volume,
            VisualEnvironment environment,
            Vector3 worldPosition,
            int raymarchSamples)
        {
            if (!volume) return;

            ResolveSkyViewObserverState(
                environment,
                worldPosition,
                out float altitudeMeters,
                out var upWs);

            int skyViewCelestialStateHash = ComputeSkyViewCelestialPropertyHash();
            int skyViewKey = LutDependencyKeys.SkyView(
                volume,
                environment,
                _groundIrradianceKey,
                skyViewCelestialStateHash,
                raymarchSamples);

            bool dependenciesChanged = skyViewKey != _skyViewKey;
            bool observerChanged =
                environment.renderingSpace.value == RenderingSpace.World &&
                (!_hasSkyViewObserverState ||
                 Mathf.Abs(altitudeMeters - _skyViewObserverAltitudeMeters) >= SkyViewAltitudeThresholdMeters);
            bool celestialDirectionChanged = HaveSkyViewLightDirectionsChanged(upWs);

            if (!dependenciesChanged && !observerChanged && !celestialDirectionChanged)
                return;

            _skyViewKey = skyViewKey;
            CaptureSkyViewLightDirections(upWs);

            if (environment.renderingSpace.value == RenderingSpace.World)
            {
                _skyViewObserverAltitudeMeters = altitudeMeters;
                _hasSkyViewObserverState = true;
            }
            else
            {
                _hasSkyViewObserverState = false;
            }

            SetValid(LutResource.SkyView, false);
        }

        private int ComputeSkyViewCelestialPropertyHash()
        {
            var hash = new HashCode();
            int lightCount = Mathf.Clamp(CelestialLightCount, 0, MaxCelestialLights);
            hash.Add(lightCount);

            for (int i = 0; i < lightCount; ++i)
            {
                AddRgb(ref hash, CelestialColors[i]);
                hash.Add(CelestialAngularRadii[i]);
                hash.Add(CelestialDistanceFromCamera[i] <= 1e30f);
            }

            return hash.ToHashCode();
        }

        private bool HaveSkyViewLightDirectionsChanged(Vector3 upWs)
        {
            int lightCount = Mathf.Clamp(CelestialLightCount, 0, MaxCelestialLights);
            
            if (!_hasSkyViewCelestialDirectionState || lightCount != _skyViewCanonicalLightCount)
                return true;

            if (lightCount == 0)
                return false;

            ResolveCelestialReferenceFrame(upWs, out var forwardWs, out var sideWs);

            for (int i = 0; i < lightCount; ++i)
            {
                var canonicalDirection = ResolveCanonicalLightDirection(i, upWs, forwardWs, sideWs);
                
                if (Vector3.Angle(_skyViewCanonicalLightDirections[i], canonicalDirection) >=
                    SkyViewLightDirectionThresholdDegrees)
                    return true;
            }

            return false;
        }

        private void CaptureSkyViewLightDirections(Vector3 upWs)
        {
            int lightCount = Mathf.Clamp(CelestialLightCount, 0, MaxCelestialLights);
            _skyViewCanonicalLightCount = lightCount;

            if (lightCount > 0)
            {
                ResolveCelestialReferenceFrame(upWs, out var forwardWs, out var sideWs);

                for (int i = 0; i < lightCount; ++i)
                    _skyViewCanonicalLightDirections[i] =
                        ResolveCanonicalLightDirection(i, upWs, forwardWs, sideWs);
            }

            _hasSkyViewCelestialDirectionState = true;
        }

        private Vector3 ResolveCanonicalLightDirection(
            int lightIndex,
            Vector3 upWs,
            Vector3 forwardWs,
            Vector3 sideWs)
        {
            var directionData = CelestialDirs[lightIndex];
            var directionToLight = new Vector3(
                -directionData.x,
                -directionData.y,
                -directionData.z);
            
            float sqrMagnitude = directionToLight.sqrMagnitude;
            directionToLight = sqrMagnitude > 1e-12f
                ? directionToLight / Mathf.Sqrt(sqrMagnitude)
                : Vector3.up;

            return new Vector3(
                    Vector3.Dot(directionToLight, sideWs),
                    Vector3.Dot(directionToLight, upWs),
                    Vector3.Dot(directionToLight, forwardWs))
                .normalized;
        }

        private void ResolveCelestialReferenceFrame(
            Vector3 upWs,
            out Vector3 forwardWs,
            out Vector3 sideWs)
        {
            Vector3 celestialLightDirection;
            
            if (CelestialLightCount > 0)
            {
                var directionData = CelestialDirs[0];
                celestialLightDirection = new Vector3(
                    -directionData.x,
                    -directionData.y,
                    -directionData.z);
            }
            else
            {
                celestialLightDirection = Vector3.up;
            }

            var lightOnPlane =
                celestialLightDirection - upWs * Vector3.Dot(celestialLightDirection, upWs);
            
            float lightOnPlaneLength = lightOnPlane.magnitude;

            if (lightOnPlaneLength > 1e-4f)
            {
                forwardWs = lightOnPlane / lightOnPlaneLength;
                sideWs = Vector3.Cross(upWs, forwardWs).normalized;
                return;
            }

            var referenceAxis = Mathf.Abs(upWs.y) < 0.999f ? Vector3.up : Vector3.forward;
            sideWs = Vector3.Cross(upWs, referenceAxis).normalized;
            forwardWs = Vector3.Cross(sideWs, upWs).normalized;
        }

        private static void ResolveSkyViewObserverState(
            VisualEnvironment environment,
            Vector3 worldPosition,
            out float altitudeMeters,
            out Vector3 upWs)
        {
            if (environment.renderingSpace.value == RenderingSpace.Camera)
            {
                altitudeMeters = 0.0f;
                upWs = Vector3.up;
                return;
            }

            var planetCenterWs = environment.GetPlanetCenterWS(worldPosition);
            var relative = worldPosition - planetCenterWs;
            float radialDistance = relative.magnitude;
            upWs = radialDistance > 1e-4f ? relative / radialDistance : Vector3.up;
            altitudeMeters = radialDistance - environment.PlanetRadiusMeters;
        }

        private static void AddRgb(ref HashCode hash, Color color)
        {
            hash.Add(color.r);
            hash.Add(color.g);
            hash.Add(color.b);
        }

        public bool SkyViewInitialized => _skyViewInitialized;

        public bool IsValid(LutResource mask)
        {
            if ((mask & LutResource.Transmittance) != 0 &&
                _transmittance is not { Valid: true })
                return false;
            if ((mask & LutResource.MultiScattering) != 0 &&
                _multiScattering is not { Valid: true })
                return false;
            if ((mask & LutResource.GroundIrradiance) != 0 &&
                _groundIrradiance is not { Valid: true })
                return false;

            var localMask = mask & ~LutResource.Shared;
            return (_validMask & localMask) == localMask;
        }

        public void InvalidateSkyView() => InvalidateSkyDependentLuts();

        private void InvalidateSkyDependentLuts()
        {
            _skyViewKey = 0;
            _hasSkyViewObserverState = false;
            _hasSkyViewCelestialDirectionState = false;
            _skyViewCanonicalLightCount = 0;
            SetValid(LutResource.SkyDependent, false);
        }

        public void SetValid(LutResource mask, bool valid)
        {
            if (valid && (mask & LutResource.SkyView) != 0)
                _skyViewInitialized = true;
            if ((mask & LutResource.Transmittance) != 0 && _transmittance != null)
                _transmittance.Valid = valid;
            if ((mask & LutResource.MultiScattering) != 0 && _multiScattering != null)
                _multiScattering.Valid = valid;
            if ((mask & LutResource.GroundIrradiance) != 0 && _groundIrradiance != null)
                _groundIrradiance.Valid = valid;

            mask &= ~LutResource.Shared;
            
            if (valid)
                _validMask |= mask;
            else
                _validMask &= ~mask;
        }

        public static Color EvaluateTransmittance(
            PhysicallyBasedSky volume,
            VisualEnvironment environment,
            Vector3 worldPosition,
            Vector3 directionToLightWs)
        {
            if (!volume || !environment)
                return Color.white;

            float bottomRadius = environment.planetRadius.value;
            float topRadius = volume.TopRadius.value;

            const float _offsetKm = 10f / AtmospherePlanet.DistanceUnitMeters;

            var planetCenterWs = environment.GetPlanetCenterWS(worldPosition);
            var viewPos = (worldPosition - planetCenterWs) / AtmospherePlanet.DistanceUnitMeters;
            float viewHeight = viewPos.magnitude;

            var up = viewHeight > 1e-6f ? viewPos / viewHeight : Vector3.up;
            float minViewHeight = bottomRadius + _offsetKm;

            if (viewHeight < minViewHeight)
            {
                viewPos = up * minViewHeight;
                viewHeight = minViewHeight;
            }

            var viewDir = directionToLightWs.sqrMagnitude > 1e-10f
                ? directionToLightWs.normalized
                : Vector3.up;

            float tTop = RaySphereIntersectNearest(viewPos, viewDir, topRadius);

            if (viewHeight > topRadius)
            {
                if (tTop < 0f)
                    return Color.white;

                viewPos += viewDir * tTop;

                var entryUp = viewPos.sqrMagnitude > 1e-10f ? viewPos.normalized : up;
                viewPos -= entryUp * _offsetKm;

                tTop = RaySphereIntersectNearest(viewPos, viewDir, topRadius);
            }

            float tBottom = RaySphereIntersectNearest(viewPos, viewDir, bottomRadius);
            
            if (tBottom >= 0f && (tTop < 0f || tBottom < tTop))
                return Color.black;

            if (tTop <= 1e-6f)
                return Color.white;

            const int _sampleCount = 40;

            float dt = tTop / _sampleCount;
            
            var opticalDepth = Vector3.zero;

            var rayleighScattering =
                ToVector3(volume.RayleighScatteringColor.value) * volume.RayleighScatteringScale.value;
            
            var mieScattering = ToVector3(volume.MieScatteringColor.value) * volume.MieScatteringScale.value;
            var mieAbsorption = ToVector3(volume.MieAbsorptionColor.value) * volume.MieAbsorptionScale.value;
            
            var ozoneExtinction = ToVector3(volume.OzoneAbsorptionExtinctionColor.value) *
                                  volume.OzoneAbsorptionExtinctionScale.value * volume.OzoneScale.value;

            for (int i = 0; i < _sampleCount; i++)
            {
                float t = (i + 0.5f) * dt;

                var samplePos = viewPos + viewDir * t;

                float radius = samplePos.magnitude;
                float altitudeKm = radius - bottomRadius;

                float densityRay = Mathf.Exp(-altitudeKm / volume.RayleighScaleHeight.value);
                float densityMie = Mathf.Exp(-altitudeKm / Mathf.Max(volume.MieScaleHeight.value, 1e-6f));
                float densityOzone = SampleOzoneDensity(altitudeKm);

                var extinction = densityRay * rayleighScattering +
                                 densityMie * (mieScattering + mieAbsorption) +
                                 densityOzone * ozoneExtinction;

                opticalDepth += extinction * dt;
            }

            return new Color(
                Mathf.Exp(-opticalDepth.x),
                Mathf.Exp(-opticalDepth.y),
                Mathf.Exp(-opticalDepth.z),
                1f);
        }

        private static float RaySphereIntersectNearest(Vector3 origin, Vector3 direction, float radius)
        {
            float b = Vector3.Dot(origin, direction);
            float c = Vector3.Dot(origin, origin) - radius * radius;
            float discriminant = b * b - c;

            if (discriminant < 0f)
                return -1f;

            float root = Mathf.Sqrt(discriminant);
            float t0 = -b - root;
            float t1 = -b + root;

            if (t0 < 0f && t1 < 0f)
                return -1f;

            return t0 >= 0f ? t0 : Mathf.Max(0f, t1);
        }

        public TextureHandle ImportTransmittance(RenderGraph rg) => _transmittance?.Import(rg) ??
                                                                    throw new InvalidOperationException(
                                                                        "Atmosphere shared LUT resources were not bound.");

        public TextureHandle ImportMultiScatter(RenderGraph rg) => _multiScattering?.Import(rg) ??
                                                                   throw new InvalidOperationException(
                                                                       "Atmosphere shared LUT resources were not bound.");

        public TextureHandle ImportGroundIrradiance(RenderGraph rg) => _groundIrradiance?.Import(rg) ??
                                                                       throw new InvalidOperationException(
                                                                           "Atmosphere shared LUT resources were not bound.");

        public TextureHandle ImportSkyView(RenderGraph rg)
        {
            var previous = _skyViewLut;
            _skyViewLut = AtmosphereTools.EnsureRenderTexture(
                LutDefinitions.SkyView,
                _skyViewLut);

            if (previous != _skyViewLut)
            {
                _skyViewInitialized = false;
                SetValid(LutResource.SkyView, false);
            }

            return _skyViewCache.Import(rg, _skyViewLut);
        }

        public TextureHandle ImportAtmosphericScattering(RenderGraph rg)
        {
            var previous = _atmosphericScatteringLut;
            EnsureLuts();

            if (previous != _atmosphericScatteringLut)
                SetValid(LutResource.AtmosphericScattering, false);

            return _atmosphericScatteringCache.Import(rg, _atmosphericScatteringLut);
        }

        private void EnsureLuts()
        {
            _atmosphericScatteringLut = AtmosphereTools.EnsureRenderTexture(
                LutDefinitions.AtmosphericScattering,
                _atmosphericScatteringLut);
        }

        private static float SampleOzoneDensity(float altitudeKm)
        {
            const float _ozoneCenterKm = 25.0f;
            const float _ozoneHalfWidthKm = 15.0f;

            return Mathf.Max(0.0f, 1.0f - Mathf.Abs(altitudeKm - _ozoneCenterKm) / _ozoneHalfWidthKm);
        }

        private static Vector3 ToVector3(Color color) => new(color.r, color.g, color.b);

        public void Dispose()
        {
            _validMask = 0;
            _skyViewKey = 0;
            _hasSkyViewObserverState = false;
            _skyViewInitialized = false;
            _hasSkyViewCelestialDirectionState = false;
            _skyViewCanonicalLightCount = 0;
            _hasAerialPerspectiveState = false;
            _aerialPerspectiveViewProjection = default;
            _aerialPerspectiveCameraPositionWs = default;
            _aerialPerspectiveVolumeHash = 0;
            _aerialPerspectiveCelestialStateHash = 0;
            _aerialPerspectivePerPixelRaymarchSamples = 0;
            _aerialPerspectiveSpaceRaymarchSamples = 0;
            
            CelestialStateHash = 0;
            CelestialLightCount = 0;

            ReleaseSharedResources();
            _skyViewCache.Dispose();
            _atmosphericScatteringCache.Dispose();

            AtmosphereTools.ReleaseRenderTexture(_skyViewLut);
            _skyViewLut = null;
            
            AtmosphereTools.ReleaseRenderTexture(_atmosphericScatteringLut);
            _atmosphericScatteringLut = null;
            
        }
        
        private void ReleaseSharedResources()
        {
            if (!_hasSharedResources) return;

            _sharedCache.ReleaseTransmittance(_transmittanceKey);
            _sharedCache.ReleaseMultiScattering(_multiScatteringKey);
            _sharedCache.ReleaseGroundIrradiance(_groundIrradianceKey);

            _transmittance = null;
            _multiScattering = null;
            _groundIrradiance = null;
            _hasSharedResources = false;
        }
    }
}
