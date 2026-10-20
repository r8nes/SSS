using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

namespace Akila.FPSFramework
{
    [AddComponentMenu("Akila/FPS Framework/Player/Door")]
    public class Door : MonoBehaviour, IInteractable
    {
        public enum DoorState
        {
            Closed = 0,
            StageOne = 1,
            FullyOpen = 2
        }

        /// <summary>
        /// Text displayed to the player when interacting with the door.
        /// </summary>
        [Tooltip("What is shown to the player when opening or closing the door")]
        public string interactionName = "Open/Close";

        [Tooltip("If enabled, the interaction must be held instead of tapped.")]
        public bool requireHoldToOpen = false;

        [Tooltip("If enabled, the door opens in two interaction stages.")]
        public bool useTwoStages = true;

        [Space]

        [Tooltip("The pivot point around which the door rotates.")]
        public Transform pivot;

        [Header("Stage One (Partially Open)")]
        [Tooltip("Local rotation offset for the first opening stage.")]
        public Vector3 stageOneRotation = new Vector3(0, -20, 0);

        [Header("Stage Two (Fully Open)")]
        [Tooltip("Local rotation offset for the fully open state.")]
        public Vector3 openRotation = new Vector3(0, -90, 0);

        [Header("Hinge Smoothness")]
        [Range(0f, 1f)]
        [Tooltip("Controls how soft and damped the door motion feels. 0 = rough/snappy, 1 = smooth/heavy.")]
        public float hingeDamping = 0.45f;

        [Tooltip("Optional cap for angular motion. Higher values feel more responsive.")]
        public float roughness = 1;

        [Space(10)]
        public UnityEvent<Transform> onInteraction;


        private Vector3 defaultLocalRotation;
        private Vector3 targetLocalRotation;

        private float velX;
        private float velY;
        private float velZ;

        public DoorState currentState { get; set; } = DoorState.Closed;

        /// <summary>
        /// Indicates whether the door is not fully closed.
        /// </summary>
        public bool isOpen => currentState != DoorState.Closed;

        /// <summary>
        /// The transform of the player who last interacted.
        /// </summary>
        public Transform interactionSource { get; protected set; }

        public bool requireHoldToInteract => requireHoldToOpen;

        public bool isHighlighted { get; set; }

        public bool IsActive { get; set; } = true;

        /// <summary>
        /// Initializes the door's default rotation.
        /// </summary>
        protected virtual void Start()
        {
            if (pivot == null)
                pivot = transform;

            defaultLocalRotation = pivot.localEulerAngles;
            targetLocalRotation = defaultLocalRotation;
        }

        /// <summary>
        /// Smoothly rotates the door toward the target rotation.
        /// </summary>
        protected virtual void Update()
        {
            Vector3 current = pivot.localEulerAngles;
            float smoothTime = GetSmoothTime();

            float x = Mathf.SmoothDampAngle(current.x, targetLocalRotation.x, ref velX, smoothTime, roughness * 200);
            float y = Mathf.SmoothDampAngle(current.y, targetLocalRotation.y, ref velY, smoothTime, roughness * 200);
            float z = Mathf.SmoothDampAngle(current.z, targetLocalRotation.z, ref velZ, smoothTime, roughness * 200);

            //Match the state to its actual intended rotation
            switch (currentState)
            {
                case DoorState.Closed:
                    SetClosed();
                    break;

                case DoorState.StageOne:
                    SetStageOne(interactionSide);
                    break;

                case DoorState.FullyOpen:
                    SetFullyOpen(interactionSide);
                    break;
            }

            pivot.localRotation = Quaternion.Euler(x, y, z);
        }

        /// <summary>
        /// Returns the damping time based on the current motion feel and door state.
        /// Lower values = rougher/snappier. Higher values = smoother/heavier.
        /// </summary>
        protected virtual float GetSmoothTime()
        {
            float baseSmooth = Mathf.Lerp(0.06f, 0.35f, hingeDamping);

            switch (currentState)
            {
                case DoorState.StageOne:
                    return baseSmooth * 0.85f;

                case DoorState.FullyOpen:
                    return baseSmooth * 1.15f;

                case DoorState.Closed:
                    return baseSmooth * 0.95f;

                default:
                    return baseSmooth;
            }
        }

        /// <summary>
        /// Gets the interaction text that the player will see.
        /// </summary>
        public virtual string GetInteractionName()
        {
            return interactionName;
        }

        /// <summary>
        /// Handles interaction from InteractionsManager.
        /// </summary>
        public virtual void Interact(InteractionsManager source)
        {
            Interact(source.transform);
        }

        public float interactionSide { get;  set; }

        /// <summary>
        /// Handles interaction using a Transform as the source.
        /// </summary>
        public virtual void Interact(Transform source)
        {
            interactionSource = source;

            Vector3 dir = (source.position - transform.position).normalized;
            interactionSide = -Mathf.Sign(Vector3.Dot(transform.right, dir));

            onInteraction?.Invoke(source);

            if (IsActive == false)
                return;

            if (useTwoStages)
            {
                switch (currentState)
                {
                    case DoorState.Closed:
                        currentState = DoorState.StageOne;
                        break;

                    case DoorState.StageOne:
                        currentState = DoorState.FullyOpen;
                        break;

                    case DoorState.FullyOpen:
                        currentState = DoorState.Closed;
                        break;
                }
            }
            else
            {
                if (currentState == DoorState.Closed)
                {
                    currentState = DoorState.FullyOpen;
                }
                else
                {
                    currentState = DoorState.Closed;
                }
            }
        }

        /// <summary>
        /// Sets the target rotation for the first opening stage.
        /// </summary>
        protected virtual void SetStageOne(float side)
        {
            targetLocalRotation = defaultLocalRotation + new Vector3(
                side * stageOneRotation.x,
                side * stageOneRotation.y,
                side * stageOneRotation.z
            );
        }

        /// <summary>
        /// Sets the target rotation for the fully open stage.
        /// </summary>
        protected virtual void SetFullyOpen(float side)
        {
            targetLocalRotation = defaultLocalRotation + new Vector3(
                side * openRotation.x,
                side * openRotation.y,
                side * openRotation.z
            );
        }

        /// <summary>
        /// Sets the target rotation back to closed.
        /// </summary>
        protected virtual void SetClosed()
        {
            targetLocalRotation = defaultLocalRotation;
        }
    }
}