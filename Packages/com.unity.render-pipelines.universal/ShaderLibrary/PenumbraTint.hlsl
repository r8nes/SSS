#ifndef UNIVERSAL_PENUMBRA_TINT_INCLUDED
#define UNIVERSAL_PENUMBRA_TINT_INCLUDED

float4 _MainLightShadowTint;

// Port of HDRP's ComputeShadowColor for the main directional light. Tint colors
// the entire shadow by default; the flag switches to HDRP's penumbra-only curve.
half3 ComputeMainLightShadowColor(half shadowAttenuation)
{
    half3 shadowTint = _MainLightShadowTint.rgb;
    half3 invTint = 1.0h - shadowTint;
    half shadow3 = shadowAttenuation * shadowAttenuation * shadowAttenuation;

    return lerp(1.0h - ((1.0h - shadowAttenuation) * invTint),
                shadow3 * invTint + shadowAttenuation * shadowTint,
                _MainLightShadowTint.a);
}

half3 ApplyMainLightShadowTint(half3 lightColor, half shadowAttenuation)
{
    return lightColor * ComputeMainLightShadowColor(shadowAttenuation);
}

#endif
