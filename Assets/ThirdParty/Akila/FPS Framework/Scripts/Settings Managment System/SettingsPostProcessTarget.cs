using UnityEngine;
using UnityEngine.Rendering;

namespace Akila.FPSFramework
{
    [AddComponentMenu("Akila/FPS Framework/Settings/Settings Post Process Target")]
    public class SettingsPostProcessTarget : MonoBehaviour
    {
        [SerializeField] private Volume volume;

        public Volume Volume => volume;
    }
}
