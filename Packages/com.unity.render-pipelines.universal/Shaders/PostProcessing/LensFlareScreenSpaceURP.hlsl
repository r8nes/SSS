#ifndef UNIVERSAL_LENS_FLARE_SCREEN_SPACE_VERTEX_INCLUDED
#define UNIVERSAL_LENS_FLARE_SCREEN_SPACE_VERTEX_INCLUDED

VaryingsSSLF VertLensFlareURP(AttributesSSLF input)
{
    VaryingsSSLF output;
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

    output.positionCS = float4(2.0, 2.0, 1.0, 1.0) * GetQuadVertexPosition(input.vertexID) - float4(1.0, 1.0, 0.0, 0.0);
    output.texcoord = GetQuadTexCoord(input.vertexID);
    return output;
}

#endif
