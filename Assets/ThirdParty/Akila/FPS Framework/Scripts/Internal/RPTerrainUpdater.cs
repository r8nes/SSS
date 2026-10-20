using UnityEngine;

namespace Akila.FPSFramework
{

#if UNITY_EDITOR
    [ExecuteInEditMode]
#endif
    internal class RPTerrainUpdater : MonoBehaviour
    {
        private Terrain terrain;

        public RPTemplate[] templates;

        [System.Serializable]
        public struct RPTemplate
        {
            public Material material;
            public RenderPipelineDetector.PipelineType target;
        }

        private void Awake()
        {
            terrain = GetComponent<Terrain>();
        }

#if UNITY_EDITOR
        private void Update()
        {
            foreach (RPTemplate temp in templates)
            {
                if (temp.target == RenderPipelineDetector.CurrentPipeline)
                    terrain.materialTemplate = temp.material;
            }
        }
#endif
    }
}