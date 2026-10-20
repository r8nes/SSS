using System;
using UnityEngine;
using UnityEngine.Rendering;
using Aetheria.Runtime.Data;
using Aetheria.Runtime.Rendering;
using Aetheria.Runtime.Sky;
using Aetheria.Runtime.Rendering.Sky;

namespace Aetheria.Runtime.Overrides
{
    public enum AtmosphereArtisticPhaseMode
    {
        Balanced,
        ZenithWeighted,
        SunsetWeighted,
        NightWeighted,
        Custom
    }

    [Serializable]
    public sealed class AtmosphereSpaceSettings
    {
        public BoolParameter Enabled = new(true);
        public TextureParameter Cubemap = new(null);
        public ColorParameter Tint = new(Color.white, overrideState: true);
        [Min(0f)] public MinFloatParameter Luminance = new(0.00025f, 0f);
        public ClampedFloatParameter Rotation = new(0f, 0f, 360f);
        [Min(0f)] public MinFloatParameter SuppressionStart = new(0.02f, 0f);
        [Min(0f)] public MinFloatParameter SuppressionEnd = new(0.2f, 0f);
        public ClampedFloatParameter SuppressionPower = new(1.5f, 0.1f, 8f);
    }

    [Serializable]
    [VolumeComponentMenu("Aetheria/Sky/Physically Based Sky")]
    [SkyUniqueID((int)SkyType.PhysicallyBased)]
    public sealed class PhysicallyBasedSky : SkySettings, IPostProcessComponent
    {
        [Header("Artistic Sky")]
        public BoolParameter ArtisticSkyEnabled = new(true);
        public ColorParameter SkyHueColor = new(new Color(0.45f, 0.65f, 1f), true);
        public ClampedFloatParameter SkyHueIntensity = new(0f, 0f, 1f);
        public ColorParameter HorizonHueColor = new(new Color(1f, 0.55f, 0.22f), true);
        public ClampedFloatParameter HorizonHueIntensity = new(0f, 0f, 1f);
        public ClampedFloatParameter SkySaturation = new(1f, 0f, 2f);
        public ClampedFloatParameter SkyContrast = new(1f, 0f, 3f);
        public EnumParameter<AtmosphereArtisticPhaseMode> ArtisticSkyPhaseMode = new(AtmosphereArtisticPhaseMode.Balanced);
        public Vector3Parameter ArtisticSkyPhaseWeights = new(Vector3.one);
        public BoolParameter ArtisticPatternEnabled = new(true);
        public ClampedFloatParameter SkyPatternIntensity = new(0f, 0f, 1f);
        public ClampedFloatParameter SkyPatternScale = new(6f, 0.25f, 64f);
        public ClampedFloatParameter SkyPatternHorizontalSize = new(1f, 0.05f, 16f);
        public ClampedFloatParameter SkyPatternVerticalSize = new(1f, 0.05f, 16f);
        public ClampedFloatParameter SkyPatternSaturation = new(0f, 0f, 1f);
        public BoolParameter ArtisticSunsetBoostEnabled = new(true);
        public ClampedFloatParameter SunsetIntensity = new(0f, 0f, 3f);
        public EnumParameter<AtmosphereArtisticPhaseMode> ArtisticSunsetPhaseMode = new(AtmosphereArtisticPhaseMode.SunsetWeighted);
        public Vector3Parameter ArtisticSunsetPhaseWeights = new(new Vector3(0f, 1f, 0f));

        [Header("Artistic Coupling")]
        public BoolParameter AffectEnvironment = new(false);
        public ClampedFloatParameter AffectEnvironmentMix = new(1f, 0f, 1f);

        public AtmosphereSpaceSettings Space = new();

        public MinFloatParameter RayleighScaleHeight = new(8f, 1f);
        public ColorParameter RayleighScatteringColor =
            new(new Color(0.175287f, 0.409607f, 1.0f), overrideState: true);
        public ClampedFloatParameter RayleighScatteringScale = new(0.0331f, 0f, 1f);

