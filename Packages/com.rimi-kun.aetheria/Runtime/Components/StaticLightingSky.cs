using Aetheria.Runtime.Rendering;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace Aetheria.Runtime.Components
{
    [AddComponentMenu("Aetheria/Atmosphere/Static Lighting Sky")]
    [DisallowMultipleComponent]
    [ExecuteAlways]
    public sealed class StaticLightingSky : MonoBehaviour
    {
        [SerializeField] private VolumeProfile _profile;
        [SerializeField] private LightingSkyResolution _resolution =
            LightingSkyResolution.Resolution512;
        [SerializeField, InspectorName("Include Sun In Reflection Baking")]
        private bool _includeSunInBaking;
        [SerializeField] private Vector3 _observerPositionWs = Vector3.zero;

        public VolumeProfile Profile => _profile;
        public LightingSkyResolution Resolution => _resolution;
        public bool IncludeSunInReflectionBaking => _includeSunInBaking;
        public Vector3 ObserverPositionWs => _observerPositionWs;

        internal static bool TryGet(Scene scene, out StaticLightingSky sky)
        {
            sky = null;
            
            if (!scene.IsValid() || !scene.isLoaded)
                return false;

            var roots = scene.GetRootGameObjects();
            
            for (int rootIndex = 0; rootIndex < roots.Length; rootIndex++)
            {
                var candidates =
                    roots[rootIndex].GetComponentsInChildren<StaticLightingSky>(true);
                
                for (int candidateIndex = 0; candidateIndex < candidates.Length; candidateIndex++)
                {
                    var candidate = candidates[candidateIndex];
                    
                    if (!IsUsable(candidate)) continue;

                    sky = candidate;
                    return true;
                }
            }

            return false;
        }

        private static bool IsUsable(StaticLightingSky sky) =>
            sky &&
            sky.isActiveAndEnabled &&
            sky.gameObject.scene.IsValid() &&
            sky.gameObject.scene.isLoaded;
    }

    [AddComponentMenu("")]
    [DisallowMultipleComponent]
    internal sealed class StaticLightingCaptureCamera : MonoBehaviour
    {
    }
}