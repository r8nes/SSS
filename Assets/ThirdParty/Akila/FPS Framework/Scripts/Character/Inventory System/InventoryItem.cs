using Akila.FPSFramework.Animation;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;



#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Events;
#endif

namespace Akila.FPSFramework
{
    /// <summary>
    /// Base class for all item that can be held by the player or the bot.
    /// </summary>
    [RequireComponent(typeof(ItemInput))]
    public abstract class InventoryItem : Item
    {
        [Tooltip("Replacement object for this item when dropped.")]
        public Pickable replacement;
        public Transform customDropPoint;

        [Header("Special")]
        public bool quickSwitch = false;
        public InputAction quickSwitchInputAction;

        public bool isTryingToDrop { get; set; }

        public void PlayHideAnimation()
        {
            animators = transform.GetComponentsInChildren<Animator>();

            if (animators == null || animators.Length == 0)
            {
                Debug.LogWarning($"{name} has no animators assigned.");
                return;
            }

            foreach (Animator animator in animators)
            {
                if (animator != null)
                {
                    animator.Play("Hide", 0, 1f); // Play from beginning
                }
                else
                {
                    Debug.LogWarning($"{name} has a null Animator reference.");
                }
            }
        }

        public bool isPlayingHidingAnimation
        {
            get
            {
                foreach (Animator animator in animators)
                {
                    if (animator.IsPlaying("Hide")) return true;
                }

                return false;
            }
        }

        /// <summary>
        /// Represents the current progress of the aiming animation, ranging from 0 (not aiming) to 1 (fully aimed).
        /// </summary>
        public float aimProgress
        {
            get
            {
                if (aimingAnimation != null)
                    return aimingAnimation.progress;

                return 0;
            }
        }

        /// <summary>
        /// Represents the firearm component of the item. If the weapon is not a firearm, this will be null.
        /// </summary>
        public Firearm firearm { get; set; }

        /// <summary>
        /// Represents the throwable component of the item. If the weapon is not throwable, this will be null.
        /// </summary>
        public Throwable throwable { get; set; }

        /// <summary>
        /// Reference to the player's inventory.
        /// </summary>
        public IInventory inventory { get; set; }

        /// <summary>
        /// Array of animator components related to this item.
        /// </summary>
        public Animator[] animators { get; set; }

        /// <summary>
        /// The procedural animator handling complex animations for this item.
        /// </summary>
        public ProceduralAnimator proceduralAnimator { get; set; }

        /// <summary>
        /// Reference to the actor (usually the player) using the item.
        /// </summary>
        public IActor actor { get; set; }

        /// <summary>
        /// Character controller associated with the actor.
        /// </summary>
        public ICharacterController character { get; set; }

        /// <summary>
        /// Manages character-specific behaviors and states.
        /// </summary>
        public CharacterManager characterManager { get; set; }

        /// <summary>
        /// Input system for the character.
        /// </summary>
        public CharacterInput characterInput { get; set; }

        public GameObject playerObj { get; set; }

        /// <summary>
        /// Input system for item-related actions (e.g., firing, reloading).
        /// </summary>
        public ItemInput itemInput { get; set; }

        /// <summary>
        /// Animation for breathing.
        /// </summary>
        public ProceduralAnimation breathingAnimation { get; protected set; }

        /// <summary>
        /// Animation for breathing while aiming.
        /// </summary>
        public ProceduralAnimation breathingAimAnimation { get; protected set; }

        /// <summary>
        /// Animation for walking.
        /// </summary>
        public ProceduralAnimation walkingAnimation { get; protected set; }

        /// <summary>
        /// Animation for sprinting.
        /// </summary>
        public ProceduralAnimation sprintingAnimation { get; protected set; }

        /// <summary>
        /// Animation for tactical sprinting.
        /// </summary>
        public ProceduralAnimation tacticalSprintingAnimation { get; protected set; }

