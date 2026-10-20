using System;
using Aetheria.Runtime.Data;
using Aetheria.Runtime.Data.CelestialLight;
using Aetheria.Runtime.Overrides;
using Aetheria.Runtime.Utils;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.RenderGraphModule;
using Object = UnityEngine.Object;

namespace Aetheria.Runtime.Rendering.Passes
{
    public class VolumetricCloudsPass : IDepthPyramidConsumer
    {
        private static readonly AetheriaProfiler.Marker TraceProfile =
            AetheriaProfiler.CreateMarker("Volumetric Clouds", "Trace");

        private static readonly AetheriaProfiler.Marker TemporalResolveProfile =
            AetheriaProfiler.CreateMarker("Volumetric Clouds", "Temporal Resolve");

        public VolumetricClouds Volume;

        private bool _hasAtmosphereContext;
        private AtmosphereFrameData _currentAtmosphereFrameData;

        private sealed class VolumetricCloudHistory : CameraHistoryItem
        {
            private int _lightingId;
            private int _dataId;
            private Hash128 _lightingDescriptorKey;
            private Hash128 _dataDescriptorKey;

            internal bool IsValid;
            internal int Width = -1;
            internal int Height = -1;
            internal int StateHash = int.MinValue;
            internal int PreviousViewportWidth = -1;
            internal int PreviousViewportHeight = -1;
            internal int TargetWidth = -1;
            internal int TargetHeight = -1;
            internal int CameraPixelWidth = -1;
            internal int CameraPixelHeight = -1;
            internal CameraType CameraType;
            internal float ScalableBufferWidthScale = -1.0f;
            internal float ScalableBufferHeightScale = -1.0f;
            internal Matrix4x4 PreviousViewProjection = Matrix4x4.identity;
            internal Matrix4x4 PreviousProjection = Matrix4x4.identity;
            internal bool HasCameraValidityState;
            internal Vector3 PreviousCameraPositionWs;
            internal Vector3 PreviousShapeAnimationOffsetPs;
            internal Vector3 PreviousSunForward = Vector3.forward;
            internal bool HasPreviousFrame;
            internal int AccumulationFrameIndex;
            internal int LastRenderedFrame = -1;

            internal RTHandle CurrentLighting => GetCurrentFrameRT(_lightingId);
            internal RTHandle PreviousLighting => GetPreviousFrameRT(_lightingId);
            internal RTHandle CurrentData => GetCurrentFrameRT(_dataId);
            internal RTHandle PreviousData => GetPreviousFrameRT(_dataId);

            public override void OnCreate(BufferedRTHandleSystem owner, uint typeId)
            {
                base.OnCreate(owner, typeId);
                _lightingId = MakeId(0);
                _dataId = MakeId(1);
            }

            public override void Reset()
            {
                ReleaseHistoryFrameRT(_lightingId);
                ReleaseHistoryFrameRT(_dataId);
                _lightingDescriptorKey = default;
                _dataDescriptorKey = default;
                ResetTemporalState(this);
                Width = -1;
                Height = -1;
                StateHash = int.MinValue;
                LastRenderedFrame = -1;
            }

            internal void Update(RenderTextureDescriptor lightingDescriptor, RenderTextureDescriptor dataDescriptor)
            {
                var lightingKey = Hash128.Compute(ref lightingDescriptor);
                var dataKey = Hash128.Compute(ref dataDescriptor);
                if (lightingKey == _lightingDescriptorKey && dataKey == _dataDescriptorKey &&
                    CurrentLighting != null && CurrentData != null)
                    return;

                ReleaseHistoryFrameRT(_lightingId);
                ReleaseHistoryFrameRT(_dataId);
                AllocHistoryFrameRT(_lightingId, 2, ref lightingDescriptor, "_VolumetricCloudsHistoryLighting");
                AllocHistoryFrameRT(_dataId, 2, ref dataDescriptor, "_VolumetricCloudsHistoryData");
                _lightingDescriptorKey = lightingKey;
                _dataDescriptorKey = dataKey;
                ResetTemporalState(this);
            }

            internal void CommitFrame(
                Matrix4x4 viewProjection,
                Vector3 cameraPositionWs,
                int viewportWidth,
                int viewportHeight,
                CameraType cameraType,
                int targetWidth,
                int targetHeight,
                int cameraPixelWidth,
                int cameraPixelHeight,
                float scalableBufferWidthScale,
                float scalableBufferHeightScale,
                Matrix4x4 projection,
                Vector3 shapeAnimationOffsetPs,
                Vector3 sunForward,
                bool hasSun)
            {
                IsValid = true;
                PreviousViewProjection = viewProjection;
                PreviousCameraPositionWs = cameraPositionWs;
                PreviousViewportWidth = viewportWidth;
                PreviousViewportHeight = viewportHeight;
                HasPreviousFrame = true;
                CameraType = cameraType;
                TargetWidth = targetWidth;
                TargetHeight = targetHeight;
                CameraPixelWidth = cameraPixelWidth;
                CameraPixelHeight = cameraPixelHeight;
                ScalableBufferWidthScale = scalableBufferWidthScale;
                ScalableBufferHeightScale = scalableBufferHeightScale;
                PreviousProjection = projection;
                PreviousShapeAnimationOffsetPs = shapeAnimationOffsetPs;
                HasCameraValidityState = true;
                AccumulationFrameIndex = (AccumulationFrameIndex + 1) % TemporalFrameCount;
                LastRenderedFrame = Time.frameCount;
                if (hasSun)
                    PreviousSunForward = sunForward;
            }
        }

        private readonly ComputeShader _traceShader;
        private readonly ComputeShader _denoisingShader;
        private readonly VolumetricCloudResources _resources;

        private readonly int _traceKernel;
        private readonly int _traceMicroErosionKernel;
        private readonly int _traceSparseFullResolutionKernel;
        private readonly int _traceSparseFullResolutionMicroErosionKernel;
        private readonly int _resolveHistoryKernel;
        private readonly int _resolveHistoryGhostReductionKernel;
        private readonly int _resolveHistorySparseFullResolutionKernel;
        private readonly int _resolveHistorySparseFullResolutionGhostReductionKernel;
        private readonly int _resolveNoHistoryKernel;

        private const string TracedLightingName = "_VolumetricCloudsTracedLighting";
        private const string TracedDataName = "_VolumetricCloudsTracedData";

        private const int TemporalFrameCount = 64;

        public const int CustomLutMapResolution = (int)CloudShaderConfig.TransferLutResolution;

        private Texture2D _customLutPresetMap;
        private RenderGraphTextureHandleCache _curveLutCache;
        private RenderGraphTextureHandleCache _cloudIntegrationNoiseCache;
        private RenderGraphTextureHandleCache _cloudShapeNoiseCache;
        private RenderGraphTextureHandleCache _cloudDetailNoiseCache;

        private readonly Color[] _customLutColorArray = new Color[CustomLutMapResolution];

        private int _cachedLutHash = int.MinValue;

        private bool _customLutInitialized;

        private float _prevTotalTime = -1f;
        private float _verticalShapeOffset;
        private float _verticalErosionOffset;

        private Vector2 _windVector = Vector2.zero;
        private FrameParameters _frameParameters;

        private readonly struct CloudViewDirectionBasis
        {
            public readonly Vector4 OriginWS;
            public readonly Vector4 StepXWS;
            public readonly Vector4 StepYWS;

            public CloudViewDirectionBasis(Vector3 originWS, Vector3 stepXWS, Vector3 stepYWS)
            {
                OriginWS = new Vector4(originWS.x, originWS.y, originWS.z, 0.0f);
                StepXWS = new Vector4(stepXWS.x, stepXWS.y, stepXWS.z, 0.0f);
                StepYWS = new Vector4(stepYWS.x, stepYWS.y, stepYWS.z, 0.0f);
            }
        }

        private readonly struct DepthPyramidBindings
        {
            internal readonly TextureHandle Texture;
            internal readonly BufferHandle MipLevelOffsets;
            internal readonly BufferHandle MipLevelOffsetsCheckerboard;
            internal readonly int MipCount;
            internal readonly int CheckerboardMipCount;
            internal readonly Vector4 Mip0Size;
            internal readonly Vector4 AtlasSize;
            internal readonly Vector4 ChannelMode;

            internal DepthPyramidBindings(
                TextureHandle texture,
                BufferHandle mipLevelOffsets,
                BufferHandle mipLevelOffsetsCheckerboard,
                int mipCount,
                int checkerboardMipCount,
                Vector4 mip0Size,
                Vector4 atlasSize,
                Vector4 channelMode)
            {
                Texture = texture;
                MipLevelOffsets = mipLevelOffsets;
                MipLevelOffsetsCheckerboard = mipLevelOffsetsCheckerboard;
                MipCount = mipCount;
                CheckerboardMipCount = checkerboardMipCount;
                Mip0Size = mip0Size;
                AtlasSize = atlasSize;
                ChannelMode = channelMode;
            }
        }

        private class TracePassData
        {
            internal ComputeShader Shader;
            internal int Kernel;
            internal FrameParameters Parameters;
            internal TextureHandle OutputLighting;
            internal TextureHandle OutputData;
            internal DepthPyramidBindings DepthPyramid;
            internal TextureHandle CurveLut;
            internal TextureHandle CloudIntegrationNoise;
            internal TextureHandle CloudShapeNoise;
            internal TextureHandle CloudDetailNoise;
            internal TextureHandle TransmittanceLut;
            internal TextureHandle AerialPerspectiveLut;
            internal Vector4 OutputSize;
            internal Vector4 IntermediateSize;
            internal Vector4 FinalSize;
            internal int IntermediateToFinalScale;
            internal Vector4 ViewDirOriginWS;
            internal Vector4 ViewDirStepXWS;
            internal Vector4 ViewDirStepYWS;
            internal float NearPlane;
            internal float AtmosphereBottomRadius;
            internal float AtmosphereTopRadius;
            internal int AccumulationFrameIndex;
            internal int SubPixelIndex;
            internal bool SparseFullResolution;
        }

