Shader "Hidden/Universal Render Pipeline/UberPost"
{
    HLSLINCLUDE
        #if defined(_DISTORTION_NEGATIVE) || defined(_DISTORTION_POSITIVE)
            #define _DISTORTION 1
        #endif

        #if defined(_CHROMA_CUSTOM_LENS) || defined(_CHROMA_TANGENTIAL)
            #define _CHROMATIC_ABERRATION 1
        #endif

        #if defined(_CHROMA_TANGENTIAL)
            #define _CHROMA_CUSTOM_LENS 1
        #endif

        #ifdef HDR_ENCODING
        #define HDR_INPUT 1 // this should be defined when HDR_ENCODING is defined
        #endif

        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Filtering.hlsl"
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/ScreenCoordOverride.hlsl"
#ifdef HDR_INPUT
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/HDROutput.hlsl"
#endif

        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/Shaders/PostProcessing/Common.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/Shaders/PostProcessing/AutoExposureCommon.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/Shaders/PostProcessing/PaniniProjection.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Debug/DebuggingFullscreen.hlsl"
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/DynamicScalingClamping.hlsl"

        // Hardcoded dependencies to reduce the number of variants
        #if _BLOOM || _BLOOM_DIRT
            #define BLOOM
            #if _BLOOM_DIRT
                #define BLOOM_DIRT
            #endif
        #endif

        #if defined(_HDR_GRADING) || defined(_TONEMAP_ACES) || defined(_TONEMAP_ACES_FILMIC) || defined(_TONEMAP_NEUTRAL) || defined(_TONEMAP_GTSPORT) || defined(_TONEMAP_GT7)
            #define _COLOR_GRADING 1
        #endif

        TEXTURE2D_X(_Bloom_Texture);
        TEXTURE2D_X(_ScreenSpaceLensFlareTexture);
        TEXTURE2D(_LensDirt_Texture);
        TEXTURE2D(_Grain_Texture);
        TEXTURE3D(_InternalLut);
        TEXTURE2D(_UserLut);
        TEXTURE2D(_BlueNoise_Texture);
        TEXTURE2D_X(_OverlayUITexture);
        float4 _BloomTexture_TexelSize;
        float4 _PaniniParams;
        float _PaniniEnabled;
        float4 _Lut_Params;
        float4 _UserLut_Params;
        float4 _Bloom_Params;
        float4 _BloomTint;
        float4 _BloomBicubicParams;
        float4 _Bloom_TextureScaleBias;
        float4 _ScreenSpaceLensFlareTexture_TexelSize;
        float4 _ScreenSpaceLensFlareTextureScaleBias;
        float4 _LensDirt_Params;
        float4 _Distortion_Params1;
        float4 _Distortion_Params2;
        float4 _Chroma_Params;
        float4 _Chroma_RadialCoefficients;
        float4 _Chroma_TangentialCoefficients;
        float4 _Chroma_TapPositions0;
        float4 _Chroma_TapPositions1;
        half4 _Vignette_Params1;
        float4 _Vignette_Params2;
        float2 _Grain_Params;
        float4 _Grain_TilingParams;
        float4 _Dithering_Params;
        float4 _HDROutputLuminanceParams;
        float4 _OffscreenUIViewportParams;

        #define DistCenter              _Distortion_Params1.xy
        #define DistAxis                _Distortion_Params1.zw
        #define DistTheta               _Distortion_Params2.x
        #define DistSigma               _Distortion_Params2.y
        #define DistScale               _Distortion_Params2.z
        #define DistIntensity           _Distortion_Params2.w

        #define ChromaAmount            _Chroma_Params.x
        #define ChromaInverseAspect     _Chroma_Params.y
        #define ChromaCenter            _Chroma_Params.zw
        #define ChromaRadialCoefficients _Chroma_RadialCoefficients.xyz
        #define ChromaAspect            _Chroma_RadialCoefficients.w
        #define ChromaTangentialCoefficients _Chroma_TangentialCoefficients.xy
        #define ChromaInverseMaxRadius  _Chroma_TangentialCoefficients.z
        #define ChromaTangentialScale   _Chroma_TangentialCoefficients.w

        #define BloomIntensity          _Bloom_Params.x
        #define DirtIntensity           _Bloom_Params.y
        #define BloomTint               _BloomTint.xyz
        #define LensDirtScale           _LensDirt_Params.xy
        #define LensDirtOffset          _LensDirt_Params.zw

        #define VignetteColor           _Vignette_Params1.xyz
        #define VignetteCenter          _Vignette_Params2.xy
        #define VignetteIntensity       _Vignette_Params2.z
        #define VignetteSmoothness      _Vignette_Params2.w
        #define VignetteRoundness       _Vignette_Params1.w

        #define LutParams               _Lut_Params.xy
        #define PostExposure            _Lut_Params.w
        #define UserLutParams           _UserLut_Params.xyz
        #define UserLutContribution     _UserLut_Params.w
        #define GrainIntensity          _Grain_Params.x
        #define GrainResponse           _Grain_Params.y
        #define GrainScale              _Grain_TilingParams.xy
        #define GrainOffset             _Grain_TilingParams.zw

        #define DitheringScale          _Dithering_Params.xy
        #define DitheringOffset         _Dithering_Params.zw

        #define AlphaScale              1.0
        #define AlphaBias               0.0

        #define MinNits                 _HDROutputLuminanceParams.x
        #define MaxNits                 _HDROutputLuminanceParams.y
        #define PaperWhite              _HDROutputLuminanceParams.z
        #define OneOverPaperWhite       _HDROutputLuminanceParams.w

        float2 DistortUVPrepared(float2 uv)
        {
            #if _DISTORTION
            {
                float2 ruv = DistAxis * (uv - 0.5 - DistCenter);
                float ru = length(float2(ruv));

                #if _DISTORTION_POSITIVE
                    float wu = ru * DistTheta;
                    ru = tan(wu) * (rcp(ru * DistSigma + HALF_MIN));
                #else
                    ru = rcp(ru) * DistTheta * atan(ru * DistSigma);
                #endif
                uv = uv + ruv * (ru - 1.0);
            }
            #endif

            return uv;
        }

        float2 PrepareDistortionUV(float2 uv)
        {
        #if _DISTORTION
            return (uv - 0.5) * DistScale + 0.5;
        #else
            return uv;
        #endif
        }

        float2 DistortUV(float2 uv)
        {
            return DistortUVPrepared(PrepareDistortionUV(uv));
        }

        float2 PresentationSourceUV(float2 uv)
        {
        #if _PANINI_PROJECTION
            float2 projectedUv = ApplyPaniniProjection(uv, _PaniniParams, 1.0);
        #else
            float2 projectedUv = uv;
        #endif
            return DYNAMIC_SCALING_APPLY_SCALEBIAS(SCREEN_COORD_REMOVE_SCALEBIAS(projectedUv));
        }

        half3 SampleInputLinear(float2 uv)
        {
            float2 sourceUv = PresentationSourceUV(uv);
            half3 color = SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_LinearClamp, ClampUVForBilinear(sourceUv, _BlitTexture_TexelSize.xy), 0.0).xyz;
        #if UNITY_COLORSPACE_GAMMA
            color = GetSRGBToLinear(color);
        #endif
            return color;
        }

        half3 ApplyBloom(half3 color, float2 uv)
        {
        #if defined(BLOOM)
            float2 uvBloom = SCREEN_COORD_REMOVE_SCALEBIAS(uv);
            uvBloom = uvBloom * _Bloom_TextureScaleBias.xy + _Bloom_TextureScaleBias.zw;
            uvBloom = ClampUVForBilinear(uvBloom, _BloomTexture_TexelSize.xy);

            half3 bloom = SampleTexture2DBicubic(TEXTURE2D_X_ARGS(_Bloom_Texture, sampler_LinearClamp), uvBloom, _BloomBicubicParams, (1.0).xx, 0).xyz;

            #if _SCREEN_SPACE_LENS_FLARE
                float2 uvFlare = SCREEN_COORD_REMOVE_SCALEBIAS(uv);
                uvFlare = uvFlare * _ScreenSpaceLensFlareTextureScaleBias.xy + _ScreenSpaceLensFlareTextureScaleBias.zw;
                uvFlare = ClampUVForBilinear(uvFlare, _ScreenSpaceLensFlareTexture_TexelSize.xy);
                // Features are explicitly Gaussian-prefiltered at their native resolution.
                // A single hardware-bilinear reconstruction is therefore the correct final
                // reconstruction stage and avoids re-filtering the low-pass signal in Uber.
                bloom += SAMPLE_TEXTURE2D_X_LOD(
                    _ScreenSpaceLensFlareTexture, sampler_LinearClamp, uvFlare, 0.0).xyz;
            #endif

            #if UNITY_COLORSPACE_GAMMA
            bloom *= bloom;
            #endif

            color += bloom * BloomTint * BloomIntensity;

            #if defined(BLOOM_DIRT)
            half3 dirt = SAMPLE_TEXTURE2D(_LensDirt_Texture, sampler_LinearClamp, uv * LensDirtScale + LensDirtOffset).xyz;
            color += bloom * dirt * DirtIntensity;
            #endif
        #endif
            return color;
        }

        half3 SampleSpectralSceneInputLinear(float2 uv)
        {
            return SampleInputLinear(uv);
        }

        #if _CHROMATIC_ABERRATION
        #define CHROMA_TAP_COUNT 6

        static const float4 kChromaSpectralTapData[CHROMA_TAP_COUNT] =
        {
            float4(-1.20, 0.015, 0.000, 0.360),
            float4(-0.72, 0.025, 0.075, 0.420),
            float4(-0.28, 0.030, 0.340, 0.180),
            float4( 0.12, 0.160, 0.450, 0.035),
            float4( 0.58, 0.420, 0.135, 0.005),
            float4( 1.00, 0.350, 0.000, 0.000)
        };

        static const float4 kChromaReducedMiddleTap =
            float4(-0.06410042, 0.190, 0.790, 0.215);
        static const float3 kChromaReducedShortWeight = float3(0.040, 0.075, 0.780);
        static const float3 kChromaReducedLongWeight = float3(0.770, 0.135, 0.005);

        struct ChromaWarpState
        {
            float2 baseUv;
            float2 uvDisplacement;
        #if _CHROMA_CUSTOM_LENS
            float2 baseSensor;
            float2 sensorDisplacement;
        #endif
        };

        float2 ChromaSensorToPreparedUV(float2 p)
        {
            p.x *= ChromaInverseAspect;
            return PrepareDistortionUV(p * 0.5 + ChromaCenter);
        }

        void BuildChromaWarpState(float2 uv, out ChromaWarpState state)
        {
            float2 centered = (uv - ChromaCenter) * 2.0;
            float r2 = dot(centered, centered);

            #if _CHROMA_CUSTOM_LENS
                float radial = r2 * mad(r2, mad(r2, ChromaRadialCoefficients.z, ChromaRadialCoefficients.y), ChromaRadialCoefficients.x);
            #else
                float radial = r2;
            #endif

            float2 endpointDisplacement = -centered * (radial * ChromaAmount);
        #if _DISTORTION
            state.baseUv = PrepareDistortionUV(uv);
            state.uvDisplacement = endpointDisplacement * DistScale;
        #else
            state.baseUv = uv;
            state.uvDisplacement = endpointDisplacement;
        #endif

        #if _CHROMA_CUSTOM_LENS
            state.baseSensor = centered;
            state.baseSensor.x *= ChromaAspect;
            state.sensorDisplacement = endpointDisplacement * 2.0;
            state.sensorDisplacement.x *= ChromaAspect;
        #endif
        }

        float2 ApplyChromaWarp(ChromaWarpState state, float tapPosition, float normalizedDispersion)
        {
        #if _CHROMA_CUSTOM_LENS
            float2 p = mad(state.sensorDisplacement, tapPosition, state.baseSensor);

        #if _CHROMA_TANGENTIAL
            float2 q = p * ChromaInverseMaxRadius;
            float qr2 = dot(q, q);
            float x = q.x;
            float y = q.y;
            float p1 = ChromaTangentialCoefficients.x;
            float p2 = ChromaTangentialCoefficients.y;
            float2 tangentialOffset = float2(
                p1 * (qr2 + 2.0 * x * x) + 2.0 * p2 * x * y,
                2.0 * p1 * x * y + p2 * (qr2 + 2.0 * y * y));

            p += tangentialOffset * (ChromaTangentialScale * normalizedDispersion);
        #endif

            return DistortUVPrepared(ChromaSensorToPreparedUV(p));
        #else
            return DistortUVPrepared(mad(state.uvDisplacement, tapPosition, state.baseUv));
        #endif
        }

        half3 ApplySpectralChromaticAberration(float2 uv)
        {
            ChromaWarpState state;
            BuildChromaWarpState(uv, state);

            float2 uv0 = ApplyChromaWarp(state, _Chroma_TapPositions0.x, kChromaSpectralTapData[0].x);
            float2 uv5 = ApplyChromaWarp(state, _Chroma_TapPositions1.y, kChromaSpectralTapData[5].x);
            float2 dispersionVectorPixels = (uv0 - uv5) * _BlitTexture_TexelSize.zw;
            float dispersionPixelsSq = dot(dispersionVectorPixels, dispersionVectorPixels);

            UNITY_BRANCH
            if (dispersionPixelsSq < 0.1225)
                return SampleSpectralSceneInputLinear(DistortUV(uv));

            UNITY_BRANCH
            if (dispersionPixelsSq < 1.0)
            {
                float2 uvMiddle = ApplyChromaWarp(
                    state, _Chroma_TapPositions1.z, kChromaReducedMiddleTap.x);
                float3 reduced =
                    SampleSpectralSceneInputLinear(uv0) * (float3)kChromaReducedShortWeight +
                    SampleSpectralSceneInputLinear(uvMiddle) * (float3)kChromaReducedMiddleTap.yzw +
                    SampleSpectralSceneInputLinear(uv5) * (float3)kChromaReducedLongWeight;
                return reduced;
            }

            float3 sum = 0.0;
            sum += SampleSpectralSceneInputLinear(uv0) * (float3)kChromaSpectralTapData[0].yzw;
            sum += SampleSpectralSceneInputLinear(
                ApplyChromaWarp(state, _Chroma_TapPositions0.y, kChromaSpectralTapData[1].x)) *
                (float3)kChromaSpectralTapData[1].yzw;
            sum += SampleSpectralSceneInputLinear(
                ApplyChromaWarp(state, _Chroma_TapPositions0.z, kChromaSpectralTapData[2].x)) *
                (float3)kChromaSpectralTapData[2].yzw;
            sum += SampleSpectralSceneInputLinear(
                ApplyChromaWarp(state, _Chroma_TapPositions0.w, kChromaSpectralTapData[3].x)) *
                (float3)kChromaSpectralTapData[3].yzw;
            sum += SampleSpectralSceneInputLinear(
                ApplyChromaWarp(state, _Chroma_TapPositions1.x, kChromaSpectralTapData[4].x)) *
                (float3)kChromaSpectralTapData[4].yzw;
            sum += SampleSpectralSceneInputLinear(uv5) * (float3)kChromaSpectralTapData[5].yzw;
            return sum;
        }

