using Aetheria.Runtime.Data;
using UnityEngine;

namespace Aetheria.Runtime.Rendering
{
    internal static partial class ShaderProperties
    {

        internal static class VolumetricFog
        {

            public static readonly int IntegratedFogTexture =
            Shader.PropertyToID("_VolumetricIntegratedFogTexture");
            public static readonly int FogEnabled = Shader.PropertyToID("_VolumetricFogEnabled");
            public static readonly int UseBiquadraticReconstruction =
            Shader.PropertyToID("_VolumetricUseBiquadraticReconstruction");
            public static readonly int PlanetUpAltitude = Shader.PropertyToID("_VolumetricPlanetUpAltitude");
            public static readonly int IntegratedFogTextureRW =
            Shader.PropertyToID("_VolumetricIntegratedFogTextureRW");
            public static readonly int FilterInput = Shader.PropertyToID("_VolumetricFilterInput");
            public static readonly int FilterOutput = Shader.PropertyToID("_VolumetricFilterOutput");

            public static readonly int FarthestDepthPyramidTexture =
            Shader.PropertyToID("_VolumetricFarthestDepthPyramidTexture");
            public static readonly int FarthestDepthMipParams =
            Shader.PropertyToID("_VolumetricFarthestDepthMipParams");
            public static readonly int FarthestDepthDilationWidth =
            Shader.PropertyToID("_VolumetricFarthestDepthDilationWidth");

            public static readonly int CameraDepthTexture = Shader.PropertyToID("_CameraDepthTexture");

            public static readonly int GridParams = Shader.PropertyToID("_VolumetricGridParams");
            public static readonly int DepthParams = Shader.PropertyToID("_VolumetricDepthParams");
            public static readonly int DepthEncodingParams = Shader.PropertyToID("_VolumetricDepthEncodingParams");
            public static readonly int DepthDecodingParams = Shader.PropertyToID("_VolumetricDepthDecodingParams");
            public static readonly int DepthStepParams = Shader.PropertyToID("_VolumetricDepthStepParams");
            public static readonly int LastSliceDistance = Shader.PropertyToID("_VolumetricLastSliceDistance");
            public static readonly int ScreenParams = Shader.PropertyToID("_VolumetricScreenParams");
            public static readonly int VBufferViewportSize = Shader.PropertyToID("_VolumetricVBufferViewportSize");
            public static readonly int CameraParams = Shader.PropertyToID("_VolumetricCameraParams");
            public static readonly int ViewDirOriginWS = Shader.PropertyToID("_VolumetricViewDirOriginWS");
            public static readonly int ViewDirStepXWS = Shader.PropertyToID("_VolumetricViewDirStepXWS");
            public static readonly int ViewDirStepYWS = Shader.PropertyToID("_VolumetricViewDirStepYWS");
            public static readonly int ViewForwardWS = Shader.PropertyToID("_VolumetricViewForwardWS");

            public static readonly int InputTexture = Shader.PropertyToID("_InputTexture");
            public static readonly int OutputTexture = Shader.PropertyToID("_OutputTexture");
            public static readonly int SrcOffsetAndLimit = Shader.PropertyToID("_SrcOffsetAndLimit");
            public static readonly int DilationWidth = Shader.PropertyToID("_DilationWidth");

            public static readonly int FogDensityParams = Shader.PropertyToID("_VolumetricFogDensityParams");
            public static readonly int FogLayerParams = Shader.PropertyToID("_VolumetricFogLayerParams");
            public static readonly int FogScatteringPhaseParams = Shader.PropertyToID("_VolumetricFogScatteringPhaseParams");
            public static readonly int FogLightingParams = Shader.PropertyToID("_VolumetricFogLightingParams");
            public static readonly int FogAmbientLightingParams =
            Shader.PropertyToID("_VolumetricFogAmbientLightingParams");

            public static readonly int AmbientProbeL0 = Shader.PropertyToID("_VolumetricAmbientProbeL0");
            public static readonly int PreviousAmbientProbeRawBuffer =
            Shader.PropertyToID("_VolumetricPreviousAmbientProbeRawBuffer");
            public static readonly int TargetAmbientProbeRawBuffer =
            Shader.PropertyToID("_VolumetricTargetAmbientProbeRawBuffer");
            public static readonly int AmbientProbeBlendFactor =
            Shader.PropertyToID("_VolumetricAmbientProbeBlendFactor");
            public static readonly int UseGpuAmbientProbe = Shader.PropertyToID("_VolumetricUseGpuAmbientProbe");
            public static readonly int FogShadowParams = Shader.PropertyToID("_VolumetricFogShadowParams");
            public static readonly int FogControlParams = Shader.PropertyToID("_VolumetricFogControlParams");

            public static readonly int MainLightDirection = Shader.PropertyToID("_FogMainLightDirectionWS");
            public static readonly int MainLightColor = Shader.PropertyToID("_FogMainLightColor");
            public static readonly int MainLightShadowmapTexture = Shader.PropertyToID("_MainLightShadowmapTexture");

            public static readonly int MediaTileBuffer = Shader.PropertyToID("_VolumetricMediaTileBuffer");
            public static readonly int MediaTileGridParams =
            Shader.PropertyToID("_VolumetricMediaTileGridParams");
            public static readonly int MediaDispatchTileBuffer =
            Shader.PropertyToID("_VolumetricMediaDispatchTileBuffer");
            public static readonly int MediaDispatchTileOffset =
            Shader.PropertyToID("_VolumetricMediaDispatchTileOffset");
            public static readonly int MediaIndirectArgs =
            Shader.PropertyToID("_VolumetricMediaIndirectArgs");
            public static readonly int MediaBoundsBuffer =
            Shader.PropertyToID("_VolumetricMediaBoundsBuffer");
            public static readonly int MediaCount = Shader.PropertyToID("_VolumetricMediaCount");
            public static readonly int MediaCategory = Shader.PropertyToID("_VolumetricMediaCategory");

            public static readonly int VolumeBuffer = Shader.PropertyToID("_VolumetricVolumeBuffer");
            public static readonly int FogMaskBuffer = Shader.PropertyToID("_VolumetricFogMaskBuffer");

            public static readonly int[] VolumeMaskAtlasTextures =
            CreateIndexedPropertyIds("_VolumetricVolumeMaskAtlas");
            public static readonly int[] FogMaskAtlasTextures =
            CreateIndexedPropertyIds("_VolumetricFogMaskAtlas");
            public static readonly int[] VolumeMaskAtlasParameters =
            CreateIndexedPropertyIds("_VolumetricVolumeMaskAtlasParams");
            public static readonly int[] FogMaskAtlasParameters =
            CreateIndexedPropertyIds("_VolumetricFogMaskAtlasParams");

            private static int[] CreateIndexedPropertyIds(string baseName)
            {
                var ids = new int[(int)VolumetricShaderConfig.AtlasGroupCount];
                ids[0] = Shader.PropertyToID(baseName);
                
                for (int i = 1; i < ids.Length; ++i)
                    ids[i] = Shader.PropertyToID(baseName + i);
                
                return ids;
            }
        }
    }
}
