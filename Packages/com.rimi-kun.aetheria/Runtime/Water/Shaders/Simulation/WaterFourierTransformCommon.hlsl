#ifndef WATER_FOURIER_TRANSFORM_COMMON_INCLUDED
#define WATER_FOURIER_TRANSFORM_COMMON_INCLUDED

#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"

RWTexture2D<float4> _FftRealInput;
RWTexture2D<float4> _FftImaginaryInput;
RWTexture2D<float4> _FftRealOutput;
#if !defined(FFT_REAL_OUTPUT_ONLY)
RWTexture2D<float4> _FftImaginaryOutput;
#endif

struct FftComplex4
{
    float4 realPart;
    float4 imaginaryPart;
};

FftComplex4 CreateFftComplex4(float4 realValue, float4 imaginaryValue)
{
    FftComplex4 value;
    value.realPart = realValue;
    value.imaginaryPart = imaginaryValue;
    return value;
}

FftComplex4 AddFftComplex4(FftComplex4 a, FftComplex4 b)
{
    return CreateFftComplex4(a.realPart + b.realPart, a.imaginaryPart + b.imaginaryPart);
}

FftComplex4 SubtractFftComplex4(FftComplex4 a, FftComplex4 b)
{
    return CreateFftComplex4(a.realPart - b.realPart, a.imaginaryPart - b.imaginaryPart);
}

FftComplex4 MultiplyFftComplex4(FftComplex4 value, float cosine, float sine)
{
    return CreateFftComplex4(
        cosine * value.realPart - sine * value.imaginaryPart,
        sine * value.realPart + cosine * value.imaginaryPart);
}

FftComplex4 MultiplyFftComplex4ByTwiddle(FftComplex4 value, float2 twiddle)
{
    return MultiplyFftComplex4(value, twiddle.x, twiddle.y);
}

FftComplex4 MultiplyFftComplex4ByPhase(FftComplex4 value, float phase)
{
    float sine;
    float cosine;
    sincos(phase, sine, cosine);
    return MultiplyFftComplex4(value, cosine, sine);
}

void ApplyInverseFftRadix4Twiddles(inout FftComplex4 value1,
    inout FftComplex4 value2, inout FftComplex4 value3, float phase)
{
    float sine;
    float cosine;
    sincos(phase, sine, cosine);

    float2 twiddle1 = float2(cosine, sine);
    float2 twiddle2 = float2(
        cosine * cosine - sine * sine,
        2.0 * sine * cosine);
    float2 twiddle3 = float2(
        twiddle2.x * cosine - twiddle2.y * sine,
        twiddle2.y * cosine + twiddle2.x * sine);

    value1 = MultiplyFftComplex4ByTwiddle(value1, twiddle1);
    value2 = MultiplyFftComplex4ByTwiddle(value2, twiddle2);
    value3 = MultiplyFftComplex4ByTwiddle(value3, twiddle3);
}

void InverseFft2(inout FftComplex4 value0, inout FftComplex4 value1)
{
    FftComplex4 input0 = value0;
    FftComplex4 input1 = value1;
    value0 = AddFftComplex4(input0, input1);
    value1 = SubtractFftComplex4(input0, input1);
}

void InverseFft4(inout FftComplex4 value0, inout FftComplex4 value1,
    inout FftComplex4 value2, inout FftComplex4 value3)
{
    FftComplex4 evenSum = AddFftComplex4(value0, value2);
    FftComplex4 evenDifference = SubtractFftComplex4(value0, value2);
    FftComplex4 oddSum = AddFftComplex4(value1, value3);
    FftComplex4 oddDifference = SubtractFftComplex4(value1, value3);

    FftComplex4 imaginaryOddDifference = CreateFftComplex4(
        -oddDifference.imaginaryPart, oddDifference.realPart);

    value0 = AddFftComplex4(evenSum, oddSum);
    value2 = SubtractFftComplex4(evenSum, oddSum);
    value1 = AddFftComplex4(evenDifference, imaginaryOddDifference);
    value3 = SubtractFftComplex4(evenDifference, imaginaryOddDifference);
}

FftComplex4 SelectInverseFft4(FftComplex4 value0, FftComplex4 value1,
    FftComplex4 value2, FftComplex4 value3, uint outputIndex)
{
    InverseFft4(value0, value1, value2, value3);
    if (outputIndex == 0u) return value0;
    if (outputIndex == 1u) return value1;
    if (outputIndex == 2u) return value2;
    return value3;
}

uint ExpandFftStockhamIndex(uint index, uint subTransformSize, uint radix)
{
    return (index / subTransformSize) * subTransformSize * radix
        + (index % subTransformSize);
}

#if defined(FFT_TRANSPOSE_COMPLEX_KERNEL)
groupshared float4 g_TransposeReal[16][17];
groupshared float4 g_TransposeImaginary[16][17];

[numthreads(16, 16, 1)]
void FFT_TRANSPOSE_COMPLEX_KERNEL(uint3 groupID : SV_GroupID, uint3 groupThreadID : SV_GroupThreadID)
{
    uint2 sourcePosition = groupID.xy * 16u + groupThreadID.xy;
    g_TransposeReal[groupThreadID.y][groupThreadID.x] = _FftRealInput[int2(sourcePosition)];
    g_TransposeImaginary[groupThreadID.y][groupThreadID.x] = _FftImaginaryInput[int2(sourcePosition)];
    GroupMemoryBarrierWithGroupSync();

    uint2 destinationPosition = groupID.yx * 16u + groupThreadID.xy;
    _FftRealOutput[int2(destinationPosition)] = g_TransposeReal[groupThreadID.x][groupThreadID.y];
    _FftImaginaryOutput[int2(destinationPosition)] = g_TransposeImaginary[groupThreadID.x][groupThreadID.y];
}
#endif

#if defined(FFT_TRANSPOSE_FINAL_KERNEL)
groupshared float4 g_TransposeReal[16][17];

[numthreads(16, 16, 1)]
void FFT_TRANSPOSE_FINAL_KERNEL(uint3 groupID : SV_GroupID, uint3 groupThreadID : SV_GroupThreadID)
{
    uint2 sourcePosition = groupID.xy * 16u + groupThreadID.xy;
    g_TransposeReal[groupThreadID.y][groupThreadID.x] = _FftRealInput[int2(sourcePosition)];
    GroupMemoryBarrierWithGroupSync();

    uint2 destinationPosition = groupID.yx * 16u + groupThreadID.xy;
    float signCorrection = ((destinationPosition.x + destinationPosition.y) & 1u) != 0u ? -1.0 : 1.0;
    _FftRealOutput[int2(destinationPosition)] =
        g_TransposeReal[groupThreadID.x][groupThreadID.y] * signCorrection;
}
#endif

#endif
