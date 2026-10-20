#ifndef WATER_WAVE_FIELD_INCLUDED
#define WATER_WAVE_FIELD_INCLUDED

#define WATER_WAVE_DISPLACEMENT_SLICE 0
#define WATER_WAVE_DEFORMATION_GRADIENT_SLICE 1
#define WATER_WAVE_ADDITIONAL_DATA_SLICE 2

Texture2DArray<float4> _WaterWaveFieldCascade0;
Texture2DArray<float4> _WaterWaveFieldCascade1;
Texture2DArray<float4> _WaterWaveFieldCascade2;

float4 SampleWaterWaveFieldLevel(float2 uv, int cascadeIndex, int sliceIndex, float mipLevel)
{
    float3 coordinate = float3(uv, sliceIndex);
    float4 value = 0.0;
    if (cascadeIndex == 0)
        value = _WaterWaveFieldCascade0.SampleLevel(sampler_LinearRepeat, coordinate, mipLevel);
    else if (cascadeIndex == 1)
        value = _WaterWaveFieldCascade1.SampleLevel(sampler_LinearRepeat, coordinate, mipLevel);
    else
        value = _WaterWaveFieldCascade2.SampleLevel(sampler_LinearRepeat, coordinate, mipLevel);
    return value;
}

float4 SampleWaterWaveFieldGrad(float2 uv, int cascadeIndex, int sliceIndex, float2 uvDx, float2 uvDy)
{
    float3 coordinate = float3(uv, sliceIndex);
    float4 value = 0.0;
    if (cascadeIndex == 0)
        value = _WaterWaveFieldCascade0.SampleGrad(sampler_LinearRepeat, coordinate, uvDx, uvDy);
    else if (cascadeIndex == 1)
        value = _WaterWaveFieldCascade1.SampleGrad(sampler_LinearRepeat, coordinate, uvDx, uvDy);
    else
        value = _WaterWaveFieldCascade2.SampleGrad(sampler_LinearRepeat, coordinate, uvDx, uvDy);
    return value;
}

struct WaterWaveField
{
    float height;
    float2 shadingSlope;
    float2 geometrySlope;
};

float2 RotateWaterWaveVector(float2 value, float4 rotation)
{
    return float2(
        rotation.x * value.x + rotation.y * value.y,
        rotation.z * value.x + rotation.w * value.y);
}

float4 RotateWaterWaveDerivativeTensor(float4 tensor, float4 rotation)
{
    float a00 = rotation.x * tensor.x + rotation.y * tensor.z;
    float a01 = rotation.x * tensor.y + rotation.y * tensor.w;
    float a10 = rotation.z * tensor.x + rotation.w * tensor.z;
    float a11 = rotation.z * tensor.y + rotation.w * tensor.w;
    return float4(
        a00 * rotation.x + a01 * rotation.y,
        a00 * rotation.z + a01 * rotation.w,
        a10 * rotation.x + a11 * rotation.y,
        a10 * rotation.z + a11 * rotation.w);
}

WaterWaveField RotateWaterWaveField(WaterWaveField input, float4 rotation)
{
    WaterWaveField output;
    output.height = input.height;
    output.shadingSlope = RotateWaterWaveVector(input.shadingSlope, rotation);
    output.geometrySlope = RotateWaterWaveVector(input.geometrySlope, rotation);
    return output;
}

WaterWaveField SampleWaterWaveFieldGrad(float2 uv, int cascadeIndex, float2 uvDx, float2 uvDy)
{
    float4 displacement = SampleWaterWaveFieldGrad(uv, cascadeIndex, WATER_WAVE_DISPLACEMENT_SLICE, uvDx, uvDy);
    float4 additionalData = SampleWaterWaveFieldGrad(uv, cascadeIndex, WATER_WAVE_ADDITIONAL_DATA_SLICE, uvDx, uvDy);
    WaterWaveField field;
    field.height = displacement.x;
    field.shadingSlope = additionalData.xy;
    field.geometrySlope = additionalData.zw;
    return field;
}

#endif
