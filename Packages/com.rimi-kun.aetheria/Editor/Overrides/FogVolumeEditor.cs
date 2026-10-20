using Aetheria.Runtime.Overrides;
using UnityEditor;
using UnityEditor.Rendering;

namespace Aetheria.Editor
{
    [CanEditMultipleObjects]
    [CustomEditor(typeof(FogVolume))]
    internal sealed class FogVolumeEditor : VolumeComponentEditor
    {
        private SerializedDataParameter _enabled;
        private SerializedDataParameter _colorMode;
        private SerializedDataParameter _color;
        private SerializedDataParameter _tint;
        private SerializedDataParameter _maxFogDistance;
        private SerializedDataParameter _mipFogMaxMip;
        private SerializedDataParameter _mipFogNear;
        private SerializedDataParameter _mipFogFar;
        private SerializedDataParameter _baseHeight;
        private SerializedDataParameter _maximumHeight;
        private SerializedDataParameter _meanFreePath;
        private SerializedDataParameter _enableVolumetricFog;
        private SerializedDataParameter _albedo;
        private SerializedDataParameter _volumetricExtinctionScale;
        private SerializedDataParameter _depthExtent;
        private SerializedDataParameter _anisotropy;
        private SerializedDataParameter _receiveMainLightShadows;
        private SerializedDataParameter _mainLightShadowDimmer;
        private SerializedDataParameter _globalLightProbeDimmer;
        private SerializedDataParameter _nearDampEnable;
        private SerializedDataParameter _fogStartDistance;
        private SerializedDataParameter _dampDistance;
        private SerializedDataParameter _gridPreset;
        private SerializedDataParameter _froxelWidth;
        private SerializedDataParameter _froxelHeight;
        private SerializedDataParameter _froxelDepth;
        private SerializedDataParameter _sliceDistributionUniformity;
        private SerializedDataParameter _gaussianFilterEnabled;
        private SerializedDataParameter _reconstructionFilter;
        private SerializedDataParameter _mainLightShadowsEnabled;

        public override void OnEnable()
        {
            var o = new PropertyFetcher<FogVolume>(serializedObject);
            _enabled = Unpack(o.Find(x => x.Enabled));
            _colorMode = Unpack(o.Find(x => x.ColorMode));
            _color = Unpack(o.Find(x => x.Color));
            _tint = Unpack(o.Find(x => x.Tint));
            _maxFogDistance = Unpack(o.Find(x => x.MaxFogDistance));
            _mipFogMaxMip = Unpack(o.Find(x => x.MipFogMaxMip));
            _mipFogNear = Unpack(o.Find(x => x.MipFogNear));
            _mipFogFar = Unpack(o.Find(x => x.MipFogFar));
            _baseHeight = Unpack(o.Find(x => x.BaseHeight));
            _maximumHeight = Unpack(o.Find(x => x.MaximumHeight));
            _meanFreePath = Unpack(o.Find(x => x.MeanFreePath));
            _enableVolumetricFog = Unpack(o.Find(x => x.EnableVolumetricFog));
            _albedo = Unpack(o.Find(x => x.Albedo));
            _volumetricExtinctionScale = Unpack(o.Find(x => x.VolumetricExtinctionScale));
            _depthExtent = Unpack(o.Find(x => x.DepthExtent));
            _anisotropy = Unpack(o.Find(x => x.Anisotropy));
            _receiveMainLightShadows = Unpack(o.Find(x => x.ReceiveMainLightShadows));
            _mainLightShadowDimmer = Unpack(o.Find(x => x.MainLightShadowDimmer));
            _globalLightProbeDimmer = Unpack(o.Find(x => x.GlobalLightProbeDimmer));
            _nearDampEnable = Unpack(o.Find(x => x.NearDampEnable));
            _fogStartDistance = Unpack(o.Find(x => x.FogStartDistance));
            _dampDistance = Unpack(o.Find(x => x.DampDistance));
            _gridPreset = Unpack(o.Find(x => x.GridPreset));
            _froxelWidth = Unpack(o.Find(x => x.FroxelWidth));
            _froxelHeight = Unpack(o.Find(x => x.FroxelHeight));
            _froxelDepth = Unpack(o.Find(x => x.FroxelDepth));
            _sliceDistributionUniformity = Unpack(o.Find(x => x.SliceDistributionUniformity));
            _gaussianFilterEnabled = Unpack(o.Find(x => x.GaussianFilterEnabled));
            _reconstructionFilter = Unpack(o.Find(x => x.ReconstructionFilter));
            _mainLightShadowsEnabled = Unpack(o.Find(x => x.MainLightShadowsEnabled));
            base.OnEnable();
        }