        public MinFloatParameter MieScaleHeight = new(1.2f, 0.0001f);
        public ColorParameter MieScatteringColor = new(Color.white, overrideState: true);
        public ClampedFloatParameter MieScatteringScale = new(0.003996f, 0f, 1f);
        public ColorParameter MieAbsorptionColor = new(Color.white, overrideState: true);
        public ClampedFloatParameter MieAbsorptionScale = new(0.000444f, 0f, 0.1f);
        public ClampedFloatParameter MiePhaseG = new(0.8f, 0f, 0.999f);

        public ColorParameter OzoneAbsorptionExtinctionColor =
            new(new Color(0.345561f, 1.0f, 0.045189f), overrideState: true);
        public ClampedFloatParameter OzoneAbsorptionExtinctionScale = new(0.001881f, 0f, 0.3f);
        public ClampedFloatParameter OzoneScale = new(1f, 0f, 4f);

        public ColorParameter GroundAlbedo =
            new(new Color(0.12f, 0.10f, 0.09f), overrideState: true);

        public MinFloatParameter TopRadius = new(6460f, 0f);

        public override bool IsActive() => active;
        public bool IsTileCompatible() => true;

        public override int GetHashCode()
        {
            var hash = new HashCode();
            hash.Add(base.GetHashCode());
            hash.Add(ArtisticSkyEnabled.value);
            hash.Add(SkyHueColor.value);
            hash.Add(SkyHueIntensity.value);
            hash.Add(HorizonHueColor.value);
            hash.Add(HorizonHueIntensity.value);
            hash.Add(SkySaturation.value);
            hash.Add(SkyContrast.value);
            hash.Add(ArtisticSkyPhaseMode.value);
            hash.Add(ArtisticSkyPhaseWeights.value);
            hash.Add(ArtisticPatternEnabled.value);
            hash.Add(SkyPatternIntensity.value);
            hash.Add(SkyPatternScale.value);
            hash.Add(SkyPatternHorizontalSize.value);
            hash.Add(SkyPatternVerticalSize.value);
            hash.Add(SkyPatternSaturation.value);
            hash.Add(ArtisticSunsetBoostEnabled.value);
            hash.Add(SunsetIntensity.value);
            hash.Add(ArtisticSunsetPhaseMode.value);
            hash.Add(ArtisticSunsetPhaseWeights.value);
            hash.Add(AffectEnvironment.value);
            hash.Add(AffectEnvironmentMix.value);
            hash.Add(Space.Enabled.value);
            hash.Add(Space.Cubemap.value ? Space.Cubemap.value.GetInstanceID() : 0);
            hash.Add(Space.Tint.value);
            hash.Add(Space.Luminance.value);
            hash.Add(Space.Rotation.value);
            hash.Add(Space.SuppressionStart.value);
            hash.Add(Space.SuppressionEnd.value);
            hash.Add(Space.SuppressionPower.value);
            hash.Add(RayleighScaleHeight.value);
            hash.Add(RayleighScatteringColor.value);
            hash.Add(RayleighScatteringScale.value);
            hash.Add(MieScaleHeight.value);
            hash.Add(MieScatteringColor.value);
            hash.Add(MieScatteringScale.value);
            hash.Add(MieAbsorptionColor.value);
            hash.Add(MieAbsorptionScale.value);
            hash.Add(MiePhaseG.value);
            hash.Add(OzoneAbsorptionExtinctionColor.value);
            hash.Add(OzoneAbsorptionExtinctionScale.value);
            hash.Add(OzoneScale.value);
            hash.Add(GroundAlbedo.value);
            hash.Add(TopRadius.value);
            return hash.ToHashCode();
        }

        internal override Color EvaluateSurfaceLightColor(Color color, Vector3 lightDirection, Vector3 samplePosition)
        {
            var stack = EnvironmentSettings.StaticLightingVolumeStack;
            var environment = stack?.GetComponent<VisualEnvironment>();
            return Utils.LightResolver.EvaluateSurfaceLightColor(
                color, lightDirection, samplePosition, this, environment);
        }

        public override Type GetSkyRendererType() => typeof(PhysicallyBasedSkyRenderer);
    }
}
