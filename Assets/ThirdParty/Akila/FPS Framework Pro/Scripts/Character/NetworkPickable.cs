using Akila.FPSFramework;
using System;
using System.Linq;
using UnityEngine;


#if MIRROR
using Mirror;
#endif

namespace Akila.FPSFrameworkPro
{
    /// <summary>
    /// Synchronizes pickable item behavior across the network.
    /// Automatically disables the pickable item at start and handles item pickup and destruction.
    /// </summary>
#if MIRROR
    [RequireComponent(typeof(Pickable), typeof(NetworkIdentity))]
    public class NetworkPickable : NetworkBehaviour
#else
    public class NetworkPickable : MonoBehaviour
#endif
    {
        #if MIRROR
        /// <summary>
        /// The local reference to the Pickable component.
        /// </summary>
        private Pickable pickable;

        public string Name
        {
            get
            {
                Pickable pickable = gameObject.GetOrAddComponent<Pickable>();

                return pickable.Name;
            }
        }

        /// <summary>
        /// Called when the object is initialized.
        /// Disables the item and subscribes to the interaction event.
        /// </summary>
        private void Start()
        {
            pickable = GetComponent<Pickable>();

            if (pickable == null)
            {
                Debug.LogError("Pickable component not found.");
                return;
            }

            pickable.isActive = false;

            // Safely add listener to onInteractWithItem UnityEvent
            if (pickable.onInteractAttemptedWithItem != null)
            {
                pickable.onInteractAttemptedWithItem.AddListener(CmdInteract);
            }
            else
            {
                Debug.LogWarning("Pickable.onInteractWithItem is null. Interaction will not work.");
            }
        }

        private void Update()
        {
            if (!isServer) return;

            if (pickable == null) return;

            if (pickable.clearIfEmpty && pickable.collectableCount <= 0)
                NetworkServer.Destroy(netIdentity.gameObject);
        }

        /// <summary>
        /// Command sent from client to server to notify interaction with this pickable.
        /// Executes on the server.
        /// </summary>
        /// <param name="sourcePlayer">The player who interacted with the item.</param>
        [Command(requiresAuthority = false)]
        private void CmdInteract(GameObject sourcePlayer)
        {
            if (sourcePlayer == null)
            {
                Debug.LogWarning("CmdInteract called with null sourcePlayer.");
                return;
            }

            if (pickable.itemToPickup == null)
            {
                Debug.LogError("Pickup failed: 'itemToPickup' is null. Ensure the 'Pickable' object is properly configured before attempting a network pickup.");
                return;
            }

            var networkItem = pickable.itemToPickup.GetComponent<NetworkInventoryItem>();
            if (networkItem == null)
            {
                Debug.LogError($"Pickup failed: '{pickable.itemToPickup.name}' does not have a NetworkInventoryItem component. Attach 'NetworkInventoryItem' or a compatible variant to enable networked pickups.");
            }


            // Broadcast to all clients to run interaction logic
            RpcInteract(sourcePlayer);
        }

        /// <summary>
        /// Client RPC that executes interaction logic on all clients.
        /// Plays audio, transfers item to inventory, and destroys the pickable on the server.
        /// </summary>
        /// <param name="sourcePlayer">The player who interacted with the item.</param>
        [ClientRpc]
        private void RpcInteract(GameObject sourcePlayer)
        {
            if (sourcePlayer == null)
            {
                Debug.LogWarning("RpcInteract called with null sourcePlayer.");
                return;
            }

            try
            {
                IInventory inventory = sourcePlayer.GetComponentInChildren<IInventory>();

                if (inventory == null)
                {
                    Debug.LogWarning("No IInventory found on sourcePlayer.");
                    return;
                }

                if (sourcePlayer.GetComponent<NetworkIdentity>().isLocalPlayer && inventory.items.Count >= inventory.maxSlots)
                    return;

                InteractionsManager interactionsManager = sourcePlayer.GetComponentInChildren<InteractionsManager>();

                // Play interaction sound if available
                if (interactionsManager?.interactionAudio != null)
                {
                    if (pickable.interactSound != null)
                        interactionsManager.interactionAudio.Play(true, pickable.interactSound);
                    else
                        interactionsManager.interactionAudio.Play(true); // Default sound fallback
                }

                // Only the local player actually picks up the item to update their inventory UI etc.
                NetworkIdentity netId = sourcePlayer.GetComponent<NetworkIdentity>();

                if (netId != null && netId.isLocalPlayer)
                {
                    if (pickable.type == PickableType.Item)
                    {
                        InteractWithItem(inventory);

                        if(pickable.includeCollectable)
                        {
                            pickable.InteractWithCollectable(interactionsManager);
                        }
                    }
                }

                // Server destroys the pickable object after pickup
                if (isServer)
                {
                    NetworkServer.Destroy(gameObject);
                }
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        /// <summary>
        /// Handles adding the picked-up item to the player's inventory and manages inventory capacity.
        /// </summary>
        /// <param name="inventory">The inventory to add the item to.</param>
        private void InteractWithItem(IInventory inventory)
        {
            // Refresh inventory current item UI etc.
            inventory.transform.SearchFor<NetworkInventory>()?.RefreshCurrentItem();

            InventoryItem itemToPickup = pickable.itemToPickup;

            if (itemToPickup == null)
            {
                Debug.LogWarning("Pickable item is null.");
                return;
            }

            InventoryItem newItem = Instantiate(itemToPickup, inventory.transform);

            // Update inventory items list
            inventory.items = inventory.transform.GetComponentsInChildren<InventoryItem>(true).ToList();


            // Switch inventory to the newly picked up item
            int index = inventory.items.IndexOf(newItem);

            if (index != -1)
                inventory.Switch(index);

            pickable.onPickupPerformed?.Invoke(newItem);
        }
#endif
    }
}