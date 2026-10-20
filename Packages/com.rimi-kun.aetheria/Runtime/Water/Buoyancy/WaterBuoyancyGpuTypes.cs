using System.Runtime.InteropServices;
using Unity.Mathematics;

namespace Aetheria.Runtime.Water
{
    [StructLayout(LayoutKind.Sequential)]
    internal struct WaterBuoyancyGpuQuery
    {
        internal float3 targetPositionWS;
        internal float tolerance;
        internal float3 startPositionWS;
        internal uint maxIterations;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct WaterBuoyancyGpuResult
    {
        internal float3 targetPositionWS;
        internal uint success;
        internal float3 projectedPositionWS;
        internal float horizontalError;
        internal float3 normalWS;
        internal uint iterationCount;
        internal float3 searchPositionWS;
        internal float searchPadding;
        internal float3 currentDirectionWS;
        internal float currentPadding;
    }
}
