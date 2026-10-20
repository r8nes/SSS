#ifndef UNIVERSAL_PARTICLES_TRANSPARENT_DEPTH_PREPASS_INCLUDED
#define UNIVERSAL_PARTICLES_TRANSPARENT_DEPTH_PREPASS_INCLUDED

half TransparentDepthPrepassFragment(VaryingsDepthOnlyParticle input) : SV_TARGET
{
    clip(_TransparentDepthPrepassEnable - 0.5);
    return DepthOnlyFragment(input);
}

#endif
