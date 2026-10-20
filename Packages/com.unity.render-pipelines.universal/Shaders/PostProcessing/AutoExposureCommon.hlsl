#ifndef URP_AUTO_EXPOSURE_COMMON_INCLUDED
#define URP_AUTO_EXPOSURE_COMMON_INCLUDED

TEXTURE2D(_AutoExposureTexture);

float4 LoadAutoExposureState()
{
    return LOAD_TEXTURE2D(_AutoExposureTexture, int2(0, 0));
}

float GetAutoExposureStops()
{
    return LoadAutoExposureState().y;
}

float GetAutoExposureMultiplier()
{
    return LoadAutoExposureState().x;
}

float3 ApplyAutoExposure(float3 hdrColor)
{
    return hdrColor * GetAutoExposureMultiplier();
}

#endif
