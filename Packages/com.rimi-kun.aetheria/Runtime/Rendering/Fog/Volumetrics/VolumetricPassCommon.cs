using Aetheria.Runtime.Utils;

namespace Aetheria.Runtime.Rendering.Fog.Volumetrics
{
    internal static class VolumetricPassCommon
    {
        public static void WriteSamplingContext(ref ShaderValueWriter writer, in VolumetricContext context)
        {
            writer.SetVector(ShaderProperties.VolumetricFog.GridParams, context.GridParams);
            writer.SetVector(ShaderProperties.VolumetricFog.DepthParams, context.DepthParams);
            writer.SetVector(ShaderProperties.VolumetricFog.DepthEncodingParams, context.DepthEncodingParams);
            writer.SetVector(ShaderProperties.VolumetricFog.DepthDecodingParams, context.DepthDecodingParams);
            writer.SetVector(ShaderProperties.VolumetricFog.DepthStepParams, context.DepthStepParams);
            writer.SetFloat(ShaderProperties.VolumetricFog.LastSliceDistance, context.LastSliceDistance);
            writer.SetFloat(
                ShaderProperties.VolumetricFog.UseBiquadraticReconstruction,
                context.UseBiquadraticReconstruction ? 1.0f : 0.0f);
            writer.SetVector(ShaderProperties.VolumetricFog.ScreenParams, context.ScreenParams);
            writer.SetVector(ShaderProperties.VolumetricFog.VBufferViewportSize, context.VBufferViewportSize);
            writer.SetVector(ShaderProperties.VolumetricFog.PlanetUpAltitude, context.PlanetUpAltitude);
            writer.SetVector(ShaderProperties.VolumetricFog.ViewDirOriginWS, context.ViewDirOriginWS);
            writer.SetVector(ShaderProperties.VolumetricFog.ViewDirStepXWS, context.ViewDirStepXWS);
            writer.SetVector(ShaderProperties.VolumetricFog.ViewDirStepYWS, context.ViewDirStepYWS);
            writer.SetVector(ShaderProperties.VolumetricFog.ViewForwardWS, context.ViewForwardWS);
        }

        public static void WriteClusteringContext(ref ShaderValueWriter writer, in VolumetricContext context)
        {
            WriteSamplingContext(ref writer, context);
            writer.SetVector(ShaderProperties.VolumetricFog.CameraParams, context.CameraParams);
        }

        public static void WriteIntegrationContext(ref ShaderValueWriter writer, in VolumetricContext context)
        {
            WriteClusteringContext(ref writer, context);
        }

        public static void WriteFogData(
            ref ShaderValueWriter writer,
            in VolumetricFogVolumeData data,
            bool mainLightShadowsActive)
        {
            writer.SetVector(ShaderProperties.VolumetricFog.FogDensityParams, data.DensityParams);
            writer.SetVector(ShaderProperties.VolumetricFog.FogLayerParams, data.LayerParams);
            writer.SetVector(
                ShaderProperties.VolumetricFog.FogScatteringPhaseParams,
                data.ScatteringPhaseParams);
            
            var lightingParams = data.LightingParams;
            lightingParams.w = mainLightShadowsActive ? 1f : 0f;
            
            writer.SetVector(ShaderProperties.VolumetricFog.FogLightingParams, lightingParams);
            writer.SetVector(
                ShaderProperties.VolumetricFog.FogAmbientLightingParams,
                data.AmbientLightingParams);
            writer.SetVector(ShaderProperties.VolumetricFog.FogControlParams, data.FogControlParams);
            writer.SetVector(ShaderProperties.VolumetricFog.FogShadowParams, data.ShadowParams);
        }
    }
}