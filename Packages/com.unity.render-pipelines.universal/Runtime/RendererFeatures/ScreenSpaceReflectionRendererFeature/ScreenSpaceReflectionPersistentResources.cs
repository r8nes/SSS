using System;

namespace UnityEngine.Rendering.Universal
{
    [Serializable]
    [SupportedOnRenderPipeline(typeof(UniversalRenderPipelineAsset))]
    [Categorization.CategoryInfo(Name = "R: SSR Resources", Order = 1010)]
    [Categorization.ElementInfo(Order = 0)]
    [HideInInspector]
    class ScreenSpaceReflectionPersistentResources : IRenderPipelineResources
    {
        [SerializeField] [ResourcePath("Shaders/Utils/ScreenSpaceReflection.compute")]
        ComputeShader m_ComputeShader;

        public ComputeShader ComputeShader
        {
            get => m_ComputeShader;
            set => this.SetValueAndNotify(ref m_ComputeShader, value);
        }

        public bool isAvailableInPlayerBuild => true;

        [SerializeField] [HideInInspector]
        int m_Version = 3;

        public int version => m_Version;
    }
}
