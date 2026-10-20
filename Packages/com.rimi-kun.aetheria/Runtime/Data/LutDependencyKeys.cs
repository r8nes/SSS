using System;
using Aetheria.Runtime.Overrides;
using UnityEngine;

namespace Aetheria.Runtime.Data
{
    internal static class LutDependencyKeys
    {
        internal static int Transmittance(PhysicallyBasedSky volume, VisualEnvironment environment)
        {
            var hash = new HashCode();
            hash.Add(environment ? environment.planetRadius.value : 0f);
            hash.Add(volume.TopRadius.value);
            hash.Add(volume.RayleighScaleHeight.value);
            AddRgb(ref hash, volume.RayleighScatteringColor.value);
            hash.Add(volume.RayleighScatteringScale.value);
            hash.Add(volume.MieScaleHeight.value);
            AddRgb(ref hash, volume.MieScatteringColor.value);
            hash.Add(volume.MieScatteringScale.value);
            AddRgb(ref hash, volume.MieAbsorptionColor.value);
            hash.Add(volume.MieAbsorptionScale.value);
            AddRgb(ref hash, volume.OzoneAbsorptionExtinctionColor.value);
            hash.Add(volume.OzoneAbsorptionExtinctionScale.value);
            hash.Add(volume.OzoneScale.value);
            return hash.ToHashCode();
        }

        internal static int MultiScattering(PhysicallyBasedSky volume, int transmittanceKey)
        {
            var hash = new HashCode();
            hash.Add(transmittanceKey);
            AddRgb(ref hash, volume.GroundAlbedo.value);
            hash.Add(LutDefinitions.MultiScatteringSqrtSampleCount);
            hash.Add(LutDefinitions.MultiScatteringRaymarchSampleCount);
            return hash.ToHashCode();
        }

        internal static int GroundIrradiance(int multiScatteringKey)
        {
            var hash = new HashCode();
            hash.Add(multiScatteringKey);
            hash.Add(LutDefinitions.GroundIrradianceSampleCount);
            hash.Add(LutDefinitions.GroundIrradianceWidth);
            hash.Add(LutDefinitions.GroundIrradianceHeight);
            return hash.ToHashCode();
        }

        internal static int SkyView(
            PhysicallyBasedSky volume,
            VisualEnvironment environment,
            int groundIrradianceKey,
            int celestialStateHash,
            int raymarchSamples)
        {
            var hash = new HashCode();
            hash.Add(groundIrradianceKey);
            hash.Add(volume.MiePhaseG.value);
            hash.Add(celestialStateHash);
            hash.Add(environment ? environment.renderingSpace.value : RenderingSpace.World);
            hash.Add(raymarchSamples);
            return hash.ToHashCode();
        }

        private static void AddRgb(ref HashCode hash, Color color)
        {
            hash.Add(color.r);
            hash.Add(color.g);
            hash.Add(color.b);
        }
    }
}
