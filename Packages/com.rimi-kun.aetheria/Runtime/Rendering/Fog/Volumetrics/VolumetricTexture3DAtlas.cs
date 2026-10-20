using UnityEngine;

namespace Aetheria.Runtime.Rendering.Fog.Volumetrics
{
    internal sealed class VolumetricTexture3DAtlas<TKey> :
        VolumetricTextureAtlas<TKey, Texture3D, VolumetricTexture3DAtlasGroup>
    {
        private readonly string _atlasName;
        private readonly TextureWrapMode _wrapMode;

        public VolumetricTexture3DAtlas(
            int groupCount,
            string atlasName,
            TextureWrapMode wrapMode)
            : base(groupCount)
        {
            _atlasName = atlasName;
            _wrapMode = wrapMode;
        }

        public bool TryGetSlot(TKey key, Texture3D texture, out int packedSlot)
        {
            int revision = texture ? unchecked((int)texture.updateCount) : 0;
            return TryGetSlotInternal(key, texture, revision, out packedSlot);
        }

        protected override VolumetricTexture3DAtlasGroup CreateGroup(Texture3D texture) =>
            new(_atlasName, _wrapMode, texture);
    }

    internal sealed class VolumetricTexture3DAtlasGroup :
        VolumetricTextureAtlasGroup<Texture3D, Texture3D>
    {
        private readonly string _atlasName;
        private readonly TextureWrapMode _wrapMode;
        private readonly TextureFormat _format;
        private readonly int _width;
        private readonly int _height;
        private readonly int _sourceDepth;

        public override Vector4 AtlasParams
        {
            get
            {
                if (!HasAtlas)
                    return Vector4.one;

                int atlasDepth = Mathf.Max(1, _sourceDepth * SliceCapacity);
                return new Vector4(atlasDepth, _sourceDepth, 1f / atlasDepth, SliceCapacity);
            }
        }

        public VolumetricTexture3DAtlasGroup(
            string atlasName,
            TextureWrapMode wrapMode,
            Texture3D texture)
        {
            _atlasName = atlasName;
            _wrapMode = wrapMode;
            _width = Mathf.Max(1, texture.width);
            _height = Mathf.Max(1, texture.height);
            _sourceDepth = Mathf.Max(1, texture.depth);
            _format = texture.format;
        }

        public override bool IsCompatible(Texture3D texture) =>
            texture &&
            texture.width == _width &&
            texture.height == _height &&
            texture.depth == _sourceDepth &&
            texture.format == _format;

        protected override Texture3D CreateAtlas(int sliceCapacity)
        {
            var atlas = new Texture3D(
                _width,
                _height,
                Mathf.Max(1, _sourceDepth * sliceCapacity),
                _format,
                false)
            {
                name = _atlasName,
                hideFlags = HideFlags.HideAndDontSave,
                wrapMode = _wrapMode,
                filterMode = FilterMode.Trilinear,
                anisoLevel = 0,
            };
            
            atlas.Apply(false, true);
            return atlas;
        }

        protected override void CopyTextureToSlot(Texture3D source, int slot)
        {
            if (!IsSlotValid(source, slot))
                return;

            int destinationBaseSlice = slot * _sourceDepth;
            
            for (int z = 0; z < _sourceDepth; ++z)
                Graphics.CopyTexture(source, z, 0, Atlas, destinationBaseSlice + z, 0);
        }
    }
}