        /// <summary>
        /// Animation for aiming.
        /// </summary>
        public ProceduralAnimation aimingAnimation { get; protected set; }

        /// <summary>
        /// Animation for recoil when firing.
        /// </summary>
        public ProceduralAnimation recoilAnimation { get; protected set; }

        /// <summary>
        /// Animation for recoil while aiming.
        /// </summary>
        public ProceduralAnimation recoilAimAnimation { get; protected set; }

        /// <summary>
        /// Animation for jumping.
        /// </summary>
        public ProceduralAnimation jumpAnimation { get; protected set; }

        /// <summary>
        /// Animation for landing.
        /// </summary>
        public ProceduralAnimation landAnimation { get; protected set; }

        /// <summary>
        /// Animation for leaning to the right.
        /// </summary>
        public ProceduralAnimation leanRightAnimation { get; protected set; }

        /// <summary>
        /// Animation for leaning to the left.
        /// </summary>
        public ProceduralAnimation leanLeftAnimation { get; protected set; }

        /// <summary>
        /// Animation for leaning to the right while aiming.
        /// </summary>
        public ProceduralAnimation leanRightAimAnimation { get; protected set; }

        /// <summary>
        /// Animation for leaning to the left while aiming.
        /// </summary>
        public ProceduralAnimation leanLeftAimAnimation { get; protected set; }

        /// <summary>
        /// Animation for when player is crouching.
        /// </summary>
        public ProceduralAnimation crouchAnimation { get; protected set; }

        /// <summary>
        /// Animation for when player is moves his item.
        /// </summary>
        public ProceduralAnimation swayAnimation { get; protected set; }

        /// <summary>
        /// Animation for when player is moves his item while aiming.
        /// </summary>
        public ProceduralAnimation swayAimingAnimation { get;  protected set; }

        /// <summary>
        /// Wave modifier for walking animation.
        /// </summary>
        private WaveAnimationModifier walkingWaveAnimationModifier { get; set; }

        public ProceduralAnimation firingAnimation { get; protected set; }
        public ProceduralAnimation aimFiringAnimation { get; protected set; }
        public ProceduralAnimation currentFiringAnimation
        {
            get
            {
                return isAiming ? aimFiringAnimation : firingAnimation;
            }
        }

        public Speedometer speedometer { get; protected set; }
        public Vector3 velocity { get => speedometer.velocity; }

        /// <summary>
        /// Invoked when the item is dropped.
        /// </summary>
        [HideInInspector] public UnityEvent OnDropAttempted;
        [HideInInspector] public UnityEvent<Pickable> OnDropPerformed;

        /// <summary>
        /// If false, the Drop() function will return after invoking the onDrop event.
        /// </summary>
        public bool isDroppingActive { get; set; } = true;

        /// <summary>
        /// Checks if the aiming animation is currently playing.
        /// </summary>
        public bool isAiming
        {
            get
            {
                // Ensure the aimingAnimation exists before checking if it's playing.
                if (aimingAnimation) return aimingAnimation.IsPlaying;
                return false;
            }
        }

        private float defaultAimingTime;

        /// <summary>
        /// Sets up the item and assigns the replacement item.
        /// </summary>
        /// <param name="replacement">The item that will replace this one.</param>
        protected virtual void Setup(Pickable replacement)
        {
            this.replacement = replacement;
        }

        protected override void Awake()
        {
            base.Awake();

            Setup();
        }

