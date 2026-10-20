#ifndef WATER_SURFACE_PASS_INPUT_INCLUDED
#define WATER_SURFACE_PASS_INPUT_INCLUDED

void SetupWaterInstance() {}

#ifdef UNITY_PROCEDURAL_INSTANCING_ENABLED
struct WaterLodPatchData
{
    float4 positionAxisX;
    float4 axisScaleGrid;
    float4 morphNormalLod;
    float4 edgeTopology;
};
StructuredBuffer<WaterLodPatchData> _WaterLodPatches;
#endif

StructuredBuffer<WaterCameraShaderVariables> _WaterCameraVariables;

struct WaterSurfaceAttributes
{
    float3 positionOS : POSITION;
    float3 normalOS : NORMAL;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct WaterSurfaceVaryings
{
    float4 positionCS : SV_POSITION;
    float3 positionWS : TEXCOORD0;
    float3 normalWS : TEXCOORD1;
    float3 waterUV0 : TEXCOORD2;
    float4 displacement : TEXCOORD3;
    float4 normalLod : TEXCOORD6;
};

#endif
