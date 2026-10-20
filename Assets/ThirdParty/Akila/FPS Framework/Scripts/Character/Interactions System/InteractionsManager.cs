using Akila.FPSFramework.Internal;
using System;
using System.Collections.Generic;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace Akila.FPSFramework
{
    [AddComponentMenu("Akila/FPS Framework/Player/Interactions Manager")]
    public class InteractionsManager : MonoBehaviour
    {
        [Tooltip("Maximum distance at which the player can interact with an object.")]
        [FormerlySerializedAs("range")]
        public float interactionRange = 2f;

        [Tooltip("How long the interact key must be held for non-instant interactions (e.g., doors, levers).")]
        [FormerlySerializedAs("interactionDuration")]
        public float holdDuration = 0.2f;

        [Tooltip("Defines the interaction angle as a fraction of 360°. Example: 1 = 360°, 0.5 = 180°.")]
        [FormerlySerializedAs("fieldOfInteractions")]
        public float interactionFieldAngle = 0.5f;

        [Tooltip("Layers that contain interactable objects.")]
        [FormerlySerializedAs("interactableLayers")]
        public LayerMask interactableMask = -1;

        [Tooltip("UI element that displays interaction prompts (e.g., 'Press E to Open').")]
        [FormerlySerializedAs("HUDObject")]
        public GameObject interactionHUD;

        [Tooltip("UI text element that shows the interact key (e.g., 'E').")]
        [FormerlySerializedAs("interactKeyText")]
        public TextMeshProUGUI interactKeyLabel;

        [Tooltip("UI text element that displays the action name (e.g., 'Open', 'Pickup').")]
        [FormerlySerializedAs("interactActionText")]
        public TextMeshProUGUI interactActionLabel;

        public Slider interactionProgrssBar;

        [Tooltip("Audio instance used to play interaction sounds.")]
        public Audio interactionAudio;

        [Tooltip("Triggered when the player interacts with an object.")]
        [FormerlySerializedAs("onInteract")]
        public UnityEvent<IInteractable> onInteraction;

        /// <summary> Reference to the player's inventory (if present). </summary>
        public IInventory Inventory { get; private set; }

        /// <summary> Whether interactions are currently enabled. </summary>
        public bool IsActive { get; set; } = true;

        /// <summary> Reference to the player's input handler. </summary>
        public CharacterInput CharacterInput { get; private set; }

        /// <summary> The current audio clip used for interactions. </summary>
        public AudioClip CurrentInteractionClip { get; private set; }

        private float holdTimer;
        private float prevHoldTimer;
        private string cachedBindingDisplayString;
        private IInteractable currentInteractable;
        private IInteractable previousInteractable;
        private Camera playerCamera;

        private readonly List<IInteractable> nearbyInteractables = new List<IInteractable>();
        private Transform interactionOrigin;

        private void Awake()
        {
            Inventory = GetComponent<IInventory>();
            CharacterInput = this.SearchFor<CharacterInput>();

            if (!playerCamera)
                playerCamera = transform.root.GetComponentInChildren<Camera>();

            interactionOrigin = playerCamera
                ? playerCamera.transform
                : transform;

            if (!playerCamera)
            {
                Debug.LogWarning(
                    "InteractionsManager requires a player camera to accurately detect interactables. No camera found; using transform instead, which may reduce detection accuracy.",
                    this
                );
            }
        }

        private void Start()
        { 
            cachedBindingDisplayString =
                CharacterInput.controls.Player.Interact.GetBindingDisplayString();
        }

        private void OnEnable()
        {
            interactionAudio.Setup(gameObject);
        }

        private void Update()
        {
            currentInteractable = FindClosestInteractable();

            // Handle highlight switching
            if (previousInteractable != currentInteractable)
            {
                if (previousInteractable != null)
                    previousInteractable.isHighlighted = false;

                if (currentInteractable != null)
                    currentInteractable.isHighlighted = true;

                previousInteractable = currentInteractable;
            }

            // Update HUD visibility
            if (interactionHUD)
                interactionHUD.SetActive(IsActive && currentInteractable != null);

            // Skip if not active or no interactable object
            if (currentInteractable == null || !IsActive)
            {
                holdTimer = 0f;
                prevHoldTimer = 0f;

                if (interactionProgrssBar)
                    interactionProgrssBar.value = 0;

                return;
            }

            // Update UI text
            if (interactKeyLabel)
                interactKeyLabel.text = cachedBindingDisplayString;

            if (interactActionLabel)
                interactActionLabel.text = currentInteractable.GetInteractionName();

            // Handle instant interaction
            if (CharacterInput.controls.Player.Interact.triggered)
            {
                holdTimer = 0f;

                if (!currentInteractable.requireHoldToInteract)
                    ExecuteInteraction();
            }

            // Handle hold interaction
            if (currentInteractable.requireHoldToInteract && CharacterInput.controls.Player.Interact.IsPressed())
            {
                holdTimer += Time.deltaTime;

                if (interactionProgrssBar)
                    interactionProgrssBar.value = holdTimer / holdDuration;
            }
            else
            {
                holdTimer = 0f;

                if (interactionProgrssBar)
                    interactionProgrssBar.value = 0;
            }

            // Trigger hold interaction once
            if (currentInteractable.requireHoldToInteract &&
                holdTimer > holdDuration &&
                prevHoldTimer < holdDuration)
            {
                ExecuteInteraction();
            }

            prevHoldTimer = holdTimer;
        }

        /// <summary>
        /// Executes the interaction logic on the current interactable.
        /// </summary>
        private void ExecuteInteraction()
        {
            if (IsActive == false) return;

            onInteraction?.Invoke(currentInteractable);
            currentInteractable.Interact(this);

            // Cancel reload for all equipped firearms
            foreach (Firearm firearm in GetComponentsInChildren<Firearm>())
            {
                firearm.CancelReload();
            }
        }

        /// <summary>
        /// Finds and returns the closest valid interactable object within range and angle.
        /// </summary>
        public IInteractable FindClosestInteractable()
        {
            if (!interactionOrigin)
                return null;

            Collider[] colliders =
                Physics.OverlapSphere(
                    interactionOrigin.position,
                    interactionRange,
                    interactableMask);

            IInteractable bestInteractable = null;
            float bestDot = interactionFieldAngle;

            Vector3 originPos = interactionOrigin.position;
            Vector3 forward = interactionOrigin.forward;

            foreach (Collider col in colliders)
            {
                foreach (var mb in col.GetComponents<MonoBehaviour>())
                {
                    if (mb is not IInteractable interactable)
                        continue;

                    Vector3 dir =
                        (interactable.transform.position - originPos)
                        .normalized;

                    float dot = Vector3.Dot(forward, dir);

                    if (dot < interactionFieldAngle)
                        continue;

                    if (dot > bestDot)
                    {
                        bestDot = dot;
                        bestInteractable = interactable;
                    }
                }
            }

            return bestInteractable;
        }

        private void OnDrawGizmosSelected()
        {
            Transform origin =
    playerCamera ? playerCamera.transform : transform;

            if (!origin)
                return;

            Vector3 originPos = origin.position;

            // Draw interaction range
            Gizmos.color = Color.white;
            Gizmos.DrawWireSphere(originPos, interactionRange);
        }

        private void OnDrawGizmos()
        {
#if UNITY_EDITOR
            if (Application.isPlaying == false)
                return;

            Transform origin =
                playerCamera ? playerCamera.transform : transform;

            if (!origin)
                return;

            Vector3 originPos = origin.position;
            Vector3 forward = origin.forward;

            // Draw interaction range
            Gizmos.color = Color.white;
            Gizmos.DrawWireSphere(originPos, interactionRange);

            Collider[] colliders =
                Physics.OverlapSphere(
                    originPos,
                    interactionRange,
                    interactableMask);

            foreach (Collider col in colliders)
            {
                foreach (var mb in col.GetComponents<MonoBehaviour>())
                {
                    if (mb is not IInteractable interactable)
                        continue;

                    Vector3 targetPos =
                        interactable.transform.position;

                    Vector3 dir = (targetPos - originPos).normalized;

                    float dot = Vector3.Dot(forward, dir);

                    // Normalize weight inside valid cone
                    float weight =
                        Mathf.InverseLerp(
                            interactionFieldAngle,
                            1f,
                            dot);

                    weight = Mathf.Clamp01(weight);

                    // White → Red
                    Gizmos.color =
                        Color.Lerp(Color.white, Color.red, weight);

                    Gizmos.DrawSphere(targetPos, 0.15f);

                    UnityEditor.Handles.Label(
                        targetPos + Vector3.up * 0.25f,
                        weight.ToString("F2"));
                }
            }
#endif
        }

        /// <summary>
        /// Adds or updates the network components required for this manager.
        /// </summary>
        [ContextMenu("Setup/Network Components")]
        private void SetupNetworkComponents()
        {
            FPSFrameworkCore.InvokeConvertMethod("ConvertInteractionsManager", this, new object[] { this });
        }
    }
}