#endif

        half4 FinalizePresentation(half4 inputColor, half3 color, float2 uv, float2 texcoord, bool applyDithering)
        {
            #if _GAMMA_20 && !UNITY_COLORSPACE_GAMMA
                color = LinearToGamma20(color);
                inputColor = LinearToGamma20(inputColor);
            #elif UNITY_COLORSPACE_GAMMA || _LINEAR_TO_SRGB_CONVERSION
                color = GetLinearToSRGB(color);
                inputColor = LinearToSRGB(inputColor);
            #endif

            #if _DITHERING
            if (applyDithering)
            {
                color = ApplyDithering(color, uv, TEXTURE2D_ARGS(_BlueNoise_Texture, sampler_PointRepeat), DitheringScale, DitheringOffset, PaperWhite, OneOverPaperWhite);
                color = max(color, 0);
            }
            #endif

            #ifdef HDR_ENCODING
                UNITY_BRANCH if (_HDR_OVERLAY)
                {
                    float2 uiCoord = texcoord * _OffscreenUIViewportParams.zw + _OffscreenUIViewportParams.xy;
                    float4 uiSample = SAMPLE_TEXTURE2D_X(_OverlayUITexture, sampler_PointClamp, uiCoord);
                    color.rgb = SceneUIComposition(uiSample, color.rgb, PaperWhite, MaxNits);
                }
            #endif

            #if _ENABLE_ALPHA_OUTPUT
                half alpha = inputColor.a * AlphaScale + AlphaBias;
                color.xyz = lerp(inputColor.xyz, color.xyz, saturate(alpha));
            #endif

            #ifdef HDR_ENCODING
                color.rgb = OETF(color.rgb, MaxNits);
            #endif

            #if defined(DEBUG_DISPLAY)
                half4 debugColor = 0;
                if (CanDebugOverrideOutputColor(half4(color, 1), uv, debugColor))
                    return debugColor;
            #endif

            #if _ENABLE_ALPHA_OUTPUT
                return half4(color, saturate(inputColor.a));
            #else
                return half4(color, 1);
            #endif
        }

        half4 SamplePresentationInput(float2 uv)
        {
            float2 sourceUv = PresentationSourceUV(uv);
            return SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp,
                ClampUVForBilinear(sourceUv, _BlitTexture_TexelSize.xy));
        }

        half4 FragGradePresent(Varyings input) : SV_Target
        {
            float2 uv = SCREEN_COORD_APPLY_SCALEBIAS(DYNAMIC_SCALING_REMOVE_SCALEBIAS(input.texcoord));
            half4 inputColor = SamplePresentationInput(uv);
            half3 color = inputColor.rgb;

            #if UNITY_COLORSPACE_GAMMA
                color = GetSRGBToLinear(color);
                inputColor = GetSRGBToLinear(inputColor);
            #endif

            #if _AUTO_EXPOSURE
                color = ApplyAutoExposure(color);
            #endif
            color *= PostExposure;
            color = ApplyColorGrading(color,
                TEXTURE3D_ARGS(_InternalLut, sampler_LinearClamp), LutParams,
                TEXTURE2D_ARGS(_UserLut, sampler_LinearClamp), UserLutParams, UserLutContribution,
                PaperWhite, OneOverPaperWhite);

            return FinalizePresentation(inputColor, color, uv, input.texcoord, false);
        }

        half4 FragIdentityPresent(Varyings input) : SV_Target
        {
            float2 uv = SCREEN_COORD_APPLY_SCALEBIAS(DYNAMIC_SCALING_REMOVE_SCALEBIAS(input.texcoord));
            half4 inputColor = SamplePresentationInput(uv);
            half3 color = inputColor.rgb;

            #if UNITY_COLORSPACE_GAMMA
                color = GetSRGBToLinear(color);
                inputColor = GetSRGBToLinear(inputColor);
            #endif

            // LDR ApplyTonemap(None) clamps before the neutral internal LUT.
            color = saturate(color);

            return FinalizePresentation(inputColor, color, uv, input.texcoord, false);
        }

        half4 FragUberPost(Varyings input) : SV_Target
        {
            float2 uv = SCREEN_COORD_APPLY_SCALEBIAS(DYNAMIC_SCALING_REMOVE_SCALEBIAS(input.texcoord));
            float2 uvDistorted = DistortUV(uv);

            // NOTE: Hlsl specifies missing input.a to fill 1 (0 for .rgb).
            // InputColor is a "bottom" layer for alpha output.
            #if _CHROMATIC_ABERRATION
                half4 inputColor = half4(0.0, 0.0, 0.0, 1.0);

                #if _ENABLE_ALPHA_OUTPUT
                inputColor = SamplePresentationInput(uvDistorted);
                    #if UNITY_COLORSPACE_GAMMA
                    inputColor = GetSRGBToLinear(inputColor);
                    #endif
                #endif

                half3 color = ApplySpectralChromaticAberration(uv);
                color = ApplyBloom(color, uvDistorted);

                #if defined(BLOOM) && _ENABLE_ALPHA_OUTPUT
                inputColor.xyz = color.xyz;
                #endif
            #else
                half4 inputColor = SamplePresentationInput(uvDistorted);
                half3 color = inputColor.rgb;

                // Gamma space... Just do the rest of Uber in linear and convert back to sRGB at the end
                #if UNITY_COLORSPACE_GAMMA
                {
                    color = GetSRGBToLinear(color);
                    inputColor = GetSRGBToLinear(inputColor);
                }
                #endif

                #if defined(BLOOM)
                {
                    color = ApplyBloom(color, uvDistorted);

                    #if _ENABLE_ALPHA_OUTPUT
                    inputColor.xyz = color.xyz;
                    #endif
                }
                #endif
            #endif

            #if _VIGNETTE
                color = ApplyVignette(color, uvDistorted, VignetteCenter, VignetteIntensity, VignetteRoundness, VignetteSmoothness, VignetteColor);
            #endif

            #if _AUTO_EXPOSURE
                color = ApplyAutoExposure(color);
            #endif
            color *= PostExposure;

            #if _COLOR_GRADING
                color = ApplyColorGrading(color, TEXTURE3D_ARGS(_InternalLut, sampler_LinearClamp), LutParams,
                    TEXTURE2D_ARGS(_UserLut, sampler_LinearClamp), UserLutParams, UserLutContribution,
                    PaperWhite, OneOverPaperWhite);
            #else
                // Scalar exposure does not require a LUT. Clamp the LDR result directly when no
                // tonemapping, grading, user LUT, or HDR output transform is active.
                color = saturate(color);
            #endif

            #if _FILM_GRAIN
            {
                color = ApplyGrain(color, uv, TEXTURE2D_ARGS(_Grain_Texture, sampler_LinearRepeat), GrainIntensity, GrainResponse, GrainScale, GrainOffset, OneOverPaperWhite);
            }
            #endif

            return FinalizePresentation(inputColor, color, uv, input.texcoord, true);
        }

    ENDHLSL

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline"}

        Pass
        {
            Name "UberPost"

            LOD 100
            ZTest Always ZWrite Off Cull Off
            //ColorMask RGB

            HLSLPROGRAM
                #pragma multi_compile_local_fragment _ _DISTORTION_NEGATIVE _DISTORTION_POSITIVE
                #pragma multi_compile_local_fragment _ _CHROMATIC_ABERRATION _CHROMA_CUSTOM_LENS _CHROMA_TANGENTIAL
                #pragma multi_compile_local_fragment _ _PANINI_PROJECTION
                #pragma multi_compile_local_fragment _ _VIGNETTE
                #pragma multi_compile_local_fragment _ _BLOOM _BLOOM_DIRT
                #pragma multi_compile_local_fragment _ _COLOR_GRADING _HDR_GRADING _TONEMAP_ACES _TONEMAP_ACES_FILMIC _TONEMAP_NEUTRAL _TONEMAP_GTSPORT _TONEMAP_GT7
                #pragma multi_compile_local_fragment _ _SCREEN_SPACE_LENS_FLARE
                #pragma multi_compile_local_fragment _ _FILM_GRAIN
                #pragma multi_compile_local_fragment _ _DITHERING
                #pragma multi_compile_local_fragment _ _LINEAR_TO_SRGB_CONVERSION
                #pragma multi_compile_local_fragment _ _USE_FAST_SRGB_LINEAR_CONVERSION
                #pragma multi_compile_local_fragment _ _ENABLE_ALPHA_OUTPUT
                #pragma multi_compile_local_fragment _ _AUTO_EXPOSURE
                #pragma multi_compile_fragment _ DEBUG_DISPLAY
                #pragma multi_compile_fragment _ SCREEN_COORD_OVERRIDE
                #pragma multi_compile_local_fragment _ HDR_INPUT HDR_ENCODING
                #pragma dynamic_branch_local_fragment _ _HDR_OVERLAY
                #pragma vertex Vert
                #pragma fragment FragUberPost
            ENDHLSL
        }



        Pass
        {
            Name "GradePresent"
            LOD 100
            ZTest Always ZWrite Off Cull Off

            HLSLPROGRAM
                #pragma multi_compile_local_fragment _ _HDR_GRADING _TONEMAP_ACES _TONEMAP_ACES_FILMIC _TONEMAP_NEUTRAL _TONEMAP_GTSPORT _TONEMAP_GT7
                #pragma multi_compile_local_fragment _ _LINEAR_TO_SRGB_CONVERSION
                #pragma multi_compile_local_fragment _ _USE_FAST_SRGB_LINEAR_CONVERSION
                #pragma multi_compile_local_fragment _ _ENABLE_ALPHA_OUTPUT
                #pragma multi_compile_local_fragment _ _AUTO_EXPOSURE
                #pragma multi_compile_fragment _ DEBUG_DISPLAY
                #pragma multi_compile_fragment _ SCREEN_COORD_OVERRIDE
                #pragma multi_compile_local_fragment _ HDR_INPUT HDR_ENCODING
                #pragma dynamic_branch_local_fragment _ _HDR_OVERLAY
                #pragma vertex Vert
                #pragma fragment FragGradePresent
            ENDHLSL
        }



        Pass
        {
            Name "IdentityPresent"
            LOD 100
            ZTest Always ZWrite Off Cull Off

            HLSLPROGRAM
                #pragma multi_compile_local_fragment _ _LINEAR_TO_SRGB_CONVERSION
                #pragma multi_compile_local_fragment _ _USE_FAST_SRGB_LINEAR_CONVERSION
                #pragma multi_compile_local_fragment _ _ENABLE_ALPHA_OUTPUT
                #pragma multi_compile_fragment _ DEBUG_DISPLAY
                #pragma multi_compile_fragment _ SCREEN_COORD_OVERRIDE
                #pragma vertex Vert
                #pragma fragment FragIdentityPresent
            ENDHLSL
        }



    }
}
