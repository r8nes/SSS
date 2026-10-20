using UnityEngine.Rendering.Universal;

namespace UnityEditor.Rendering.Universal
{
    [CustomEditor(typeof(Bloom))]
    internal sealed class BloomEditor : VolumeComponentEditor
    {
        private SerializedDataParameter m_Threshold;
        private SerializedDataParameter m_Intensity;
        private SerializedDataParameter m_Scatter;
        private SerializedDataParameter m_Tint;
        private SerializedDataParameter m_DirtTexture;
        private SerializedDataParameter m_DirtIntensity;
        private SerializedDataParameter m_Resolution;
        private SerializedDataParameter m_HighQualityPrefiltering;
        private SerializedDataParameter m_HighQualityFiltering;
        private SerializedDataParameter m_Anamorphic;

        public override void OnEnable()
        {
            var o = new PropertyFetcher<Bloom>(serializedObject);

            m_Threshold = Unpack(o.Find(x => x.threshold));
            m_Intensity = Unpack(o.Find(x => x.intensity));
            m_Scatter = Unpack(o.Find(x => x.scatter));
            m_Tint = Unpack(o.Find(x => x.tint));
            m_DirtTexture = Unpack(o.Find(x => x.dirtTexture));
            m_DirtIntensity = Unpack(o.Find(x => x.dirtIntensity));
            m_Resolution = Unpack(o.Find("m_Resolution"));
            m_HighQualityPrefiltering = Unpack(o.Find("m_HighQualityPrefiltering"));
            m_HighQualityFiltering = Unpack(o.Find("m_HighQualityFiltering"));
            m_Anamorphic = Unpack(o.Find(x => x.anamorphic));
        }

        public override void OnInspectorGUI()
        {
            PropertyField(m_Threshold);
            PropertyField(m_Intensity);
            PropertyField(m_Scatter);
            PropertyField(m_Tint);
            PropertyField(m_DirtTexture, EditorGUIUtility.TrTextContent("Texture"));
            PropertyField(m_DirtIntensity, EditorGUIUtility.TrTextContent("Intensity"));
            PropertyField(m_Resolution);
            PropertyField(m_HighQualityPrefiltering);
            PropertyField(m_HighQualityFiltering);
            PropertyField(m_Anamorphic);
        }
    }
}
