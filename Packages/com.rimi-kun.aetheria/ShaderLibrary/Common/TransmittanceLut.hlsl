#ifndef ATMOSPHERE_TRANSMITTANCE_LUT_INCLUDED
#define ATMOSPHERE_TRANSMITTANCE_LUT_INCLUDED

#include "../../Runtime/Data/ShaderConfigs/AtmosphereShaderConfig.cs.hlsl"
#include "../Atmosphere/AtmosphereShaderBindings.hlsl"
#include "SphericalShellIntersection.hlsl"

#define _DistanceUnitMeter ATMOSPHERESHADERCONFIG_DISTANCE_UNIT_METERS

float _BottomRadius;
float _TopRadius;

#define _TransmittanceLutResolution \
    float2(ATMOSPHERESHADERCONFIG_TRANSMITTANCE_LUT_WIDTH, \
           ATMOSPHERESHADERCONFIG_TRANSMITTANCE_LUT_HEIGHT)

TEXTURE2D(ATMOSPHERE_TRANSMITTANCE_LUT);

void LutTransmittanceParamsToUv(in float viewHeight, in float viewZenithCosAngle, out float2 uv)
{
    float H = sqrt(max(0.0f, _TopRadius * _TopRadius - _BottomRadius * _BottomRadius));
    float rho  = sqrt(max(0.0f, viewHeight * viewHeight - _BottomRadius * _BottomRadius));

    float discriminant = viewHeight * viewHeight * (viewZenithCosAngle * viewZenithCosAngle - 1.0)
                       + _TopRadius * _TopRadius;
    float d = max(0.0, -viewHeight * viewZenithCosAngle + sqrt(discriminant));

    float dmin = _TopRadius - viewHeight;
    float dmax = rho + H;
    float mu = (d - dmin) / (dmax - dmin);
    float r = rho / H;

    uv = float2(mu, r);
}

void UvToLutTransmittanceParams(out float viewHeight, out float viewZenithCosAngle, in float2 uv)
{
    float H = sqrt(_TopRadius * _TopRadius - _BottomRadius * _BottomRadius);
    float rho = H * uv.y;
    viewHeight = sqrt(rho * rho + _BottomRadius * _BottomRadius);

    float dmin = _TopRadius - viewHeight;
    float dmax = rho + H;
    float d = dmin + uv.x * (dmax - dmin);

    viewZenithCosAngle = d == 0.0 ? 1.0 : (H * H - rho * rho - d * d) / (2.0 * viewHeight * d);
    viewZenithCosAngle = clamp(viewZenithCosAngle, -1.0, 1.0);
}

float3 SampleTransmittanceLut(float viewHeight_km, float cosTheta)
{
    float2 uv = 0.0f;
    LutTransmittanceParamsToUv(viewHeight_km, cosTheta, uv);
    return SAMPLE_TEXTURE2D_LOD(
        ATMOSPHERE_TRANSMITTANCE_LUT,
        ATMOSPHERE_TRANSMITTANCE_LUT_SAMPLER,
        uv,
        0).rgb;
}

#endif
