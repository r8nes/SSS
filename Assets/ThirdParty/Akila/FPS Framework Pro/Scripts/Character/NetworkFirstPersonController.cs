using UnityEngine;
using Akila.FPSFramework;

#if MIRROR
using Mirror;
#endif

namespace Akila.FPSFrameworkPro
{
    /// <summary>
    /// A network-enabled first-person controller that synchronizes player actions
    /// such as footstep, jump, and landing sounds across the network.
    /// </summary>
#if MIRROR
    public class NetworkFirstPersonController : NetworkBehaviour
#else
    public class NetworkFirstPersonController : MonoBehaviour
#endif
    {
        #if MIRROR
        [Tooltip("Assign the player object to be deactivated if owned by the player.")]
        public Renderer[] playerMeshes;
        public bool toggleChildrenColliders = true;
        public bool toggleMinimapObjects = true;

        /// <summary>
        /// Reference to the FirstPersonController component.
        /// </summary>
        public FirstPersonController FirstPersonController { get; private set; }

        private IDamageable damageable;

        private void Awake()
        {
            FirstPersonController = GetComponent<FirstPersonController>();

            FirstPersonController.lockCursor = false;
            FirstPersonController.IsSetMinimapPlayerActive = false;
        }

        /// <summary>
        /// Called when the script instance is being loaded.
        /// Caches required components and sets up network events.
        /// </summary>
        private void Start()
        {
            FirstPersonController = GetComponent<FirstPersonController>();
            damageable = GetComponent<IDamageable>();

            if (FirstPersonController == null)
            {
                Debug.LogError($"[{nameof(NetworkFirstPersonController)}] Missing FirstPersonController component on {gameObject.name}", gameObject);
                enabled = false;
                return;
            }

            if (playerMeshes == null)
            {
                Debug.LogWarning($"[{nameof(NetworkFirstPersonController)}] PlayerObject is not assigned on {gameObject.name}. player Object will be auto-assigned to Orientation.");
            }
            else
            {
                playerMeshes = transform.FindDeepChild("Orientation").GetComponentsInChildren<Renderer>();
            }

            //hide and lock cursor if there is no pause menu in the scene
            if (isLocalPlayer)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }

            InitializeController();
        }

        /// <summary>
        /// Sets up event listeners and toggles components based on ownership.
        /// </summary>
        private void InitializeController()
        {
            if (isOwned)
            {
                // Subscribe to local player events and send commands to server on trigger
                FirstPersonController.onStep.AddListener(CmdPlayFootStepSound);
                FirstPersonController.onJump.AddListener(CmdPlayJumpSound);
                FirstPersonController.onLand.AddListener(CmdPlayLandSound);
            }

            // Enable or disable controller input and HUD based on ownership
            FirstPersonController.SetActive(isOwned);

            if (Minimap.Instance)
            {
                Minimap.Instance.Visible = true;
                Minimap.Instance.AutoFindPlayer = false;
            }

            // Toggle visibility and collision of the player object for local player vs others
            if (playerMeshes != null)
            {
                ToggleMeshes(!isLocalPlayer);
            }

            if(toggleChildrenColliders)
            {
                ToggleColliders(!isLocalPlayer);
            }

            if(toggleMinimapObjects)
            {
                ToggleMinimapObjects(isLocalPlayer);
            }

            if (TryGetComponent<Ragdoll>(out Ragdoll ragdoll) && isLocalPlayer)
                ragdoll.enabled = false;
        }

        public void ToggleMeshes(bool toggle)
        {
            foreach (Renderer renderer in playerMeshes)
            {
                renderer.enabled = toggle;
            }
        }

        public void ToggleColliders(bool toggle)
        {
            foreach (Collider collider in GetComponentsInChildren<Collider>())
            {
                if (collider != GetComponent<Collider>())
                    collider.enabled = toggle;
            }
        }

        public void ToggleMinimapObjects(bool toggle)
        {
            foreach(MinimapObject minimapObject in GetComponentsInChildren<MinimapObject>())
            {
                minimapObject.visible = toggle;
            }
            
            if(toggle == true && netIdentity.isLocalPlayer)
            {
                if (Minimap.Instance)
                    Minimap.Instance.player = FirstPersonController.Orientation;
            }
        }

        private void OnDestroy()
        {
            if (toggleMinimapObjects)
                ToggleMinimapObjects(false);
        }

        #region Network Commands and RPCs

        [Command]
        private void CmdPlayJumpSound() => RpcPlayJumpSound();

        [ClientRpc]
        private void RpcPlayJumpSound()
        {
            if (isOwned) return;

                FirstPersonController?.jumpSFX?.Play(true);
        }

        [Command]
        private void CmdPlayLandSound() => RpcPlayLandSound();

        [ClientRpc]
        private void RpcPlayLandSound()
        {
            if (isOwned) return;

                FirstPersonController?.landSFX?.Play(true);
        }

        /// <summary>
        /// Command triggered by local player to notify server of footstep sound.
        /// </summary>
        /// <param name="soundIndex">Index of footstep sound clip.</param>
        [Command]
        private void CmdPlayFootStepSound(int soundIndex) => RpcPlayFootStepSound(soundIndex);

        /// <summary>
        /// Plays footstep sound on remote clients.
        /// </summary>
        /// <param name="soundIndex">Index of footstep sound clip.</param>
        [ClientRpc]
        private void RpcPlayFootStepSound(int soundIndex)
        {
            if (isOwned) return;

            if (FirstPersonController?.footstepsSFX != null &&
                soundIndex >= 0 && soundIndex < FirstPersonController.footstepsSFX.Length)
            {
                var audioSource = FirstPersonController.footstepsSFX[soundIndex];

                    audioSource?.Play(true);
            }
            else
            {
                Debug.LogWarning($"[{nameof(NetworkFirstPersonController)}] Invalid footstep soundIndex: {soundIndex}");
            }
        }

        #endregion
#endif
    }
}
