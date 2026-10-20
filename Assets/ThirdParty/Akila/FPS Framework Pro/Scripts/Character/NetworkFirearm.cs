using Akila.FPSFramework;
using UnityEngine;

namespace Akila.FPSFrameworkPro
{
    /// <summary>
    /// Networked wrapper for a firearm item that syncs input, HUD, audio, and firing events over the network.
    /// Inherits from NetworkInventoryItem.
    /// </summary>
    [RequireComponent(typeof(Firearm))]
    public class NetworkFirearm : NetworkInventoryItem
    {
#if MIRROR
        /// <summary>
        /// Reference to the networked projectile this firearm fires.
        /// </summary>
        public NetworkProjectile networkProjectile;

        /// <summary>
        /// Reference to the local Firearm component.
        /// </summary>
        public Firearm TargetFirearm { get; protected set; }

        /// <summary>
        /// Reference to the parent NetworkItemsManager that manages this firearm.
        /// </summary>
        public NetworkItemsManager NetworkItemsManager { get; protected set; }

        public LaserSight[] laserSights { get; protected set; }

        /// <summary>
        /// Awake is called when the script instance is being loaded.
        /// Caches references to required components.
        /// </summary>
        protected virtual void Awake()
        {
            TargetFirearm = GetComponent<Firearm>();

            laserSights = GetComponentsInChildren<LaserSight>();

            foreach(LaserSight laserSight in laserSights)
            {
                if (isLocalPlayer == false)
                    laserSight.Stop();
            }

            if (TargetFirearm == null)
            {
                Debug.LogError($"[{nameof(NetworkFirearm)}] Firearm component missing on {gameObject.name}!", gameObject);
                enabled = false;
                return;
            }

            NetworkItemsManager = GetComponentInParent<NetworkItemsManager>();

            if (NetworkItemsManager == null)
            {
                Debug.LogError($"[{nameof(NetworkFirearm)}] NetworkItemsManager not found in parents of {gameObject.name}!", gameObject);
                enabled = false;
                return;
            }
        }

        /// <summary>
        /// Start is called before the first frame update.
        /// Initializes the firearm state and hooks into firing and reload events.
        /// </summary>
        protected override void Start()
        {
            base.Start();

            // Enable HUD and input only for the local player
            TargetFirearm.isHudActive = isLocalPlayer;
            TargetFirearm.isInputActive = isLocalPlayer;
            TargetFirearm.isAudioActive = false;
            TargetFirearm.isParticleEffectsActive = false;
            TargetFirearm.isDroppingActive = false;

            // Enable procedural animation only for the local player
            if (TargetFirearm.proceduralAnimator != null)
            {
                TargetFirearm.proceduralAnimator.isActive = isLocalPlayer;
            }
            else
            {
                Debug.LogWarning($"[{nameof(NetworkFirearm)}] proceduralAnimator missing on {gameObject.name}");
            }

            // Subscribe network event handlers for firing and reloading
            TargetFirearm.events.OnFireDone.AddListener(NetworkItemsManager.FireFirearm);
            TargetFirearm.events.OnFireApplied.AddListener(NetworkItemsManager.CmdApplyFirearmFire);

            if (TargetFirearm.preset.reloadMethod == Firearm.ReloadType.Default)
                TargetFirearm.events.OnReloadStart.AddListener(NetworkItemsManager.PlayReloadSound);
            else
                TargetFirearm.events.OnReloadAppliedOnce.AddListener(NetworkItemsManager.PlayReloadSound);
        }

        /// <summary>
        /// OnEnable is called when the object becomes enabled and active.
        /// Registers this firearm in the parent NetworkItemsManager.
        /// </summary>
        private void OnEnable()
        {
            if (NetworkItemsManager == null)
            {
                NetworkItemsManager = GetComponentInParent<NetworkItemsManager>();
                if (NetworkItemsManager == null)
                {
                    Debug.LogError($"[{nameof(NetworkFirearm)}] Cannot find NetworkItemsManager in parents on enable for {gameObject.name}.");
                    return;
                }
            }

            NetworkItemsManager.firearm = this;
        }
#endif
    }
}
