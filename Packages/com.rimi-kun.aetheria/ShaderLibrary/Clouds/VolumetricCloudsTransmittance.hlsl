#ifndef VOLUMETRIC_CLOUDS_TRANSMITTANCE_INCLUDED
#define VOLUMETRIC_CLOUDS_TRANSMITTANCE_INCLUDED

#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"

float EvaluateVolumetricCloudPerceptualTransmittance(float referenceLuminance, float physicalTransmittance, float perceptualBlend)
{
    if (referenceLuminance > 0.0f)
    {
        float resultLuminance =
            referenceLuminance * rcp(referenceLuminance + 1.0f) * physicalTransmittance;
        resultLuminance = resultLuminance * rcp(1.0f - resultLuminance);
        float finalTransmittance = max(
            resultLuminance * rcp(referenceLuminance),
            pow(physicalTransmittance, 6.0f));
        physicalTransmittance = lerp(
            physicalTransmittance,
            finalTransmittance,
            perceptualBlend);
    }

    return saturate(physicalTransmittance);
}

float EvaluateVolumetricCloudCompositeTransmittance(
    float3 cameraColor,
    float physicalCloudTransmittance,
    float perceptualBlend)
{
    float referenceLuminance = Luminance(max(cameraColor, 0.0f));

    return EvaluateVolumetricCloudPerceptualTransmittance(
        referenceLuminance,
        physicalCloudTransmittance,
        perceptualBlend);
}

#endif
