using UnityEngine.Rendering;

namespace UnityEditor.Rendering
{
    class STPResourceStripper : IRenderPipelineGraphicsSettingsStripper<STP.RuntimeResources>
    {
        public bool active => true;

        public bool CanRemoveSettings(STP.RuntimeResources resources)
        {
            foreach (var asset in CoreBuildData.instance.renderPipelineAssets)
            {
                if (asset is ISTPEnabledRenderPipeline)
                    return false;
            }

            return true;
        }
    }
}
