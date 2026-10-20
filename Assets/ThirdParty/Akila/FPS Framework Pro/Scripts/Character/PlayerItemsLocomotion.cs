using Akila.FPSFramework;
using UnityEngine;

#if MIRROR
using Mirror;
#endif

namespace Akila.FPSFrameworkPro
{
    /// <summary>
    /// Handles the visibility and animation layer of this item object
    /// depending on whether it's the currently selected item in the inventory.
    /// It disables its children and their renderers when not active.
    /// </summary>
    public class PlayerItemsLocomotion : MonoBehaviour
    {
#if MIRROR
        public InventoryItem targetItem;
        public FirearmAttachmentsManager playerAttachmentsManager;
        public Transform casingEjectionPort;
        public string locomotionAnimationLayerName;
        public bool playReloadAnimation;
        public bool playFireAnimation;
        public bool playEquipAnimation;
        public bool playTriggerAnimation;
        public bool playThrowAnimations;

        [HideInInspector]
        public bool isInUse, isVisibleLocally, isVisibleLocomotion, isFiring;

        protected Animator animator;

        protected NetworkIdentity networkIdentity;

        protected IInventory playerInventory;

        protected InventoryItem currentlyUsedItem;

        protected InventoryItem lastUsedItem;

        protected NetworkItemsManager itemsManager;

        protected LaserSight[] laserSights;

        protected int currentlyUsedItemIndex;

        protected Renderer[] renderers;

        protected int animatorLayerIndex;

        protected int currentFirearmAmmo;
        protected int lastFirearmAmmo;
        private float fireTimer;
        private float fireInterval;

        protected virtual void Start()
        {
            animator = transform.GetComponentInParent<Animator>();

            itemsManager = transform.DeepSearch<NetworkItemsManager>();

            playerInventory = transform.DeepSearch<IInventory>();

            networkIdentity = transform.DeepSearch<NetworkIdentity>();

            renderers = GetComponentsInChildren<Renderer>();
        }

        protected virtual void Update()
        {
            if (playerInventory == null)
            {
                Debug.LogError("Couldn't set items locomotion animations due to null or missing inventory.", gameObject);

                return;
            }

            //Copy current item index
            currentlyUsedItemIndex = playerInventory.currentItemIndex;

            //Check and set the current local item ref to shorten code and make it cleaner
            currentlyUsedItem = playerInventory.currentItem;

            //If currently used item is the same as targetItem isInUse returns true
            isInUse = currentlyUsedItem ? currentlyUsedItem.Name == targetItem.Name : false;

            //If isInUse and local player, isVisibleLocally returns true
            isVisibleLocally = isInUse && networkIdentity.isLocalPlayer;

            //If isInUse and not the local player, isVisibleLocomotion returns false
            isVisibleLocomotion = isInUse && !networkIdentity.isLocalPlayer;

            //Show or Hide locomotion item mesh (visible to other players)
            foreach (Renderer renderer in renderers)
            {
                renderer.enabled = isVisibleLocomotion;
            }

            //Deactivate the mesh for the item in inventory for other players
            if (currentlyUsedItem != null)
            {
                //Show or hide the local item mesh (visible to the local player)
                foreach (Renderer localRenderer in currentlyUsedItem.renderers)
                {
                    if (networkIdentity.isLocalPlayer == false)
                        localRenderer.enabled = false;
                }
            }

            if (isInUse && itemsManager)
            {
                itemsManager.casingEjectionLocation = casingEjectionPort;
            }

            ///--------ANIMATIONS--------

            animatorLayerIndex = animator.GetLayerIndex(locomotionAnimationLayerName);

            if (animatorLayerIndex == -1)
            {
                Debug.LogWarning("Couldn't find an animation layer with the name ''. Please make sure the name matches the one in the animator.", gameObject);

                return;
            }

            //Switch animation layer On/Off
            animator.SetLayerWeight(animatorLayerIndex, isVisibleLocomotion ? 1 : 0);

            //Play equip animation
            if (currentlyUsedItem != lastUsedItem)
            {
                if (playEquipAnimation)
                    animator.Play("Take", animatorLayerIndex, 0);
            }

            if(currentlyUsedItem != null && currentlyUsedItem.throwable)
            {
                if (playTriggerAnimation && currentlyUsedItem.throwable.performedTriggerLastFrame)
                {
                    animator.Play("Trigger", animatorLayerIndex, 0);
                }

                if (playThrowAnimations && currentlyUsedItem.throwable.performedThrowLastFrame)
                {
                    animator.Play("Throw", animatorLayerIndex, 0);
                }
            }

            if (playReloadAnimation && currentlyUsedItem && currentlyUsedItem.firearm)
            {
                if (networkIdentity.isLocalPlayer)
                    animator.SetBool("IsReloading", currentlyUsedItem.firearm.isReloading);
            }

            if (currentlyUsedItem != null && currentlyUsedItem.firearm)
            {
                currentFirearmAmmo = currentlyUsedItem.firearm.remainingAmmoCount;

                //Event based firing
                if (currentFirearmAmmo != lastFirearmAmmo && currentlyUsedItem == lastUsedItem)
                {
                    if (networkIdentity.isLocalPlayer && playFireAnimation && isInUse)
                    {
                        animator.Play("Fire", animatorLayerIndex, 0);
                    }
                }
            }

            lastUsedItem = currentlyUsedItem;
            lastFirearmAmmo = currentFirearmAmmo;

            ///-------- Copy attachments -----
            if (currentlyUsedItem != null)
            {
                if (currentlyUsedItem.TryGetComponent<FirearmAttachmentsManager>(out FirearmAttachmentsManager attachmentsManager))
                {
                    if (playerAttachmentsManager != null)
                    {
                        //They are copied directly from the used item since the firearm attachments manager is synced over the network
                        playerAttachmentsManager.activeAttachments = attachmentsManager.activeAttachments;
                    }
                }

                if (isInUse)
                {
                    laserSights = currentlyUsedItem.GetComponentsInChildren<LaserSight>();

                    foreach (LaserSight laser in laserSights)
                    {
                        laser.isActive = isVisibleLocally;
                    }
                }
            }
        }
#endif
    }
}