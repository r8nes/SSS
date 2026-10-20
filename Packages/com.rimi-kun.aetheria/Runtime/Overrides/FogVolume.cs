using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Aetheria.Runtime.Overrides
{
    public enum FogColorMode
    {
        ConstantColor,
        SkyColor
    }

    public enum VolumetricFogGridPreset
    {
        Low,
        Medium,
        High,
        Ultra,
        Custom
    }

    public enum VolumetricFogReconstructionFilter
    {
        Linear,
        Biquadratic
    }

    [Serializable]
    public sealed class FogColorModeParameter : VolumeParameter<FogColorMode>
    {
        public FogColorModeParameter(FogColorMode value, bool overrideState = false)
            : base(value, overrideState) { }
    }

    [Serializable]
    public sealed class VolumetricFogGridPresetParameter : VolumeParameter<VolumetricFogGridPreset>
    {
        public VolumetricFogGridPresetParameter(VolumetricFogGridPreset value, bool overrideState = false)
            : base(value, overrideState) { }
    }

    [Serializable]
    public sealed class VolumetricFogReconstructionFilterParameter : VolumeParameter<VolumetricFogReconstructionFilter>
    {
        public VolumetricFogReconstructionFilterParameter(
            VolumetricFogReconstructionFilter value,
            bool overrideState = false)
            : base(value, overrideState) { }
    }
    
    [Serializable]
    [VolumeComponentMenu("Aetheria/Fog")]
    [SupportedOnRenderPipeline(typeof(UniversalRenderPipelineAsset))]
    public sealed class FogVolume : VolumeComponent, IPostProcessComponent
    {
        public BoolParameter Enabled = new(false, BoolParameter.DisplayType.EnumPopup);
        
        public FogColorModeParameter ColorMode = new(FogColorMode.SkyColor);
        
        public ColorParameter Color = new(UnityEngine.Color.grey, hdr: true, showAlpha: false, showEyeDropper: true);
        public ColorParameter Tint = new(UnityEngine.Color.white, hdr: true, showAlpha: false, showEyeDropper: true);
        
        public MinFloatParameter MaxFogDistance = new(5000.0f, 0.0f);
        
        public ClampedFloatParameter MipFogMaxMip = new(0.5f, 0.0f, 1.0f);

        public MinFloatParameter MipFogNear = new(0.0f, 0.0f);
        public MinFloatParameter MipFogFar = new(1000.0f, 0.0f);

        public FloatParameter BaseHeight = new(0.0f);
        public FloatParameter MaximumHeight = new(50.0f);
        
        public MinFloatParameter MeanFreePath = new(400.0f, 1.0f);

        public ColorParameter Albedo = new(UnityEngine.Color.white, hdr: false, showAlpha: false, showEyeDropper: true);
        
        public BoolParameter EnableVolumetricFog = new(false);
        
        public ClampedFloatParameter VolumetricExtinctionScale = new(1.0f, 0.0f, 10.0f);
        
        public MinFloatParameter DepthExtent = new(64.0f, 0.1f);
        
        public ClampedFloatParameter Anisotropy = new(0.0f, -0.96f, 0.96f);
        
        public BoolParameter ReceiveMainLightShadows = new(true);
        
        public ClampedFloatParameter MainLightShadowDimmer = new(1.0f, 0.0f, 1.0f);
        public ClampedFloatParameter GlobalLightProbeDimmer = new(1.0f, 0.0f, 1.0f);

        public BoolParameter NearDampEnable = new(false);
        public FloatParameter FogStartDistance = new(10.0f);
        public MinFloatParameter DampDistance = new(10.0f, 0.0001f);

        [Header("Volumetric Fog Quality")]
        public VolumetricFogGridPresetParameter GridPreset = new(VolumetricFogGridPreset.High);
        public ClampedIntParameter FroxelWidth = new(160, 16, 512);
        public ClampedIntParameter FroxelHeight = new(90, 16, 512);
        public ClampedIntParameter FroxelDepth = new(64, 8, 256);
        public ClampedFloatParameter SliceDistributionUniformity = new(0.75f, 0.0f, 1.0f);
        public BoolParameter GaussianFilterEnabled = new(true);
        public VolumetricFogReconstructionFilterParameter ReconstructionFilter =
            new(VolumetricFogReconstructionFilter.Linear);
        public BoolParameter MainLightShadowsEnabled = new(true);

        public bool IsActive() =>
            active &&
            Enabled.value &&
            MaxFogDistance.value > 0.0f &&
            MeanFreePath.value > 0.0f;

        public bool IsVolumetricActive() =>
            IsActive() &&
            EnableVolumetricFog.value &&
            DepthExtent.value > 0.0f &&
            VolumetricExtinctionScale.value > 0.0f;

        public bool IsTileCompatible() => false;

        public override int GetHashCode()
        {
            var hash = new HashCode();
            hash.Add(Enabled.value);
            hash.Add(ColorMode.value);
            hash.Add(Color.value);
            hash.Add(Tint.value);
            hash.Add(MaxFogDistance.value);
            hash.Add(MipFogMaxMip.value);
            hash.Add(MipFogNear.value);
            hash.Add(MipFogFar.value);
            hash.Add(BaseHeight.value);
            hash.Add(MaximumHeight.value);
            hash.Add(MeanFreePath.value);
            hash.Add(EnableVolumetricFog.value);
            hash.Add(Albedo.value);
            hash.Add(VolumetricExtinctionScale.value);
            hash.Add(DepthExtent.value);
            hash.Add(Anisotropy.value);
            hash.Add(ReceiveMainLightShadows.value);
            hash.Add(MainLightShadowDimmer.value);
            hash.Add(GlobalLightProbeDimmer.value);
            hash.Add(NearDampEnable.value);
            hash.Add(FogStartDistance.value);
            hash.Add(DampDistance.value);
            hash.Add(GridPreset.value);
            hash.Add(FroxelWidth.value);
            hash.Add(FroxelHeight.value);
            hash.Add(FroxelDepth.value);
            hash.Add(SliceDistributionUniformity.value);
            hash.Add(GaussianFilterEnabled.value);
            hash.Add(ReconstructionFilter.value);
            hash.Add(MainLightShadowsEnabled.value);
            return hash.ToHashCode();
        }
    }
}
