#ifndef WATER_FOAM_UTILITIES_INCLUDED
#define WATER_FOAM_UTILITIES_INCLUDED

Texture2D<float4> _WaterFoamTexture;

float2 EvaluateWaterFoamFlowDirection(float2 positionOS)
{
    float orientation = _WaterReferenceOrientation.x;
#if defined(WATER_RASTER_PATH) && defined(WATER_LOCAL_CURRENT)
    WaterCurrentData currentData;
    EvaluateWaterCurrentData(positionOS, currentData);
    float currentAngle = orientation + currentData.angle;
    return float2(cos(currentAngle), sin(currentAngle));
#elif defined(WATER_RASTER_PATH)
    return float2(cos(orientation), sin(orientation));
#else
    float2 direction = float2(cos(orientation), sin(orientation));
    if (_WaterCurrentMapInfluence > 1e-4)
    {
        WaterCurrentData currentData;
        EvaluateWaterCurrentData(positionOS, currentData);
        float currentAngle = orientation + currentData.angle;
        direction = float2(cos(currentAngle), sin(currentAngle));
    }
    return direction;
#endif
}
#endif
