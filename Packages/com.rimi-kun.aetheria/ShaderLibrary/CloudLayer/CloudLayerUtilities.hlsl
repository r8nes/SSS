#ifndef CLOUD_LAYER_UTILITIES_INCLUDED
#define CLOUD_LAYER_UTILITIES_INCLUDED

#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
#include "../Common/PhaseFunctions.hlsl"
#include "../Common/SphericalShellIntersection.hlsl"

float2 GetCloudLayerLatLongCoords(float3 direction, bool upperHemisphereOnly)
{
    float2 uv = DirectionToLatLongCoordinate(normalize(direction));
    uv.y = upperHemisphereOnly ? uv.y * 2.0 - 1.0 : uv.y;
    return uv;
}

float3 InverseCloudLayerLatLong(float2 uv, bool upperHemisphereOnly)
{
    uv.y = upperHemisphereOnly ? uv.y * 0.5 + 0.5 : uv.y;
    return LatlongToDirectionCoordinate(uv);
}
#endif
