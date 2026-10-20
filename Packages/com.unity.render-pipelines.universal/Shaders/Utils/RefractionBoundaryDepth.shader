Shader "Hidden/Universal Render Pipeline/Refraction Boundary Depth"
{
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            ZWrite On
            ZTest LEqual
            Cull Off

            Stencil
            {
                Ref 128
                ReadMask 128
                WriteMask 128
                Comp Always
                Pass Replace
            }

            HLSLPROGRAM
            #pragma only_renderers d3d11 vulkan
            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            Texture2D<float> _RefractionBoundaryDepthTexture;

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
            };

            Varyings Vert(uint vertexID : SV_VertexID)
            {
                Varyings output;
                float2 position = float2((vertexID << 1) & 2, vertexID & 2);
                output.positionCS = float4(position * 2.0 - 1.0, 0.0, 1.0);
                return output;
            }

            float Frag(Varyings input) : SV_Depth
            {
                uint2 pixel = uint2(input.positionCS.xy);
                float depth = _RefractionBoundaryDepthTexture.Load(int3(pixel, 0));
                if (depth == UNITY_RAW_FAR_CLIP_VALUE)
                    discard;
                return depth;
            }
            ENDHLSL
        }
    }
}
