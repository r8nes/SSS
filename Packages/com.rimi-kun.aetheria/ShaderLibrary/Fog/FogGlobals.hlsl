#ifndef AETHERIA_FOG_GLOBALS_INCLUDED
#define AETHERIA_FOG_GLOBALS_INCLUDED

float _FogEnabled;
float _VolumetricFogEnabled;
float4 _FogColor;
float4 _FogMipParameters;
float _FogConvolutionMipCount;
float _FogColorMode;
float _FogSkyColorEnabled;
float _FogMaxDistance;
float _HeightFogBaseExtinction;
float3 _HeightFogBaseScattering;
float _HeightFogBaseHeight;
float2 _HeightFogExponents;
float4 _PlanetUpAltitude;
TEXTURECUBE(_FogFilteredSky);

#endif
