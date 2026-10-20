using Aetheria.Runtime.Overrides;
using UnityEditor;
using UnityEditor.Rendering;

namespace Aetheria.Editor
{
    [CanEditMultipleObjects]
    [CustomEditor(typeof(CloudLayer))]
    internal sealed class CloudLayerVolumeEditor : VolumeComponentEditor
    {
        private struct MapParameter
        {
            public SerializedDataParameter Texture;
            public SerializedDataParameter OpacityR;
            public SerializedDataParameter OpacityG;
            public SerializedDataParameter OpacityB;
            public SerializedDataParameter OpacityA;
            public SerializedDataParameter Altitude;
            public SerializedDataParameter Rotation;
            public SerializedDataParameter Tint;
            public SerializedDataParameter Exposure;
            public SerializedDataParameter DistortionMode;
            public SerializedDataParameter ScrollOrientation;
            public SerializedDataParameter ScrollSpeed;
            public SerializedDataParameter SelfShadow;
            public SerializedDataParameter LightSteps;
            public SerializedDataParameter Density;
            public SerializedDataParameter AmbientProbeDimmer;
        }

        private SerializedDataParameter _state;
        private SerializedDataParameter _opacity;
        private SerializedDataParameter _upperHemisphereOnly;
        private SerializedDataParameter _layers;
        private SerializedDataParameter _resolution;
        private MapParameter _layerA;
        private MapParameter _layerB;

        public override void OnEnable()
        {
            var o = new PropertyFetcher<CloudLayer>(serializedObject);
            _state = Unpack(o.Find(x => x.State));
            _opacity = Unpack(o.Find(x => x.Opacity));
            _upperHemisphereOnly = Unpack(o.Find(x => x.UpperHemisphereOnly));
            _layers = Unpack(o.Find(x => x.Layers));
            _resolution = Unpack(o.Find(x => x.Resolution));

            _layerA = UnpackMap(serializedObject.FindProperty(nameof(CloudLayer.LayerA)));
            _layerB = UnpackMap(serializedObject.FindProperty(nameof(CloudLayer.LayerB)));
            base.OnEnable();
        }

        public override void OnInspectorGUI()
        {
            PropertyField(_state);

            if ((CloudLayerState)_state.value.intValue == CloudLayerState.Disabled)
                return;

            PropertyField(_opacity);
            PropertyField(_upperHemisphereOnly);
            PropertyField(_layers);
            PropertyField(_resolution);

            DrawMap(_layerA);

            if ((CloudMapMode)_layers.value.intValue == CloudMapMode.Double)
                DrawMap(_layerB);
        }

        private MapParameter UnpackMap(SerializedProperty layer)
        {
            return new MapParameter
            {
                Texture = Unpack(layer.FindPropertyRelative(nameof(CloudLayer.Map.Texture))),
                OpacityR = Unpack(layer.FindPropertyRelative(nameof(CloudLayer.Map.OpacityR))),
                OpacityG = Unpack(layer.FindPropertyRelative(nameof(CloudLayer.Map.OpacityG))),
                OpacityB = Unpack(layer.FindPropertyRelative(nameof(CloudLayer.Map.OpacityB))),
                OpacityA = Unpack(layer.FindPropertyRelative(nameof(CloudLayer.Map.OpacityA))),
                Altitude = Unpack(layer.FindPropertyRelative(nameof(CloudLayer.Map.Altitude))),
                Rotation = Unpack(layer.FindPropertyRelative(nameof(CloudLayer.Map.Rotation))),
                Tint = Unpack(layer.FindPropertyRelative(nameof(CloudLayer.Map.Tint))),
                Exposure = Unpack(layer.FindPropertyRelative(nameof(CloudLayer.Map.Exposure))),
                DistortionMode = Unpack(layer.FindPropertyRelative(nameof(CloudLayer.Map.DistortionMode))),
                ScrollOrientation = Unpack(layer.FindPropertyRelative(nameof(CloudLayer.Map.scrollOrientation))),
                ScrollSpeed = Unpack(layer.FindPropertyRelative(nameof(CloudLayer.Map.scrollSpeed))),
                SelfShadow = Unpack(layer.FindPropertyRelative(nameof(CloudLayer.Map.SelfShadow))),
                LightSteps = Unpack(layer.FindPropertyRelative(nameof(CloudLayer.Map.LightSteps))),
                Density = Unpack(layer.FindPropertyRelative(nameof(CloudLayer.Map.Density))),
                AmbientProbeDimmer = Unpack(layer.FindPropertyRelative(nameof(CloudLayer.Map.AmbientProbeDimmer)))
            };
        }

        private void DrawMap(MapParameter layer)
        {
            PropertyField(layer.Texture);
            PropertyField(layer.OpacityR);
            PropertyField(layer.OpacityG);
            PropertyField(layer.OpacityB);
            PropertyField(layer.OpacityA);
            PropertyField(layer.Altitude);
            PropertyField(layer.Rotation);

            PropertyField(layer.DistortionMode);

            var distortionMode = (CloudDistortionMode)layer.DistortionMode.value.intValue;

            if (distortionMode != CloudDistortionMode.None)
            {
                using (new IndentLevelScope())
                {
                    PropertyField(layer.ScrollOrientation);
                    PropertyField(layer.ScrollSpeed);
                }
            }

            PropertyField(layer.Tint);
            PropertyField(layer.Exposure);
            PropertyField(layer.AmbientProbeDimmer);
            PropertyField(layer.SelfShadow);

            if (!layer.SelfShadow.value.boolValue) return;

            using (new IndentLevelScope())
            {
                PropertyField(layer.LightSteps);
                PropertyField(layer.Density);
            }
        }
    }
}