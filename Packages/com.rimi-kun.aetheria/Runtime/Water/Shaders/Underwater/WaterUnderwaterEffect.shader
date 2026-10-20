Shader "Hidden/Aetheria/Water/Underwater/Effect"
{
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" }
        ZWrite Off
        Blend Off

        Pass
        {
            Name "Water Underwater Effect"
            Cull Off
            ZTest Always

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex VertUnderwater
            #pragma fragment FragUnderwater
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "WaterUnderwaterEffect.hlsl"
            ENDHLSL
        }
    }
    FallBack Off
}
