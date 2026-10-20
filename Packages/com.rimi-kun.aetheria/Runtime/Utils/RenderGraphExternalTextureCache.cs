using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;

namespace Aetheria.Runtime.Utils
{
    internal sealed class RenderGraphExternalTextureCache
    {
        private sealed class Entry
        {
            internal RTHandle Handle;
            internal int LastUsedFrame;
        }

        private readonly Dictionary<Texture, Entry> _entries = new();
        private readonly List<Texture> _releaseKeys = new();
        private int _currentFrame = -1;

        internal void BeginFrame(int frameIndex)
        {
            if (frameIndex == _currentFrame)
                return;

            _currentFrame = frameIndex;
            _releaseKeys.Clear();
            foreach (var pair in _entries)
            {
                if (!pair.Key || pair.Value == null || pair.Value.LastUsedFrame < frameIndex - 1)
                    _releaseKeys.Add(pair.Key);
            }

            for (int i = 0; i < _releaseKeys.Count; ++i)
            {
                Texture texture = _releaseKeys[i];
                if (!_entries.TryGetValue(texture, out var entry))
                    continue;

                ReleaseEntry(entry);
                _entries.Remove(texture);
            }
        }

        internal TextureHandle Import(RenderGraph renderGraph, Texture texture)
        {
            if (!texture)
                return TextureHandle.nullHandle;

            if (!_entries.TryGetValue(texture, out var entry) || entry?.Handle == null)
            {
                entry = new Entry
                {
                    Handle = RenderGraphTextureCache.AllocateHandle(texture),
                    LastUsedFrame = _currentFrame
                };
                _entries[texture] = entry;
            }
            else
            {
                entry.LastUsedFrame = _currentFrame;
            }

            return RenderGraphTextureCache.ImportExternalTexture(
                renderGraph,
                texture,
                entry.Handle);
        }

        internal void Release()
        {
            foreach (var entry in _entries.Values)
                ReleaseEntry(entry);

            _entries.Clear();
            _releaseKeys.Clear();
            _currentFrame = -1;
        }

        private static void ReleaseEntry(Entry entry)
        {
            if (entry == null)
                return;

            RTHandle handle = entry.Handle;
            RenderGraphTextureCache.ReleaseHandle(ref handle);
            entry.Handle = null;
        }
    }
}
