using System;
using System.Collections.Generic;
using Aetheria.Runtime.Utils;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;

namespace Aetheria.Runtime.Rendering.Fog.Volumetrics
{
    internal interface IVolumetricTextureAtlas
    {
        bool HasAtlasGroup(int groupIndex);
        Vector4 GetAtlasParams(int groupIndex);
        TextureHandle Import(RenderGraph renderGraph, int groupIndex);
    }

    internal interface IVolumetricTextureAtlasGroup<TTexture> : IDisposable
        where TTexture : Texture
    {
        bool HasAtlas { get; }
        bool HasFreeSlot { get; }
        Vector4 AtlasParams { get; }
        bool IsCompatible(TTexture texture);
        int AddTexture(TTexture texture, int revision);
        bool TryUpdateTexture(int slot, TTexture texture, int revision);
        bool RemoveTexture(int slot);
        TextureHandle Import(RenderGraph renderGraph);
    }

    internal abstract class VolumetricTextureAtlasGroup<TTexture, TAtlas> :
        IVolumetricTextureAtlasGroup<TTexture>
        where TTexture : Texture
        where TAtlas : Texture
    {
        private readonly List<AtlasEntry> _entries = new(VolumetricGridConfig.AtlasSlicesPerGroup);
        private RenderGraphTextureHandleCache _atlasHandleCache;

        protected TAtlas Atlas { get; private set; }
        protected int SliceCapacity { get; private set; }

        protected virtual bool IsAtlasCreated => true;
        public bool HasAtlas => IsAlive(Atlas) && IsAtlasCreated && SliceCapacity > 0;
        public virtual Vector4 AtlasParams => Vector4.one;

        public bool HasFreeSlot
        {
            get
            {
                if (_entries.Count < VolumetricGridConfig.AtlasSlicesPerGroup)
                    return true;

                for (int i = 0; i < _entries.Count; ++i)
                {
                    if (_entries[i] == null)
                        return true;
                }

                return false;
            }
        }

        public abstract bool IsCompatible(TTexture texture);
        protected abstract TAtlas CreateAtlas(int sliceCapacity);
        protected abstract void CopyTextureToSlot(TTexture source, int slot);
        
        protected virtual void ReleaseAtlasResource(TAtlas atlas)  { }

        public int AddTexture(TTexture texture, int revision)
        {
            if (!IsCompatible(texture))
                return -1;

            int slot = FindFreeSlot();
            
            var entry = new AtlasEntry(texture, revision);
            
            if (slot < 0)
            {
                if (_entries.Count >= VolumetricGridConfig.AtlasSlicesPerGroup)
                    return -1;

                slot = _entries.Count;
                _entries.Add(entry);
            }
            else
            {
                _entries[slot] = entry;
            }

            EnsureAtlasCapacity(slot + 1);
            CopyTextureToSlot(texture, slot);
            return slot;
        }

        public bool TryUpdateTexture(int slot, TTexture texture, int revision)
        {
            if (!IsCompatible(texture) || slot < 0 || slot >= _entries.Count)
                return false;

            var entry = _entries[slot];
            
            if (entry == null)
                return false;

            if (entry.Texture == texture && entry.Revision == revision)
                return true;

            entry.Texture = texture;
            entry.Revision = revision;
            EnsureAtlasCapacity(slot + 1);
            CopyTextureToSlot(texture, slot);
            return true;
        }

        public bool RemoveTexture(int slot)
        {
            if (slot < 0 || slot >= _entries.Count)
                return _entries.Count == 0;

            _entries[slot] = null;
            
            while (_entries.Count > 0 && _entries[^1] == null)
                _entries.RemoveAt(_entries.Count - 1);
            
            return _entries.Count == 0;
        }

        public TextureHandle Import(RenderGraph renderGraph) =>
            !HasAtlas ? default : _atlasHandleCache.Import(renderGraph, Atlas);

        protected bool IsSlotValid(TTexture source, int slot) =>
            HasAtlas && IsAlive(source) && IsCompatible(source) && slot >= 0 && slot < SliceCapacity;

        private int FindFreeSlot()
        {
            for (int i = 0; i < _entries.Count; ++i)
            {
                if (_entries[i] == null)
                    return i;
            }

            return -1;
        }

        private void EnsureAtlasCapacity(int requiredSlices)
        {
            if (HasAtlas && SliceCapacity >= requiredSlices)
                return;

            int newCapacity = Mathf.Max(VolumetricGridConfig.InitialAtlasSliceCapacity, SliceCapacity);
            
            while (newCapacity < requiredSlices)
                newCapacity <<= 1;

            RecreateAtlas(Mathf.Min(newCapacity, VolumetricGridConfig.AtlasSlicesPerGroup));
        }

        private void RecreateAtlas(int newCapacity)
        {
            ReleaseAtlas();
            SliceCapacity = Mathf.Clamp(newCapacity, 1, VolumetricGridConfig.AtlasSlicesPerGroup);
            Atlas = CreateAtlas(SliceCapacity);

            for (int i = 0; i < _entries.Count; ++i)
            {
                var texture = _entries[i]?.Texture;
                
                if (IsAlive(texture) && IsCompatible(texture))
                    CopyTextureToSlot(texture, i);
            }
        }

        private void ReleaseAtlas()
        {
            _atlasHandleCache.Dispose();

            if (!IsAlive(Atlas))
                return;

            ReleaseAtlasResource(Atlas);
            CoreUtils.Destroy(Atlas);
            Atlas = null;
        }

        public void Dispose()
        {
            ReleaseAtlas();
            _entries.Clear();
            SliceCapacity = 0;
        }

        private static bool IsAlive(UnityEngine.Object value) => value;

        private sealed class AtlasEntry
        {
            public TTexture Texture;
            public int Revision;

            public AtlasEntry(TTexture texture, int revision)
            {
                Texture = texture;
                Revision = revision;
            }
        }
    }
    
