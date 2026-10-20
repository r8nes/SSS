#ifndef UNIVERSAL_TRANSPARENT_REFRACTION_SORTING_INCLUDED
#define UNIVERSAL_TRANSPARENT_REFRACTION_SORTING_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RefractionBoundary.hlsl"

void SplitTransparentRefraction(float4 positionCS, inout half4 color, out half4 beforeRefractionColor)
{
    uint2 positionSS = uint2(positionCS.xy);
    bool hasWaterSurface = HasRefractionBoundary(positionSS);
    float waterDepth = LoadRefractionBoundaryDepth(positionSS);
    bool beforeRefraction = hasWaterSurface && IsFragmentBeforeRefractionBoundary(positionCS.z, waterDepth);
    if (beforeRefraction)
    {
#if defined(_BLENDMODE_ADD) || (defined(VFX_BLENDMODE_ADD) && VFX_BLENDMODE_ADD)
        beforeRefractionColor = half4(color.rgb, 0.0);
#elif defined(_ALPHAPREMULTIPLY_ON) || (defined(VFX_BLENDMODE_PREMULTIPLY) && VFX_BLENDMODE_PREMULTIPLY)
        beforeRefractionColor = color;
#else
        beforeRefractionColor = half4(color.rgb * color.a, color.a);
#endif
        color = 0.0;
    }
    else
    {
        beforeRefractionColor = 0.0;
    }
}

#endif