        private class TemporalResolvePassData
        {
            internal ComputeShader Shader;
            internal int Kernel;

            internal TextureHandle SourceLighting;
            internal TextureHandle SourceData;
            internal DepthPyramidBindings DepthPyramid;
            internal TextureHandle PreviousHistoryLighting;
            internal TextureHandle PreviousHistoryData;
            internal TextureHandle OutputLighting;
            internal TextureHandle OutputData;

            internal Vector4 SourceSize;
            internal Vector4 OutputSize;
            internal Vector4 FullResolutionSize;
            internal int OutputToFullResolutionScale;
            internal Vector4 HistoryViewportScale;
            internal bool HistoryValid;
            internal Matrix4x4 PreviousViewProjection;
            internal Matrix4x4 CurrentViewProjection;
            internal Vector4 ViewDirOriginWS;
            internal Vector4 ViewDirStepXWS;
            internal Vector4 ViewDirStepYWS;
            internal float NearPlaneReprojection;
            internal float NearPlane;
            internal float HistoryInvalidation;
            internal float TemporalAccumulationFactor;
            internal Vector4 CloudHistoryPositionOffsetWS;
            internal Vector3 ShapeAnimationOffsetPs;
            internal int SubPixelIndex;
            internal VolumetricCloudHistory History;
            internal Vector3 CameraPositionWs;
            internal CameraType CameraType;
            internal int TargetWidth;
            internal int TargetHeight;
            internal int CameraPixelWidth;
            internal int CameraPixelHeight;
            internal float ScalableBufferWidthScale;
            internal float ScalableBufferHeightScale;
            internal Matrix4x4 Projection;
            internal Vector3 SunForward;
            internal bool HasSun;
        }

        internal struct FrameParameters : IShaderValueSource
        {
            public float MaxStepSize;
            public int NumPrimarySteps;
            public int NumLightSteps;
            public float HighestAltitude;
            public float LowestAltitude;
            public Vector4 PlanetCenterWS;
            public float PlanetRadius;
            public float CameraSpace;
            public Vector4 ShapeNoiseOffset;
            public float VerticalShapeNoiseOffset;
            public Vector4 WindDirection;
            public Vector4 WindVector;
            public float VerticalShapeDisplacement;
            public float VerticalErosionDisplacement;
            public float MediumWindSpeed;
            public float SmallWindSpeed;
            public float AltitudeDistortion;
            public float DensityMultiplier;
            public float PowderEffectIntensity;
            public float MultiScattering;
            public float ShapeScale;
            public float ShapeFactor;
            public float ErosionScale;
            public float ErosionFactor;
            public float ErosionOcclusion;
            public float MicroErosionScale;
            public float MicroErosionFactor;
            public float FadeInStart;
            public float FadeInDistance;
            public Vector4 ScatteringTint;
            public Vector4 SunColor;
            public Vector4 SunDirection;
            public Vector4 AmbientTop;
            public Vector4 AmbientBottom;
            public Vector4 AmbientSHAr;
            public Vector4 AmbientSHAg;
            public Vector4 AmbientSHAb;
            public Vector4 AmbientSHBr;
            public Vector4 AmbientSHBg;
            public Vector4 AmbientSHBb;
            public Vector4 AmbientSHC;

            public void WriteTo(ref ShaderValueWriter writer)
            {
                writer.SetFloat(ShaderProperties.Clouds.MaxStepSize, MaxStepSize);
                writer.SetInt(ShaderProperties.Clouds.NumPrimarySteps, NumPrimarySteps);
                writer.SetInt(ShaderProperties.Clouds.NumLightSteps, NumLightSteps);
                writer.SetFloat(ShaderProperties.Clouds.HighestAltitude, HighestAltitude);
                writer.SetFloat(ShaderProperties.Clouds.LowestAltitude, LowestAltitude);
                float lowestRadiusSquared = LowestAltitude * LowestAltitude;
                float highestRadiusSquared = HighestAltitude * HighestAltitude;
                writer.SetVector(
                    ShaderProperties.Clouds.AltitudeSquaredParams,
                    new Vector4(
                        lowestRadiusSquared,
                        1.0f / (highestRadiusSquared - lowestRadiusSquared),
                        LowestAltitude,
                        1.0f / (HighestAltitude - LowestAltitude)));
                writer.SetVector(ShaderProperties.Clouds.PlanetCenterWS, PlanetCenterWS);
                writer.SetFloat(ShaderProperties.Clouds.PlanetRadius, PlanetRadius);
                writer.SetFloat(ShaderProperties.Clouds.CameraSpace, CameraSpace);
                writer.SetVector(ShaderProperties.Clouds.ShapeNoiseOffset, ShapeNoiseOffset);
                writer.SetFloat(ShaderProperties.Clouds.VerticalShapeNoiseOffset, VerticalShapeNoiseOffset);
                writer.SetVector(ShaderProperties.Clouds.WindDirection, WindDirection);
                writer.SetVector(ShaderProperties.Clouds.WindVector, WindVector);
                writer.SetFloat(ShaderProperties.Clouds.VerticalShapeDisplacement, VerticalShapeDisplacement);
                writer.SetFloat(ShaderProperties.Clouds.VerticalErosionDisplacement, VerticalErosionDisplacement);
                writer.SetFloat(ShaderProperties.Clouds.MediumWindSpeed, MediumWindSpeed);
                writer.SetFloat(ShaderProperties.Clouds.SmallWindSpeed, SmallWindSpeed);
                writer.SetFloat(ShaderProperties.Clouds.AltitudeDistortion, AltitudeDistortion);
                writer.SetFloat(ShaderProperties.Clouds.DensityMultiplier, DensityMultiplier);
                writer.SetFloat(ShaderProperties.Clouds.ShapeFactor, ShapeFactor);
                writer.SetFloat(ShaderProperties.Clouds.ErosionFactor, ErosionFactor);
                writer.SetFloat(ShaderProperties.Clouds.MicroErosionFactor, MicroErosionFactor);
                writer.SetFloat(ShaderProperties.Clouds.PowderEffectIntensity, PowderEffectIntensity);
                writer.SetFloat(ShaderProperties.Clouds.MultiScattering, MultiScattering);
                const float inverseNoiseNormalization = 1.0f / 100000.0f;
                writer.SetVector(
                    ShaderProperties.Clouds.NoiseScales,
                    new Vector4(
                        ShapeScale * inverseNoiseNormalization,
                        ErosionScale * inverseNoiseNormalization,
                        MicroErosionScale * inverseNoiseNormalization,
                        0.0f));
                writer.SetFloat(ShaderProperties.Clouds.ErosionOcclusion, ErosionOcclusion);
                writer.SetFloat(ShaderProperties.Clouds.FadeInStart, FadeInStart);
                writer.SetVector(
                    ShaderProperties.Clouds.DistanceReciprocals,
                    new Vector4(
                        1.0f / (FadeInStart + FadeInDistance),
                        1.0f / (100000.0f - 3000.0f),
                        1.0f / AtmospherePlanet.DistanceUnitMeters,
                        0.0f));
                writer.SetVector(ShaderProperties.Clouds.ScatteringTint, ScatteringTint);
                writer.SetVector(ShaderProperties.Clouds.SunColor, SunColor);
                writer.SetVector(ShaderProperties.Clouds.SunDirection, SunDirection);
                writer.SetVector(ShaderProperties.Clouds.AmbientTop, AmbientTop);
                writer.SetVector(ShaderProperties.Clouds.AmbientBottom, AmbientBottom);
                writer.SetVector(ShaderProperties.Clouds.AmbientSHAr, AmbientSHAr);
                writer.SetVector(ShaderProperties.Clouds.AmbientSHAg, AmbientSHAg);
                writer.SetVector(ShaderProperties.Clouds.AmbientSHAb, AmbientSHAb);
                writer.SetVector(ShaderProperties.Clouds.AmbientSHBr, AmbientSHBr);
                writer.SetVector(ShaderProperties.Clouds.AmbientSHBg, AmbientSHBg);
                writer.SetVector(ShaderProperties.Clouds.AmbientSHBb, AmbientSHBb);
                writer.SetVector(ShaderProperties.Clouds.AmbientSHC, AmbientSHC);
            }
        }

        internal readonly struct EnvironmentPayload
        {
            public readonly FrameParameters Parameters;
            public readonly Vector3 OriginWs;
            public readonly bool MicroErosion;

            public EnvironmentPayload(
                FrameParameters parameters,
                Vector3 originWs,
                bool microErosion)
            {
                Parameters = parameters;
                OriginWs = originWs;
                MicroErosion = microErosion;
            }
        }

        internal readonly struct EnvironmentState
        {
            public readonly EnvironmentPayload Payload;
            public readonly Texture2D CurveLut;
            public readonly Texture2D CloudIntegrationNoise;
            public readonly Texture3D CloudShapeNoise;
            public readonly Texture3D CloudDetailNoise;

            public EnvironmentState(
                EnvironmentPayload payload,
                Texture2D curveLut,
                Texture2D cloudIntegrationNoise,
                Texture3D cloudShapeNoise,
                Texture3D cloudDetailNoise)
            {
                Payload = payload;
                CurveLut = curveLut;
                CloudIntegrationNoise = cloudIntegrationNoise;
                CloudShapeNoise = cloudShapeNoise;
                CloudDetailNoise = cloudDetailNoise;
            }
        }

