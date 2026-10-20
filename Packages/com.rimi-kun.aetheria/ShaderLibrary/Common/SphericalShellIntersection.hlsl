#ifndef SPHERICAL_SHELL_INTERSECTION_INCLUDED
#define SPHERICAL_SHELL_INTERSECTION_INCLUDED

#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/GeometricTools.hlsl"

float ComputeSphericalHorizonCosine(float radialDistance, float planetRadius)
{
    float normalizedRadius = planetRadius * rcp(radialDistance);
    return -SinFromCos(normalizedRadius);
}

bool ExitSphericalShellVolume(
    float radialProjection,
    float radialDistanceSquared,
    float lowerRadius,
    float upperRadius,
    out float tExit)
{
    float projectionSquared = radialProjection * radialProjection;
    tExit = -radialProjection
        + sqrt(max(projectionSquared + upperRadius * upperRadius - radialDistanceSquared, 0.0f));

    return radialProjection >= 0.0f
        || projectionSquared <= radialDistanceSquared - lowerRadius * lowerRadius;
}

bool IntersectSphericalShellVolume(
    float3 originPS,
    float3 direction,
    float lowerRadius,
    float upperRadius,
    float planetRadius,
    out float tEntry,
    out float tExit)
{
    float2 innerIntersection = 0.0f;
    float2 outerIntersection = 0.0f;
    bool hitInner = IntersectRaySphere(originPS, direction, lowerRadius, innerIntersection);
    bool hitOuter = IntersectRaySphere(originPS, direction, upperRadius, outerIntersection);

    tEntry = 0.0f;
    tExit = 0.0f;
    bool intersects = hitOuter && outerIntersection.y >= 0.0f;
    if (intersects)
    {
        float radialDistance = length(originPS);
        float cosChi = dot(originPS, direction) * rcp(max(radialDistance, 1.0e-6f));
        bool startsInsideInnerShell = hitInner && innerIntersection.x < 0.0f && innerIntersection.y >= 0.0f;

        if (startsInsideInnerShell)
        {
            tEntry = innerIntersection.y;
            tExit = outerIntersection.y;
            intersects = cosChi >= ComputeSphericalHorizonCosine(radialDistance, planetRadius);
        }
        else
        {
            tEntry = max(outerIntersection.x, 0.0f);
            tExit = hitInner && innerIntersection.x >= 0.0f
                ? innerIntersection.x
                : outerIntersection.y;
            intersects = tExit >= tEntry;
        }
    }

    return intersects;
}

#endif
