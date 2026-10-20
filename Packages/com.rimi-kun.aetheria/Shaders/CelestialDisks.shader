Shader "Hidden/Aetheria/Atmosphere/CelestialDisks"
{
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            Name "CelestialDisks"
            ZTest Equal
            ZWrite Off
            Cull Off
            Blend One One
            ColorMask RGB

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment Fragment

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "../ShaderLibrary/Atmosphere/AtmosphereScattering.hlsl"

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                nointerpolation uint bodyIndex : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            float2 DiskVertex(uint vertexID)
            {
                if (vertexID == 1u) return float2(1.0, -1.0);
                if (vertexID == 2u || vertexID == 4u) return float2(1.0, 1.0);
                if (vertexID == 5u) return float2(-1.0, 1.0);
                return float2(-1.0, -1.0);
            }

            Varyings Vert(uint vertexID : SV_VertexID, uint instanceID : SV_InstanceID)
            {
                Varyings output;
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                CelestialBodyData body = GetCelestialBody(instanceID);
                float3 L = -body.forward.xyz;
                float flareSize = body.type == 0 ? max(body.flareSize, 0.0) : 0.0;
                float angularRadius = body.angularRadius + max(flareSize, 0.01);
                float distanceToDisk = max(body.distanceFromCamera, 1.0);
                float radius = tan(angularRadius) * distanceToDisk;
                float2 corner = DiskVertex(vertexID);
                float3 positionWS = _WorldSpaceCameraPos + L * distanceToDisk +
                                    (body.right.xyz * corner.x + body.up.xyz * corner.y) * radius;
                output.positionCS = TransformWorldToHClip(positionWS);
                output.positionCS.z = UNITY_RAW_FAR_CLIP_VALUE * output.positionCS.w;
                output.bodyIndex = instanceID;
                return output;
            }

            float3 RenderGroundCelestial(uint bodyIndex, float3 viewDirWS)
            {
                float viewHeight = _BottomRadius;
                float3 upVector = float3(0.0, 1.0, 0.0);
                float3 viewPos = upVector * viewHeight;
                float3 unusedSpace = 0.0;
                ResolveAtmosphereViewContext(viewDirWS, viewHeight, upVector, viewPos, unusedSpace);

                if (viewHeight >= _TopRadius)
                    return 0.0;

                float tGround = -1.0;
                bool intersectGround = ResolveGroundIntersection(viewPos, viewDirWS, tGround);
                float2 atmosphereIntersections = 0.0;
                bool hitAtmosphere = IntersectRaySphere(viewPos, viewDirWS, _TopRadius, atmosphereIntersections);
                float tExit = hitAtmosphere && atmosphereIntersections.y >= 0.0
                    ? (atmosphereIntersections.x >= 0.0 ? atmosphereIntersections.x : atmosphereIntersections.y)
                    : -1.0;
                float tFrag = 1e30;
                float3 radiance = RenderCelestialDisk(bodyIndex, tFrag, tExit, viewDirWS);
                
                if (all(radiance <= 0.0) || (intersectGround && tFrag >= tGround))
                    return 0.0;

                float viewZenithCosAngle = dot(viewDirWS, upVector);
                float2 transUv = 0.0;
                
                LutTransmittanceParamsToUv(viewHeight, viewZenithCosAngle, transUv);
                
                float3 transmittance = SAMPLE_TEXTURE2D_LOD(
                    ATMOSPHERE_TRANSMITTANCE_LUT,
                    ATMOSPHERE_TRANSMITTANCE_LUT_SAMPLER,
                    transUv,
                    0).rgb;
                
                return radiance * transmittance * _IntensityMultiplier;
            }

            float4 Fragment(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 screenUv = GetNormalizedScreenSpaceUV(input.positionCS);
                float3 viewDirWS = GetCameraFrustumRayWS(screenUv);
                return float4(RenderGroundCelestial(input.bodyIndex, viewDirWS), 0.0);
            }
            ENDHLSL
        }
    }
}