        public override void OnInspectorGUI()
        {
            PropertyField(_enabled);

            PropertyField(_gridPreset);
            if (!_gridPreset.value.hasMultipleDifferentValues &&
                (VolumetricFogGridPreset)_gridPreset.value.intValue == VolumetricFogGridPreset.Custom)
            {
                using (new IndentLevelScope())
                {
                    PropertyField(_froxelWidth);
                    PropertyField(_froxelHeight);
                    PropertyField(_froxelDepth);
                }
            }
            PropertyField(_sliceDistributionUniformity);
            PropertyField(_gaussianFilterEnabled);
            PropertyField(_reconstructionFilter);
            PropertyField(_mainLightShadowsEnabled);

            if (!_enabled.value.hasMultipleDifferentValues && !_enabled.value.boolValue)
                return;

            DrawHeader("Height Fog");
            PropertyField(_meanFreePath);
            PropertyField(_baseHeight);
            PropertyField(_maximumHeight);
            PropertyField(_maxFogDistance);
            ClampOrdered(_baseHeight, _maximumHeight);

            DrawHeader("Fog Color");
            PropertyField(_colorMode);
            if (!_colorMode.value.hasMultipleDifferentValues &&
                (FogColorMode)_colorMode.value.intValue == FogColorMode.ConstantColor)
            {
                PropertyField(_color);
            }
            else
            {
                PropertyField(_tint);
                PropertyField(_mipFogNear);
                PropertyField(_mipFogFar);
                PropertyField(_mipFogMaxMip, new UnityEngine.GUIContent("Distant Sky Detail",
                    "Amount of sky detail restored beyond Mip Fog Far. Zero keeps the coarsest prefiltered level; one reaches the unfiltered sky."));
                ClampOrdered(_mipFogNear, _mipFogFar);
            }

            DrawHeader("Volumetric Fog");
            PropertyField(_enableVolumetricFog);
            if (!_enableVolumetricFog.value.hasMultipleDifferentValues &&
                !_enableVolumetricFog.value.boolValue)
                return;

            PropertyField(_albedo);
            PropertyField(_volumetricExtinctionScale);
            PropertyField(_depthExtent);
            PropertyField(_anisotropy);

            DrawHeader("Volumetric Lighting");
            PropertyField(_receiveMainLightShadows);
            if (_receiveMainLightShadows.value.hasMultipleDifferentValues ||
                _receiveMainLightShadows.value.boolValue)
            {
                using (new IndentLevelScope())
                    PropertyField(_mainLightShadowDimmer);
            }
            PropertyField(_globalLightProbeDimmer);

            DrawHeader("Near Fade");
            PropertyField(_nearDampEnable);
            if (_nearDampEnable.value.hasMultipleDifferentValues || _nearDampEnable.value.boolValue)
            {
                using (new IndentLevelScope())
                {
                    PropertyField(_fogStartDistance);
                    PropertyField(_dampDistance);
                }
            }
        }

        private void ClampOrdered(SerializedDataParameter minimum, SerializedDataParameter maximum)
        {
            if (minimum.value.hasMultipleDifferentValues || maximum.value.hasMultipleDifferentValues ||
                maximum.value.floatValue >= minimum.value.floatValue)
                return;

            maximum.value.floatValue = minimum.value.floatValue;
            serializedObject.ApplyModifiedProperties();
        }
    }
}
