namespace UnityEngine.Rendering.Universal
{
    internal class PostProcessMaterialLibrary
    {
        public readonly Material stopNaN;
        public readonly Material subpixelMorphologicalAntialiasing;
        public readonly Material gaussianDepthOfField;
        public readonly Material bokehDepthOfField;
        public readonly ComputeShader bokehDepthOfFieldBlur;
        public readonly ComputeShader motionBlurCS;
        public readonly ComputeShader lensFlareScreenSpaceCompose;
        public readonly ComputeShader temporalAntialiasing;
        public readonly ComputeShader bloomPrefilterCS;
        public readonly ComputeShader bloomBlurCS;
        public readonly ComputeShader bloomUpsampleCS;
        public readonly Material lensFlareScreenSpace;
        public readonly Material lensFlareDataDriven;
        public readonly Material uber;
        public readonly Material scalingSetup;
        public readonly Material easu;
        public readonly Material finalPass;

        private readonly PostProcessMaterialKeywordCache m_MaterialKeywords = new PostProcessMaterialKeywordCache();

        internal PostProcessData m_Resources;
        public PostProcessData resources => m_Resources;

        public PostProcessMaterialLibrary(PostProcessData data)
        {
            // NOTE NOTE NOTE NOTE NOTE NOTE
            // If you create something here you must also destroy it in Cleanup()
            // or it will leak during enter/leave play mode cycles
            // NOTE NOTE NOTE NOTE NOTE NOTE
            stopNaN = Load(data.shaders.stopNanPS);
            subpixelMorphologicalAntialiasing = Load(data.shaders.subpixelMorphologicalAntialiasingPS);
            gaussianDepthOfField = Load(data.shaders.gaussianDepthOfFieldPS);
            bokehDepthOfField = Load(data.shaders.bokehDepthOfFieldPS);
            bokehDepthOfFieldBlur = data.shaders.bokehDepthOfFieldBlurCS;
            motionBlurCS = data.shaders.motionBlurCS;
            lensFlareScreenSpaceCompose = data.shaders.lensFlareScreenSpaceComposeCS;
            temporalAntialiasing = data.shaders.temporalAntialiasingCS;
            bloomPrefilterCS = data.shaders.bloomPrefilterCS;
            bloomBlurCS = data.shaders.bloomBlurCS;
            bloomUpsampleCS = data.shaders.bloomUpsampleCS;
            lensFlareScreenSpace = Load(data.shaders.LensFlareScreenSpacePS);
            lensFlareDataDriven = Load(data.shaders.LensFlareDataDrivenPS);
            uber = Load(data.shaders.uberPostPS);
            scalingSetup = Load(data.shaders.scalingSetupPS);
            easu = Load(data.shaders.easuPS);
            finalPass = Load(data.shaders.finalPostPassPS);

            m_Resources = data;
        }

        private Material Load(Shader shader)
        {
            if (shader == null)
            {
                Debug.LogErrorFormat($"Missing shader. PostProcessing render passes will not execute. Check for missing reference in the renderer resources.");
                return null;
            }
            else if (!shader.isSupported)
            {
                return null;
            }

            return CoreUtils.CreateEngineMaterial(shader);
        }

        internal Material GetKeywordVariant(Material source, PostProcessMaterialKey key)
        {
            return m_MaterialKeywords.Get(source, key);
        }

        internal void Cleanup()
        {
            m_MaterialKeywords.Cleanup();

            CoreUtils.Destroy(stopNaN);
            CoreUtils.Destroy(subpixelMorphologicalAntialiasing);
            CoreUtils.Destroy(gaussianDepthOfField);
            CoreUtils.Destroy(bokehDepthOfField);
            CoreUtils.Destroy(lensFlareScreenSpace);
            CoreUtils.Destroy(lensFlareDataDriven);
            CoreUtils.Destroy(scalingSetup);
            CoreUtils.Destroy(uber);
            CoreUtils.Destroy(easu);
            CoreUtils.Destroy(finalPass);
        }
    }
}
