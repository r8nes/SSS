namespace Aetheria.Runtime.Rendering
{
    public enum EnvironmentUpdateMode
    {
        OnChanged = 0,
        OnDemand = 1,
        Realtime = 2
    }
    
    public enum LightingSkyResolution
    {
        Resolution256 = 256,
        Resolution512 = 512,
        Resolution1024 = 1024
    }

    internal readonly struct EnvironmentLightingPreset
    {
        public readonly int OutputResolution;
        public readonly int CloudTraceResolution;

        private EnvironmentLightingPreset(int outputResolution)
        {
            OutputResolution = outputResolution;
            CloudTraceResolution = System.Math.Max(8, outputResolution >> 2);
        }

        public static EnvironmentLightingPreset From(LightingSkyResolution resolution) => resolution switch
        {
            LightingSkyResolution.Resolution256 => new EnvironmentLightingPreset(256),
            LightingSkyResolution.Resolution512 => new EnvironmentLightingPreset(512),
            LightingSkyResolution.Resolution1024 => new EnvironmentLightingPreset(1024),
            _ => new EnvironmentLightingPreset(512)
        };
    }
}