Shader "ShooterShop/Interactable Outline"
{
    Properties
    {
        _OutlineColor ("Color", Color) = (1, 0.78, 0.25, 1)
        _OutlineWidth ("Width (m)", Range(0, 0.05)) = 0.012
    }

    HLSLINCLUDE
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

    CBUFFER_START(UnityPerMaterial)
        half4 _OutlineColor;
        float _OutlineWidth;
    CBUFFER_END

    struct Attributes
    {
        float4 positionOS : POSITION;
        float3 normalOS : NORMAL;
        UNITY_VERTEX_INPUT_INSTANCE_ID
    };

    struct Varyings
    {
        float4 positionCS : SV_POSITION;
        float3 normalWS : TEXCOORD0;
        UNITY_VERTEX_OUTPUT_STEREO
    };

    Varyings Vert(Attributes input)
    {
        Varyings output;
        UNITY_SETUP_INSTANCE_ID(input);
        UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

        float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
        float3 positionWS = TransformObjectToWorld(input.positionOS.xyz) + normalWS * _OutlineWidth;
        output.positionCS = TransformWorldToHClip(positionWS);
        output.normalWS = normalWS;
        return output;
    }

    half4 FragColor(Varyings input) : SV_Target
    {
        return _OutlineColor;
    }

    half4 FragDepth(Varyings input) : SV_Target
    {
        return 0;
    }

    half4 FragDepthNormals(Varyings input) : SV_Target
    {
        return half4(NormalizeNormalPerPixel(-input.normalWS), 0);
    }
    ENDHLSL

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry+10" }
        Cull Front

        Pass
        {
            Name "Outline"
            Tags { "LightMode" = "UniversalForward" }
            ZWrite On
            ZTest LEqual

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragColor
            #pragma multi_compile_instancing
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On
            ColorMask R

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragDepth
            #pragma multi_compile_instancing
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }
            ZWrite On

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragDepthNormals
            #pragma multi_compile_instancing
            ENDHLSL
        }
    }
}
