using System;
using System.Collections.Generic;
using Aetheria.Runtime.Rendering;
using Aetheria.Runtime.Utils;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;

namespace Aetheria.Runtime.Data
{
    internal sealed class SharedLutCache : IDisposable
    {
        internal sealed class Resource : IDisposable
        {
            private readonly AtmosphereTools.RenderTextureSpec _spec;

            private RenderTexture _texture;
            private RenderGraphTextureHandleCache _handleCache;

            private bool _valid;

            public bool Valid
            {
                get => _valid;
                set => _valid = value;
            }

            public Resource(in AtmosphereTools.RenderTextureSpec spec) => _spec = spec;

            public TextureHandle Import(RenderGraph renderGraph)
            {
                var previousTexture = _texture;
                _texture = AtmosphereTools.EnsureRenderTexture(_spec, _texture);
                if (_texture != previousTexture)
                    _valid = false;

                return _handleCache.Import(renderGraph, _texture);
            }

            public void Dispose()
            {
                _handleCache.Dispose();
                AtmosphereTools.ReleaseRenderTexture(_texture);

                _texture = null;
                _valid = false;
            }
        }

        private sealed class Entry
        {
            public int RefCount;
            public Resource Resource;
        }

        private readonly Dictionary<int, Entry> _transmittance = new();
        private readonly Dictionary<int, Entry> _multiScattering = new();
        private readonly Dictionary<int, Entry> _groundIrradiance = new();

        public Resource AcquireTransmittance(int key) =>
            Acquire(_transmittance, key, LutDefinitions.Transmittance);

        public Resource AcquireMultiScattering(int key) =>
            Acquire(_multiScattering, key, LutDefinitions.MultiScattering);

        public Resource AcquireGroundIrradiance(int key) =>
            Acquire(_groundIrradiance, key, LutDefinitions.GroundIrradiance);

        public void ReleaseTransmittance(int key) => Release(_transmittance, key);
        public void ReleaseMultiScattering(int key) => Release(_multiScattering, key);
        public void ReleaseGroundIrradiance(int key) => Release(_groundIrradiance, key);

        private static Resource Acquire(
            Dictionary<int, Entry> cache,
            int key,
            in AtmosphereTools.RenderTextureSpec spec)
        {
            if (!cache.TryGetValue(key, out var entry))
            {
                entry = new Entry { Resource = new Resource(spec) };
                cache.Add(key, entry);
            }

            entry.RefCount++;
            return entry.Resource;
        }

        private static void Release(Dictionary<int, Entry> cache, int key)
        {
            if (!cache.TryGetValue(key, out var entry)) return;

            entry.RefCount--;

            if (entry.RefCount > 0) return;

            entry.Resource.Dispose();
            cache.Remove(key);
        }

        public void Dispose()
        {
            DisposeCache(_transmittance);
            DisposeCache(_multiScattering);
            DisposeCache(_groundIrradiance);
        }

        private static void DisposeCache(Dictionary<int, Entry> cache)
        {
            foreach (var entry in cache.Values)
                entry.Resource.Dispose();

            cache.Clear();
        }
    }
}