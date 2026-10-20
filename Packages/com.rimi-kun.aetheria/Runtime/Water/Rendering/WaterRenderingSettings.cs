using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using System;

namespace Aetheria.Runtime.Water
{

    [Serializable, VolumeComponentMenu("Aetheria/Water")]
    [SupportedOnRenderPipeline(typeof(UniversalRenderPipelineAsset))]
    [DisplayInfo(name = "Water")]
    public sealed class WaterRenderingSettings : VolumeComponent
    {

        [Tooltip("When enabled, the water surfaces are rendered.")]
        public BoolParameter enabled = new BoolParameter(false, BoolParameter.DisplayType.EnumPopup);

        [Tooltip("Maximum FFT resolution used by the water simulation.")]
        public EnumParameter<WaterSimulationResolution> simulationResolution =
            new EnumParameter<WaterSimulationResolution>(WaterSimulationResolution.High256);

        [Tooltip("Controls the influence of the ambient light probe on the water surfaces.")]
        public ClampedFloatParameter ambientProbeDimmer = new ClampedFloatParameter(0.5f, 0.0f, 1.0f);

        [Tooltip("Controls the intensity of environment reflections on the water surfaces without affecting screen space reflections.")]
        public ClampedFloatParameter environmentReflectionIntensity = new ClampedFloatParameter(1.0f, 0.0f, 4.0f);

    }
}
