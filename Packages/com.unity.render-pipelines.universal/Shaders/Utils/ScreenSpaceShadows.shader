Shader "Hidden/Universal Render Pipeline/ScreenSpaceShadows"
{
    SubShader
    {
        Tags{ "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True"}

        HLSLINCLUDE

        //Keep compiler quiet about Shadows.hlsl.
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/EntityLighting.hlsl"
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/ImageBasedLighting.hlsl"
        // Core.hlsl for XR dependencies
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareNormalsTexture.hlsl"

        TEXTURE2D_X_FLOAT(_ScreenSpaceShadowReceiverDepthTexture);
        TEXTURE2D_X_FLOAT(_ScreenSpaceShadowReceiverNormalsTexture);
        int _ScreenSpaceShadowReceiverEnabled;
        int _ScreenSpaceShadowReceiverNormalEncoding;

        float3 DecodeScreenSpaceShadowReceiverNormal(uint2 positionSS)
        {
            float3 encoded = LOAD_TEXTURE2D_X(_ScreenSpaceShadowReceiverNormalsTexture, positionSS).xyz;
            if (_ScreenSpaceShadowReceiverNormalEncoding == 1)
            {
                float2 oct = Unpack888ToFloat2(encoded) * 2.0 - 1.0;
                return UnpackNormalOctQuadEncode(oct);
            }

            return encoded;
        }

        half EvaluateScreenSpaceShadow(float2 uv, float deviceDepth, float3 normalWS)
        {
#if !UNITY_REVERSED_Z
            deviceDepth = deviceDepth * 2.0 - 1.0;
#endif
            float3 positionWS = ComputeWorldSpacePosition(uv, deviceDepth, unity_MatrixInvVP);
            return MainLightRealtimeShadowRaw(
                float4(0.0, 0.0, 0.0, 0.0),
                positionWS,
                normalWS,
                GetMainLightShadowParams(),
                GetMainLightShadowSamplingData(uv));
        }

        half4 Fragment(Varyings input) : SV_Target
        {
            UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

            uint2 positionSS = input.positionCS.xy;
            float sceneDepth = LoadSceneDepth(positionSS);
            half sceneShadow = 1.0h;
            if (COMPARE_DEVICE_DEPTH_CLOSER(sceneDepth, UNITY_RAW_FAR_CLIP_VALUE))
                sceneShadow = EvaluateScreenSpaceShadow(
                    input.texcoord.xy, sceneDepth, SampleSceneNormals(input.texcoord.xy));

            half receiverShadow = 1.0h;
            UNITY_BRANCH
            if (_ScreenSpaceShadowReceiverEnabled != 0)
            {
                float receiverDepth = LOAD_TEXTURE2D_X(
                    _ScreenSpaceShadowReceiverDepthTexture, positionSS).r;
                UNITY_BRANCH
                if (COMPARE_DEVICE_DEPTH_CLOSER(receiverDepth, UNITY_RAW_FAR_CLIP_VALUE))
                {
                    receiverShadow = EvaluateScreenSpaceShadow(
                        input.texcoord.xy,
                        receiverDepth,
                        DecodeScreenSpaceShadowReceiverNormal(positionSS));
                }
            }

            return half4(sceneShadow, receiverShadow, 1.0h, 1.0h);
        }

        ENDHLSL

        Pass
        {
            Name "ScreenSpaceShadows"
            ZTest Always
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma multi_compile _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH

            #pragma vertex   Vert
            #pragma fragment Fragment
            ENDHLSL
        }
    }
}
