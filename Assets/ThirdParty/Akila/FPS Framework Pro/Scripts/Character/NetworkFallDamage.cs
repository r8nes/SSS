using Akila.FPSFramework.Experimental;
using UnityEngine;

#if MIRROR
using Mirror;
#endif

namespace Akila.FPSFrameworkPro
{
    [RequireComponent(typeof(FallDamage), typeof(NetworkDamageable))]
    #if MIRROR
    public class NetworkFallDamage : NetworkBehaviour
#else
    public class NetworkFallDamage : MonoBehaviour
    #endif
    {
#if MIRROR
        public NetworkDamageable networkDamageable;
        public FallDamage fallDamage;

        private void Start()
        {
            networkDamageable = GetComponent<NetworkDamageable>();
            fallDamage = GetComponent<FallDamage>();

            fallDamage.isDamageActive = false;
            fallDamage.onDamaged.AddListener(PerformNetworkDamage);
        }

        private void PerformNetworkDamage(float damage)
        {
            networkDamageable.Damage(damage);
        }
#endif
    }
}