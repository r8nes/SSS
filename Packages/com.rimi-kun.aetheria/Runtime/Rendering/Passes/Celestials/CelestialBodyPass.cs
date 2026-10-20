using System;
using System.Collections.Generic;
using Aetheria.Runtime.Components;
using Aetheria.Runtime.Data;
using Aetheria.Runtime.Data.CelestialLight;
using Aetheria.Runtime.Overrides;
using Aetheria.Runtime.Utils;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace Aetheria.Runtime.Rendering.Passes
{
    public static class CelestialBodyPass
    {
        private static readonly AetheriaProfiler.Marker Profile =
            AetheriaProfiler.CreateMarker("Atmosphere", "Celestial");

        private static CelestialBodyData[] _bodyData;

        private static readonly Texture[] CookieSources = new Texture[AtmosphereData.MaxCelestialLights];
        private static readonly List<CelestialBody> RegisteredBodies = new();
        private static readonly List<CelestialLight> ResolvedBodies = new(AtmosphereData.MaxCelestialLights);
        private static readonly RenderGraphTextureHandleCache[] CookieSourceCaches =
            new RenderGraphTextureHandleCache[AtmosphereData.MaxCelestialLights];
        private static readonly int[] CookiePackedSlots = new int[AtmosphereData.MaxCelestialLights];
        private static readonly Vector4[] SharedColorRadius = new Vector4[AtmosphereData.MaxCelestialLights];
        private static readonly Vector4[] SharedForwardDistance = new Vector4[AtmosphereData.MaxCelestialLights];
        private static readonly Vector4[] SharedRightAngularRadius = new Vector4[AtmosphereData.MaxCelestialLights];
        private static readonly Vector4[] SharedUpType = new Vector4[AtmosphereData.MaxCelestialLights];
        private static readonly Vector4[] SharedSurfaceEarthshine = new Vector4[AtmosphereData.MaxCelestialLights];
        private static readonly Vector4[] SharedCookieAtlasScaleOffset = new Vector4[AtmosphereData.MaxCelestialLights];

        private static readonly Vector4[] SharedSunDirectionLimbDarkening =
            new Vector4[AtmosphereData.MaxCelestialLights];

        private static readonly Vector4[] SharedCookieParams = new Vector4[AtmosphereData.MaxCelestialLights];
        private static readonly Vector4[] SharedFlareColor = new Vector4[AtmosphereData.MaxCelestialLights];
        private static readonly Vector4[] SharedFlareParams = new Vector4[AtmosphereData.MaxCelestialLights];

        private static RenderTexture _cookieAtlas;
        
        private static RenderGraphTextureHandleCache _cookieAtlasCache;
        
        private static uint _lightCount;
        private static uint _bodyCount;


        private static int _cookieAtlasHash;
        private static int _cookieAtlasTileSize = -1;
        private static int _cookieAtlasColumns;
        private static int _cookieAtlasRows;
        private static int _cookieCount;
        private static bool _cookieAtlasDirty;

        private const int CookieAtlasMaxGridSize = 4;
        private const int CookieAtlasCapacity = CookieAtlasMaxGridSize * CookieAtlasMaxGridSize;
        private const int CookieAtlasPaddingPixels = 2;
        private const int MaxCookieTileSize = 1024;
        private const float SunDiskLightingUnitsMultiplier = 50.0f;

        public readonly struct GlobalStateData
        {
            public readonly TextureHandle CookieAtlas;
            public readonly int BodyCount;
            public readonly int LightCount;
            internal readonly Vector4[] ColorRadius;
            internal readonly Vector4[] ForwardDistance;
            internal readonly Vector4[] RightAngularRadius;
            internal readonly Vector4[] UpType;
            internal readonly Vector4[] SurfaceEarthshine;
            internal readonly Vector4[] CookieAtlasScaleOffset;
            internal readonly Vector4[] SunDirectionLimbDarkening;
            internal readonly Vector4[] CookieParams;
            internal readonly Vector4[] FlareColor;
            internal readonly Vector4[] FlareParams;

            public GlobalStateData(
                TextureHandle cookieAtlas,
                int bodyCount,
                int lightCount,
                Vector4[] colorRadius,
                Vector4[] forwardDistance,
                Vector4[] rightAngularRadius,
                Vector4[] upType,
                Vector4[] surfaceEarthshine,
                Vector4[] cookieAtlasScaleOffset,
                Vector4[] sunDirectionLimbDarkening,
                Vector4[] cookieParams,
                Vector4[] flareColor,
                Vector4[] flareParams)
            {
                CookieAtlas = cookieAtlas;
                BodyCount = bodyCount;
                LightCount = lightCount;
                ColorRadius = colorRadius;
                ForwardDistance = forwardDistance;
                RightAngularRadius = rightAngularRadius;
                UpType = upType;
                SurfaceEarthshine = surfaceEarthshine;
                CookieAtlasScaleOffset = cookieAtlasScaleOffset;
                SunDirectionLimbDarkening = sunDirectionLimbDarkening;
                CookieParams = cookieParams;
                FlareColor = flareColor;
                FlareParams = flareParams;
            }
        }

        private sealed class AtlasPassData
        {
            public TextureHandle[] CookieTextures;
            public int[] PackedSlots;
            public int BodyCount;
            public int TileSize;
            public int Columns;
            public int Width;
            public int Height;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            ReleaseSharedResources();
            _lightCount = 0;
            _bodyCount = 0;
            _cookieAtlasHash = 0;
            _cookieAtlasTileSize = -1;
            _cookieAtlasColumns = 0;
            _cookieAtlasRows = 0;
            _cookieCount = 0;
            _cookieAtlasDirty = false;
        }

        internal static void ReleaseSharedResources()
        {
            _bodyData = null;

            _cookieAtlasCache.Dispose();
            AtmosphereTools.ReleaseRenderTexture(_cookieAtlas);
            _cookieAtlas = null;


            for (int i = 0; i < CookieSourceCaches.Length; i++)
                CookieSourceCaches[i].Dispose();

            for (int i = 0; i < CookieSources.Length; i++)
            {
                CookieSources[i] = null;
                CookiePackedSlots[i] = -1;
            }

            _cookieAtlasDirty = false;
        }

        private static float ComputeAngularRadius(CelestialBody celestial)
        {
            var settings = celestial.Settings;
            
            float angularDiameter = settings.DiameterMultiplierMode
                ? settings.DiameterMultiplier * settings.AngularDiameter
                : settings.DiameterOverride;

            return angularDiameter * 0.5f * Mathf.Deg2Rad;
        }

        internal static void PopulateAtmosphereData(AtmosphereData data)
        {
            if (_bodyData == null)
            {
                data.CelestialStateHash = 0;
                data.CelestialLightCount = 0;
                return;
            }

            int count = Mathf.Min((int)_lightCount, AtmosphereData.MaxCelestialLights);
            data.CelestialLightCount = count;

            var hash = new HashCode();
            hash.Add(count);

            for (int i = 0; i < count; i++)
            {
                ref readonly var src = ref _bodyData[i];
                data.CelestialDirs[i] = new Vector4(src.forward.x, src.forward.y, src.forward.z, 0f);
                data.CelestialColors[i] = new Color(src.color.x, src.color.y, src.color.z, 1f);
                data.CelestialAngularRadii[i] = src.angularRadius;
                data.CelestialDistanceFromCamera[i] = src.distanceFromCamera;
                hash.Add(src.forward);
                hash.Add(src.color);
                hash.Add(src.angularRadius);
                hash.Add(src.surfaceColor);
                hash.Add(src.flareColor);
                hash.Add(src.flareCosInner);
                hash.Add(src.flareCosOuter);
                hash.Add(src.flareSize);
                hash.Add(src.flareFalloff);
            }

            data.CelestialStateHash = hash.ToHashCode();
        }

        internal static CelestialLight PrepareFrameData(
            UniversalLightData lightData,
            PhysicallyBasedSky volume,
            VisualEnvironment visualEnvironment,
            Camera camera)
        {
            EnsureBodyDataArray();

            CelestialBody.CollectActiveInstances(RegisteredBodies);
            var lightFrame = LightResolver.ResolveFrame(lightData, RegisteredBodies, ResolvedBodies);

            uint lights = 0;

            for (int i = 0; i < ResolvedBodies.Count && lights < AtmosphereData.MaxCelestialLights; i++)
            {
                var resolved = ResolvedBodies[i];
                
                if (!resolved.Body.InteractsWithSky || resolved.Luminance <= 0f)
                    continue;

                FillBodyData(
                    resolved,
                    lightData,
                    lightFrame.BrightestLight,
                    lightFrame.SecondBrightestLight,
                    (int)lights,
                    ref _bodyData[(int)lights]);
                lights++;
            }

            uint bodies = lights;

            for (int i = 0; i < ResolvedBodies.Count && bodies < AtmosphereData.MaxCelestialLights; i++)
            {
                var resolved = ResolvedBodies[i];
                
                if (!resolved.Body.InteractsWithSky || resolved.Luminance > 0f)
                    continue;

                FillBodyData(
                    resolved,
                    lightData,
                    lightFrame.BrightestLight,
                    lightFrame.SecondBrightestLight,
                    (int)bodies,
                    ref _bodyData[(int)bodies]);
                bodies++;
            }

            for (int i = 0; i < ResolvedBodies.Count && bodies < AtmosphereData.MaxCelestialLights; i++)
            {
                var resolved = ResolvedBodies[i];
                
                if (resolved.Body.InteractsWithSky)
                    continue;

                FillBodyData(
                    resolved,
                    lightData,
                    lightFrame.BrightestLight,
                    lightFrame.SecondBrightestLight,
                    (int)bodies,
                    ref _bodyData[(int)bodies]);
                bodies++;
            }

            _lightCount = lights;
            _bodyCount = bodies;

            for (int i = (int)_bodyCount; i < CookieSources.Length; i++)
                CookieSources[i] = null;

            UpdateCookieMetadata();
            ApplyCookieScaleOffsets();
            PackBodyData();

            if (LightResolver.TryResolveContext(
                    lightFrame.PrimaryLight,
                    volume,
                    visualEnvironment,
                    camera,
                    out var lightContext))
                LightResolver.ApplySurfaceLightToUrp(lightData, lightFrame.PrimaryLight, lightContext);

            return lightFrame.PrimaryLight;
        }

        internal static CelestialLight ResolvePrimaryLight(UniversalLightData lightData)
        {
            CelestialBody.CollectActiveInstances(RegisteredBodies);
            return LightResolver.ResolveFrame(lightData, RegisteredBodies, ResolvedBodies).PrimaryLight;
        }

        internal static GlobalStateData BuildGlobalStateData(
            RenderGraph renderGraph,
            AtmosphereData cameraData)
        {
            if (cameraData == null)
                throw new ArgumentNullException(nameof(cameraData));

            Array.Copy(SharedColorRadius, cameraData.CelestialColorRadius, AtmosphereData.MaxCelestialLights);
            Array.Copy(SharedForwardDistance, cameraData.CelestialForwardDistance, AtmosphereData.MaxCelestialLights);
            Array.Copy(SharedRightAngularRadius, cameraData.CelestialRightAngularRadius,
                AtmosphereData.MaxCelestialLights);
            Array.Copy(SharedUpType, cameraData.CelestialUpType, AtmosphereData.MaxCelestialLights);
            Array.Copy(SharedSurfaceEarthshine, cameraData.CelestialSurfaceEarthshine,
                AtmosphereData.MaxCelestialLights);
            Array.Copy(SharedCookieAtlasScaleOffset, cameraData.CelestialCookieAtlasScaleOffset,
                AtmosphereData.MaxCelestialLights);
            Array.Copy(SharedSunDirectionLimbDarkening, cameraData.CelestialSunDirectionLimbDarkening,
                AtmosphereData.MaxCelestialLights);
            Array.Copy(SharedCookieParams, cameraData.CelestialCookieParams, AtmosphereData.MaxCelestialLights);
            Array.Copy(SharedFlareColor, cameraData.CelestialFlareColor, AtmosphereData.MaxCelestialLights);
            Array.Copy(SharedFlareParams, cameraData.CelestialFlareParams, AtmosphereData.MaxCelestialLights);

            return new GlobalStateData(
                ImportCookie(renderGraph),
                (int)_bodyCount,
                (int)_lightCount,
                cameraData.CelestialColorRadius,
                cameraData.CelestialForwardDistance,
                cameraData.CelestialRightAngularRadius,
                cameraData.CelestialUpType,
                cameraData.CelestialSurfaceEarthshine,
                cameraData.CelestialCookieAtlasScaleOffset,
                cameraData.CelestialSunDirectionLimbDarkening,
                cameraData.CelestialCookieParams,
                cameraData.CelestialFlareColor,
                cameraData.CelestialFlareParams);
        }

        public static void BindComputeParams(
            ComputeCommandBuffer cmd,
            ComputeShader shader,
            int kernel,
            GlobalStateData passData)
        {
            var writer = ShaderValueWriter.ForCompute(cmd, shader);
            BindShaderParameters(ref writer, passData);

            if (passData.CookieAtlas.IsValid())
                cmd.SetComputeTextureParam(shader, kernel, ShaderProperties.Celestial.CookieAtlasId, passData.CookieAtlas);
        }

        public static void BindMaterialParams(
            MaterialPropertyBlock properties,
            GlobalStateData passData)
        {
            var writer = ShaderValueWriter.ForMaterialPropertyBlock(properties);
            BindShaderParameters(ref writer, passData);
        }

        public static void BindMaterialParams(
            Material material,
            GlobalStateData passData)
        {
            if (!material) return;

            var writer = ShaderValueWriter.ForMaterial(material);
            BindShaderParameters(ref writer, passData);
        }

        private static void BindShaderParameters(
            ref ShaderValueWriter writer,
            GlobalStateData passData)
        {
            writer.SetVectorArray(ShaderProperties.Celestial.ColorRadius, passData.ColorRadius);
            writer.SetVectorArray(ShaderProperties.Celestial.ForwardDistance, passData.ForwardDistance);
            writer.SetVectorArray(ShaderProperties.Celestial.RightAngularRadius, passData.RightAngularRadius);
            writer.SetVectorArray(ShaderProperties.Celestial.UpType, passData.UpType);
            writer.SetVectorArray(ShaderProperties.Celestial.SurfaceEarthshine, passData.SurfaceEarthshine);
            writer.SetVectorArray(ShaderProperties.Celestial.CookieAtlasScaleOffset, passData.CookieAtlasScaleOffset);
            writer.SetVectorArray(ShaderProperties.Celestial.SunDirectionLimbDarkening, passData.SunDirectionLimbDarkening);
            writer.SetVectorArray(ShaderProperties.Celestial.CookieParams, passData.CookieParams);
            writer.SetVectorArray(ShaderProperties.Celestial.FlareColor, passData.FlareColor);
            writer.SetVectorArray(ShaderProperties.Celestial.FlareParams, passData.FlareParams);
            BindCelestialScalars(ref writer, passData);
        }

        private static void BindCelestialScalars(
            ref ShaderValueWriter writer,
            GlobalStateData passData)
        {
            writer.SetInt(ShaderProperties.Celestial.BodyCount, passData.BodyCount);
            writer.SetInt(ShaderProperties.Celestial.LightCount, passData.LightCount);
        }

        private static void EnsureBodyDataArray() =>
            _bodyData ??= new CelestialBodyData[AtmosphereData.MaxCelestialLights];

        private static TextureHandle ImportCookie(RenderGraph renderGraph)
        {
            return _cookieAtlas
                ? _cookieAtlasCache.Import(renderGraph, _cookieAtlas)
                : renderGraph.defaultResources.whiteTexture;
        }

        private static void PackBodyData()
        {
            for (int i = 0; i < AtmosphereData.MaxCelestialLights; i++)
            {
                ref readonly var body = ref _bodyData[i];

                SharedColorRadius[i] = new Vector4(body.color.x, body.color.y, body.color.z, body.radius);
                SharedForwardDistance[i] =
                    new Vector4(body.forward.x, body.forward.y, body.forward.z, body.distanceFromCamera);
                SharedRightAngularRadius[i] =
                    new Vector4(body.right.x, body.right.y, body.right.z, body.angularRadius);
                SharedUpType[i] = new Vector4(body.up.x, body.up.y, body.up.z, body.type);
                SharedSurfaceEarthshine[i] = new Vector4(body.surfaceColor.x, body.surfaceColor.y,
                    body.surfaceColor.z,
                    body.earthshine);
                SharedCookieAtlasScaleOffset[i] = body.cookieAtlasScaleOffset;
                SharedSunDirectionLimbDarkening[i] = new Vector4(body.sunDirection.x, body.sunDirection.y,
                    body.sunDirection.z, body.limbDarkening);
                SharedCookieParams[i] = new Vector4(body.cookieRotation, body.lunarLambertWeight, body.padding, 0f);
                SharedFlareColor[i] = new Vector4(body.flareColor.x, body.flareColor.y, body.flareColor.z,
                    body.flareFalloff);
                SharedFlareParams[i] = new Vector4(body.flareCosInner, body.flareCosOuter, body.flareSize, 0f);
            }
        }

        internal static void RecordCookiePass(RenderGraph renderGraph)
        {
            if (!_cookieAtlas || !_cookieAtlasDirty) return;

            var atlasHandle = ImportCookie(renderGraph);
            var cookieTextures = new TextureHandle[CookieAtlasCapacity];

            for (int i = 0; i < CookieAtlasCapacity; i++)
                cookieTextures[i] = ImportSourceTexture(renderGraph, i);

            using var profiledPass = Profile.AddRasterPass<AtlasPassData>(
                renderGraph,
                out var pd);
            var builder = profiledPass.Builder;

            pd.CookieTextures = cookieTextures;
            pd.PackedSlots = CookiePackedSlots;
            pd.BodyCount = (int)_bodyCount;
            pd.TileSize = _cookieAtlasTileSize;
            pd.Columns = _cookieAtlasColumns;
            pd.Width = _cookieAtlas.width;
            pd.Height = _cookieAtlas.height;

            builder.SetRenderAttachment(atlasHandle, 0, AccessFlags.WriteAll);

            for (int i = 0; i < cookieTextures.Length; i++)
            {
                if (cookieTextures[i].IsValid())
                    builder.UseTexture(cookieTextures[i]);
            }

            // The atlas is a persistent cache consumed by later frames; its dirty flag is cleared below.
            builder.AllowPassCulling(false);
            
            builder.SetRenderFunc(static (AtlasPassData data, RasterGraphContext ctx) =>
            {
                ctx.cmd.SetViewport(new Rect(0f, 0f, data.Width, data.Height));
                ctx.cmd.ClearRenderTarget(false, true, Color.white);

                int innerTileSize = Mathf.Max(data.TileSize - 2 * CookieAtlasPaddingPixels, 1);

                for (int i = 0; i < data.BodyCount; i++)
                {
                    if (!data.CookieTextures[i].IsValid()) continue;

                    int packedSlot = data.PackedSlots[i];
                    if (packedSlot < 0) continue;

                    int column = packedSlot % data.Columns;
                    int row = packedSlot / data.Columns;

                    ctx.cmd.SetViewport(new Rect(
                        column * data.TileSize + CookieAtlasPaddingPixels,
                        row * data.TileSize + CookieAtlasPaddingPixels,
                        innerTileSize,
                        innerTileSize));

                    Blitter.BlitTexture(ctx.cmd, data.CookieTextures[i], new Vector4(1f, 1f, 0f, 0f), 0, false);
                }

                ctx.cmd.SetViewport(new Rect(0f, 0f, data.Width, data.Height));
            });

            _cookieAtlasDirty = false;
        }

        private static void FillBodyData(
            in CelestialLight resolved,
            UniversalLightData lightData,
            in CelestialLight sun,
            in CelestialLight sun2,
            int slotIndex,
            ref CelestialBodyData data)
        {
            var celestial = resolved.Body;
            var light = resolved.Light;
            var t = light ? light.transform : celestial.transform;
            var lightColor = resolved.Color;
            var settings = celestial.Settings;

            CookieSources[slotIndex] = settings.SurfaceCookieTexture;

            data.color = new Vector3(lightColor.r, lightColor.g, lightColor.b);
            data.forward = t.forward;
            data.right = t.right.normalized;
            data.up = t.up.normalized;

            data.angularRadius = ComputeAngularRadius(celestial);
            data.distanceFromCamera = settings.Distance;
            data.radius = Mathf.Tan(data.angularRadius) * data.distanceFromCamera;

            var tint = new Vector3(settings.SurfaceTint.linear.r, settings.SurfaceTint.linear.g,
                settings.SurfaceTint.linear.b);

            data.earthshine = settings.Earthshine;
            data.limbDarkening = settings.LimbDarkening;
            data.cookieRotation = settings.CookieRotation * Mathf.Deg2Rad;
            data.lunarLambertWeight = settings.MoonLunarLambertWeight;
            data.cookieAtlasScaleOffset = Vector4.zero;
            data.padding = 0f;
            data.flareColor = Vector3.zero;
            data.flareFalloff = Mathf.Max(0f, settings.FlareFalloff);
            data.flareSize = Mathf.Max(0f, settings.FlareSize) * Mathf.Deg2Rad;
            data.flareCosInner = Mathf.Cos(data.angularRadius);
            data.flareCosOuter = Mathf.Cos(data.angularRadius + data.flareSize);

            if (settings.ShadingSource == CelestialBodyShadingSource.Emission)
            {
                float diskRadianceMultiplier = ComputeDiskRadianceMultiplier(data.angularRadius);
                var surfaceRadiance = Vector3.Scale(new Vector3(lightColor.r, lightColor.g, lightColor.b), tint) *
                                      diskRadianceMultiplier;

                data.type = CelestialBodyData.EmissionType;
                data.surfaceColor = surfaceRadiance;
                data.flareColor = surfaceRadiance;
                data.sunDirection = Vector3.forward;
                return;
            }

            Color sunColor;

            if (settings.ShadingSource == CelestialBodyShadingSource.Manual)
            {
                var rot = Quaternion.AngleAxis(settings.MoonPhaseRotation, data.forward);
                var remap = Quaternion.FromToRotation(Vector3.right, data.forward);

                float phase = settings.MoonPhase * 2f * Mathf.PI;

                sunColor = settings.SunColor * settings.SunIntensity;
                data.sunDirection = rot * remap * new Vector3(Mathf.Cos(phase), 0, Mathf.Sin(phase));
            }
            else
            {
                var alternateSun = sun.Light == light ? sun2 : sun;
                CelestialLight sunResolved;

                if (settings.SunLightOverride)
                {
                    LightResolver.TryResolveByLight(lightData, settings.SunLightOverride, out sunResolved);
                }
                else
                {
                    sunResolved = alternateSun;
                }

                if (sunResolved.IsValid && sunResolved.Light && sunResolved.Light != light)
                {
                    sunColor = sunResolved.Color;
                    data.sunDirection = sunResolved.ForwardWS;
                }
                else
                {
                    sunColor = Color.black;
                    data.sunDirection = Vector3.forward;
                }
            }

            data.type = CelestialBodyData.ReflectiveType;
            data.surfaceColor = Vector3.Scale(new Vector3(sunColor.r, sunColor.g, sunColor.b), tint);
        }

        private static float ComputeDiskRadianceMultiplier(float angularRadius)
        {
            float cosRadius = Mathf.Cos(Mathf.Max(0f, angularRadius));
            float solidAngle = 2f * Mathf.PI * Mathf.Max(1f - cosRadius, 1e-6f);
            return 1f / (solidAngle * SunDiskLightingUnitsMultiplier);
        }

        private static void ApplyCookieScaleOffsets()
        {
            for (int i = 0; i < (int)_bodyCount; i++)
                _bodyData[i].cookieAtlasScaleOffset = ComputeCookieScaleOffset(
                    CookiePackedSlots[i],
                    _cookieAtlasTileSize);
        }

        private static Vector4 ComputeCookieScaleOffset(int slotIndex, int tileSize)
        {
            if (slotIndex < 0 || _cookieAtlasColumns <= 0 || _cookieAtlasRows <= 0)
                return Vector4.zero;

            float tileStepX = 1f / _cookieAtlasColumns;
            float tileStepY = 1f / _cookieAtlasRows;
            
            int column = slotIndex % _cookieAtlasColumns;
            int row = slotIndex / _cookieAtlasColumns;

            float atlasWidth = Mathf.Max(_cookieAtlasColumns * Mathf.Max(tileSize, 4), 1);
            float atlasHeight = Mathf.Max(_cookieAtlasRows * Mathf.Max(tileSize, 4), 1);
            float paddingUvX = CookieAtlasPaddingPixels / atlasWidth;
            float paddingUvY = CookieAtlasPaddingPixels / atlasHeight;

            return new Vector4(
                tileStepX - 2f * paddingUvX,
                tileStepY - 2f * paddingUvY,
                column * tileStepX + paddingUvX,
                row * tileStepY + paddingUvY);
        }

        private static void UpdateCookieMetadata()
        {
            if (!HasAnyTextures())
            {
                ReleaseAtlas();

                for (int i = 0; i < CookieSourceCaches.Length; i++)
                    CookieSourceCaches[i].Dispose();

                _cookieAtlasHash = 0;
                _cookieAtlasTileSize = -1;
                _cookieAtlasColumns = 0;
                _cookieAtlasRows = 0;
                _cookieCount = 0;
                _cookieAtlasDirty = false;
                return;
            }

            UpdateCookieLayout();
            int tileSize = DetermineCookieTileSize();
            int atlasHash = ComputeAtlasHash(tileSize);

            if (_cookieAtlas && _cookieAtlasTileSize == tileSize && _cookieAtlasHash == atlasHash)
                return;

            _cookieAtlasTileSize = tileSize;
            _cookieAtlasHash = atlasHash;

            EnsureCookie(tileSize);
            _cookieAtlasDirty = true;
        }

        private static int DetermineCookieTileSize()
        {
            int maxDimension = 4;

            for (int i = 0; i < (int)_bodyCount; i++)
            {
                var texture = CookieSources[i];

                if (!texture) continue;

                maxDimension = Mathf.Max(maxDimension, Mathf.Max(texture.width, texture.height));
            }

            return Mathf.NextPowerOfTwo(Mathf.Clamp(maxDimension, 4, MaxCookieTileSize));
        }

        private static int ComputeAtlasHash(int tileSize)
        {
            unchecked
            {
                int hash = tileSize;
                hash = hash * 397 ^ (int)_bodyCount;
                hash = hash * 397 ^ _cookieAtlasColumns;
                hash = hash * 397 ^ _cookieAtlasRows;

                for (int i = 0; i < (int)_bodyCount; i++)
                {
                    var texture = CookieSources[i];
                    hash = hash * 397 ^ (texture ? texture.GetInstanceID() : 0);
                    hash = hash * 397 ^ (texture ? texture.width : 0);
                    hash = hash * 397 ^ (texture ? texture.height : 0);
                }

                return hash;
            }
        }

        private static bool HasAnyTextures()
        {
            for (int i = 0; i < (int)_bodyCount; i++)
            {
                if (CookieSources[i])
                    return true;
            }

            return false;
        }

        private static void EnsureCookie(int tileSize)
        {
            int atlasWidth = tileSize * _cookieAtlasColumns;
            int atlasHeight = tileSize * _cookieAtlasRows;

            if (_cookieAtlas &&
                _cookieAtlas.width == atlasWidth &&
                _cookieAtlas.height == atlasHeight)
                return;

            _cookieAtlasCache.Dispose();
            AtmosphereTools.ReleaseRenderTexture(_cookieAtlas);

            _cookieAtlas = new RenderTexture(atlasWidth, atlasHeight, 0, RenderTextureFormat.ARGB32)
            {
                name = "Celestial Cookie Atlas",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                useMipMap = false,
                autoGenerateMips = false
            };

            _cookieAtlas.Create();
        }

        private static void UpdateCookieLayout()
        {
            _cookieCount = 0;
            for (int i = 0; i < CookiePackedSlots.Length; ++i)
                CookiePackedSlots[i] = -1;

            for (int i = 0; i < (int)_bodyCount; ++i)
            {
                if (CookieSources[i])
                    CookiePackedSlots[i] = _cookieCount++;
            }

            switch (_cookieCount)
            {
                case <= 1:
                    _cookieAtlasColumns = 1;
                    _cookieAtlasRows = 1;
                    return;
                case <= 4:
                    _cookieAtlasColumns = 2;
                    _cookieAtlasRows = 2;
                    return;
                default:
                    _cookieAtlasColumns = Mathf.Min(
                        CookieAtlasMaxGridSize,
                        Mathf.CeilToInt(Mathf.Sqrt(_cookieCount)));
                    _cookieAtlasRows = CoreUtils.DivRoundUp(_cookieCount, _cookieAtlasColumns);
                    break;
            }
        }

        private static void ReleaseAtlas()
        {
            _cookieAtlasCache.Dispose();
            AtmosphereTools.ReleaseRenderTexture(_cookieAtlas);
            _cookieAtlas = null;
        }

        private static TextureHandle ImportSourceTexture(RenderGraph renderGraph, int slotIndex)
        {
            var source = slotIndex < (int)_bodyCount ? CookieSources[slotIndex] : null;
            if (source)
                return CookieSourceCaches[slotIndex].Import(renderGraph, source);

            CookieSourceCaches[slotIndex].Dispose();
            return TextureHandle.nullHandle;
        }
    }
}