        internal readonly struct EnvironmentData
        {
            public readonly EnvironmentPayload Payload;
            public readonly TextureHandle CurveLut;
            public readonly TextureHandle CloudIntegrationNoise;
            public readonly TextureHandle CloudShapeNoise;
            public readonly TextureHandle CloudDetailNoise;

            public EnvironmentData(
                EnvironmentPayload payload,
                TextureHandle curveLut,
                TextureHandle cloudIntegrationNoise,
                TextureHandle cloudShapeNoise,
                TextureHandle cloudDetailNoise)
            {
                Payload = payload;
                CurveLut = curveLut;
                CloudIntegrationNoise = cloudIntegrationNoise;
                CloudShapeNoise = cloudShapeNoise;
                CloudDetailNoise = cloudDetailNoise;
            }
        }

        private static void BindNoiseTextures(
            ComputeCommandBuffer cmd,
            ComputeShader shader,
            int kernel,
            TextureHandle curveLut,
            TextureHandle cloudIntegrationNoise,
            TextureHandle cloudShapeNoise,
            TextureHandle cloudDetailNoise)
        {
            var writer = ShaderValueWriter.ForCompute(cmd, shader);
            cmd.SetComputeTextureParam(shader, kernel, ShaderProperties.Clouds.CurveLut, curveLut);
            cmd.SetComputeTextureParam(shader, kernel, ShaderProperties.Clouds.IntegrationNoise, cloudIntegrationNoise);
            cmd.SetComputeTextureParam(shader, kernel, ShaderProperties.Clouds.CloudShapeNoise, cloudShapeNoise);
            cmd.SetComputeTextureParam(shader, kernel, ShaderProperties.Clouds.CloudDetailNoise, cloudDetailNoise);
        }

        private static void BindTraceParameters(ComputeCommandBuffer cmd, TracePassData data)
        {
            var shader = data.Shader;
            int kernel = data.Kernel;
            var p = data.Parameters;

            var writer = ShaderValueWriter.ForCompute(cmd, shader);
            writer.Write(p);

            writer.SetVector(ShaderProperties.Clouds.ViewDirOriginWS, data.ViewDirOriginWS);
            writer.SetVector(ShaderProperties.Clouds.ViewDirStepXWS, data.ViewDirStepXWS);
            writer.SetVector(ShaderProperties.Clouds.ViewDirStepYWS, data.ViewDirStepYWS);
            writer.SetInt(ShaderProperties.Clouds.AccumulationFrameIndex, data.AccumulationFrameIndex);
            writer.SetInt(ShaderProperties.Clouds.SubPixelIndex, data.SubPixelIndex);
            writer.SetVector(ShaderProperties.Clouds.TraceSize, data.OutputSize);
            writer.SetVector(ShaderProperties.Clouds.IntermediateSize, data.IntermediateSize);
            writer.SetVector(ShaderProperties.Clouds.FinalSize, data.FinalSize);
            writer.SetInt(
                ShaderProperties.Clouds.IntermediateToFinalScale,
                data.IntermediateToFinalScale);
            writer.SetFloat(ShaderProperties.Clouds.NearPlane, data.NearPlane);
            writer.SetFloat(ShaderProperties.Atmosphere.BottomRadius, data.AtmosphereBottomRadius);
            writer.SetFloat(ShaderProperties.Atmosphere.TopRadius, data.AtmosphereTopRadius);
            writer.SetFloat(ShaderProperties.Atmosphere.ScatteringMaxDistance, AtmosphereLutPass.ScatteringMaxDistance);

            BindNoiseTextures(
                cmd,
                shader,
                kernel,
                data.CurveLut,
                data.CloudIntegrationNoise,
                data.CloudShapeNoise,
                data.CloudDetailNoise);
            cmd.SetComputeTextureParam(shader, kernel, ShaderProperties.Clouds.TransmittanceLut, data.TransmittanceLut);
            cmd.SetComputeTextureParam(shader, kernel, ShaderProperties.Textures.AtmosphericScatteringId,
                data.AerialPerspectiveLut);
        }

        private static void BindDepthPyramid(
            ComputeCommandBuffer cmd,
            ComputeShader shader,
            int kernel,
            DepthPyramidBindings depthPyramid)
        {
            cmd.SetComputeTextureParam(
                shader,
                kernel,
                DepthPyramidShaderIDs.DepthPyramid,
                depthPyramid.Texture);
            cmd.SetComputeBufferParam(
                shader,
                kernel,
                DepthPyramidShaderIDs.DepthPyramidMipLevelOffsets,
                depthPyramid.MipLevelOffsets);
            cmd.SetComputeBufferParam(
                shader,
                kernel,
                DepthPyramidShaderIDs.DepthPyramidMipLevelOffsetsCheckerboard,
                depthPyramid.MipLevelOffsetsCheckerboard);
            cmd.SetComputeIntParam(
                shader,
                DepthPyramidShaderIDs.DepthPyramidMipCount,
                depthPyramid.MipCount);
            cmd.SetComputeIntParam(
                shader,
                DepthPyramidShaderIDs.DepthPyramidCheckerboardMipCount,
                depthPyramid.CheckerboardMipCount);
            cmd.SetComputeVectorParam(
                shader,
                DepthPyramidShaderIDs.DepthPyramidMip0Size,
                depthPyramid.Mip0Size);
            cmd.SetComputeVectorParam(
                shader,
                DepthPyramidShaderIDs.DepthPyramidAtlasSize,
                depthPyramid.AtlasSize);
            cmd.SetComputeVectorParam(
                shader,
                DepthPyramidShaderIDs.DepthPyramidChannelMode,
                depthPyramid.ChannelMode);
        }

        private static void ExecuteTracePass(TracePassData data, ComputeGraphContext ctx)
        {
            var cmd = ctx.cmd;
            var shader = data.Shader;

            int kernel = data.Kernel;

            BindTraceParameters(cmd, data);

            cmd.SetComputeTextureParam(shader, kernel, ShaderProperties.Clouds.IntermediateTexture,
                data.OutputLighting);
            cmd.SetComputeTextureParam(shader, kernel, ShaderProperties.Clouds.TraceDataTexture, data.OutputData);
            BindDepthPyramid(cmd, shader, kernel, data.DepthPyramid);

            int dispatchWidth = data.SparseFullResolution
                ? ((int)data.OutputSize.x + 1) >> 1
                : (int)data.OutputSize.x;
            cmd.DispatchCompute(shader, kernel,
                CoreUtils.DivRoundUp(dispatchWidth, 8),
                CoreUtils.DivRoundUp((int)data.OutputSize.y, 8),
                1);
        }

        internal static void BindEnvironmentCompute(
            ComputeCommandBuffer cmd,
            ComputeShader shader,
            int kernel,
            in EnvironmentData data)
        {
            var writer = ShaderValueWriter.ForCompute(cmd, shader);
            writer.Write(data.Payload.Parameters);

            writer.SetVector(
                ShaderProperties.Clouds.WorldSpaceCameraPos,
                new Vector4(data.Payload.OriginWs.x, data.Payload.OriginWs.y, data.Payload.OriginWs.z, 1.0f));
            BindNoiseTextures(
                cmd,
                shader,
                kernel,
                data.CurveLut,
                data.CloudIntegrationNoise,
                data.CloudShapeNoise,
                data.CloudDetailNoise);
        }

        internal bool TryBuildEnvironmentState(
            in AtmosphereFrameData atmosphereFrameData,
            Vector3 originWs,
            out EnvironmentState state)
        {
            state = default;

            if (!Volume || !Volume.IsActive() ||
                !atmosphereFrameData.Transmittance.IsValid())
                return false;

            _hasAtmosphereContext = true;
            _currentAtmosphereFrameData = atmosphereFrameData;

            bool microErosion = Volume.cloudSimpleMode.value == VolumetricClouds.CloudSimpleMode.Quality;

            var hasLightContext =
                LightResolver.TryResolveContext(
                    atmosphereFrameData.PrimaryCelestialLight,
                    atmosphereFrameData.Volume,
                    atmosphereFrameData.VisualEnvironment,
                    originWs,
                    out var lightContext);

            var parameters = BuildFrameParameters(
                hasLightContext ? lightContext : default,
                originWs,
                0.0f);
            parameters.CameraSpace = 0.0f;
            parameters.PlanetCenterWS = new Vector4(
                originWs.x,
                originWs.y - parameters.PlanetRadius,
                originWs.z,
                1.0f);

            PrepareCustomLutData(Volume, parameters);

            if (_customLutPresetMap == null)
                return false;

            state = new EnvironmentState(
                new EnvironmentPayload(
                    parameters,
                    originWs,
                    microErosion),
                _customLutPresetMap,
                _resources.IntegrationNoise,
                _resources.ShapeNoise,
                _resources.DetailNoise);

            return true;
        }

        internal EnvironmentData ImportEnvironmentResources(
            RenderGraph renderGraph,
            in EnvironmentState state)
        {
            var curveLut = _curveLutCache.Import(renderGraph, state.CurveLut);
            var cloudIntegrationNoise = _cloudIntegrationNoiseCache.Import(renderGraph, state.CloudIntegrationNoise);
            var cloudShapeNoise = _cloudShapeNoiseCache.Import(renderGraph, state.CloudShapeNoise);
            var cloudDetailNoise = _cloudDetailNoiseCache.Import(renderGraph, state.CloudDetailNoise);
            return new EnvironmentData(
                state.Payload,
                curveLut,
                cloudIntegrationNoise,
                cloudShapeNoise,
                cloudDetailNoise);
        }
        private static TextureHandle CreateCloudTexture(
            RenderGraph renderGraph,
            in TextureDesc sourceDescriptor,
            int width,
            int height,
            GraphicsFormat format,
            string name)
        {
            var textureDesc = new TextureDesc(sourceDescriptor)
            {
                sizeMode = TextureSizeMode.Explicit,
                width = width,
                height = height,
                colorFormat = format,
                depthBufferBits = DepthBits.None,
                msaaSamples = MSAASamples.None,
                bindTextureMS = false,
                enableRandomWrite = true,
                useMipMap = false,
                autoGenerateMips = false,
                memoryless = RenderTextureMemoryless.None,
                clearBuffer = false,
                name = name
            };

            return renderGraph.CreateTexture(textureDesc);
        }

