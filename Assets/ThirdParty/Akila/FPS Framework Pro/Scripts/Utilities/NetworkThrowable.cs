using Akila.FPSFramework;

#if MIRROR
using Mirror;
#endif

namespace Akila.FPSFrameworkPro {

    public class NetworkThrowable : NetworkInventoryItem
    {
        #if MIRROR
        public Throwable throwable { get; private set; }

        public NetworkRigidbodyReliable networkThrowableItem;

        protected override void Start()
        {
            base.Start();

            throwable = GetComponent<Throwable>();

            throwable.IsThrowActive = false;

            NetworkItemsManager networkItemsManager = GetComponentInParent<NetworkItemsManager>();

            if(isLocalPlayer)
            throwable.OnThrowAttempt?.AddListener(networkItemsManager.ThrowOnNetwork);
        }
#endif
    }
}