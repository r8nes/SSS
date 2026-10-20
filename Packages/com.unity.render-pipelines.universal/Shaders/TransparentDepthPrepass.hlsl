#ifndef UNIVERSAL_TRANSPARENT_DEPTH_PREPASS_INCLUDED
#define UNIVERSAL_TRANSPARENT_DEPTH_PREPASS_INCLUDED

half TransparentDepthPrepassFragment(Varyings input) : SV_TARGET
{
    clip(_TransparentDepthPrepassEnable - 0.5);
    return DepthOnlyFragment(input);
}

#endif