        private static RenderTextureDescriptor CreateHistoryDescriptor(
            in TextureDesc sourceDescriptor,
            int width,
            int height,
            GraphicsFormat format)
        {
            return new RenderTextureDescriptor(width, height, format, 0)
            {
                dimension = sourceDescriptor.dimension,
                volumeDepth = sourceDescriptor.slices,
                msaaSamples = 1,
                bindMS = false,
                enableRandomWrite = true,
                useMipMap = false,
                autoGenerateMips = false,
                useDynamicScale = sourceDescriptor.useDynamicScale,
                vrUsage = sourceDescriptor.vrUsage,
                memoryless = RenderTextureMemoryless.None
            };
        }

        private static void ExecuteTemporalResolvePass(TemporalResolvePassData data, ComputeGraphContext ctx)
        {
            var cmd = ctx.cmd;
            var shader = data.Shader;
            var writer = ShaderValueWriter.ForCompute(cmd, shader);

            writer.SetVector(ShaderProperties.VolumetricResolve.SourceSize, data.SourceSize);
            writer.SetVector(ShaderProperties.VolumetricResolve.OutputSize, data.OutputSize);
            writer.SetVector(ShaderProperties.VolumetricResolve.FullResolutionSize, data.FullResolutionSize);
            writer.SetInt(
                ShaderProperties.VolumetricResolve.OutputToFullResolutionScale,
                data.OutputToFullResolutionScale);
            if (data.HistoryValid)
            {
                writer.SetVector(ShaderProperties.VolumetricResolve.HistoryViewportScale, data.HistoryViewportScale);
            }

            cmd.SetComputeTextureParam(shader, data.Kernel, ShaderProperties.VolumetricResolve.SourceLightingTexture,
                data.SourceLighting);
            cmd.SetComputeTextureParam(shader, data.Kernel, ShaderProperties.VolumetricResolve.SourceDataTexture,
                data.SourceData);
            BindDepthPyramid(cmd, shader, data.Kernel, data.DepthPyramid);
            if (data.HistoryValid)
            {
                cmd.SetComputeTextureParam(shader, data.Kernel,
                    ShaderProperties.VolumetricResolve.HistoryLightingTexture, data.PreviousHistoryLighting);
                cmd.SetComputeTextureParam(shader, data.Kernel, ShaderProperties.VolumetricResolve.HistoryDataTexture,
                    data.PreviousHistoryData);
            }

            cmd.SetComputeTextureParam(shader, data.Kernel, ShaderProperties.VolumetricResolve.OutputLightingTexture,
                data.OutputLighting);
            cmd.SetComputeTextureParam(shader, data.Kernel, ShaderProperties.VolumetricResolve.OutputDataTexture,
                data.OutputData);
            if (data.HistoryValid)
            {
                writer.SetMatrix(ShaderProperties.VolumetricResolve.PrevViewProjection, data.PreviousViewProjection);
                writer.SetVector(ShaderProperties.Clouds.ViewDirOriginWS, data.ViewDirOriginWS);
                writer.SetVector(ShaderProperties.Clouds.ViewDirStepXWS, data.ViewDirStepXWS);
                writer.SetVector(ShaderProperties.Clouds.ViewDirStepYWS, data.ViewDirStepYWS);
                writer.SetFloat(ShaderProperties.VolumetricResolve.NearPlaneReprojection, data.NearPlaneReprojection);
                writer.SetFloat(ShaderProperties.VolumetricResolve.HistoryInvalidation, data.HistoryInvalidation);
                writer.SetFloat(
                    ShaderProperties.VolumetricResolve.TemporalAccumulationFactor,
                    data.TemporalAccumulationFactor);
                writer.SetVector(
                    ShaderProperties.VolumetricResolve.CloudHistoryPositionOffsetWS,
                    data.CloudHistoryPositionOffsetWS);
            }

            writer.SetFloat(ShaderProperties.VolumetricResolve.NearPlane, data.NearPlane);
            writer.SetInt(ShaderProperties.VolumetricResolve.SubPixelIndex, data.SubPixelIndex);
            cmd.DispatchCompute(shader, data.Kernel,
                CoreUtils.DivRoundUp((int)data.OutputSize.x, 8),
                CoreUtils.DivRoundUp((int)data.OutputSize.y, 8),
                1);
            data.History.CommitFrame(
                data.CurrentViewProjection,
                data.CameraPositionWs,
                (int)data.OutputSize.x,
                (int)data.OutputSize.y,
                data.CameraType,
                data.TargetWidth,
                data.TargetHeight,
                data.CameraPixelWidth,
                data.CameraPixelHeight,
                data.ScalableBufferWidthScale,
                data.ScalableBufferHeightScale,
                data.Projection,
                data.ShapeAnimationOffsetPs,
                data.SunForward,
                data.HasSun);
        }

        private readonly struct ResolutionLayout
        {
            public readonly int TraceWidth;
            public readonly int TraceHeight;
            public readonly int IntermediateWidth;
            public readonly int IntermediateHeight;
            public readonly int FinalWidth;
            public readonly int FinalHeight;

            public ResolutionLayout(int traceWidth, int traceHeight, int intermediateWidth, int intermediateHeight,
                int finalWidth, int finalHeight)
            {
                TraceWidth = Mathf.Max(1, traceWidth);
                TraceHeight = Mathf.Max(1, traceHeight);
                IntermediateWidth = Mathf.Max(1, intermediateWidth);
                IntermediateHeight = Mathf.Max(1, intermediateHeight);
                FinalWidth = Mathf.Max(1, finalWidth);
                FinalHeight = Mathf.Max(1, finalHeight);
            }
        }

        private static ResolutionLayout BuildResolutionLayout(
            int finalWidth,
            int finalHeight,
            bool fullResolution)
        {
            int intermediateWidth = fullResolution
                ? finalWidth
                : Mathf.Max(1, (finalWidth + 1) >> 1);
            int intermediateHeight = fullResolution
                ? finalHeight
                : Mathf.Max(1, (finalHeight + 1) >> 1);
            int traceWidth = Mathf.Max(1, (intermediateWidth + 1) >> 1);
            int traceHeight = Mathf.Max(1, (intermediateHeight + 1) >> 1);

            return new ResolutionLayout(traceWidth, traceHeight, intermediateWidth, intermediateHeight, finalWidth,
                finalHeight);
        }

        public static bool TryGetAtmosphereFrameData(ContextContainer frameData,
            out AtmosphereFrameData atmosphereFrameData)
        {
            atmosphereFrameData = null;

            if (frameData == null || !frameData.Contains<AtmosphereFrameData>())
                return false;

            atmosphereFrameData = frameData.Get<AtmosphereFrameData>();

            return atmosphereFrameData is { IsActive: true } &&
                   atmosphereFrameData.Volume &&
                   atmosphereFrameData.Volume.IsActive();
        }

        private static float ComputeNearPlane(
            Camera camera,
            VolumetricClouds volume,
            Vector3 cameraPositionWs,
            in AtmosphereFrameData atmosphereFrameData)
        {
            var planetCenterWs = GetPlanetCenterWs(atmosphereFrameData);
            float lowerRadius = atmosphereFrameData.GetCloudLayerBottomRadiusMeters(volume.bottomAltitude.value);
            float upperRadius = atmosphereFrameData.GetCloudLayerTopRadiusMeters(volume.bottomAltitude.value,
                volume.altitudeRange.value);

            return ComputeNearPlane(camera, cameraPositionWs, planetCenterWs, lowerRadius,
                upperRadius);
        }

        private static Vector3 GetPlanetCenterWs(in AtmosphereFrameData atmosphereFrameData)
        {
            return atmosphereFrameData.PlanetCenterWS;
        }

        private static float ComputeNearPlane(
            Camera camera,
            Vector3 cameraPositionWs,
            Vector3 planetCenterWs,
            float lowerRadius,
            float upperRadius)
        {
            float radialDistance = (cameraPositionWs - planetCenterWs).magnitude;

            float rcpRadialDistance = 1.0f / radialDistance;
            const float cosChi = 1.0f;

            var tInner = IntersectSphere(lowerRadius, cosChi, radialDistance, rcpRadialDistance);
            var tOuter = IntersectSphere(upperRadius, -cosChi, radialDistance, rcpRadialDistance);

            float distanceToVolume = tInner is { x: < 0.0f, y: >= 0.0f }
                ? tInner.y
                : Mathf.Max(tOuter.x, 0.0f);

            return Mathf.Max(distanceToVolume, camera.nearClipPlane);
        }

        private static Vector2 IntersectSphere(float sphereRadius, float cosChi, float radialDistance,
            float rcpRadialDistance)
        {
            float normalizedRadius = sphereRadius * rcpRadialDistance;
            float discriminant = normalizedRadius * normalizedRadius
                                 - Mathf.Clamp01(1.0f - cosChi * cosChi);

            if (discriminant < 0.0f)
                return new Vector2(discriminant, discriminant);

            float sqrtDiscriminant = Mathf.Sqrt(discriminant);
            return radialDistance * new Vector2(-cosChi - sqrtDiscriminant, -cosChi + sqrtDiscriminant);
        }

        private static void ResetTemporalState(VolumetricCloudHistory state)
        {
            state.IsValid = false;
            state.HasPreviousFrame = false;
            state.AccumulationFrameIndex = 0;
            state.PreviousViewportWidth = -1;
            state.PreviousViewportHeight = -1;
            state.HasCameraValidityState = false;
            state.PreviousShapeAnimationOffsetPs = Vector3.zero;
        }

        private static bool MatrixDiffers(Matrix4x4 lhs, Matrix4x4 rhs, float epsilon = 1.0e-6f)
        {
            for (int i = 0; i < 16; ++i)
            {
                if (Mathf.Abs(lhs[i] - rhs[i]) > epsilon)
                    return true;
            }

            return false;
        }

