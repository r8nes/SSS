using System;
using Aetheria.Runtime.Data;
using Aetheria.Runtime.Data.CelestialLight;
using Aetheria.Runtime.Overrides;
using Aetheria.Runtime.Utils;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace Aetheria.Runtime.Rendering.Passes
{
    internal sealed class CloudLayerPass : IDisposable
    {
        private static readonly AetheriaProfiler.Marker BaseProfile =
            AetheriaProfiler.CreateMarker("Cloud Layer", "Bake Base");
        private static readonly AetheriaProfiler.Marker LightingProfile =
            AetheriaProfiler.CreateMarker("Cloud Layer", "Bake Lighting");
        private static readonly AetheriaProfiler.Marker CombineProfile =
            AetheriaProfiler.CreateMarker("Cloud Layer", "Commit Bake");

        private const int LightingResolutionDivisor = 2;
        private const int LightingSliceCount = 2;

        private sealed class BasePassData
        {
            public ComputeShader Shader;
            public int Kernel;
            public TextureHandle Output;
            public TextureHandle SourceTextureA;
            public TextureHandle SourceTextureB;
            public Vector4[] LayerOpacities;
            public Vector4[] LayerParameters;
            public Vector4 Parameters;
            public Vector4 BaseSize;
            public int Width;
            public int Height;
            public bool DoubleLayer;
        }

        private sealed class LightingPassData
        {
            public ComputeShader Shader;
            public int Kernel;
            public TextureHandle Base;
            public TextureHandle SourceTextureA;
            public TextureHandle SourceTextureB;
            public TextureHandle Output;
            public Vector4[] LayerOpacities;
            public Vector4[] LayerParameters;
            public Vector4 Parameters;
            public Vector4 BaseSize;
            public Vector4 LightingSize;
            public int Width;
            public int RowOffset;
            public int RowCount;
            public bool DoubleLayer;
        }

        private sealed class CombinePassData
        {
            public ComputeShader Shader;
            public int Kernel;
            public TextureHandle Base;
            public TextureHandle Lighting;
            public TextureHandle Output;
            public Vector4 Parameters;
            public Vector4 BaseSize;
            public Vector4 LightingSize;
            public int Width;
            public int Height;
            public bool DoubleLayer;
        }

        private readonly ComputeShader _shader;
        private readonly int _baseSingleKernel;
        private readonly int _baseDoubleKernel;
        private readonly int _lightingSingleKernel;
        private readonly int _lightingDoubleKernel;
        private readonly int _combineSingleKernel;
        private readonly int _combineDoubleKernel;
        private readonly Texture2D _defaultMap;

        private readonly Vector4[] _layerOpacities = new Vector4[2];
        private readonly Vector4[] _layerParameters = new Vector4[2];
        private readonly Vector4[] _pendingLayerOpacities = new Vector4[2];
        private readonly Vector4[] _pendingLayerParameters = new Vector4[2];

        private RTHandle _textureHandle;
        private RTHandle _baseTextureHandle;
        private RTHandle _lightingTextureHandle;
        private RenderGraphTextureHandleCache _sourceTextureCacheA;
        private RenderGraphTextureHandleCache _sourceTextureCacheB;

        private int _textureWidth;
        private int _textureHeight;
        private int _textureLayers;
        private int _lightingWidth;
        private int _lightingHeight;

        private int _lastBaseHash = int.MinValue;
        private int _lastLightingHash = int.MinValue;
        private bool _hasCommittedBake;
        private Vector3 _committedLightDirectionWs = Vector3.up;
        private bool _committedHasLight;

        private bool _lightingJobActive;
        private int _pendingLightingHash = int.MinValue;
        private int _pendingLightingSlice;
        private Vector4 _pendingParameters;
        private bool _pendingDoubleLayer;
        private Vector3 _pendingLightDirectionWs = Vector3.up;
        private bool _pendingHasLight;

        private float _lastAnimationTime = float.NaN;
        private bool _wasActive;

        public CloudLayerPass(CloudLayerResources resources)
        {
            _shader = resources.Bake;
            _baseSingleKernel = _shader.FindKernel("BakeBaseSingle");
            _baseDoubleKernel = _shader.FindKernel("BakeBaseDouble");
            _lightingSingleKernel = _shader.FindKernel("BakeLightingSingle");
            _lightingDoubleKernel = _shader.FindKernel("BakeLightingDouble");
            _combineSingleKernel = _shader.FindKernel("CombineSingle");
            _combineDoubleKernel = _shader.FindKernel("CombineDouble");
            _defaultMap = resources.DefaultMap;
        }

        public void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            if (!frameData.Contains<AtmosphereFrameData>())
            {
                ReleaseIfPreviouslyActive();
                return;
            }

            var cameraData = frameData.Get<UniversalCameraData>();
            var atmosphereFrame = frameData.Get<AtmosphereFrameData>();
            var atmosphere = atmosphereFrame.Volume;

            if (!atmosphereFrame.IsActive ||
                atmosphereFrame.CameraData == null ||
                !atmosphere ||
                !atmosphere.IsActive())
            {
                ReleaseIfPreviouslyActive();
                return;
            }

            var cloudLayer = AtmosphereTools.GetVolumeComponent<CloudLayer>(cameraData);
            if (!cloudLayer || !cloudLayer.IsActive())
            {
                ReleaseIfPreviouslyActive();
                return;
            }

            var mapA = ResolveMap(cloudLayer.LayerA);
            var mapB = cloudLayer.LayerCount == 2
                ? ResolveMap(cloudLayer.LayerB)
                : _defaultMap;
            if (!mapA || !mapB)
            {
                ReleaseIfPreviouslyActive();
                return;
            }

            _wasActive = true;
            UpdateScroll(cloudLayer, atmosphereFrame.VisualEnvironment);

            int resolution = Mathf.Clamp((int)cloudLayer.Resolution.value, 256, 8192);
            int height = cloudLayer.UpperHemisphereOnly.value ? resolution / 2 : resolution;
            int layerCount = cloudLayer.LayerCount;
            EnsureTextures(resolution, height, layerCount);

            var cloudTexture = renderGraph.ImportTexture(_textureHandle);
            var baseTexture = renderGraph.ImportTexture(_baseTextureHandle);
            var lightingTexture = renderGraph.ImportTexture(_lightingTextureHandle);
            var sourceTextureA = _sourceTextureCacheA.Import(renderGraph, mapA);
            var sourceTextureB = _sourceTextureCacheB.Import(renderGraph, mapB);

            bool hasLight = LightResolver.TryResolveContext(
                                atmosphereFrame.PrimaryCelestialLight,
                                atmosphere,
                                atmosphereFrame.VisualEnvironment,
                                atmosphereFrame.CameraPositionWs,
                                out var lightContext) &&
                            lightContext.IsValid;
            var lightDirectionWs = hasLight
                ? lightContext.DirectionToLightWS.normalized
                : Vector3.up;
            float planetRadiusMeters = atmosphereFrame.PlanetRadiusMeters;

            int baseHash = ComputeBaseHash(
                cloudLayer,
                mapA,
                mapB,
                hasLight,
                planetRadiusMeters);
            int lightingHash = ComputeLightingHash(
                cloudLayer,
                baseHash,
                hasLight,
                lightDirectionWs,
                _lightingWidth);

            bool baseChanged = !_hasCommittedBake || baseHash != _lastBaseHash;
            if (baseChanged)
            {
                _lightingJobActive = false;
                FillLayerData(cloudLayer, hasLight, _layerOpacities, _layerParameters);
                var parameters = BuildBakeParameters(
                    lightDirectionWs,
                    cloudLayer.UpperHemisphereOnly.value,
                    planetRadiusMeters);

                RecordBaseBake(
                    renderGraph,
                    sourceTextureA,
                    sourceTextureB,
                    baseTexture,
                    parameters,
                    _layerOpacities,
                    _layerParameters,
                    resolution,
                    height,
                    layerCount == 2);
                RecordLightingBake(
                    renderGraph,
                    baseTexture,
                    sourceTextureA,
                    sourceTextureB,
                    lightingTexture,
                    parameters,
                    _layerOpacities,
                    _layerParameters,
                    0,
                    _lightingHeight,
                    layerCount == 2);
                RecordCombine(
                    renderGraph,
                    baseTexture,
                    lightingTexture,
                    cloudTexture,
                    parameters,
                    resolution,
                    height,
                    layerCount == 2);

                CommitBake(baseHash, lightingHash, lightDirectionWs, hasLight);
            }
            else
            {
                if (!_lightingJobActive && lightingHash != _lastLightingHash)
                {
                    BeginLightingJob(
                        cloudLayer,
                        lightingHash,
                        lightDirectionWs,
                        hasLight,
                        planetRadiusMeters);
                }

                if (_lightingJobActive)
                {
                        int rowsPerSlice = CoreUtils.DivRoundUp(_lightingHeight, LightingSliceCount);
                        int rowOffset = _pendingLightingSlice * rowsPerSlice;
                        int rowCount = Mathf.Min(rowsPerSlice, _lightingHeight - rowOffset);

                        if (rowCount > 0)
                        {
                            RecordLightingBake(
                                renderGraph,
                                baseTexture,
                                sourceTextureA,
                                sourceTextureB,
                                lightingTexture,
                                _pendingParameters,
                                _pendingLayerOpacities,
                                _pendingLayerParameters,
                                rowOffset,
                                rowCount,
                                _pendingDoubleLayer);
                        }

                        _pendingLightingSlice++;
                        if (_pendingLightingSlice >= LightingSliceCount ||
                            rowOffset + rowCount >= _lightingHeight)
                        {
                            RecordCombine(
                                renderGraph,
                                baseTexture,
                                lightingTexture,
                                cloudTexture,
                                _pendingParameters,
                                resolution,
                                height,
                                _pendingDoubleLayer);

                            _lastLightingHash = _pendingLightingHash;
                            _committedLightDirectionWs = _pendingLightDirectionWs;
                            _committedHasLight = _pendingHasLight;
                            _lightingJobActive = false;
                            _pendingLightingHash = int.MinValue;
                                        }
                }
                else
                {
                    }
            }

            var cloudFrame = frameData.GetOrCreate<CloudLayerFrameData>();
            var renderLightDirection = _hasCommittedBake
                ? _committedLightDirectionWs
                : lightDirectionWs;
            bool renderHasLight = hasLight && (!_hasCommittedBake || _committedHasLight);

            PopulateFrameData(
                cloudFrame,
                cloudLayer,
                cloudTexture,
                _textureHandle.rt,
                hasLight ? lightContext : default,
                renderHasLight,
                renderLightDirection,
                atmosphereFrame.VisualEnvironment);
        }

        private void BeginLightingJob(
            CloudLayer cloudLayer,
            int lightingHash,
            Vector3 lightDirectionWs,
            bool hasLight,
            float planetRadiusMeters)
        {
            FillLayerData(cloudLayer, hasLight, _pendingLayerOpacities, _pendingLayerParameters);
            _pendingParameters = BuildBakeParameters(
                lightDirectionWs,
                cloudLayer.UpperHemisphereOnly.value,
                planetRadiusMeters);
            _pendingLightingHash = lightingHash;
            _pendingLightingSlice = 0;
            _pendingDoubleLayer = cloudLayer.LayerCount == 2;
            _pendingLightDirectionWs = lightDirectionWs;
            _pendingHasLight = hasLight;
            _lightingJobActive = true;
        }

        private void CommitBake(
            int baseHash,
            int lightingHash,
            Vector3 lightDirectionWs,
            bool hasLight)
        {
            _lastBaseHash = baseHash;
            _lastLightingHash = lightingHash;
            _committedLightDirectionWs = lightDirectionWs;
            _committedHasLight = hasLight;
            _hasCommittedBake = true;
            _lightingJobActive = false;
            _pendingLightingHash = int.MinValue;
        }

        private void RecordBaseBake(
            RenderGraph renderGraph,
            TextureHandle sourceTextureA,
            TextureHandle sourceTextureB,
            TextureHandle output,
            Vector4 parameters,
            Vector4[] layerOpacities,
            Vector4[] layerParameters,
            int width,
            int height,
            bool doubleLayer)
        {
            using var profiledPass = BaseProfile.AddComputePass<BasePassData>(
                renderGraph,
                out var pass);
            var builder = profiledPass.Builder;
            pass.Shader = _shader;
            pass.Kernel = doubleLayer ? _baseDoubleKernel : _baseSingleKernel;
            pass.Output = output;
            pass.SourceTextureA = sourceTextureA;
            pass.SourceTextureB = sourceTextureB;
            pass.LayerOpacities = (Vector4[])layerOpacities.Clone();
            pass.LayerParameters = (Vector4[])layerParameters.Clone();
            pass.Parameters = parameters;
            pass.BaseSize = BuildSizeVector(width, height);
            pass.Width = width;
            pass.Height = height;
            pass.DoubleLayer = doubleLayer;

            builder.UseTexture(pass.SourceTextureA);
            if (doubleLayer)
                builder.UseTexture(pass.SourceTextureB);
            builder.UseTexture(pass.Output, AccessFlags.Write);
            builder.AllowPassCulling(false);
            builder.SetRenderFunc(static (BasePassData data, ComputeGraphContext context) =>
            {
                var command = context.cmd;
                var writer = ShaderValueWriter.ForCompute(command, data.Shader);
                command.SetComputeTextureParam(
                    data.Shader, data.Kernel, ShaderProperties.CloudLayer.BaseTextureRW, data.Output);
                command.SetComputeTextureParam(
                    data.Shader, data.Kernel, ShaderProperties.CloudLayer.SourceTextureA, data.SourceTextureA);
                if (data.DoubleLayer)
                {
                    command.SetComputeTextureParam(
                        data.Shader, data.Kernel, ShaderProperties.CloudLayer.SourceTextureB, data.SourceTextureB);
                }
                writer.SetVectorArray(ShaderProperties.CloudLayer.LayerOpacities, data.LayerOpacities);
                writer.SetVectorArray(ShaderProperties.CloudLayer.LayerParameters, data.LayerParameters);
                writer.SetVector(ShaderProperties.CloudLayer.Parameters, data.Parameters);
                writer.SetVector(ShaderProperties.CloudLayer.BaseSize, data.BaseSize);
                command.DispatchCompute(
                    data.Shader,
                    data.Kernel,
                    CoreUtils.DivRoundUp(data.Width, 8),
                    CoreUtils.DivRoundUp(data.Height, 8),
                    1);
            });
        }

        private void RecordLightingBake(
            RenderGraph renderGraph,
            TextureHandle baseTexture,
            TextureHandle sourceTextureA,
            TextureHandle sourceTextureB,
            TextureHandle output,
            Vector4 parameters,
            Vector4[] layerOpacities,
            Vector4[] layerParameters,
            int rowOffset,
            int rowCount,
            bool doubleLayer)
        {
            using var profiledPass = LightingProfile.AddComputePass<LightingPassData>(
                renderGraph,
                out var pass);
            var builder = profiledPass.Builder;
            pass.Shader = _shader;
            pass.Kernel = doubleLayer ? _lightingDoubleKernel : _lightingSingleKernel;
            pass.Base = baseTexture;
            pass.SourceTextureA = sourceTextureA;
            pass.SourceTextureB = sourceTextureB;
            pass.Output = output;
            pass.LayerOpacities = (Vector4[])layerOpacities.Clone();
            pass.LayerParameters = (Vector4[])layerParameters.Clone();
            pass.Parameters = parameters;
            pass.BaseSize = BuildSizeVector(_textureWidth, _textureHeight);
            pass.LightingSize = BuildSizeVector(_lightingWidth, _lightingHeight);
            pass.Width = _lightingWidth;
            pass.RowOffset = rowOffset;
            pass.RowCount = rowCount;
            pass.DoubleLayer = doubleLayer;

            builder.UseTexture(pass.Base);
            builder.UseTexture(pass.SourceTextureA);
            if (doubleLayer)
                builder.UseTexture(pass.SourceTextureB);
            builder.UseTexture(pass.Output, AccessFlags.Write);
            builder.AllowPassCulling(false);
            builder.SetRenderFunc(static (LightingPassData data, ComputeGraphContext context) =>
            {
                var command = context.cmd;
                var writer = ShaderValueWriter.ForCompute(command, data.Shader);
                command.SetComputeTextureParam(
                    data.Shader, data.Kernel, ShaderProperties.CloudLayer.BaseTexture, data.Base);
                command.SetComputeTextureParam(
                    data.Shader, data.Kernel, ShaderProperties.CloudLayer.SourceTextureA, data.SourceTextureA);
                if (data.DoubleLayer)
                {
                    command.SetComputeTextureParam(
                        data.Shader, data.Kernel, ShaderProperties.CloudLayer.SourceTextureB, data.SourceTextureB);
                }
                command.SetComputeTextureParam(
                    data.Shader, data.Kernel, ShaderProperties.CloudLayer.LightingTextureRW, data.Output);
                writer.SetVectorArray(ShaderProperties.CloudLayer.LayerOpacities, data.LayerOpacities);
                writer.SetVectorArray(ShaderProperties.CloudLayer.LayerParameters, data.LayerParameters);
                writer.SetVector(ShaderProperties.CloudLayer.Parameters, data.Parameters);
                writer.SetVector(ShaderProperties.CloudLayer.BaseSize, data.BaseSize);
                writer.SetVector(ShaderProperties.CloudLayer.LightingSize, data.LightingSize);
                writer.SetInt(ShaderProperties.CloudLayer.LightingRowOffset, data.RowOffset);
                writer.SetInt(ShaderProperties.CloudLayer.LightingRowCount, data.RowCount);
                command.DispatchCompute(
                    data.Shader,
                    data.Kernel,
                    CoreUtils.DivRoundUp(data.Width, 8),
                    CoreUtils.DivRoundUp(data.RowCount, 8),
                    1);
            });
        }

        private void RecordCombine(
            RenderGraph renderGraph,
            TextureHandle baseTexture,
            TextureHandle lightingTexture,
            TextureHandle output,
            Vector4 parameters,
            int width,
            int height,
            bool doubleLayer)
        {
            using var profiledPass = CombineProfile.AddComputePass<CombinePassData>(
                renderGraph,
                out var pass);
            var builder = profiledPass.Builder;
            pass.Shader = _shader;
            pass.Kernel = doubleLayer ? _combineDoubleKernel : _combineSingleKernel;
            pass.Base = baseTexture;
            pass.Lighting = lightingTexture;
            pass.Output = output;
            pass.Parameters = parameters;
            pass.BaseSize = BuildSizeVector(width, height);
            pass.LightingSize = BuildSizeVector(_lightingWidth, _lightingHeight);
            pass.Width = width;
            pass.Height = height;
            pass.DoubleLayer = doubleLayer;

            builder.UseTexture(pass.Base);
            builder.UseTexture(pass.Lighting);
            builder.UseTexture(pass.Output, AccessFlags.Write);
            builder.AllowPassCulling(false);
            builder.SetRenderFunc(static (CombinePassData data, ComputeGraphContext context) =>
            {
                var command = context.cmd;
                var writer = ShaderValueWriter.ForCompute(command, data.Shader);
                command.SetComputeTextureParam(
                    data.Shader, data.Kernel, ShaderProperties.CloudLayer.BaseTexture, data.Base);
                command.SetComputeTextureParam(
                    data.Shader, data.Kernel, ShaderProperties.CloudLayer.LightingTexture, data.Lighting);
                command.SetComputeTextureParam(
                    data.Shader, data.Kernel, ShaderProperties.CloudLayer.TextureId, data.Output);
                writer.SetVector(ShaderProperties.CloudLayer.Parameters, data.Parameters);
                writer.SetVector(ShaderProperties.CloudLayer.BaseSize, data.BaseSize);
                writer.SetVector(ShaderProperties.CloudLayer.LightingSize, data.LightingSize);
                command.DispatchCompute(
                    data.Shader,
                    data.Kernel,
                    CoreUtils.DivRoundUp(data.Width, 8),
                    CoreUtils.DivRoundUp(data.Height, 8),
                    1);
            });
        }

        private static Vector4 BuildSizeVector(int width, int height) =>
            new(width, height, 1.0f / width, 1.0f / height);

        private static Vector4 BuildBakeParameters(
            Vector3 lightDirectionWs,
            bool upperHemisphereOnly,
            float planetRadiusMeters) =>
            new(
                lightDirectionWs.x,
                lightDirectionWs.y,
                lightDirectionWs.z,
                (upperHemisphereOnly ? 1.0f : -1.0f) * planetRadiusMeters);

        private static void FillLayerData(
            CloudLayer cloudLayer,
            bool hasLight,
            Vector4[] opacities,
            Vector4[] parameters)
        {
            opacities[0] = cloudLayer.LayerA.Opacities;
            opacities[1] = cloudLayer.LayerB.Opacities;
            parameters[0] = cloudLayer.LayerA.GetBakeParameters();
            parameters[1] = cloudLayer.LayerB.GetBakeParameters();

            if (!hasLight)
            {
                parameters[0].y = 0.0f;
                parameters[1].y = 0.0f;
            }
        }

        private static int ComputeBaseHash(
            CloudLayer cloudLayer,
            Texture mapA,
            Texture mapB,
            bool hasLight,
            float planetRadiusMeters)
        {
            var hash = new HashCode();
            hash.Add(cloudLayer.UpperHemisphereOnly.value);
            hash.Add(cloudLayer.Layers.value);
            hash.Add(cloudLayer.Resolution.value);
            hash.Add(planetRadiusMeters);
            AddBaseLayerHash(ref hash, cloudLayer.LayerA, mapA, hasLight);
            if (cloudLayer.LayerCount == 2)
                AddBaseLayerHash(ref hash, cloudLayer.LayerB, mapB, hasLight);
            return hash.ToHashCode();
        }

        private static void AddBaseLayerHash(
            ref HashCode hash,
            CloudLayer.Map layer,
            Texture map,
            bool hasLight)
        {
            hash.Add(map ? map.GetInstanceID() : 0);
            hash.Add(layer.OpacityR.value);
            hash.Add(layer.OpacityG.value);
            hash.Add(layer.OpacityB.value);
            hash.Add(layer.OpacityA.value);
            hash.Add(layer.Rotation.value);

            bool shadowedAlpha = hasLight && layer.SelfShadow.value;
            hash.Add(shadowedAlpha);
            if (shadowedAlpha)
            {
                hash.Add(layer.Altitude.value);
                hash.Add(layer.Density.value);
            }
        }

        private static int ComputeLightingHash(
            CloudLayer cloudLayer,
            int baseHash,
            bool hasLight,
            Vector3 lightDirectionWs,
            int angularResolution)
        {
            var hash = new HashCode();
            hash.Add(baseHash);
            int lightStepsA = hasLight ? cloudLayer.LayerA.BakedLightSteps : 0;
            int lightStepsB = hasLight && cloudLayer.LayerCount == 2
                ? cloudLayer.LayerB.BakedLightSteps
                : 0;
            hash.Add(lightStepsA);
            hash.Add(lightStepsB);

            if (lightStepsA > 0 || lightStepsB > 0)
            {
                var direction = lightDirectionWs.sqrMagnitude > 0.0f
                    ? lightDirectionWs.normalized
                    : Vector3.zero;
                int resolution = Mathf.Max(1, angularResolution);
                hash.Add(new Vector2Int(
                    Mathf.RoundToInt(
                        Mathf.Atan2(direction.x, direction.z) * resolution / (2.0f * Mathf.PI)),
                    Mathf.RoundToInt(
                        Mathf.Asin(Mathf.Clamp(direction.y, -1.0f, 1.0f)) * resolution / Mathf.PI)));
            }

            return hash.ToHashCode();
        }

        private static void PopulateFrameData(
            CloudLayerFrameData frame,
            CloudLayer cloudLayer,
            TextureHandle cloudTexture,
            RenderTexture textureResource,
            in LightContext lightContext,
            bool hasLight,
            Vector3 lightDirectionWs,
            VisualEnvironment visualEnvironment)
        {
            frame.IsActive = true;
            frame.Texture = cloudTexture;
            frame.TextureResource = textureResource;
            frame.ShaderData = new CloudLayerShaderData
            {
                GlobalParams = new Vector4(
                    1.0f,
                    cloudLayer.UpperHemisphereOnly.value ? 1.0f : 0.0f,
                    cloudLayer.Opacity.value,
                    cloudLayer.LayerCount),
                SunDirection = new Vector4(lightDirectionWs.x, lightDirectionWs.y, lightDirectionWs.z, 0.0f),
                LightParam = EvaluateLayerLightParam(cloudLayer.LayerA, lightContext, hasLight),
                LightParamB = EvaluateLayerLightParam(cloudLayer.LayerB, lightContext, hasLight),
                AmbientColor = EvaluateAmbientColor(cloudLayer.LayerA),
                AmbientColorB = EvaluateAmbientColor(cloudLayer.LayerB),
                AmbientProbeDimmers = new Vector4(
                    cloudLayer.LayerA.AmbientProbeDimmer.value,
                    cloudLayer.LayerB.AmbientProbeDimmer.value,
                    0.0f,
                    0.0f),
                AnimationParamA = cloudLayer.LayerA.GetAnimationParameters(visualEnvironment),
                AnimationParamB = cloudLayer.LayerB.GetAnimationParameters(visualEnvironment)
            };
            frame.SettingsHash = cloudLayer.GetHashCode();
        }

        private static Vector4 EvaluateLayerLightParam(
            CloudLayer.Map layer,
            in LightContext lightContext,
            bool hasLight)
        {
            var lightColor = hasLight ? lightContext.CloudSunColor : Color.black;
            var tint = AtmosphereTools.SanitizeColor(layer.EvaluatedTint);
            var directColor = AtmosphereTools.SanitizeColor(new Color(
                lightColor.r * tint.r,
                lightColor.g * tint.g,
                lightColor.b * tint.b,
                1.0f));

            return new Vector4(
                directColor.r,
                directColor.g,
                directColor.b,
                layer.Altitude.value);
        }

        private static Vector4 EvaluateAmbientColor(CloudLayer.Map layer)
        {
            var probe = AmbientProbeShaderData.From(RenderSettings.ambientProbe);
            Vector4 direction = new(0.0f, -1.0f, 0.0f, 1.0f);
            float r = Vector4.Dot(probe.SHAr, direction) - probe.SHC.x;
            float g = Vector4.Dot(probe.SHAg, direction) - probe.SHC.y;
            float b = Vector4.Dot(probe.SHAb, direction) - probe.SHC.z;
            float dimmer = layer.AmbientProbeDimmer.value;
            return AtmosphereTools.SanitizeColor(new Color(
                r * dimmer,
                g * dimmer,
                b * dimmer,
                1.0f));
        }

        private Texture ResolveMap(CloudLayer.Map layer) =>
            layer.Texture.value as Texture2D ?? _defaultMap;

        private void UpdateScroll(CloudLayer cloudLayer, VisualEnvironment visualEnvironment)
        {
            float currentTime = Application.isPlaying ? Time.time : Time.realtimeSinceStartup;
            float deltaTime = float.IsNaN(_lastAnimationTime)
                ? 0.0f
                : currentTime - _lastAnimationTime;
            _lastAnimationTime = currentTime;
            cloudLayer.LayerA.UpdateScroll(deltaTime, visualEnvironment);
            if (cloudLayer.LayerCount == 2)
                cloudLayer.LayerB.UpdateScroll(deltaTime, visualEnvironment);
        }

        private void EnsureTextures(int width, int height, int layers)
        {
            int lightingWidth = Mathf.Max(1, width / LightingResolutionDivisor);
            int lightingHeight = Mathf.Max(1, height / LightingResolutionDivisor);
            if (_textureHandle != null &&
                _baseTextureHandle != null &&
                _lightingTextureHandle != null &&
                _textureWidth == width &&
                _textureHeight == height &&
                _textureLayers == layers &&
                _lightingWidth == lightingWidth &&
                _lightingHeight == lightingHeight &&
                _textureHandle.rt && _textureHandle.rt.IsCreated() &&
                _baseTextureHandle.rt && _baseTextureHandle.rt.IsCreated() &&
                _lightingTextureHandle.rt && _lightingTextureHandle.rt.IsCreated())
                return;

            ReleaseTextures();

            _textureHandle = RTHandles.Alloc(
                width,
                height,
                slices: layers,
                wrapModeU: TextureWrapMode.Repeat,
                wrapModeV: TextureWrapMode.Clamp,
                colorFormat: GraphicsFormat.R16G16_SFloat,
                dimension: TextureDimension.Tex2DArray,
                enableRandomWrite: true,
                useMipMap: false,
                filterMode: FilterMode.Bilinear,
                name: "Cloud Layer Texture");
            _baseTextureHandle = RTHandles.Alloc(
                width,
                height,
                slices: layers,
                wrapModeU: TextureWrapMode.Repeat,
                wrapModeV: TextureWrapMode.Clamp,
                colorFormat: GraphicsFormat.R16G16_SFloat,
                dimension: TextureDimension.Tex2DArray,
                enableRandomWrite: true,
                useMipMap: false,
                filterMode: FilterMode.Bilinear,
                name: "Cloud Layer Base");
            _lightingTextureHandle = RTHandles.Alloc(
                lightingWidth,
                lightingHeight,
                slices: layers,
                wrapModeU: TextureWrapMode.Repeat,
                wrapModeV: TextureWrapMode.Clamp,
                colorFormat: GraphicsFormat.R16G16_SFloat,
                dimension: TextureDimension.Tex2DArray,
                enableRandomWrite: true,
                useMipMap: false,
                filterMode: FilterMode.Bilinear,
                name: "Cloud Layer Lighting Work");

            _textureWidth = width;
            _textureHeight = height;
            _textureLayers = layers;
            _lightingWidth = lightingWidth;
            _lightingHeight = lightingHeight;
            _lastBaseHash = int.MinValue;
            _lastLightingHash = int.MinValue;
            _hasCommittedBake = false;
            _lightingJobActive = false;
        }

        private void ReleaseIfPreviouslyActive()
        {
            if (_wasActive)
                ReleaseRuntimeResources();
        }

        private void ReleaseRuntimeResources()
        {
            _wasActive = false;
            _lastBaseHash = int.MinValue;
            _lastLightingHash = int.MinValue;
            _lastAnimationTime = float.NaN;
            _hasCommittedBake = false;
            _lightingJobActive = false;
            ReleaseTextures();
            _sourceTextureCacheA.Dispose();
            _sourceTextureCacheB.Dispose();
        }

        private void ReleaseTextures()
        {
            _textureHandle?.Release();
            _baseTextureHandle?.Release();
            _lightingTextureHandle?.Release();
            _textureHandle = null;
            _baseTextureHandle = null;
            _lightingTextureHandle = null;
            _textureWidth = 0;
            _textureHeight = 0;
            _textureLayers = 0;
            _lightingWidth = 0;
            _lightingHeight = 0;
        }

        public void Dispose() => ReleaseRuntimeResources();
    }
}
