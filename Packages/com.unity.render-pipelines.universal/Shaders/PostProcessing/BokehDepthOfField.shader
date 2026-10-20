Shader "Hidden/Universal Render Pipeline/BokehDepthOfField"
{
    HLSLINCLUDE
        #pragma multi_compile_local_fragment _ _USE_FAST_SRGB_LINEAR_CONVERSION

        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/Shaders/PostProcessing/Common.hlsl"

        TEXTURE2D_X(_DofTexture);
        float4 _DofTexture_TexelSize;
        half4 _SourceSize;
        half4 _CoCParams;

        #define FocusDist _CoCParams.x
        #define MaxCoC    _CoCParams.y
        #define MaxRadius _CoCParams.z

        half ComputeSignedCoC(float2 uv)
        {
            float depth = SampleSceneDepth(uv);
            float linearEyeDepth = LinearEyeDepth(depth, _ZBufferParams);
            half coc = (1.0 - FocusDist / max(linearEyeDepth, 1e-5)) * MaxCoC;
            return clamp(coc, -1.0, 1.0);
        }

        half4 SampleFusedPostFilterReconstruction(float2 uv)
        {
            float2 halfCoord = uv * _DofTexture_TexelSize.xy - 0.5;
            float2 baseCoord = floor(halfCoord);
            float2 f = frac(halfCoord);

            float2 lowWeight = 0.75 - 0.5 * f;
            float2 highWeight = 0.25 + 0.5 * f;

            float2 lowOffset = -(0.25 * (1.0 - f)) / max(lowWeight, 1e-6);
            float2 highOffset = (0.25 + 0.75 * f) / max(highWeight, 1e-6);

            float2 baseCenter = baseCoord + 0.5;
            float2 uvLL = (baseCenter + float2(lowOffset.x,  lowOffset.y))  * _DofTexture_TexelSize.zw;
            float2 uvHL = (baseCenter + float2(highOffset.x, lowOffset.y))  * _DofTexture_TexelSize.zw;
            float2 uvLH = (baseCenter + float2(lowOffset.x,  highOffset.y)) * _DofTexture_TexelSize.zw;
            float2 uvHH = (baseCenter + float2(highOffset.x, highOffset.y)) * _DofTexture_TexelSize.zw;

            uvLL = ClampUVForBilinear(uvLL, _DofTexture_TexelSize.zw);
            uvHL = ClampUVForBilinear(uvHL, _DofTexture_TexelSize.zw);
            uvLH = ClampUVForBilinear(uvLH, _DofTexture_TexelSize.zw);
            uvHH = ClampUVForBilinear(uvHH, _DofTexture_TexelSize.zw);

            half4 result = 0.0h;
            result += SAMPLE_TEXTURE2D_X(_DofTexture, sampler_LinearClamp, uvLL) * (half)(lowWeight.x  * lowWeight.y);
            result += SAMPLE_TEXTURE2D_X(_DofTexture, sampler_LinearClamp, uvHL) * (half)(highWeight.x * lowWeight.y);
            result += SAMPLE_TEXTURE2D_X(_DofTexture, sampler_LinearClamp, uvLH) * (half)(lowWeight.x  * highWeight.y);
            result += SAMPLE_TEXTURE2D_X(_DofTexture, sampler_LinearClamp, uvHH) * (half)(highWeight.x * highWeight.y);
            return result;
        }

        half4 FragFusedComposite(Varyings input) : SV_Target
        {
            float2 uv = input.texcoord;
            half4 dof = SampleFusedPostFilterReconstruction(uv);
            half coc = ComputeSignedCoC(uv) * MaxRadius;
            float ffa = smoothstep(_SourceSize.w * 2.0, _SourceSize.w * 4.0, coc);

            half4 color = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp,
                ClampUVForBilinear(uv, _BlitTexture_TexelSize.xy));

        #if defined(UNITY_COLORSPACE_GAMMA)
            color = GetSRGBToLinear(color);
        #endif

            half alpha = Max3(dof.r, dof.g, dof.b);
            half blend = (half)(ffa + dof.a - ffa * dof.a);
            half4 outColor = lerp(color, half4(dof.rgb, alpha), blend);

        #if _ENABLE_ALPHA_OUTPUT
            outColor.rgb = color.a > 0 ? outColor.rgb : color.rgb;
            outColor.a = color.a;
        #endif

        #if defined(UNITY_COLORSPACE_GAMMA)
            outColor = GetLinearToSRGB(outColor);
        #endif
            return outColor;
        }
    ENDHLSL

    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }
        LOD 100
        ZTest Always ZWrite Off Cull Off

        Pass
        {
            Name "Bokeh Depth Of Field Fused Composite"

            HLSLPROGRAM
                #pragma vertex Vert
                #pragma fragment FragFusedComposite
                #pragma target 3.5
                #pragma multi_compile_fragment _ _ENABLE_ALPHA_OUTPUT
            ENDHLSL
        }
    }
}
