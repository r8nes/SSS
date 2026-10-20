using Akila.FPSFramework;
using UnityEngine;

#if MIRROR
using Mirror;
#endif

namespace Akila.FPSFrameworkPro
{
    /// <summary>
    /// Represents a network-aware inventory item with ownership and drop handling.
    /// </summary>
    public class NetworkInventoryItem : MonoBehaviour
    {
#if MIRROR
        [Header("Overrides")]
        [Tooltip("Optional override for the network pickable component.")]
        public NetworkPickable networkPickable;

        /// <summary>
        /// Whether this item belongs to the local player.
        /// </summary>
        public bool isLocalPlayer { get; protected set; } = true;

        /// <summary>
        /// Reference to the InventoryItem component.
        /// </summary>
        public InventoryItem inventoryItem
        {
            get
            {
                if(_inventoryItem == null)
                    _inventoryItem = GetComponent<InventoryItem>();

                return _inventoryItem;
            }
        }

        private InventoryItem _inventoryItem;

        public bool isDropping { get; set; }

        /// <summary>
        /// Initialization logic to set up ownership, animation, and drop callbacks.
        /// </summary>
        protected virtual void Start()
        {
            if (inventoryItem == null)
            {
                Debug.LogError($"[{nameof(NetworkInventoryItem)}] InventoryItem component missing on {gameObject.name}", gameObject);
                enabled = false;
                return;
            }

            NetworkIdentity identity = GetComponentInParent<NetworkIdentity>();
            if (identity == null)
            {
                Debug.LogError($"[{nameof(NetworkInventoryItem)}] No NetworkIdentity found in parent hierarchy of {gameObject.name}", gameObject);
                enabled = false;
                return;
            }

            isLocalPlayer = identity.isLocalPlayer;

            // Disable input and dropping for non-local player items
            inventoryItem.isActive = false;
            inventoryItem.isDroppingActive = false;

            // Activate procedural animator only for the local player
            if (inventoryItem.proceduralAnimator != null)
                inventoryItem.proceduralAnimator.isActive = isLocalPlayer;

            NetworkItemsManager networkItemsManager = GetComponentInParent<NetworkItemsManager>();
            if (networkItemsManager == null)
            {
                Debug.LogWarning($"[{nameof(NetworkInventoryItem)}] No NetworkItemsManager found in parent of {gameObject.name}", gameObject);
                return;
            }

            // Subscribe to drop event to notify NetworkItemsManager
            inventoryItem.OnDropAttempted.AddListener(() => networkItemsManager.DropItem(this));
        }
#endif
    }
}