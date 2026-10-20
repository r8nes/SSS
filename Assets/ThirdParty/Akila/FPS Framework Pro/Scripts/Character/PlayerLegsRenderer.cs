using UnityEngine;

#if MIRROR
using Mirror;
#endif

namespace Akila.FPSFramework
{
    public class PlayerLegsRenderer : MonoBehaviour
    {
        #if MIRROR
        public Renderer upperBodyMesh;
        public Renderer lowerBodyMesh;
        public int lowerBodyLayer;
        public Vector3 lowerBodyOffset = new Vector3(0, 0, -0.15f);

        private NetworkIdentity networkIdentity;

        private void Start()
        {
            networkIdentity = transform.DeepSearch<NetworkIdentity>();

            if (networkIdentity.isLocalPlayer || !networkIdentity)
            {
                upperBodyMesh.enabled = false;
                lowerBodyMesh.enabled = true;

                transform.position += lowerBodyOffset;
                lowerBodyMesh.gameObject.layer = lowerBodyLayer;
            }
        }

        private void Update()
        {
            if(networkIdentity == null)
            {
                lowerBodyMesh.gameObject.layer = 0;
            }
        }
#endif
    }
}