        private static bool CameraHistoryStateChanged(
            VolumetricCloudHistory state,
            Camera camera,
            RenderTextureDescriptor targetDescriptor)
        {
            if (!state.HasCameraValidityState)
                return true;

            if (state.LastRenderedFrame >= 0 && Time.frameCount - state.LastRenderedFrame > 1)
                return true;

            if (camera.TryGetComponent<UniversalAdditionalCameraData>(out var additionalCameraData) &&
                additionalCameraData.resetHistory)
                return true;

            if (state.CameraType != camera.cameraType)
                return true;

            if (state.TargetWidth != targetDescriptor.width || state.TargetHeight != targetDescriptor.height)
                return true;

            if (state.CameraPixelWidth != camera.pixelWidth || state.CameraPixelHeight != camera.pixelHeight)
                return true;

            if (!Mathf.Approximately(state.ScalableBufferWidthScale, ScalableBufferManager.widthScaleFactor) ||
                !Mathf.Approximately(state.ScalableBufferHeightScale, ScalableBufferManager.heightScaleFactor))
                return true;

            return MatrixDiffers(state.PreviousProjection, camera.projectionMatrix);
        }

        private int ComputeTemporalHistoryStateHash()
        {
            unchecked
            {
                int hash = 17;
                hash = hash * 31 + _hasAtmosphereContext.GetHashCode();
                if (_hasAtmosphereContext)
                {
                    var renderingSpace = _currentAtmosphereFrameData.RenderingSpace;
                    hash = hash * 31 + _currentAtmosphereFrameData.PlanetRadiusMeters.GetHashCode();
                    hash = hash * 31 + renderingSpace.GetHashCode();

                    if (renderingSpace == RenderingSpace.World)
                        hash = hash * 31 + _currentAtmosphereFrameData.PlanetCenterWS.GetHashCode();
                }

                hash = hash * 31 + Volume.GetHashCode();
                hash = hash * 31 + Volume.cloudSimpleMode.value.GetHashCode();
                hash = hash * 31 + Volume.cloudPreset.GetHashCode();
                hash = hash * 31 + Volume.numPrimarySteps.value.GetHashCode();
                hash = hash * 31 + Volume.numLightSteps.value.GetHashCode();
                hash = hash * 31 + Volume.temporalAccumulationFactor.value.GetHashCode();
                hash = hash * 31 + Volume.ghostingReduction.value.GetHashCode();
                hash = hash * 31 + Volume.fullResolution.value.GetHashCode();
                hash = hash * 31 + Volume.altitudeRange.value.GetHashCode();
                hash = hash * 31 + Volume.bottomAltitude.value.GetHashCode();
                hash = hash * 31 + Volume.shapeScale.value.GetHashCode();
                hash = hash * 31 + Volume.erosionScale.value.GetHashCode();
                hash = hash * 31 + Volume.microErosionScale.value.GetHashCode();
                hash = hash * 31 + Volume.shapeFactor.value.GetHashCode();
                hash = hash * 31 + Volume.erosionFactor.value.GetHashCode();
                hash = hash * 31 + Volume.microErosionFactor.value.GetHashCode();
                hash = hash * 31 + Volume.multiScattering.value.GetHashCode();
                hash = hash * 31 + Volume.fadeInMode.value.GetHashCode();
                hash = hash * 31 + Volume.fadeInStart.value.GetHashCode();
                hash = hash * 31 + Volume.fadeInDistance.value.GetHashCode();
                hash = hash * 31 + Volume.densityMultiplier.value.GetHashCode();
                hash = hash * 31 + Volume.shapeOffset.value.GetHashCode();
                hash = hash * 31 + Volume.globalWindSpeed.GetHashCode();
                hash = hash * 31 + Volume.orientation.GetHashCode();
                hash = hash * 31 + Volume.shapeSpeedMultiplier.value.GetHashCode();
                hash = hash * 31 + Volume.erosionSpeedMultiplier.value.GetHashCode();
                hash = hash * 31 + Volume.altitudeDistortion.value.GetHashCode();
                hash = hash * 31 + Volume.verticalShapeWindSpeed.value.GetHashCode();
                hash = hash * 31 + Volume.verticalErosionWindSpeed.value.GetHashCode();
                hash = hash * 31 + Volume.ambientLightProbeDimmer.value.GetHashCode();
                hash = hash * 31 + Volume.sunLightDimmer.value.GetHashCode();
                hash = hash * 31 + Volume.erosionOcclusion.value.GetHashCode();
                hash = hash * 31 + Volume.powderEffectIntensity.value.GetHashCode();
                hash = hash * 31 + Volume.scatteringTint.value.GetHashCode();
                hash = hash * 31 + ComputeCurveHash(Volume.densityCurve.value);
                hash = hash * 31 + ComputeCurveHash(Volume.erosionCurve.value);
                hash = hash * 31 + ComputeCurveHash(Volume.ambientOcclusionCurve.value);
                return hash;
            }
        }

        private static Vector3 ComputeShapeAnimationOffsetPs(in FrameParameters parameters)
        {
            return new Vector3(
                parameters.WindVector.x * parameters.MediumWindSpeed,
                parameters.VerticalShapeDisplacement,
                parameters.WindVector.y * parameters.MediumWindSpeed);
        }

        private static Vector3 ComputeCloudHistoryPositionOffsetWs(
            VolumetricCloudHistory state,
            in FrameParameters parameters,
            Vector3 currentShapeAnimationOffsetPs,
            Vector3 currentCameraPositionWs)
        {
            Vector3 coordinateOffsetDelta =
                currentShapeAnimationOffsetPs - state.PreviousShapeAnimationOffsetPs;

            if (parameters.CameraSpace > 0.5f)
            {
                Vector3 cameraDelta = currentCameraPositionWs - state.PreviousCameraPositionWs;
                coordinateOffsetDelta.x += cameraDelta.x;
                coordinateOffsetDelta.z += cameraDelta.z;
            }

            // Shape coordinates shear Y by X/3 + Z/7 before applying animation.
            // Invert that transform so the current cloud point is projected to
            // the previous position of the same procedural shape feature.
            return new Vector3(
                coordinateOffsetDelta.x,
                coordinateOffsetDelta.y
                    - coordinateOffsetDelta.x / 3.0f
                    - coordinateOffsetDelta.z / 7.0f,
                coordinateOffsetDelta.z);
        }

        private static Vector4 ComputeHistoryViewportScale(VolumetricCloudHistory state)
        {
            if (!state.IsValid || !state.HasPreviousFrame || state.PreviousViewportWidth <= 0 ||
                state.PreviousViewportHeight <= 0)
                return new Vector4(1f, 1f, 0f, 0f);

            var historyHandle = state.PreviousLighting;

            if (!historyHandle?.rt || historyHandle.rt.width <= 0 || historyHandle.rt.height <= 0)
                return new Vector4(1f, 1f, 0f, 0f);

            return new Vector4(
                state.PreviousViewportWidth / (float)historyHandle.rt.width,
                state.PreviousViewportHeight / (float)historyHandle.rt.height,
                0f,
                0f);
        }

        public void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            var cloudFrameData = frameData.GetOrCreate<VolumetricCloudFrameData>();
            cloudFrameData.Reset();

            if (!Volume || !Volume.IsActive()) return;

            var resourceData = frameData.Get<UniversalResourceData>();
            var cameraData = frameData.Get<UniversalCameraData>();
            if (!resourceData.cameraDepthTexture.IsValid())
                return;

            _hasAtmosphereContext = TryGetAtmosphereFrameData(frameData, out _currentAtmosphereFrameData);

            if (!_hasAtmosphereContext ||
                !_currentAtmosphereFrameData.Transmittance.IsValid() ||
                !_currentAtmosphereFrameData.AerialPerspective.IsValid())
                return;

            LightResolver.TryResolveContext(
                _currentAtmosphereFrameData.PrimaryCelestialLight,
                _currentAtmosphereFrameData.Volume,
                _currentAtmosphereFrameData.VisualEnvironment,
                cameraData.camera,
                out var lightContext);

            var depthPyramid = frameData.GetOrCreate<DepthPyramidFrameData>();
            if (!depthPyramid.isValid ||
                depthPyramid.CheckerboardMipCount < 2 ||
                depthPyramid.MipLevelOffsetsBuffer == null ||
                depthPyramid.MipLevelOffsetsCheckerboardBuffer == null)
                return;

            var depthPyramidBindings = new DepthPyramidBindings(
                depthPyramid.DepthPyramid,
                renderGraph.ImportBuffer(depthPyramid.MipLevelOffsetsBuffer),
                renderGraph.ImportBuffer(depthPyramid.MipLevelOffsetsCheckerboardBuffer),
                depthPyramid.MipCount,
                depthPyramid.CheckerboardMipCount,
                depthPyramid.Mip0Size,
                depthPyramid.AtlasSize,
                depthPyramid.ChannelMode);

            if (cameraData.historyManager == null)
                return;

            cameraData.historyManager.RequestAccess<VolumetricCloudHistory>();
            var temporalState = cameraData.historyManager.GetHistoryForWrite<VolumetricCloudHistory>();
            if (temporalState == null)
                return;

            int currentHistoryStateHash = ComputeTemporalHistoryStateHash();
            if (temporalState.StateHash != currentHistoryStateHash)
            {
                ResetTemporalState(temporalState);
                temporalState.StateHash = currentHistoryStateHash;
            }

            var cameraTargetDescriptor = renderGraph.GetTextureDesc(resourceData.cameraDepthTexture);
            var cameraTargetDimensions = cameraTargetDescriptor.CalculateFinalDimensions();
            bool fullResolution = Volume.fullResolution.value;
            var layout = BuildResolutionLayout(
                cameraTargetDimensions.x,
                cameraTargetDimensions.y,
                fullResolution);
            int intermediateToFinalScale = fullResolution ? 1 : 2;
            temporalState.AccumulationFrameIndex %= TemporalFrameCount;
            int subPixelIndex = temporalState.AccumulationFrameIndex % 4;

