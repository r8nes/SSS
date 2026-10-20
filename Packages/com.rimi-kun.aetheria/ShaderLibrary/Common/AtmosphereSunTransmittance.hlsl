#ifndef ATMOSPHERE_SUN_TRANSMITTANCE_INCLUDED
#define ATMOSPHERE_SUN_TRANSMITTANCE_INCLUDED

#include "TransmittanceLut.hlsl"

half3 EvaluateAtmosphereSunTransmittance(float3 positionPS, float3 sunDirection, bool estimatePenumbra)
{
    half3 transmittance = 0.0h;
    float radialDistance = max(length(positionPS), 1.0e-5f);
    float radiusKm = max(radialDistance / _DistanceUnitMeter, _BottomRadius + 10.0f / _DistanceUnitMeter);

    float cosTheta = dot(positionPS, sunDirection) / radialDistance;
    float cosHorizon = ComputeSphericalHorizonCosine(radiusKm, _BottomRadius);

    if (cosTheta >= cosHorizon)
    {
        transmittance = SampleTransmittanceLut(radiusKm, cosTheta);

        if (estimatePenumbra)
            transmittance *= saturate((cosTheta - cosHorizon) / 0.0019f);
    }

    return transmittance;
}

#endif
