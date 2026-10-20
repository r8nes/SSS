#if MIRROR
using Mirror;

#endif
using UnityEngine;

namespace Akila.FPSFrameworkPro
{
#if MIRROR
    public class NetworkDestroyer : NetworkBehaviour
#else
    public class NetworkDestroyer : MonoBehaviour
#endif
    {
        #if MIRROR
        public bool destoryOnAwake = false;
        public float delay = 5;

        private void Start()
        {
            if (destoryOnAwake)
                StartDestory(delay);
        }

        public bool destoryOrdered { get; protected set; }

        public void StartDestory(float delay)
        {
            Invoke(nameof(DestroySelf), delay);
        }

        [Command]
        protected virtual void DestroySelf()
        {
            NetworkServer.Destroy(gameObject);
        }
#endif
    }
}