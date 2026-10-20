using Akila.FPSFramework;
using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using static Akila.FPSFramework.FirearmAttachmentsManager;
using System;
using System.Linq;






#if MIRROR
using Mirror;
#endif

namespace Akila.FPSFrameworkPro
{
    [RequireComponent(typeof(Inventory))]
#if MIRROR
    public class NetworkItemsManager : NetworkBehaviour
#else
    public class NetworkItemsManager : MonoBehaviour
#endif
    {
#if MIRROR
        public Transform casingEjectionLocation { get; set; }

        private IInventory inventory;

        [HideInInspector]
        public bool isReloading;

        [SyncVar, HideInInspector]
        public bool isReloadingSynced;

        private NetworkInventoryItem inventoryItem;
        private CharacterManager characterManager;
        public NetworkFirearm firearm { get; set; }

        [SyncVar(hook = nameof(OnAttachmentsTextChanged)), HideInInspector]
        public string attachmentsText;

        private string lastSyncedAttachments = "";
        private string lastAppliedAttachments = "";

        private void Start()
        {
            inventoryItem = GetComponentInChildren<NetworkInventoryItem>();
            characterManager = transform.SearchFor<CharacterManager>();
            firearm = GetComponentInChildren<NetworkFirearm>();
        }

        private void Update()
        {
            if (firearm)
                isReloading = firearm.TargetFirearm?.isReloading ?? false;

            if (isLocalPlayer)
            {
                if (isReloadingSynced != isReloading)
                    CmdUpdateIsReloadingState();

                string currentText = BuildAttachmentsString();
                if (currentText != lastSyncedAttachments)
                {
                    lastSyncedAttachments = currentText;
                    CmdUpdateAttachments(currentText);
                }
            }
        }

        private void OnAttachmentsTextChanged(string oldText, string newText)
        {
            if (isLocalPlayer)
                return;

            if (string.IsNullOrEmpty(newText) || newText == lastAppliedAttachments)
                return;

            if (firearm?.TargetFirearm?.firearmAttachmentsManager == null)
            {
                StartCoroutine(WaitAndApplyAttachments(newText));
                return;
            }

            ApplyAttachmentsFromText(newText);
        }

        private IEnumerator WaitAndApplyAttachments(string text)
        {
            float timeout = 2f;
            float timer = 0f;

            while ((firearm?.TargetFirearm?.firearmAttachmentsManager == null) && timer < timeout)
            {
                timer += Time.deltaTime;
                yield return null;
            }

            if (firearm?.TargetFirearm?.firearmAttachmentsManager != null)
                ApplyAttachmentsFromText(text);
        }

        private void ApplyAttachmentsFromText(string text)
        {
            var attachments = ParseAttachments(text);
            firearm.TargetFirearm.firearmAttachmentsManager.SetActiveAttachments(attachments);
            lastAppliedAttachments = text;
        }

        private string BuildAttachmentsString()
        {
            if (firearm?.TargetFirearm?.firearmAttachmentsManager == null)
                return "";

            var attachments = firearm.TargetFirearm.firearmAttachmentsManager.activeAttachments;
            string result = "";
            foreach (var a in attachments)
                result += $"[{a.type}/{a.name}]";

            return result;
        }

        private List<AttachmentData> ParseAttachments(string text)
        {
            var attachments = new List<AttachmentData>();

            if (string.IsNullOrWhiteSpace(text))
                return attachments;

            int current = 0;
            while (current < text.Length)
            {
                int start = text.IndexOf('[', current);
                int end = text.IndexOf(']', start + 1);

                if (start == -1 || end == -1)
                    break;

                string entry = text.Substring(start + 1, end - start - 1); // e.g., "Sight/RedDot"

                int slashIndex = entry.IndexOf('/');
                if (slashIndex > 0)
                {
                    string type = entry.Substring(0, slashIndex);
                    string name = entry.Substring(slashIndex + 1); // can be empty
                    attachments.Add(new AttachmentData(type, name));
                }

                current = end + 1;
            }

            return attachments;
        }


        [Command]
        private void CmdUpdateAttachments(string newText)
        {
            attachmentsText = newText;
        }

        [Command]
        private void CmdUpdateIsReloadingState()
        {
            isReloadingSynced = isReloading;
        }

        #region Inventory Drop

        public virtual void DropItem(NetworkInventoryItem itemToDrop)
        {
            if (itemToDrop == null)
            {
                Debug.Log("ItemToDrop is null or missing.", gameObject);

                return;
            }

            if(itemToDrop.inventoryItem == null)
            {
                Debug.LogError("ItemToDrop doesn't' have an InventoryItem component. Make sure to attach any.", itemToDrop);

                return;
            }

            if (isLocalPlayer)
                CmdDropItem(itemToDrop.inventoryItem.Name, itemToDrop.inventoryItem.replacement.Name);
        }

        [Command]
        protected virtual void CmdDropItem(string itemName, string pickableName)
        {
            try
            {
                // --- Ensure inventory ---
                if (inventory == null)
                    inventory = transform.SearchFor<IInventory>();

                if (inventory == null)
                {
                    Debug.LogError("Inventory is null. Aborting drop.", gameObject);
                    return;
                }

                // --- Refresh item list ---
                inventory.items = inventory.transform.GetComponentsInChildren<InventoryItem>().ToList();

                if (inventory.items == null || inventory.items.Count == 0)
                {
                    Debug.LogWarning("Inventory has no items to drop.", gameObject);
                    return;
                }

                // --- Get index safely ---
                int index = inventory.currentItemIndex - 1;

                // Auto-correct invalid index values
                if (index < 0 || index >= inventory.items.Count)
                {
                    index = Mathf.Clamp(index, 0, inventory.items.Count - 1);

                    // If still invalid (empty list or corrupted index), stop.
                    if (inventory.items.Count == 0 || index < 0)
                    {
                        Debug.LogWarning("Failed to correct invalid item index. Aborting drop.", gameObject);
                        return;
                    }

                    //Debug.LogWarning($"Adjusted invalid drop index to {index}.", gameObject);
                }

                NetworkPickable networkPickable = null;

                //Find pickable item in network manager
                {
                    foreach(GameObject go in NetworkManager.singleton.spawnPrefabs)
                    {
                        if(go.TryGetComponent<NetworkPickable>(out NetworkPickable _networkPickable))
                        {
                            if (_networkPickable.Name == pickableName)
                            {
                                networkPickable = _networkPickable;
                            }
                        }
                    }
                }

                if (networkPickable == null)
                {
                    Debug.LogWarning("NetworkPickable is missing on item. Aborting drop.", gameObject);
                    return;
                }

                // --- Drop position ---
                Vector3 dropPos = transform.position;
                Quaternion dropRot = transform.rotation;

                if (inventory.dropPoint)
                {
                    dropPos = inventory.dropPoint.position;
                    dropRot = inventory.dropPoint.rotation;
                }

                // --- Spawn ---
                GameObject newPickable = Instantiate(networkPickable.gameObject, dropPos, dropRot);
                if (newPickable == null)
                {
                    Debug.LogError("Failed to instantiate new NetworkPickable.", gameObject);
                    return;
                }

                try
                {
                    NetworkServer.Spawn(newPickable);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"Exception during NetworkServer.Spawn(): {ex}", gameObject);
                }

                RpcDropItem(newPickable);
            }
            catch (Exception e)
            {
                Debug.LogException(e, gameObject);
            }
        }