        /// <summary>
        /// Initializes necessary components for the item, including animations and references.
        /// </summary>
        protected void Setup()
        {
            quickSwitchInputAction.Enable();

            // Get necessary components for the script
            itemInput = GetComponent<ItemInput>(); // Get the ItemInput component
            animators = GetComponentsInChildren<Animator>(); // Get all Animator components in the children
            proceduralAnimator = transform.SearchFor<ProceduralAnimator>(); // Get the ProceduralAnimator component
            itemInput = GetComponent<ItemInput>();

            speedometer = gameObject.GetOrAddComponent<Speedometer>(); // Add Speedometer component

            firearm = GetComponent<Firearm>(); // Get the Firearm component
            throwable = GetComponent<Throwable>(); // Get the Throwable component

            // Check for inventory in parent, needed to respawn items in the item holder.
            inventory = transform.DeepSearch<IInventory>();

            // Assign references from itemsHolder
            actor = inventory.transform.SearchFor<IActor>();
            character = GetComponentInParent<ICharacterController>();
            characterManager = GetComponentInParent<CharacterManager>();
            characterInput = GetComponentInParent<CharacterInput>();

            playerObj = character.gameObject;

            // Initialize animations if proceduralAnimator exists
            if (proceduralAnimator != null)
            {
                firingAnimation = proceduralAnimator.GetAnimation("Recoil");
                aimFiringAnimation = proceduralAnimator.GetAnimation("Aim Recoil");
                breathingAnimation = proceduralAnimator.GetAnimation("Breathing");
                breathingAimAnimation = proceduralAnimator.GetAnimation("Breathing Aim");
                walkingAnimation = proceduralAnimator.GetAnimation("Walking");
                sprintingAnimation = proceduralAnimator.GetAnimation("Sprinting");
                tacticalSprintingAnimation = proceduralAnimator.GetAnimation("Tactical Sprinting");
                aimingAnimation = proceduralAnimator.GetAnimation("Aiming");
                recoilAnimation = proceduralAnimator.GetAnimation("Recoil");
                recoilAimAnimation = proceduralAnimator.GetAnimation("Recoil Aim");
                jumpAnimation = proceduralAnimator.GetAnimation("Jump");
                landAnimation = proceduralAnimator.GetAnimation("Land");
                leanRightAnimation = proceduralAnimator.GetAnimation("Lean Right");
                leanLeftAnimation = proceduralAnimator.GetAnimation("Lean Left");
                leanRightAimAnimation = proceduralAnimator.GetAnimation("Lean Right Aim");
                leanLeftAimAnimation = proceduralAnimator.GetAnimation("Lean Left Aim");
                crouchAnimation = proceduralAnimator.GetAnimation("Crouch");
                swayAnimation = proceduralAnimator.GetAnimation("Sway");
                swayAimingAnimation = proceduralAnimator.GetAnimation("Sway Aiming");

                // Set default aiming time
                if (aimingAnimation) defaultAimingTime = aimingAnimation.length;
            }

            // Set up listeners for character manager events
            if (characterManager != null)
            {
                if (jumpAnimation)
                    characterManager.onJump.AddListener(() => jumpAnimation.Play(0));

                if (landAnimation)
                    characterManager.onLand.AddListener(() => landAnimation.Play(0));
            }

            if (walkingAnimation)
                walkingWaveAnimationModifier = walkingAnimation.GetComponent<WaveAnimationModifier>();

            if(inventory != null && inventory.dropPoint != null)
            {
                currentDropPoint = inventory.dropPoint;
            }
        }

