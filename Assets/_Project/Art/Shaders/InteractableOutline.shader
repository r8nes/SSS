Shader "ShooterShop/Interactable Outline"
{
    Properties
    {
        _OutlineColor ("Color", Color) = (1, 0.78, 0.25, 1)
        _OutlineWidthPx ("Width (px at 1080p)", Range(0.5, 12)) = 3
        [IntRange] _StencilRef ("Stencil Ref", Range(1, 255)) = 64
    }

    HLSLINCLUDE
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

    CBUFFER_START(UnityPerMaterial)
        half4 _OutlineColor;
        float _OutlineWidthPx;
        float _StencilRef;
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
        UNITY_VERTEX_OUTPUT_STEREO
    };

    Varyings VertMask(Attributes input)
    {
        Varyings output;
        UNITY_SETUP_INSTANCE_ID(input);
        UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
        output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
        return output;
    }

    Varyings VertOutline(Attributes input)
    {
        Varyings output;
        UNITY_SETUP_INSTANCE_ID(input);
        UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

        float4 positionCS = TransformObjectToHClip(input.positionOS.xyz);
        float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
        float2 normalCS = mul((float3x3)GetWorldToHClipMatrix(), normalWS).xy;
        float lengthCS = max(length(normalCS), 1e-5);
        float2 pixelScale = float2(_ScreenParams.y / _ScreenParams.x, 1.0) * (_ScreenParams.y / 1080.0);
        float2 offset = normalCS / lengthCS * pixelScale * (_OutlineWidthPx * 2.0 / _ScreenParams.y);

        positionCS.xy += offset * positionCS.w;
        output.positionCS = positionCS;
        return output;
    }

    half4 FragNothing(Varyings input) : SV_Target
    {
        return 0;
    }

    half4 FragOutline(Varyings input) : SV_Target
    {
        return _OutlineColor;
    }
    ENDHLSL

    SubShader
    {
        Tags { "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" "Queue" = "Transparent+50" "IgnoreProjector" = "True" }

        Pass
        {
            Name "OutlineMask"
            Tags { "LightMode" = "SRPDefaultUnlit" }
            Cull Back
            ZWrite Off
            ZTest LEqual
            Offset -1, -1
            ColorMask 0

            Stencil
            {
                Ref [_StencilRef]
                Comp Always
                Pass Replace
            }

            HLSLPROGRAM
            #pragma vertex VertMask
            #pragma fragment FragNothing
            #pragma multi_compile_instancing
            ENDHLSL
        }

        Pass
        {
            Name "Outline"
            Tags { "LightMode" = "UniversalForward" }
            Cull Off
            ZWrite Off
            ZTest LEqual

            Stencil
            {
                Ref [_StencilRef]
                Comp NotEqual
                Pass Keep
            }

            HLSLPROGRAM
            #pragma vertex VertOutline
            #pragma fragment FragOutline
            #pragma multi_compile_instancing
            ENDHLSL
        }
    }
}