            var currentViewProjection = ComputeGpuViewProjection(cameraData);
            var currentInvViewProjection = currentViewProjection.inverse;
            var cloudViewDirectionBasis = ComputeCloudViewDirectionBasis(
                currentInvViewProjection,
                cameraData.camera.transform.position,
                layout.FinalWidth,
                layout.FinalHeight);

            float currentNearPlane = ComputeNearPlane(
                cameraData.camera,
                Volume,
                cameraData.camera.transform.position,
                _currentAtmosphereFrameData);
            float previousNearPlane = temporalState.HasPreviousFrame &&
                                      _currentAtmosphereFrameData.RenderingSpace == RenderingSpace.World
                ? ComputeNearPlane(cameraData.camera, Volume, temporalState.PreviousCameraPositionWs,
                    _currentAtmosphereFrameData)
                : currentNearPlane;
            float nearPlaneReprojection = currentNearPlane / previousNearPlane;

            const GraphicsFormat lightingFormat = GraphicsFormat.B10G11R11_UFloatPack32;

            var intermediateLightingFormat = Volume.ghostingReduction.value
                ? lightingFormat
                : GraphicsFormat.R16G16B16A16_SFloat;
            var tracedLightingRg = CreateCloudTexture(
                renderGraph,
                cameraTargetDescriptor,
                layout.TraceWidth,
                layout.TraceHeight,
                lightingFormat,
                TracedLightingName);
            var tracedDataRg = CreateCloudTexture(
                renderGraph,
                cameraTargetDescriptor,
                layout.TraceWidth,
                layout.TraceHeight,
                GraphicsFormat.R16G16_SFloat,
                TracedDataName);
            var intermediateLightingDesc = CreateHistoryDescriptor(
                cameraTargetDescriptor,
                layout.IntermediateWidth,
                layout.IntermediateHeight,
                intermediateLightingFormat);
            var intermediateDataDesc = CreateHistoryDescriptor(
                cameraTargetDescriptor,
                layout.IntermediateWidth,
                layout.IntermediateHeight,
                GraphicsFormat.R16G16B16A16_SFloat);

            bool historySizeChanged =
                temporalState.Width != layout.IntermediateWidth || temporalState.Height != layout.IntermediateHeight;
            bool cameraHistoryStateChanged = CameraHistoryStateChanged(
                temporalState,
                cameraData.camera,
                intermediateLightingDesc);

            temporalState.Update(intermediateLightingDesc, intermediateDataDesc);

            if (historySizeChanged || cameraHistoryStateChanged)
            {
                temporalState.IsValid = false;
                temporalState.Width = layout.IntermediateWidth;
                temporalState.Height = layout.IntermediateHeight;
                temporalState.AccumulationFrameIndex = 0;
                subPixelIndex = 0;
                temporalState.HasPreviousFrame = false;
            }

            Update(lightContext, cameraData.camera);

            bool historyValid = temporalState.IsValid && temporalState.HasPreviousFrame;
            Vector3 currentShapeAnimationOffsetPs = ComputeShapeAnimationOffsetPs(_frameParameters);
            Vector3 cloudHistoryPositionOffsetWs = historyValid
                ? ComputeCloudHistoryPositionOffsetWs(
                    temporalState,
                    _frameParameters,
                    currentShapeAnimationOffsetPs,
                    cameraData.camera.transform.position)
                : Vector3.zero;

            var historyViewportScale = ComputeHistoryViewportScale(temporalState);
            var previousHistoryLightingRg = renderGraph.ImportTexture(temporalState.PreviousLighting);
            var previousHistoryDataRg = renderGraph.ImportTexture(temporalState.PreviousData);
            var currentHistoryLightingRg = renderGraph.ImportTexture(temporalState.CurrentLighting);
            var currentHistoryDataRg = renderGraph.ImportTexture(temporalState.CurrentData);

            var curveLutRg = _curveLutCache.Import(renderGraph, _customLutPresetMap);
            var cloudIntegrationNoiseRg = _cloudIntegrationNoiseCache.Import(renderGraph, _resources.IntegrationNoise);
            var cloudShapeNoiseRg = _cloudShapeNoiseCache.Import(renderGraph, _resources.ShapeNoise);
            var cloudDetailNoiseRg = _cloudDetailNoiseCache.Import(renderGraph, _resources.DetailNoise);
            var transmittanceLut = _currentAtmosphereFrameData.Transmittance;
            var aerialPerspectiveLut = _currentAtmosphereFrameData.AerialPerspective;
            bool useMicroErosion =
                Volume.cloudSimpleMode.value == VolumetricClouds.CloudSimpleMode.Quality;
            bool useSparseFullResolutionTrace = fullResolution && historyValid;
            int traceKernel = useSparseFullResolutionTrace
                ? (useMicroErosion
                    ? _traceSparseFullResolutionMicroErosionKernel
                    : _traceSparseFullResolutionKernel)
                : (useMicroErosion ? _traceMicroErosionKernel : _traceKernel);

            var traceStage = TraceProfile;
            using (var profiledPass = traceStage.AddComputePass<TracePassData>(
                       renderGraph,
                       out var pd))
            {
                var builder = profiledPass.Builder;
                pd.Shader = _traceShader;
                pd.Kernel = traceKernel;
                pd.Parameters = _frameParameters;
                pd.NearPlane = currentNearPlane;
                pd.AtmosphereBottomRadius = _currentAtmosphereFrameData.VisualEnvironment.planetRadius.value;
                pd.AtmosphereTopRadius = _currentAtmosphereFrameData.Volume.TopRadius.value;
                pd.OutputLighting = tracedLightingRg;
                pd.OutputData = tracedDataRg;
                pd.DepthPyramid = depthPyramidBindings;
                pd.CurveLut = curveLutRg;
                pd.CloudIntegrationNoise = cloudIntegrationNoiseRg;
                pd.CloudShapeNoise = cloudShapeNoiseRg;
                pd.CloudDetailNoise = cloudDetailNoiseRg;
                pd.TransmittanceLut = transmittanceLut;
                pd.AerialPerspectiveLut = aerialPerspectiveLut;
                pd.OutputSize = new Vector4(layout.TraceWidth, layout.TraceHeight, 1f / layout.TraceWidth,
                    1f / layout.TraceHeight);
                pd.IntermediateSize = new Vector4(layout.IntermediateWidth, layout.IntermediateHeight,
                    1f / layout.IntermediateWidth, 1f / layout.IntermediateHeight);
                pd.FinalSize = new Vector4(layout.FinalWidth, layout.FinalHeight,
                    1f / layout.FinalWidth, 1f / layout.FinalHeight);
                pd.IntermediateToFinalScale = intermediateToFinalScale;
                pd.ViewDirOriginWS = cloudViewDirectionBasis.OriginWS;
                pd.ViewDirStepXWS = cloudViewDirectionBasis.StepXWS;
                pd.ViewDirStepYWS = cloudViewDirectionBasis.StepYWS;
                pd.AccumulationFrameIndex = temporalState.AccumulationFrameIndex / 4;
                pd.SubPixelIndex = subPixelIndex;
                pd.SparseFullResolution = useSparseFullResolutionTrace;

                builder.UseTexture(pd.CurveLut);
                builder.UseTexture(pd.CloudIntegrationNoise);
                builder.UseTexture(pd.CloudShapeNoise);
                builder.UseTexture(pd.CloudDetailNoise);
                builder.UseTexture(pd.TransmittanceLut);
                builder.UseTexture(pd.AerialPerspectiveLut);

                builder.UseTexture(pd.DepthPyramid.Texture);
                builder.UseBuffer(pd.DepthPyramid.MipLevelOffsets);
                builder.UseBuffer(pd.DepthPyramid.MipLevelOffsetsCheckerboard);
                builder.UseTexture(pd.OutputLighting, AccessFlags.Write);
                builder.UseTexture(pd.OutputData, AccessFlags.Write);

                builder.SetRenderFunc(static (TracePassData data, ComputeGraphContext ctx) =>
                    ExecuteTracePass(data, ctx));
            }

