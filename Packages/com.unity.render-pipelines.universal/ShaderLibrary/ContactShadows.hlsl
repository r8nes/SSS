#ifndef UNIVERSAL_CONTACT_SHADOWS_INCLUDED
#define UNIVERSAL_CONTACT_SHADOWS_INCLUDED

#define CONTACT_SHADOW_FADE_BITS (8)
#define CONTACT_SHADOW_MASK_BITS (24)
#define CONTACT_SHADOW_FADE_MASK (255)
#define CONTACT_SHADOW_MASK_MASK (16777215)

TYPED_TEXTURE2D(uint, _ContactShadowTexture);

int _MainLightContactShadowMask;
float _ContactShadowOpacity;

void UnpackContactShadowData(uint contactShadowData, out float fade, out uint mask)
{
    fade = float(contactShadowData >> CONTACT_SHADOW_MASK_BITS) / ((float)CONTACT_SHADOW_FADE_MASK);
    mask = contactShadowData & CONTACT_SHADOW_MASK_MASK;
}

uint PackContactShadowData(float fade, uint mask)
{
    uint fadeAsByte = (uint(saturate(fade) * CONTACT_SHADOW_FADE_MASK) << CONTACT_SHADOW_MASK_BITS);
    return fadeAsByte | mask;
}

half GetMainLightContactShadow(float2 screenUV)
{
    if (_MainLightContactShadowMask == 0)
        return half(1.0);

    uint2 screenSize = (uint2)_ScaledScreenParams.xy;
    uint2 positionSS = min((uint2)(screenUV * screenSize), screenSize - 1u);
    uint packedContactShadow = LOAD_TEXTURE2D(_ContactShadowTexture, positionSS).x;
    float fade;
    uint mask;
    UnpackContactShadowData(packedContactShadow, fade, mask);
    bool occluded = (mask & (uint)_MainLightContactShadowMask) != 0;
    return half(1.0 - occluded * fade * _ContactShadowOpacity);
}

#endif
