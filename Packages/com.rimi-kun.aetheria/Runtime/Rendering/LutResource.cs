using System;

namespace Aetheria.Runtime.Rendering
{
    [Flags]
    internal enum LutResource : uint
    {
        None = 0,
        Transmittance = 1u << 0,
        MultiScattering = 1u << 1,
        GroundIrradiance = 1u << 2,
        SkyView = 1u << 3,
        AtmosphericScattering = 1u << 4,

        Shared = Transmittance | MultiScattering | GroundIrradiance,
        SkyDependent = SkyView | AtmosphericScattering
    }
}
