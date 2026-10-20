using Aetheria.Runtime.Rendering;

namespace Aetheria.Runtime.Water
{
    internal static class WaterProfiling
    {
        internal static readonly AetheriaProfiler.Marker SpectralSimulation =
            AetheriaProfiler.CreateMarker("Water/Simulation", "Spectral Simulation");
        internal static readonly AetheriaProfiler.Marker WaveMipGeneration =
            AetheriaProfiler.CreateMarker("Water/Simulation", "Wave Mip Generation");
        internal static readonly AetheriaProfiler.Marker FoamSimulation =
            AetheriaProfiler.CreateMarker("Water/Simulation", "Foam Simulation");
        internal static readonly AetheriaProfiler.Marker SurfaceGBuffer =
            AetheriaProfiler.CreateMarker("Water/Rendering", "Surface GBuffer");
        internal static readonly AetheriaProfiler.Marker DeferredLighting =
            AetheriaProfiler.CreateMarker("Water/Rendering", "Deferred Lighting");
        internal static readonly AetheriaProfiler.Marker UnderwaterLine =
            AetheriaProfiler.CreateMarker("Water/Underwater", "Line");
        internal static readonly AetheriaProfiler.Marker UnderwaterEffect =
            AetheriaProfiler.CreateMarker("Water/Underwater", "Composite");
    }
}
