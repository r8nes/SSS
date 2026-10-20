using Aetheria.Runtime.Rendering;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Aetheria.Runtime.Utils
{
    internal static class AtmosphereTools
    {
        public readonly struct RenderTextureSpec
        {
            public readonly string Name;
            public readonly int Width;
            public readonly int Height;
            public readonly int Depth;
            public readonly GraphicsFormat Format;
            public readonly FilterMode Filter;
            public readonly TextureWrapMode WrapU;
            public readonly TextureWrapMode WrapV;
            public readonly TextureWrapMode WrapW;
            public readonly TextureDimension Dimension;
            public readonly bool EnableRandomWrite;
            public readonly bool UseMipMap;
            public readonly bool AutoGenerateMips;
            public readonly int MipCount;

            public RenderTextureSpec(
                string name,
                int width,
                int height,
                int depth,
                GraphicsFormat format,
                FilterMode filter,
                TextureWrapMode wrapU,
                TextureWrapMode wrapV,
                TextureWrapMode wrapW,
                TextureDimension dimension,
                bool enableRandomWrite,
                bool useMipMap = false,
                bool autoGenerateMips = false,
                int mipCount = 1)
            {
                Name = name;
                Width = width;
                Height = height;
                Depth = depth;
                Format = format;
                Filter = filter;
                WrapU = wrapU;
                WrapV = wrapV;
                WrapW = wrapW;
                Dimension = dimension;
                EnableRandomWrite = enableRandomWrite;
                UseMipMap = useMipMap;
                AutoGenerateMips = autoGenerateMips;
                MipCount = Mathf.Max(1, mipCount);
            }

            public static RenderTextureSpec Lut2D(
                string name,
                int width,
                int height,
                GraphicsFormat format,
                FilterMode filter = FilterMode.Bilinear,
                bool enableRandomWrite = false,
                TextureWrapMode wrapU = TextureWrapMode.Clamp,
                TextureWrapMode wrapV = TextureWrapMode.Clamp) =>
                new(name, width, height, 1, format, filter, wrapU, wrapV, TextureWrapMode.Clamp,
                    TextureDimension.Tex2D, enableRandomWrite);

            public static RenderTextureSpec Lut3D(
                string name,
                int width,
                int height,
                int depth,
                GraphicsFormat format,
                FilterMode filter = FilterMode.Bilinear,
                bool enableRandomWrite = true,
                TextureWrapMode wrapU = TextureWrapMode.Clamp,
                TextureWrapMode wrapV = TextureWrapMode.Clamp,
                TextureWrapMode wrapW = TextureWrapMode.Clamp) =>
                new(name, width, height, depth, format, filter, wrapU, wrapV, wrapW,
                    TextureDimension.Tex3D, enableRandomWrite);

        }

        public static RenderTexture EnsureRenderTexture(RenderTextureSpec spec, RenderTexture current)
        {
            if (IsCompatible(current, spec))
                return current;

            ReleaseRenderTexture(current);

            var descriptor = new RenderTextureDescriptor(spec.Width, spec.Height)
            {
                graphicsFormat = spec.Format,
                depthStencilFormat = GraphicsFormat.None,
                msaaSamples = 1,
                volumeDepth = spec.Dimension is TextureDimension.Tex3D or TextureDimension.Tex2DArray
                    ? spec.Depth
                    : 1,
                dimension = spec.Dimension,
                enableRandomWrite = spec.EnableRandomWrite,
                useMipMap = spec.UseMipMap,
                autoGenerateMips = spec.AutoGenerateMips,
                mipCount = spec.MipCount
            };

            var next = new RenderTexture(descriptor)
            {
                name = spec.Name,
                filterMode = spec.Filter,
                wrapModeU = spec.WrapU,
                wrapModeV = spec.WrapV,
                wrapModeW = spec.WrapW,
            };

            next.Create();
            return next;
        }

        public static void ReleaseRenderTexture(RenderTexture texture)
        {
            if (!texture) return;

            texture.Release();
            CoreUtils.Destroy(texture);
        }

        public static Color SanitizeColor(Color color)
        {
            color.r = SanitizeNonNegative(color.r);
            color.g = SanitizeNonNegative(color.g);
            color.b = SanitizeNonNegative(color.b);
            color.a = SanitizeNonNegative(color.a);
            return color;
        }

        public static float SanitizeNonNegative(float value) =>
            !float.IsNaN(value) && !float.IsInfinity(value) ? Mathf.Max(0f, value) : 0f;

        public static VolumeStack GetVolumeStack(UniversalCameraData cameraData) => GetVolumeStack(cameraData?.camera);

        public static VolumeStack GetVolumeStack(Camera camera)
        {
            if (camera &&
                camera.cameraType == CameraType.Reflection &&
                EnvironmentSettings.StaticLightingBindingActive &&
                EnvironmentSettings.StaticLightingVolumeStack != null)
            {
                return EnvironmentSettings.StaticLightingVolumeStack;
            }

            if (camera && camera.TryGetComponent<UniversalAdditionalCameraData>(out var additional) &&
                additional.volumeStack != null)
                return additional.volumeStack;

            return VolumeManager.instance.stack;
        }

        public static T GetVolumeComponent<T>(UniversalCameraData cameraData) where T : VolumeComponent =>
            GetVolumeStack(cameraData)?.GetComponent<T>();

        public static T GetVolumeComponent<T>(Camera camera) where T : VolumeComponent =>
            GetVolumeStack(camera)?.GetComponent<T>();

        private static bool IsCompatible(RenderTexture texture, in RenderTextureSpec spec)
        {
            if (!texture ||
                !texture.IsCreated() ||
                texture.width != spec.Width ||
                texture.height != spec.Height ||
                texture.graphicsFormat != spec.Format ||
                texture.filterMode != spec.Filter ||
                texture.wrapModeU != spec.WrapU ||
                texture.wrapModeV != spec.WrapV ||
                texture.wrapModeW != spec.WrapW ||
                texture.dimension != spec.Dimension ||
                texture.enableRandomWrite != spec.EnableRandomWrite ||
                texture.useMipMap != spec.UseMipMap ||
                texture.autoGenerateMips != spec.AutoGenerateMips ||
                texture.mipmapCount != spec.MipCount)
                return false;

            return spec.Dimension != TextureDimension.Tex3D &&
                   spec.Dimension != TextureDimension.Tex2DArray ||
                   texture.volumeDepth == spec.Depth;
        }
    }
}