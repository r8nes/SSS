using UnityEngine.Rendering;

namespace Aetheria.Runtime.Rendering
{
    internal sealed class AetheriaNativeFrameData : ContextItem
    {
        internal bool IsRelevant;
        internal bool StaticLightingCapture;
        internal bool LightingCamera;
        internal bool ReflectionCamera;
        internal bool FinalLightingCamera;
        internal bool EnvironmentDriver;
        internal bool EnvironmentActive;
        internal bool LiveEnvironmentLayers;
        internal bool AtmosphereActive;
        internal bool FogActive;
        internal bool WaterActive;
        internal bool WaterSsrActive;
        internal bool LightShaftsActive;

        public override void Reset()
        {
            IsRelevant = false;
            StaticLightingCapture = false;
            LightingCamera = false;
            ReflectionCamera = false;
            FinalLightingCamera = false;
            EnvironmentDriver = false;
            EnvironmentActive = false;
            LiveEnvironmentLayers = false;
            AtmosphereActive = false;
            FogActive = false;
            WaterActive = false;
            WaterSsrActive = false;
            LightShaftsActive = false;
        }
    }
}
