#ifndef VOLUMETRIC_DEPTH_INCLUDED
#define VOLUMETRIC_DEPTH_INCLUDED

#include "../../Common/VolumetricDepth.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"


float LoadLinearDepthAtPixel(int2 pixel)
{
    pixel = clamp(pixel, int2(0, 0), int2(_VolumetricScreenParams.xy) - int2(1, 1));
    
    return LinearizeClampedVolumetricDepth(
        LoadSceneDepth(uint2(pixel)),
        _VolumetricDepthParams.y);
}

#endif
