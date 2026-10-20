using Akila.FPSFramework;
using UnityEngine;

#if MIRROR
using Mirror;
#endif

namespace Akila.FPSFrameworkPro
{
    /// <summary>
    /// Manages networked interaction enabling/disabling based on ownership.
    /// Syncs the local InteractionsManager state with the network player ownership.
    /// </summary>
    [RequireComponent(typeof(InteractionsManager))]
#if MIRROR
    public class NetworkInteractionsManager : NetworkBehaviour
#else

    public class NetworkInteractionsManager : MonoBehaviour
#endif
    {
        #if MIRROR
        /// <summary>
        /// Reference to the InteractionsManager component on this GameObject.
        /// </summary>
        private InteractionsManager interactionsManager;

        /// <summary>
        /// Initializes the NetworkInteractionsManager by caching the InteractionsManager
        /// and enabling it only for the local player.
        /// </summary>
        private void Start()
        {
            interactionsManager = GetComponent<InteractionsManager>();

            // Enable interactions only if this is the local player
            interactionsManager.IsActive = isOwned;
        }

        /// <summary>
        /// Allows externally updating the active state of InteractionsManager.
        /// Useful for future extensions or external control.
        /// </summary>
        /// <param name="active">Whether interactions should be enabled.</param>
        public void SetActive(bool active)
        {
            interactionsManager.IsActive = active;
        }
#endif
    }
}