        /// <summary>
        /// Updates the state of the item and animations each frame.
        /// </summary>
        protected virtual void Update()
        {
            if (customDropPoint != null)
                currentDropPoint = customDropPoint;

            // Handle item input actions
            if (itemInput.DropInput)
            {
                Drop();
            }

            if (walkingWaveAnimationModifier != null)
            {
                // Update walking wave animation based on character velocity
                float characterVelocity = characterManager.velocity.magnitude;
                walkingWaveAnimationModifier.speed = Mathf.Lerp(walkingWaveAnimationModifier.speed, characterVelocity, Time.deltaTime * 5);

                if (characterManager.velocity.magnitude > 1 && characterManager.isGrounded)
                    walkingWaveAnimationModifier.amount = Mathf.Lerp(walkingWaveAnimationModifier.amount, characterVelocity, Time.deltaTime * 5);
                else
                    walkingWaveAnimationModifier.amount = Mathf.Lerp(walkingWaveAnimationModifier.amount, 0, Time.deltaTime * 5);
            }

            if(swayAnimation)
            {
                swayAnimation.swayAnimationModifiers[0].inputX = (characterInput.LookInput.x / Time.deltaTime) * 0.5f;
                swayAnimation.swayAnimationModifiers[0].inputY = (characterInput.LookInput.y / Time.deltaTime) * 0.5f;
            }

            if (swayAimingAnimation)
            {
                swayAimingAnimation.swayAnimationModifiers[0].inputX = (characterInput.LookInput.x / Time.deltaTime) * 0.5f;
                swayAimingAnimation.swayAnimationModifiers[0].inputY = (characterInput.LookInput.y / Time.deltaTime) * 0.5f;
            }

            if (sprintingAnimation != null)
            {
                sprintingAnimation.triggerType = ProceduralAnimation.TriggerType.None;
                sprintingAnimation.IsPlaying = characterInput.SprintInput;
            }

            if (tacticalSprintingAnimation != null)
            {
                tacticalSprintingAnimation.triggerType = ProceduralAnimation.TriggerType.None;
                tacticalSprintingAnimation.IsPlaying = characterInput.TacticalSprintInput;
            }

            if (aimingAnimation != null)
            {
                // Adjust aiming animation speed based on firearm aim speed
                if (firearm && firearm.firearmAttachmentsManager)
                    aimingAnimation.length = defaultAimingTime / firearm.firearmAttachmentsManager.aimSpeed;

                aimingAnimation.triggerType = ProceduralAnimation.TriggerType.None;
                aimingAnimation.IsPlaying = itemInput.AimInput;
            }

            // Update crouch animation state
            if (crouchAnimation)
            {
                crouchAnimation.triggerType = ProceduralAnimation.TriggerType.None;
                crouchAnimation.IsPlaying = characterInput.CrouchInput;
            }

            // Update firearm-related animations based on reload and firing state
            if (firearm)
            {
                if (firearm.isReloading || firearm.attemptingToFire)
                {
                    if (recoilAnimation != null) recoilAnimation.weight = firearm.firearmAttachmentsManager.visualRecoil;
                    if (recoilAimAnimation != null) recoilAimAnimation.weight = firearm.firearmAttachmentsManager.visualRecoil;

                    if (sprintingAnimation != null) sprintingAnimation.internalAlwaysStayIdle = true;
                    if (tacticalSprintingAnimation != null) tacticalSprintingAnimation.internalAlwaysStayIdle = true;
                }
                else
                {
                    if (sprintingAnimation != null) sprintingAnimation.internalAlwaysStayIdle = false;
                    if (tacticalSprintingAnimation != null) tacticalSprintingAnimation.internalAlwaysStayIdle = false;
                }
            }

            // Handle leaning animations
            UpdateLeanAnimations();
        }

        /// <summary>
        /// Updates the state of leaning animations based on input.
        /// </summary>
        private void UpdateLeanAnimations()
        {
            // Update right and left leaning animations
            if (leanRightAnimation != null)
            {
                leanRightAnimation.IsPlaying = characterInput.LeanRightInput;
            }

            if (leanLeftAnimation != null)
            {
                leanLeftAnimation.IsPlaying = characterInput.LeanLeftInput;
            }

            // Update right and left aiming lean animations
            if (leanRightAimAnimation != null)
            {
                leanRightAimAnimation.IsPlaying = characterInput.LeanRightInput;
            }

            if (leanLeftAimAnimation != null)
            {
                leanLeftAimAnimation.IsPlaying = characterInput.LeanLeftInput;
            }
        }

        public Transform currentDropPoint { get; protected set; }

