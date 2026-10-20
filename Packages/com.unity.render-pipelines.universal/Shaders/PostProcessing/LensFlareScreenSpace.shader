Shader "Hidden/Universal Render Pipeline/LensFlareScreenSpace"
{
    SubShader
    {
        Tags{ "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            Name "LensFlareScreenSpac Prefilter"
            ZWrite Off
            Cull Off
            ZTest Always

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex VertLensFlareURP
            #pragma fragment FragmentPrefilter

            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/UnityInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/PostProcessing/Common.hlsl"
            #define URP_LENS_FLARE_SCREEN_SPACE
            #include "Packages/com.unity.render-pipelines.core/Runtime/PostProcessing/Shaders/LensFlareScreenSpaceCommon.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/PostProcessing/LensFlareScreenSpaceURP.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "LensFlareScreenSpace Downsample"
            ZWrite Off
            Cull Off
            ZTest Always

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex VertLensFlareURP
            #pragma fragment FragmentDownsample

            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/UnityInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/PostProcessing/Common.hlsl"
            #define URP_LENS_FLARE_SCREEN_SPACE
            #include "Packages/com.unity.render-pipelines.core/Runtime/PostProcessing/Shaders/LensFlareScreenSpaceCommon.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/PostProcessing/LensFlareScreenSpaceURP.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "LensFlareScreenSpace Upsample"
            ZWrite Off
            Cull Off
            ZTest Always

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex VertLensFlareURP
            #pragma fragment FragmentUpsample

            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/UnityInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/PostProcessing/Common.hlsl"
            #define URP_LENS_FLARE_SCREEN_SPACE
            #include "Packages/com.unity.render-pipelines.core/Runtime/PostProcessing/Shaders/LensFlareScreenSpaceCommon.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/PostProcessing/LensFlareScreenSpaceURP.hlsl"
            ENDHLSL
        }
    }
}
