using System;
using Aetheria.Runtime.Components.Volumetrics;
using Aetheria.Runtime.Overrides;
using Aetheria.Runtime.Utils;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace Aetheria.Runtime.Rendering.Fog.Volumetrics
{
    internal readonly struct VolumetricFrameData
    {
        public readonly VolumetricRuntimeSettings Settings;
        public readonly VolumetricContext Context;
        public readonly Vector4 AmbientProbeL0;
        public readonly BufferHandle PreviousAmbientProbeBuffer;
        public readonly BufferHandle TargetAmbientProbeBuffer;
        public readonly float AmbientProbeBlendFactor;
        public readonly bool MainLightShadowsActive;
        public readonly VolumetricVolumeCollector Volumes;
        public readonly VolumetricFogMaskCollector FogMasks;
        public readonly FogMainLightData MainLight;

        public VolumetricFrameData(
            VolumetricRuntimeSettings settings,
            VolumetricContext context,
            Vector4 ambientProbeL0,
            BufferHandle previousAmbientProbeBuffer,
            BufferHandle targetAmbientProbeBuffer,
            float ambientProbeBlendFactor,
            bool mainLightShadowsActive,
            VolumetricVolumeCollector volumes,
            VolumetricFogMaskCollector fogMasks,
            FogMainLightData mainLight)
        {
            Settings = settings;
            Context = context;
            AmbientProbeL0 = ambientProbeL0;
            PreviousAmbientProbeBuffer = previousAmbientProbeBuffer;
            TargetAmbientProbeBuffer = targetAmbientProbeBuffer;
            AmbientProbeBlendFactor = ambientProbeBlendFactor;
            MainLightShadowsActive = mainLightShadowsActive;
            Volumes = volumes;
            FogMasks = fogMasks;
            MainLight = mainLight;
        }
    }

    internal sealed class VolumetricFrameDataBuilder : IDisposable
    {
        private static readonly AetheriaProfiler.Marker BuildProfile =
            AetheriaProfiler.CreateMarker("Fog/Volumetrics", "Build Frame Data");

        private static readonly AetheriaProfiler.Marker CollectVolumesProfile =
            AetheriaProfiler.CreateMarker("Fog/Volumetrics", "Collect Local Volumes");

        private static readonly AetheriaProfiler.Marker CollectFogMasksProfile =
            AetheriaProfiler.CreateMarker("Fog/Volumetrics", "Collect Fog Masks");

        private readonly VolumetricCullingContext _disabledCullingContext = new();
        private readonly VolumetricCullingContext _cullingContext = new();
        private readonly VolumetricVolumeCollector _volumeCollector = new();
        private readonly VolumetricFogMaskCollector _fogMaskCollector = new();

        public bool TryBuildFrameData(
            FogVolume fogVolume,
            AtmosphereFrameData atmosphere,
            ContextContainer frameContext,
            Vector4 planetUpAltitude,
            out VolumetricFrameData frameData)
        {
            using var buildScope = BuildProfile.CpuMarker.Auto();
            
            frameData = default;
            var settings = new VolumetricRuntimeSettings(fogVolume);
            
            if (!settings.Enabled)
                return false;

            var cameraData = frameContext.Get<UniversalCameraData>();
            var lightData = frameContext.Get<UniversalLightData>();
            var sky = frameContext.Get<SkyFrameData>();
            var camera = cameraData.camera;
            
            if (!camera)
                return false;

            bool hasFogMasks = VolumetricFogMask.HasAnyActive;
            float volumetricMaxDistance = settings.MaxDistance;
            _cullingContext.Setup(camera, volumetricMaxDistance);

            var context = new VolumetricContext(
                cameraData,
                settings.Quality,
                volumetricMaxDistance,
                planetUpAltitude);
            
            var fogData = settings.FogData;
            
            using (CollectVolumesProfile.CpuMarker.Auto())
                _volumeCollector.Collect(_cullingContext);
            using (CollectFogMasksProfile.CpuMarker.Auto())
                _fogMaskCollector.Collect(hasFogMasks ? _cullingContext : _disabledCullingContext);
            
            var mainLight = new FogMainLightData(atmosphere, lightData);

            bool receiveMainLightShadows = fogData.LightingParams.z > 0.5f;
            bool mainLightShadowsActive = receiveMainLightShadows &&
                                          MainLightHasRealtimeShadows(lightData);
            frameData = new VolumetricFrameData(
                settings,
                context,
                GetAmbientProbeL0(fogData),
                sky.Environment.PreviousAmbientProbe,
                sky.Environment.TargetAmbientProbe,
                sky.Environment.AmbientProbeBlendFactor,
                mainLightShadowsActive,
                _volumeCollector,
                _fogMaskCollector,
                mainLight);
            return true;
        }

        private static Vector4 GetAmbientProbeL0(VolumetricFogVolumeData fogData)
        {
            var probe = RenderSettings.ambientProbe;
            float dimmer = Mathf.Clamp01(fogData.LightingParams.y);
            
            return new Vector4(
                Mathf.Max(probe[0, 0] * dimmer, 0.0f),
                Mathf.Max(probe[1, 0] * dimmer, 0.0f),
                Mathf.Max(probe[2, 0] * dimmer, 0.0f),
                1.0f);
        }

        private static bool MainLightHasRealtimeShadows(UniversalLightData lightData)
        {
            int mainLightIndex = lightData.mainLightIndex;
            if (mainLightIndex < 0 ||
                !lightData.visibleLights.IsCreated ||
                mainLightIndex >= lightData.visibleLights.Length)
                return false;

            var visibleLight = lightData.visibleLights[mainLightIndex];
            var light = visibleLight.light;
            return light && light.enabled && visibleLight.lightType == LightType.Directional &&
                   light.shadows != LightShadows.None && light.shadowStrength > 0.0f;
        }

        public void Dispose()
        {
            _volumeCollector.Dispose();
            _fogMaskCollector.Dispose();
        }
    }
}