        /// <summary>
        /// Drops the item on the ground.
        /// </summary>
        /// <param name="removeFromList">If true, the item will be removed from the Inventory's Items List.</param>
        public virtual void Drop(bool removeFromList = true)
        {
            isTryingToDrop = true;

            // Invoke the onDropped event if set.
            OnDropAttempted?.Invoke();

            // Check if dropping is active, if not log a warning and return early.
            if (!isDroppingActive)
            {
                return;
            }

            // Search for the CameraManager component to reset field of view.
            CameraManager cameraManager = transform.SearchFor<CameraManager>();

            // Calculate the drop force and torque based on inventory settings.
            Vector3 force = Vector3.down * (inventory != null ? inventory.dropForce * 3 : 10);
            Vector3 torque = transform.right * (inventory != null ? inventory.dropForce * 3 : 10);

            // If CameraManager exists, reset its field of view.
            if (cameraManager)
            {
                cameraManager.ResetFieldOfView();
            }

            // Check if a replacement item exists; if not, switch to the last item in the inventory.
            if (replacement == null)
            {
                if (inventory != null)
                {
                    inventory.Switch(inventory.items.Count - 1);
                }
                else
                {
                    //Debug.LogError("[InventoryItem] Inventory is null or contains no items. Unable to switch to a fallback item.");
                }

                // Check if the item is a Firearm or Throwable and throw an appropriate error message
                if (GetComponent<Firearm>() != null)
                {
                    Debug.LogError($"[InventoryItem] Failed to drop '{name}' because the 'replacement' field is null. " +
                                   "For Firearm items, ensure the replacement field is assigned in the Firearm preset.");
                }
                else if (GetComponent<Throwable>() != null)
                {
                    Debug.LogError($"[InventoryItem] Failed to drop '{name}' because the 'replacement' field is null. " +
                                   "For Throwable items, ensure the replacement field is assigned in the Throwable component.");
                }
                else
                {
                    Debug.LogError($"[InventoryItem] Failed to drop '{name}' because the 'replacement' field is null. " +
                                   "Ensure the replacement field is assigned to your custom public field as intended.");
                }

                Destroy(gameObject);
                return;
            }

            // Instantiate the replacement item and apply physics if it has a Rigidbody.
            Vector3 dropPosition = Vector3.zero;
            Quaternion dropRotation = Quaternion.identity;

            if(currentDropPoint)
            {
                dropPosition = currentDropPoint.position;
                dropRotation = currentDropPoint.rotation;
            }

            Pickable newPickable = Instantiate(replacement, dropPosition, dropRotation);

            OnDropPerformed?.Invoke(newPickable);

            if (newPickable.GetComponent<Rigidbody>())
            {
                newPickable.GetComponent<Rigidbody>().AddForce(force, ForceMode.VelocityChange);
                newPickable.GetComponent<Rigidbody>().AddTorque(torque, ForceMode.VelocityChange);
            }
            else
            {
                Debug.LogError("The replacement object doesn't have a Rigidbody component.");
            }

            // Remove the current item from the inventory list if specified.
            if (removeFromList) inventory.items.Remove(this);

            // Switch to the last item in the inventory.
            inventory?.Switch(inventory.items.Count - 1);

            // Destroy the current item after dropping it.
            Destroy(gameObject);
        }


#if UNITY_EDITOR
        [ContextMenu("Setup/Default Animations")]
        public void SetupDefaultAnimation()
        {
            proceduralAnimator = transform.SearchFor<ProceduralAnimator>();

            if(!proceduralAnimator)
            {
                proceduralAnimator = gameObject.AddComponent<ProceduralAnimator>();
            }

            string defaultAnimationGUID = WizardGUIDs.defaultAnimationsObject;

            string animationsPath = AssetDatabase.GUIDToAssetPath(defaultAnimationGUID);

            GameObject animationsPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(animationsPath);

            GameObject animationsObj = Instantiate(animationsPrefab, transform);
            animationsObj.name = animationsPrefab.name;
            animationsObj.transform.Reset();

            proceduralAnimator.animationsHolder = animationsObj;

            proceduralAnimator.RefreshAnimationsList();
        }
#endif
    }
}