    internal abstract class VolumetricTextureAtlas<TKey, TTexture, TGroup> : IDisposable, IVolumetricTextureAtlas
        where TTexture : Texture
        where TGroup : class, IVolumetricTextureAtlasGroup<TTexture>
    {
        private const int MaxSlicesPerGroup = VolumetricGridConfig.AtlasSlicesPerGroup;

        private readonly Dictionary<TKey, PackedSlot> _slots;
        private readonly HashSet<TKey> _usedKeys = new();
        private readonly List<TKey> _staleKeys = new();
        private readonly TGroup[] _groups;
        private int _collectionFrame = -1;

        protected VolumetricTextureAtlas(int groupCount)
        {
            int clampedGroupCount = Mathf.Clamp(groupCount, 1, VolumetricGridConfig.MaxAtlasGroups);
            _groups = new TGroup[clampedGroupCount];
            _slots = new Dictionary<TKey, PackedSlot>(MaxSlicesPerGroup * clampedGroupCount);
        }

        protected abstract TGroup CreateGroup(TTexture texture);

        public void BeginCollection()
        {
            int frame = Time.frameCount;
            
            if (_collectionFrame == frame)
                return;

            ReleaseUnusedEntries();
            _usedKeys.Clear();
            _collectionFrame = frame;
        }

        protected bool TryGetSlotInternal(TKey key, TTexture texture, int revision, out int packedSlot)
        {
            packedSlot = -1;
            
            if (!IsAlive(texture))
                return false;

            if (_slots.TryGetValue(key, out var existing))
            {
                var existingGroup = _groups[existing.Group];
                
                if (existingGroup != null &&
                    existingGroup.IsCompatible(texture) &&
                    existingGroup.TryUpdateTexture(existing.Slot, texture, revision))
                {
                    _usedKeys.Add(key);
                    packedSlot = existing.PackedIndex;
                    return true;
                }

                RemoveSlot(existing);
                _slots.Remove(key);
            }

            int groupIndex = FindOrCreateGroup(texture);
            
            if (groupIndex < 0)
                return false;

            int slot = _groups[groupIndex].AddTexture(texture, revision);
            
            if (slot < 0)
                return false;

            var packed = new PackedSlot(groupIndex, slot);
            _slots.Add(key, packed);
            _usedKeys.Add(key);
            packedSlot = packed.PackedIndex;
            return true;
        }

        public bool HasAtlasGroup(int groupIndex) =>
            groupIndex >= 0 && groupIndex < _groups.Length && _groups[groupIndex]?.HasAtlas == true;

        public Vector4 GetAtlasParams(int groupIndex) =>
            groupIndex >= 0 && groupIndex < _groups.Length && _groups[groupIndex] != null
                ? _groups[groupIndex].AtlasParams
                : Vector4.one;

        public TextureHandle Import(RenderGraph renderGraph, int groupIndex)
        {
            if (groupIndex < 0 || groupIndex >= _groups.Length || _groups[groupIndex] == null)
                return default;

            return _groups[groupIndex].Import(renderGraph);
        }

        private void ReleaseUnusedEntries()
        {
            _staleKeys.Clear();
            
            foreach (var pair in _slots)
            {
                if (!_usedKeys.Contains(pair.Key))
                    _staleKeys.Add(pair.Key);
            }

            for (int i = 0; i < _staleKeys.Count; ++i)
            {
                var key = _staleKeys[i];
                var slot = _slots[key];
                RemoveSlot(slot);
                _slots.Remove(key);
            }
        }

        private void RemoveSlot(PackedSlot packedSlot)
        {
            var group = _groups[packedSlot.Group];
            
            if (group == null || !group.RemoveTexture(packedSlot.Slot))
                return;

            group.Dispose();
            _groups[packedSlot.Group] = null;
        }

        private int FindOrCreateGroup(TTexture texture)
        {
            int firstEmpty = -1;
            for (int i = 0; i < _groups.Length; ++i)
            {
                var group = _groups[i];
                
                if (group != null)
                {
                    if (group.IsCompatible(texture) && group.HasFreeSlot)
                        return i;
                }
                else if (firstEmpty < 0)
                {
                    firstEmpty = i;
                }
            }

            if (firstEmpty < 0)
                return -1;

            _groups[firstEmpty] = CreateGroup(texture);
            return firstEmpty;
        }

        public void Dispose()
        {
            for (int i = 0; i < _groups.Length; ++i)
            {
                _groups[i]?.Dispose();
                _groups[i] = null;
            }

            _slots.Clear();
            _usedKeys.Clear();
            _staleKeys.Clear();
            _collectionFrame = -1;
        }

        private static bool IsAlive(UnityEngine.Object value) => value;

        private readonly struct PackedSlot
        {
            public readonly int Group;
            public readonly int Slot;
            public int PackedIndex => Group * MaxSlicesPerGroup + Slot;

            public PackedSlot(int group, int slot)
            {
                Group = group;
                Slot = slot;
            }
        }
    }
}