            var resolveStage = TemporalResolveProfile;
            using (var profiledPass = resolveStage.AddComputePass<TemporalResolvePassData>(
                       renderGraph,
                       out var pd))
            {
                var builder = profiledPass.Builder;
                pd.Shader = _denoisingShader;
                pd.HistoryValid = historyValid;
                pd.Kernel = pd.HistoryValid
                    ? (useSparseFullResolutionTrace
                        ? (Volume.ghostingReduction.value
                            ? _resolveHistorySparseFullResolutionGhostReductionKernel
                            : _resolveHistorySparseFullResolutionKernel)
                        : (Volume.ghostingReduction.value
                            ? _resolveHistoryGhostReductionKernel
                            : _resolveHistoryKernel))
                    : _resolveNoHistoryKernel;
                pd.SourceLighting = tracedLightingRg;
                pd.SourceData = tracedDataRg;
                pd.DepthPyramid = depthPyramidBindings;
                pd.PreviousHistoryLighting = previousHistoryLightingRg;
                pd.PreviousHistoryData = previousHistoryDataRg;
                pd.OutputLighting = currentHistoryLightingRg;
                pd.OutputData = currentHistoryDataRg;
                pd.SourceSize = new Vector4(layout.TraceWidth, layout.TraceHeight, 1f / layout.TraceWidth,
                    1f / layout.TraceHeight);
                pd.OutputSize = new Vector4(layout.IntermediateWidth, layout.IntermediateHeight,
                    1f / layout.IntermediateWidth, 1f / layout.IntermediateHeight);
                pd.FullResolutionSize = new Vector4(layout.FinalWidth, layout.FinalHeight,
                    1f / layout.FinalWidth, 1f / layout.FinalHeight);
                pd.OutputToFullResolutionScale = intermediateToFinalScale;
                pd.PreviousViewProjection = temporalState.PreviousViewProjection;
                pd.CurrentViewProjection = currentViewProjection;
                pd.ViewDirOriginWS = cloudViewDirectionBasis.OriginWS;
                pd.ViewDirStepXWS = cloudViewDirectionBasis.StepXWS;
                pd.ViewDirStepYWS = cloudViewDirectionBasis.StepYWS;
                pd.HistoryViewportScale = historyViewportScale;
                pd.NearPlaneReprojection = nearPlaneReprojection;
                pd.NearPlane = currentNearPlane;
                pd.HistoryInvalidation = ComputeHistoryInvalidation(temporalState, lightContext);
                pd.TemporalAccumulationFactor = Volume.temporalAccumulationFactor.value;
                pd.CloudHistoryPositionOffsetWS = new Vector4(
                    cloudHistoryPositionOffsetWs.x,
                    cloudHistoryPositionOffsetWs.y,
                    cloudHistoryPositionOffsetWs.z,
                    0.0f);
                pd.ShapeAnimationOffsetPs = currentShapeAnimationOffsetPs;
                pd.SubPixelIndex = subPixelIndex;
                pd.History = temporalState;
                pd.CameraPositionWs = cameraData.camera.transform.position;
                pd.CameraType = cameraData.camera.cameraType;
                pd.TargetWidth = intermediateLightingDesc.width;
                pd.TargetHeight = intermediateLightingDesc.height;
                pd.CameraPixelWidth = cameraData.camera.pixelWidth;
                pd.CameraPixelHeight = cameraData.camera.pixelHeight;
                pd.ScalableBufferWidthScale = ScalableBufferManager.widthScaleFactor;
                pd.ScalableBufferHeightScale = ScalableBufferManager.heightScaleFactor;
                pd.Projection = cameraData.camera.projectionMatrix;
                pd.SunForward = lightContext.ForwardWS;
                pd.HasSun = lightContext.IsValid;

                builder.UseTexture(pd.SourceLighting);
                builder.UseTexture(pd.SourceData);
                builder.UseTexture(pd.DepthPyramid.Texture);
                builder.UseBuffer(pd.DepthPyramid.MipLevelOffsets);
                builder.UseBuffer(pd.DepthPyramid.MipLevelOffsetsCheckerboard);
                if (pd.HistoryValid)
                {
                    builder.UseTexture(pd.PreviousHistoryLighting);
                    builder.UseTexture(pd.PreviousHistoryData);
                }

                builder.UseTexture(pd.OutputLighting, AccessFlags.Write);
                builder.UseTexture(pd.OutputData, AccessFlags.Write);
                builder.AllowPassCulling(false);
                builder.SetRenderFunc(static (TemporalResolvePassData data, ComputeGraphContext ctx) =>
                    ExecuteTemporalResolvePass(data, ctx));
            }

            cloudFrameData.IsActive = true;
            cloudFrameData.ResolvedLighting = currentHistoryLightingRg;
            cloudFrameData.ResolvedData = currentHistoryDataRg;
            cloudFrameData.DepthPyramid = depthPyramidBindings.Texture;
            cloudFrameData.ResolvedSize = new Vector4(
                layout.IntermediateWidth,
                layout.IntermediateHeight,
                1.0f / layout.IntermediateWidth,
                1.0f / layout.IntermediateHeight);
            cloudFrameData.IsFullResolution = fullResolution;
            cloudFrameData.NearPlane = currentNearPlane;
            cloudFrameData.ImprovedTransmittanceBlend = Volume.perceptualBlending.value;
        }

        private static CloudViewDirectionBasis ComputeCloudViewDirectionBasis(
            Matrix4x4 inverseViewProjection,
            Vector3 cameraPositionWS,
            int width,
            int height)
        {
            float inverseWidth = 1.0f / width;
            float inverseHeight = 1.0f / height;
            float clipYSign = SystemInfo.graphicsUVStartsAtTop ? -1.0f : 1.0f;
            float farDeviceDepth = SystemInfo.usesReversedZBuffer ? 0.0f : 1.0f;

            var clipOrigin = new Vector4(
                inverseWidth - 1.0f,
                clipYSign * (inverseHeight - 1.0f),
                farDeviceDepth,
                1.0f);
            var clipStepX = new Vector4(2.0f * inverseWidth, 0.0f, 0.0f, 0.0f);
            var clipStepY = new Vector4(0.0f, clipYSign * 2.0f * inverseHeight, 0.0f, 0.0f);

            return new CloudViewDirectionBasis(
                TransformClipToCameraRay(inverseViewProjection, clipOrigin, cameraPositionWS),
                TransformClipToCameraRay(inverseViewProjection, clipStepX, cameraPositionWS),
                TransformClipToCameraRay(inverseViewProjection, clipStepY, cameraPositionWS));
        }

        private static Vector3 TransformClipToCameraRay(
            Matrix4x4 inverseViewProjection,
            Vector4 clipPosition,
            Vector3 cameraPositionWS)
        {
            var worldPosition = inverseViewProjection * clipPosition;
            return new Vector3(
                worldPosition.x - cameraPositionWS.x * worldPosition.w,
                worldPosition.y - cameraPositionWS.y * worldPosition.w,
                worldPosition.z - cameraPositionWS.z * worldPosition.w);
        }

        private static Matrix4x4 ComputeGpuViewProjection(UniversalCameraData cameraData)
        {
            var gpuProjection = GL.GetGPUProjectionMatrix(cameraData.camera.projectionMatrix, true);
            return gpuProjection * cameraData.GetViewMatrix();
        }

        private static float ComputeHistoryInvalidation(
            VolumetricCloudHistory temporalState,
            in LightContext lightContext)
        {
            float sunInvalidation = 1.0f;

            if (!lightContext.IsValid) return sunInvalidation;

            float sunAngleDifference = Vector3.Angle(temporalState.PreviousSunForward, lightContext.ForwardWS);
            sunInvalidation = Mathf.Lerp(1.0f, 0.0f, Mathf.Clamp01(sunAngleDifference / 10.0f));

            return sunInvalidation;
        }

        private void Update(in LightContext lightContext, Camera camera)
        {
            UpdateAnimationState(camera);
            _frameParameters = BuildFrameParameters(
                lightContext,
                camera.transform.position,
                camera.nearClipPlane);
            PrepareCustomLutData(Volume, _frameParameters);
        }

        private void UpdateAnimationState(Camera camera)
        {
            if (!camera || camera.cameraType == CameraType.Reflection)
                return;

            float totalTime = Application.isPlaying ? Time.time : Time.realtimeSinceStartup;
            float deltaTime = Mathf.Approximately(_prevTotalTime, -1.0f)
                ? 0.0f
                : totalTime - _prevTotalTime;

#if UNITY_EDITOR
            if (UnityEditor.EditorApplication.isPaused)
                deltaTime = 0.0f;
#endif

            deltaTime *= -0.277778f;
            var visualEnvironment = _currentAtmosphereFrameData?.VisualEnvironment;
            float windOrientation = Volume.orientation.GetValue(visualEnvironment);
            float windSpeed = Volume.globalWindSpeed.GetValue(visualEnvironment);
            float theta = windOrientation / 180.0f * Mathf.PI;
            var windDirection = new Vector2(Mathf.Cos(theta), Mathf.Sin(theta));

            _prevTotalTime = totalTime;
            _windVector += deltaTime * windSpeed * windDirection;
            _verticalShapeOffset += deltaTime * Volume.verticalShapeWindSpeed.value;
            _verticalErosionOffset += deltaTime * Volume.verticalErosionWindSpeed.value;
        }

