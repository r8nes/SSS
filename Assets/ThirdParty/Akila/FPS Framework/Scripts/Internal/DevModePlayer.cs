#if UNITY_EDITOR
using Akila.FPSFramework;
using UnityEngine;

namespace Akila.FPSFramework.Internal
{
    [AddComponentMenu("")]
    internal class DevModePlayer : MonoBehaviour
    {
        private void Update()
        {
            if (FPSFrameworkCore.IsDeveloperMode == false)
            {
                //FPSFrameworkCore.DisposeDevMode();

                DestroyImmediate(gameObject);
            }
        }
    }
}
#endif