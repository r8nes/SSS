#ifndef ENVIRONMENT_CUBEMAP_INCLUDED
#define ENVIRONMENT_CUBEMAP_INCLUDED

#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Sampling/Sampling.hlsl"

float3 CubeFaceTexelDirection(uint face, uint2 coord, uint size)
{
    float2 positionNVC = (float2(coord) + 0.5f) / (float)size * 2.0f - 1.0f;
    return CubemapTexelToDirection(positionNVC, face);
}

float3 DirectionToCubeCoordinates(float3 direction)
{
    float3 a = abs(direction);
    uint face;
    float2 uv;

    if (a.x >= a.y && a.x >= a.z)
    {
        face = direction.x >= 0.0f ? 0u : 1u;
        uv = direction.x >= 0.0f
            ? float2(-direction.z, -direction.y) / a.x
            : float2(direction.z, -direction.y) / a.x;
    }
    else if (a.y >= a.z)
    {
        face = direction.y >= 0.0f ? 2u : 3u;
        uv = direction.y >= 0.0f
            ? float2(direction.x, direction.z) / a.y
            : float2(direction.x, -direction.z) / a.y;
    }
    else
    {
        face = direction.z >= 0.0f ? 4u : 5u;
        uv = direction.z >= 0.0f
            ? float2(direction.x, -direction.y) / a.z
            : float2(-direction.x, -direction.y) / a.z;
    }

    return float3(uv * 0.5f + 0.5f, face);
}

#endif