        private FrameParameters BuildFrameParameters(
            in LightContext lightContext,
            Vector3 cameraPositionWs,
            float nearClipPlane)
        {
            float lowestRadius =
                _currentAtmosphereFrameData.GetCloudLayerBottomRadiusMeters(Volume.bottomAltitude.value);
            float highestRadius = _currentAtmosphereFrameData.GetCloudLayerTopRadiusMeters(
                Volume.bottomAltitude.value,
                Volume.altitudeRange.value);
            var planetCenter = GetPlanetCenterWs(_currentAtmosphereFrameData);
            float planetRadius = _currentAtmosphereFrameData.PlanetRadiusMeters;
            float windOrientation = Volume.orientation.GetValue(_currentAtmosphereFrameData.VisualEnvironment);
            float theta = windOrientation / 180f * Mathf.PI;
            var windDirection = new Vector2(Mathf.Cos(theta), Mathf.Sin(theta));

            float fadeInStart = Volume.fadeInMode.value == VolumetricClouds.CloudFadeInMode.Automatic
                ? Mathf.Max(Volume.altitudeRange.value * 0.2f, nearClipPlane)
                : Mathf.Max(Volume.fadeInStart.value, nearClipPlane);

            float fadeInDistance = Volume.fadeInMode.value == VolumetricClouds.CloudFadeInMode.Automatic
                ? Volume.altitudeRange.value * 0.3f
                : Volume.fadeInDistance.value;

            var ambientProbe = AmbientProbeShaderData.FromApproximateMiePhase(
                RenderSettings.ambientProbe,
                (float)CloudShaderConfig.DropletDiameterMicrometers);
            float ambientMultiplier = Volume.ambientLightProbeDimmer.value;
            var ambientTopRgb = ambientProbe.Evaluate(Vector3.up) * ambientMultiplier;
            var ambientBottomRgb = ambientProbe.Evaluate(Vector3.down) * ambientMultiplier;
            var ambientTop = new Vector4(ambientTopRgb.x, ambientTopRgb.y, ambientTopRgb.z, 0.0f);
            var ambientBottom = new Vector4(ambientBottomRgb.x, ambientBottomRgb.y, ambientBottomRgb.z, 0.0f);
            Vector4 ScaleAmbientSH(Vector4 coefficient) => coefficient * ambientMultiplier;

            Vector4 sunColor = lightContext.IsValid
                ? lightContext.RawLightColor * Volume.sunLightDimmer.value
                : Vector4.zero;
            var sunDirection = lightContext.IsValid ? lightContext.DirectionToLightWS : Vector3.up;
            float multiScattering = 1.0f - Volume.multiScattering.value * 0.95f;
            Vector4 scatteringTint = Color.white - Volume.scatteringTint.value * 0.75f;

            return new FrameParameters
            {
                MaxStepSize = Volume.altitudeRange.value / 8f,
                NumPrimarySteps = Volume.numPrimarySteps.value,
                NumLightSteps = Volume.numLightSteps.value,
                HighestAltitude = highestRadius,
                LowestAltitude = lowestRadius,
                PlanetCenterWS = new Vector4(planetCenter.x, planetCenter.y, planetCenter.z, 1f),
                PlanetRadius = planetRadius,
                CameraSpace = _currentAtmosphereFrameData.RenderingSpace == RenderingSpace.Camera ? 1f : 0f,
                ShapeNoiseOffset = new Vector4(Volume.shapeOffset.value.x, Volume.shapeOffset.value.z, 0f, 0f),
                VerticalShapeNoiseOffset = Volume.shapeOffset.value.y,
                WindDirection = new Vector4(-windDirection.x, -windDirection.y, 0f, 0f),
                WindVector = new Vector4(_windVector.x, _windVector.y, 0f, 0f),
                VerticalShapeDisplacement = _verticalShapeOffset,
                VerticalErosionDisplacement = _verticalErosionOffset,
                MediumWindSpeed = Volume.shapeSpeedMultiplier.value,
                SmallWindSpeed = Volume.erosionSpeedMultiplier.value,
                AltitudeDistortion = Volume.altitudeDistortion.value * 0.25f,
                DensityMultiplier = Volume.densityMultiplier.value * Volume.densityMultiplier.value * 2f,
                PowderEffectIntensity = Volume.powderEffectIntensity.value,
                MultiScattering = multiScattering,
                ShapeScale = Volume.shapeScale.value,
                ShapeFactor = Volume.shapeFactor.value,
                ErosionScale = Volume.erosionScale.value,
                ErosionFactor = Volume.erosionFactor.value,
                ErosionOcclusion = Volume.erosionOcclusion.value,
                MicroErosionScale = Volume.microErosionScale.value,
                MicroErosionFactor = Volume.microErosionFactor.value,
                FadeInStart = fadeInStart,
                FadeInDistance = fadeInDistance,
                ScatteringTint = scatteringTint,
                SunColor = sunColor,
                SunDirection = new Vector4(
                    sunDirection.x,
                    sunDirection.y,
                    sunDirection.z,
                    lightContext.IsValid ? 1f : 0f),
                AmbientTop = ambientTop,
                AmbientBottom = ambientBottom,
                AmbientSHAr = ScaleAmbientSH(ambientProbe.SHAr),
                AmbientSHAg = ScaleAmbientSH(ambientProbe.SHAg),
                AmbientSHAb = ScaleAmbientSH(ambientProbe.SHAb),
                AmbientSHBr = ScaleAmbientSH(ambientProbe.SHBr),
                AmbientSHBg = ScaleAmbientSH(ambientProbe.SHBg),
                AmbientSHBb = ScaleAmbientSH(ambientProbe.SHBb),
                AmbientSHC = ScaleAmbientSH(ambientProbe.SHC)
            };
        }

        private void PrepareCustomLutData(
            VolumetricClouds volume,
            in FrameParameters parameters)
        {
            if (!_customLutPresetMap)
            {
                _customLutPresetMap = new Texture2D(
                    1,
                    CustomLutMapResolution,
                    GraphicsFormat.R16G16B16A16_SFloat,
                    TextureCreationFlags.None)
                {
                    name = "Cloud Runtime Transfer LUT",
                    filterMode = FilterMode.Bilinear,
                    wrapMode = TextureWrapMode.Clamp,
                    hideFlags = HideFlags.HideAndDontSave
                };

                _customLutInitialized = false;
            }

            int currentLutHash = ComputeLutHash(volume, parameters);
            if (_customLutInitialized && _cachedLutHash == currentLutHash)
                return;

            FillLut(volume, parameters);
            _customLutPresetMap.SetPixels(_customLutColorArray);
            _customLutPresetMap.Apply(updateMipmaps: false, makeNoLongerReadable: false);
            _cachedLutHash = currentLutHash;
            _customLutInitialized = true;
        }

        private void FillLut(
            VolumetricClouds volume,
            in FrameParameters parameters)
        {
            var densityCurve = volume.densityCurve.value;
            var erosionCurve = volume.erosionCurve.value;
            var aoCurve = volume.ambientOcclusionCurve.value;
            bool hasDensityCurve = densityCurve is { length: > 0 };

            float lowestRadiusSquared = parameters.LowestAltitude * parameters.LowestAltitude;
            float highestRadiusSquared = parameters.HighestAltitude * parameters.HighestAltitude;
            float inverseAltitudeRange = 1.0f / (parameters.HighestAltitude - parameters.LowestAltitude);
            const float step = 1f / (CustomLutMapResolution - 1f);

            for (int row = 0; row < CustomLutMapResolution; ++row)
            {
                float squaredRadiusCoordinate = step * row;
                float radius = Mathf.Sqrt(Mathf.Lerp(
                    lowestRadiusSquared,
                    highestRadiusSquared,
                    squaredRadiusCoordinate));
                float height = Mathf.Clamp01(
                    (radius - parameters.LowestAltitude) * inverseAltitudeRange);

                float density = hasDensityCurve
                    ? row is 0 or CustomLutMapResolution - 1
                        ? 0f
                        : Mathf.Clamp01(densityCurve.Evaluate(height))
                    : 1f;
                float erosion = Mathf.Clamp01(erosionCurve?.Evaluate(height) ?? 1f);
                float ambientOcclusion = Mathf.Clamp01(1f - (aoCurve?.Evaluate(height) ?? 0f));

                // Keep the transfer LUT well-conditioned, like HDRP's simple preset:
                // R: density profile, G: erosion profile, B: raw AO.
                // A preserves the old invalid-slice sentinel without storing the
                // singular slope/offset representation: valid slices are +1, empty
                // density/erosion slices are -1. Bilinear interpolation therefore
                // fades the support out instead of creating sparse noise peaks at
                // the top and bottom of the cloud layer.
                float shapeBias = density > 0.0f && erosion > 0.0f ? 1.0f : -1.0f;
                _customLutColorArray[row] = new Color(
                    density,
                    erosion,
                    ambientOcclusion,
                    shapeBias);
            }
        }

        private static int ComputeLutHash(
            VolumetricClouds volume,
            in FrameParameters parameters)
        {
            unchecked
            {
                int hash = 17;
                hash = hash * 31 + ComputeCurveHash(volume.densityCurve.value);
                hash = hash * 31 + ComputeCurveHash(volume.erosionCurve.value);
                hash = hash * 31 + ComputeCurveHash(volume.ambientOcclusionCurve.value);
                hash = hash * 31 + parameters.LowestAltitude.GetHashCode();
                hash = hash * 31 + parameters.HighestAltitude.GetHashCode();
                return hash;
            }
        }

        private static int ComputeCurveHash(AnimationCurve curve)
        {
            if (curve == null) return 0;

            unchecked
            {
                int hash = 17;
                int keyCount = curve.length;

                hash = hash * 31 + keyCount;

                for (int i = 0; i < keyCount; i++)
                {
                    var key = curve[i];
                    hash = hash * 31 + key.time.GetHashCode();
                    hash = hash * 31 + key.value.GetHashCode();
                    hash = hash * 31 + key.inTangent.GetHashCode();
                    hash = hash * 31 + key.outTangent.GetHashCode();
                    hash = hash * 31 + key.inWeight.GetHashCode();
                    hash = hash * 31 + key.outWeight.GetHashCode();
                    hash = hash * 31 + (int)key.weightedMode;
                }

                return hash;
            }
        }

        internal VolumetricCloudsPass(VolumetricCloudResources resources)
        {
            _resources = resources;
            _traceShader = resources.Trace;
            _denoisingShader = resources.Denoising;
            _traceKernel = _traceShader.FindKernel("Trace");
            _traceMicroErosionKernel = _traceShader.FindKernel("TraceMicroErosion");
            _traceSparseFullResolutionKernel = _traceShader.FindKernel("TraceSparseFullResolution");
            _traceSparseFullResolutionMicroErosionKernel =
                _traceShader.FindKernel("TraceSparseFullResolutionMicroErosion");
            _resolveHistoryKernel = _denoisingShader.FindKernel("ResolveHistory");
            _resolveHistoryGhostReductionKernel =
                _denoisingShader.FindKernel("ResolveHistoryGhostReduction");
            _resolveHistorySparseFullResolutionKernel =
                _denoisingShader.FindKernel("ResolveHistorySparseFullResolution");
            _resolveHistorySparseFullResolutionGhostReductionKernel =
                _denoisingShader.FindKernel("ResolveHistorySparseFullResolutionGhostReduction");
            _resolveNoHistoryKernel = _denoisingShader.FindKernel("ResolveNoHistory");
        }

        public bool requiresDepthPyramid => Volume && Volume.IsActive();
        public int requiredDepthPyramidMipCount => 2;
        public int requiredDepthPyramidCheckerboardMipCount => 1;

        public void Dispose()
        {
            if (_customLutPresetMap)
                CoreUtils.Destroy(_customLutPresetMap);

            _customLutPresetMap = null;
            _cachedLutHash = int.MinValue;
            _customLutInitialized = false;

            _curveLutCache.Dispose();
            _cloudIntegrationNoiseCache.Dispose();
            _cloudShapeNoiseCache.Dispose();
            _cloudDetailNoiseCache.Dispose();
        }

    }
}