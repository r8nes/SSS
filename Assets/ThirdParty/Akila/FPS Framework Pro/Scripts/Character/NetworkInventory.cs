using Akila.FPSFramework;
using System;
using System.Collections.Generic;
using UnityEngine;

#if MIRROR
using Mirror;
#endif

namespace Akila.FPSFrameworkPro
{
    /// <summary>
    /// Manages a networked inventory, synchronizing the active item across clients.
    /// </summary>
#if MIRROR
    public class NetworkInventory : NetworkBehaviour
#else
    public class NetworkInventory : MonoBehaviour
#endif
    {
        #if MIRROR
        /// <summary>
        /// Reference to the local Inventory component.
        /// </summary>
        private Inventory inventory;

        /// <summary>
        /// List of inventory items that can be instantiated for network sync.
        /// Should be assigned in inspector or initialized externally.
        /// </summary>
        public List<InventoryItem> items = new List<InventoryItem>();

        /// <summary>
        /// The name of the currently active inventory item, synced across the network.
        /// </summary>
        [SyncVar(hook = nameof(UpdateActiveItem)), HideInInspector]
        public string activeItemName;

        /// <summary>
        /// Tracks the currently selected local item name for comparison with synced data.
        /// </summary>
        public string localCurrentItem { get; private set; }

        /// <summary>
        /// Initializes the local inventory and sets input active only for the local player.
        /// </summary>
        private void Start()
        {
            inventory = GetComponent<Inventory>();

            if (inventory == null)
            {
                Debug.LogError($"[{nameof(NetworkInventory)}] Inventory component missing on {gameObject.name}", gameObject);
                enabled = false;
                return;
            }

            inventory.isInputActive = isLocalPlayer;
        }

        /// <summary>
        /// Periodically checks for changes in the local current item and triggers sync if needed.
        /// </summary>
        private void FixedUpdate()
        {
            if (inventory != null && inventory.items != null && inventory.items.Count > 0)
            {
                int index = Mathf.Clamp(inventory.currentItemIndex, 0, inventory.items.Count - 1);
                localCurrentItem = inventory.items[index].Name;
            }
            else
            {
                localCurrentItem = null;
            }

            if (isLocalPlayer && activeItemName != localCurrentItem)
            {
                RefreshCurrentItem();
            }
        }

        /// <summary>
        /// Requests the server to update the current active item name.
        /// </summary>
        public void RefreshCurrentItem()
        {
            if (isLocalPlayer && isClient && NetworkClient.ready)
            {
                CmdUpdateCurrentItem(localCurrentItem);
            }
        }

        /// <summary>
        /// Server command to update the active item name and notify clients.
        /// </summary>
        /// <param name="newItemName">The new active item name.</param>
        [Command]
        private void CmdUpdateCurrentItem(string newItemName)
        {
            activeItemName = newItemName;
        }

        /// <summary>
        /// Called on clients when the active item name changes.
        /// Updates the item mesh accordingly.
        /// </summary>
        /// <param name="oldValue">Previous active item name.</param>
        /// <param name="newValue">New active item name.</param>
        private void UpdateActiveItem(string oldValue, string newValue)
        {
            try
            {
                if (isOwned) return; // Local player handles its own inventory visuals

                // Clear existing child item models
                transform.ClearChildren();

                if (!string.IsNullOrEmpty(newValue))
                {
                    // Find the inventory item by name
                    InventoryItem itemToSpawn = items.Find(i => i.Name == newValue);

                    if (itemToSpawn != null)
                    {
                        // Instantiate the item under this transform
                        Instantiate(itemToSpawn, transform);
                    }
                    else
                    {
                        Debug.LogError($"[{nameof(NetworkInventory)}] Item '{newValue}' not found in NetworkInventory's items list on {gameObject.name}.", gameObject);
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }
#endif
    }
}