        [ClientRpc]
        protected virtual void RpcDropItem(GameObject pickable)
        {
            try
            {
                if (inventory == null)
                    inventory = transform.SearchFor<IInventory>();

                if (inventory == null)
                {
                    Debug.LogWarning("Inventory is null on client. Aborting RpcDropItem.", gameObject);
                    return;
                }

                if(pickable.TryGetComponent<Pickable>(out Pickable _pickable))
                {
                    if (inventory != null)
                        inventory.currentItem?.OnDropPerformed?.Invoke(_pickable);
                }

                inventory.Switch(0);

                InventoryItem item = inventory.currentItem;

                if (item == null)
                {
                    Debug.LogWarning("Selected item is null on client.", gameObject);
                    return;
                }

                inventoryItem = item.GetComponent<NetworkInventoryItem>();
                if (inventoryItem == null)
                {
                    Debug.LogWarning("No NetworkInventoryItem found on selected item.", gameObject);
                    return;
                }

                if (isLocalPlayer)
                {
                    Destroy(inventoryItem.gameObject);
                }
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }



        #endregion

        #region Firearm Firing & Reloading

        public virtual void FireFirearm(Vector3 position, Vector3 direction)
        {
            CmdFireFirearm(position, direction);
        }

        NetworkPool pool;

        [Command]
        protected virtual void CmdFireFirearm(Vector3 position, Vector3 direction)
        {
            try
            {
                if (firearm == null || firearm.networkProjectile == null)
                    return;

                pool = FindFirstObjectByType<NetworkPool>();

                GameObject newProjectile = Instantiate(firearm.networkProjectile, position, Quaternion.identity).gameObject;

                var netProj = newProjectile.GetComponent<NetworkProjectile>();

                if (netProj != null)
                {
                    Vector3 rayPosition = characterManager.GetCameraPosition();

                    netProj.GetComponent<Projectile>().lastPosition = rayPosition;
                    netProj.GetComponent<Projectile>().startPosition = rayPosition;

                    netProj.sourcePlayer = netIdentity;

                    netProj.velocity = direction * firearm.TargetFirearm.preset.muzzleVelocity;
                    netProj.hittableLayers = firearm.TargetFirearm.preset.hittableLayers;
                    netProj.startPosition = position;

                    Rigidbody mover = netProj.GetOrAddComponent<Rigidbody>();

                    mover.AddForce(netProj.velocity, ForceMode.VelocityChange);
                }

                NetworkServer.Spawn(newProjectile, netIdentity.gameObject);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        [Command]
        public virtual void CmdApplyFirearmFire()
        {
            RpcApplyFirearmFire();
        }

        [ClientRpc]
        protected virtual void RpcApplyFirearmFire()
        {
            try
            {
                Firearm localFirearm = GetComponentInChildren<Firearm>();

                if (localFirearm == null) return;

                localFirearm.currentFireAudio.Play();

                foreach (ParticleSystem particle in localFirearm.firearmParticleEffects)
                {
                    if (particle != localFirearm.chamberingEffects)
                        particle.Play();
                }

                if (!isLocalPlayer)
                {
                    localFirearm.ThrowCasing(casingEjectionLocation);
                }
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        public void PlayReloadSound(int value = 0)
        {
            CmdPlayReloadSound();
        }

        public void PlayReloadSound()
        {
            CmdPlayReloadSound();
        }

        public void ApplyReloadOnce(int amount)
        {

        }

        [Command]
        private void CmdPlayReloadSound()
        {

            RpcApplyFirearmReload();
        }

        [ClientRpc]
        private void RpcApplyFirearmReload()
        {
            Firearm localFirearm = firearm?.TargetFirearm;

            if (localFirearm == null) return;

          localFirearm.currentFireAudio?.DisableEvents();

            if (localFirearm.remainingAmmoCount > 0)
                localFirearm.reloadAudio?.Play(true);
            else
                localFirearm.reloadEmptyAudio?.Play(true);
        }
        #endregion

        [Command]
        public void ThrowOnNetwork(Vector3 position, Vector3 direction)
        {
            NetworkThrowable networkThrowable = GetComponentInChildren<NetworkThrowable>();

            NetworkRigidbodyReliable networkThrowableItem = networkThrowable.networkThrowableItem;
            Throwable throwable = networkThrowable.throwable;

            //Do throw logic
            NetworkRigidbodyReliable newThrowable = Instantiate(networkThrowableItem, throwable.throwPoint.position, throwable.throwPoint.rotation);

            Rigidbody rb = newThrowable.GetComponent<Rigidbody>();

            rb.AddForce(throwable.throwPoint.forward * throwable.throwForce, ForceMode.VelocityChange);

            if (newThrowable.TryGetComponent<Explosive>(out Explosive explosive))
            {
                explosive.DamageSource = throwable.actor.gameObject;
                explosive.delay = 10;

                explosive.GetComponent<NetworkDamageable>().health = explosive.health;

                explosive.GetComponent<NetworkExplosive>().sourcePlayer = netIdentity;
            }

            NetworkServer.Spawn(newThrowable.gameObject, netIdentity.gameObject);

            RpcThrowOnNetwork(newThrowable.gameObject);
        }

        [ClientRpc]
        private void RpcThrowOnNetwork(GameObject obj)
        {
            if (isLocalPlayer)
            {
                Explosive explosive = obj.GetComponent<Explosive>();
                NetworkThrowable networkThrowable = GetComponentInChildren<NetworkThrowable>();
                Throwable throwable = networkThrowable.throwable;

                explosive.delay = throwable.maxCookingTime - throwable.cookingTimer;
            }
        }
#endif
    }
}
