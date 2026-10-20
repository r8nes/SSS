Shader "Aetheria/Water/Surface"
{
    Properties
    {
    }

    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }

        HLSLINCLUDE
        #define WATER_USE_CAMERA_DEPTH
        #define WATER_RASTER_PATH 1
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "../Common/WaterCommon.hlsl"
        #include "../../Core/WaterShaderVariables.cs.hlsl"

        TEXTURE2D(_WaterShorelineFoamNoiseMap);
        SAMPLER(sampler_WaterShorelineFoamNoiseMap);

        #include "WaterSurfacePassInput.hlsl"
        #include "WaterSurfaceTransform.hlsl"

        bool ResolveWaterSurfaceFrontFace(
            WaterSurfaceVaryings input,
            FRONT_FACE_TYPE faceSign)
        {
        #if defined(SHADER_STAGE_FRAGMENT)
            
            float3 dpdx = ddx(input.positionWS);
            float3 dpdy = ddy(input.positionWS);
            float3 geometricNormal = cross(dpdx, dpdy);
            float normalLengthSq = dot(geometricNormal, geometricNormal);

            if (normalLengthSq > 1e-10)
            {
                geometricNormal *= rsqrt(normalLengthSq);
                WaterCameraShaderVariables cameraData = _WaterCameraVariables[_WaterSurfaceIndex];
                float3 upWS = GetWaterSurfaceUp();
                if (dot(geometricNormal, upWS) < 0.0)
                    geometricNormal = -geometricNormal;

                return dot(GetCameraPositionWS() - input.positionWS, geometricNormal) >= 0.0;
            }
        #endif

            return IS_FRONT_VFACE(faceSign, true, false);
        }

        ENDHLSL

        Pass
        {
            Name "Water GBuffer"
            Tags { "LightMode"="UniversalForward" }
            Blend Off
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
            #pragma target 4.5
            #pragma vertex VertWater
            #pragma fragment FragWaterGBuffer
            #pragma multi_compile_instancing
            #pragma instancing_options procedural:SetupWaterInstance
            #include "WaterSurfaceGeometry.hlsl"
            #include "WaterGBuffer.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "Water GBuffer Debug"
            Tags { "LightMode"="UniversalForward" }
            Blend Off
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
            #pragma target 4.5
            #pragma vertex VertWater
            #pragma fragment FragWaterGBuffer
            #pragma multi_compile_instancing
            #pragma instancing_options procedural:SetupWaterInstance
            #define WATER_DEBUG_PASS 1
            #include "WaterSurfaceGeometry.hlsl"
            #include "WaterGBuffer.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "Water GBuffer With Current"
            Tags { "LightMode"="UniversalForward" }
            Blend Off
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
            #pragma target 4.5
            #pragma vertex VertWater
            #pragma fragment FragWaterGBuffer
            #pragma multi_compile_instancing
            #pragma instancing_options procedural:SetupWaterInstance
            #define WATER_LOCAL_CURRENT 1
            #include "WaterSurfaceGeometry.hlsl"
            #include "WaterGBuffer.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "Water GBuffer Debug With Current"
            Tags { "LightMode"="UniversalForward" }
            Blend Off
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
            #pragma target 4.5
            #pragma vertex VertWater
            #pragma fragment FragWaterGBuffer
            #pragma multi_compile_instancing
            #pragma instancing_options procedural:SetupWaterInstance
            #define WATER_LOCAL_CURRENT 1
            #define WATER_DEBUG_PASS 1
            #include "WaterSurfaceGeometry.hlsl"
            #include "WaterGBuffer.hlsl"
            ENDHLSL
        }

    }
    FallBack Off
}
