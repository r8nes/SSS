using UnityEngine;

#if MIRROR
using Mirror;
#endif

namespace Akila.FPSFrameworkPro
{
#if MIRROR
    //TODO: Expand NetworkManager with custom features.
    public class FPSFNetworkManager : NetworkManager
#else
    [ExecuteAlways]
    public class FPSFNetworkManager : MonoBehaviour
#endif
    {
#if MIRROR
        public Camera UISelectionCamera;

        public override void OnStartClient()
        {
            base.OnStartClient();

            if (UISelectionCamera)
                UISelectionCamera.gameObject.SetActive(false);
        }
#else
        private void Update()
        {
            Debug.LogError(
                "[FPS Framework Pro] Missing dependency: Mirror Networking.\n" +
                "FPS Framework Pro cannot function without Mirror installed." +
                "Install Mirror from the following link:\n" +
                "Asset Store: https://assetstore.unity.com/packages/tools/network/mirror-129321"
            );
        }
#endif
    }
}