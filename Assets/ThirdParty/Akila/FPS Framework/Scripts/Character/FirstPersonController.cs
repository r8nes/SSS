using Akila.FPSFramework.Animation;
using Akila.FPSFramework.Internal;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.HID;
using UnityEngine.InputSystem.XR;
using UnityEngine.Serialization;

namespace Akila.FPSFramework
{
    [RequireComponent(typeof(CharacterManager))]
    [RequireComponent(typeof(CharacterController), typeof(CharacterInput))]
    [AddComponentMenu("Akila/FPS Framework/Player/First Person Controller")]
    public class FirstPersonController : MonoBehaviour, ICharacterController
    {
        [Header("Movement")]
        [Tooltip("How quickly the player accelerates to the target movement speed."), FormerlySerializedAs("acceleration")]
        public float acceleration = 0.1f;

        [Tooltip("Default walking speed."), FormerlySerializedAs("walkSpeed")]
        public float walkSpeed = 5;

        [Tooltip("Movement speed while crouching."), FormerlySerializedAs("crouchSpeed")]
        public float crouchSpeed = 3;

        [Tooltip("Movement speed while sprinting."), FormerlySerializedAs("sprintSpeed")]
        public float sprintSpeed = 10;

        [Tooltip("Movement speed during tactical sprinting (faster than normal sprint).")]
        public float tacticalSprintSpeed = 11;

        [Tooltip("How high the player can jump.")]
        public float jumpHeight = 6;

        [Tooltip("Player’s height when crouched.")]
        public float crouchHeight = 1.5f;

        public float crouchTime = 0.1f;

        [Tooltip("Distance between footstep sounds (lower = more frequent).")]
        public float walkingStepInterval = 4.5f;
        public float sprintingStepInterval = 7f;

        [Tooltip("Automatically detects and follows moving platforms.")]
        public bool autoDetectMovingPlatforms = true;

        [Tooltip("If true, maintains horizontal momentum when jumping or falling.")]
        public bool preserveMomentum = true;

        [Range(0f, 1f)]
        [Tooltip("Fraction of momentum preserved when jumping or falling. For example, 0.2 means 20% is lost and 80% is carried over.")]
        public float momentumLoss = 0.1f;

        [Header("Slopes")]
        [Tooltip("If true, the player will slide down steep slopes automatically.")]
        public bool slideDownSlopes = true;

        [Tooltip("Speed at which the player slides down slopes.")]
        public float slopeSlideSpeed = 1;

        [Space]
        [Tooltip("Strength of gravity applied to the player.")]
        public float gravity = 1;

        [Tooltip("Maximum speed the player can fall.")]
        public float maxFallSpeed = 350;

        [Tooltip("Extra downward force applied to keep the player grounded on slopes or uneven terrain.")]
        public float stickToGroundForce = 0.5f;
        public float leaveGroundForce = 3;

        [Header("Camera")]
        [FormerlySerializedAs("_Camera")]
        [Tooltip("Reference to the player’s camera transform.")]
        public Transform cameraTransform;

        [Tooltip("Maximum upward camera rotation in degrees.")]
        public float maximumX = 90f;

        [Tooltip("Maximum downward camera rotation in degrees.")]
        public float minimumX = -90f;

        [Tooltip("Camera position offset relative to the player.")]
        public Vector3 offset = new Vector3(0, -0.05f, 0);

        [Tooltip("Locks and hides the cursor when the game starts.")]
        public bool lockCursor = true;

        [Tooltip("If true, the player rotation uses a global orientation rather than being camera-relative.")]
        public bool globalOrientation = false;

        [Header("Sensitivity")]
        [Tooltip("Mouse look sensitivity multiplier.")]
        public float sensitivityOnMouse = 1;

        [Tooltip("Gamepad look sensitivity multiplier.")]
        public float sensitivityOnGamepad = 1;

        [Tooltip("Adjusts look sensitivity based on camera FOV using this curve.")]
        public AnimationCurve fovToSensitivityCurve = new AnimationCurve(new Keyframe[] { new Keyframe(0, 1), new Keyframe(1, 0) });

        [Tooltip("If enabled, sensitivity dynamically changes depending on FOV.")]
        public bool isDynamicSensitivityEnabled = true;

        [Header("Audio")]
        [Tooltip("Footstep sounds played based on surface type.")]
        public Audio[] footstepsSFX;

        [Tooltip("Sound played when the player jumps.")]
        public Audio jumpSFX;

        [Tooltip("Sound played when the player lands.")]
        public Audio landSFX;

        public CollisionFlags CollisionFlags { get; set; }
        public CharacterController controller { get; set; }
        public CharacterManager characterManager { get; set; }
        public CharacterInput CharacterInput { get; private set; }

        //input velocity
        private Vector3 desiredVelocityRef;
        private Vector3 desiredVelocity;

        //used to know what hard set velocity to smooth damp to
        private Vector3 targetVelocity;

        //used to use for in air velocity preservation with %
        private Vector3 reducedVelocity;
        private Vector3 slideVelocity;

        //out put velocity
        private Vector3 velocity;

        public Transform Orientation { get; set; }
        public float tacticalSprintAmount { get; set; }
        public bool canTacticalSprint { get; set; }


        private Vector3 slopeDirection;

        private float yRotation = 0;
        private float xRotation = 0;

        private float speed;

        private float defaultHeight;
        private float defaultstepOffset;

        private float stepCycle;
        private float nextStep;

        [Space]
        public UnityEvent<int> onStep = new UnityEvent<int>();
        public UnityEvent onJump = new UnityEvent();
        public UnityEvent onLand = new UnityEvent();

        public bool isCrouching { get; set; }

        public bool isActive { get; protected set; } = true;

        public float currentGravityForce { get; protected set; }

        private Quaternion cameraRotation;
        private Quaternion playerRotation;

        public ProceduralAnimator proceduralAnimator { get; protected set; }
        public ProceduralAnimation leanRightAnimation { get; protected set; }
        public ProceduralAnimation leanLeftAnimation { get; protected set; }

        private bool onMovingPlatform;


        /// <summary>
        /// Cached value indicating whether crouch exit is blocked.
        /// Returns true when there is insufficient space above the player to stand up.
        /// </summary>
        public bool IsChrouchBlocked { get; private set; }

        /// <summary>
        /// Checks whether there is an obstacle above the player that prevents standing up.
        /// </summary>
        /// <returns>
        /// True if standing up is blocked; otherwise, false.
        /// </returns>
        private bool GetIsChrouchBlocked()
        {
            if (Physics.Raycast(controller.transform.position + controller.center, transform.up, out RaycastHit hit, 1))
            {
                return true;
            }

            return false;
        }

        protected virtual void Awake()
        {
            characterManager = GetComponent<CharacterManager>();
            CharacterInput = GetComponent<CharacterInput>();
            controller = GetComponent<CharacterController>();
            proceduralAnimator = transform.SearchFor<ProceduralAnimator>();

            controller.enableOverlapRecovery = true;

            characterManager.onJump.AddListener(() =>
            {
                if(attemptedToJump == false)
                {
                    currentGravityForce = -leaveGroundForce;
                }
            });

            characterManager.onLand.AddListener(() =>
            {
                attemptedToJump = false;
            });

            controller.center = Vector3.up * controller.height * 0.5f;

            if(proceduralAnimator)
            {
                leanRightAnimation = proceduralAnimator.GetAnimation("Lean Right");
                leanLeftAnimation = proceduralAnimator.GetAnimation("Lean Left");
            }

            characterManager.SetValues(Vector3.zero, controller.isGrounded, walkSpeed, sprintSpeed);
        }

        float GetSignedAngle(float angle)
        {
            if (angle > 180)
                angle -= 360;
            return angle;
        }

        public bool IsSetMinimapPlayerActive { get; set; } = true;

        protected virtual void Start()
        {
            if (!cameraTransform) cameraTransform = GetComponentInChildren<Camera>().transform;

            if (cameraTransform != null)
            {
                float xRot = GetSignedAngle(cameraTransform.eulerAngles.x);

                xRotation = xRot;
            }

            if (transform.Find("Orientation") != null)
            {
                Orientation = transform.Find("Orientation");
            }
            else
            {
                Orientation = new GameObject("Orientation").transform;

                Orientation.parent = transform;
                Orientation.localRotation = transform.rotation;
            }

            if (Minimap.Instance && IsSetMinimapPlayerActive)
            {
                Minimap.Instance.player = Orientation;
                Minimap.Instance.Visible = true;
            }

            ResetSpeedMultiplier();

            //get defaults
            defaultHeight = controller.height;
            defaultstepOffset = controller.stepOffset;
            controller.skinWidth = controller.radius / 10;
            controller.enableOverlapRecovery = true;

            //hide and lock cursor if there is no pause menu in the scene
            if (lockCursor)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }

            characterManager.onLand.AddListener(PlayLandSFX);

            controller.center = Vector3.up * controller.height * 0.5f;

            cameraTransform.position = transform.position + ((Vector3.up * (controller.height - 1) + offset + controller.center));

            transform.position -= controller.center;

            Orientation.localPosition = controller.center;

            foreach (Audio a in footstepsSFX)
                a.Setup(gameObject);

            jumpSFX.Setup(gameObject);
            landSFX.Setup(gameObject);
        }

        private bool attemptedToJump;

        protected virtual void Update()
        {
            IsChrouchBlocked = GetIsChrouchBlocked();

            if (!isActive) return;

            if(leanRightAnimation)
            {
                leanRightAnimation.IsPlaying = CharacterInput.LeanRightInput;
                leanLeftAnimation.IsPlaying = CharacterInput.LeanLeftInput;
            }

            //slide down slope if on maxed angle slope
            if (slideDownSlopes && OnMaxedAngleSlope())
                slideVelocity += new Vector3(slopeDirection.x, -slopeDirection.y, slopeDirection.z) * slopeSlideSpeed * Time.deltaTime;
            else
                //reset velocity if not on slope
                slideVelocity = Vector3.zero;

            Vector3 rawSpeedInput = (SlopeDirection() * CharacterInput.MoveInput.y + Orientation.right * CharacterInput.MoveInput.x).normalized * speed * CharacterInput.MoveInput.magnitude;

            reducedVelocity = preserveMomentum ? velocity * (1 - momentumLoss) : rawSpeedInput;

            if (controller.isGrounded)
            {
                targetVelocity = rawSpeedInput;
            }

            //update desiredVelocity in order to normlize it and smooth the movement
            desiredVelocity = slideVelocity + Vector3.SmoothDamp(desiredVelocity, controller.isGrounded ? targetVelocity : reducedVelocity, ref desiredVelocityRef, acceleration);

            if (!controller.isGrounded || OnSlope())
            {
                controller.stepOffset = 0;
            }
            else
            {
                controller.stepOffset = defaultstepOffset;
            }

            //copy desiredVelocity x, z with normlized values
            velocity.x = (desiredVelocity.x);
            velocity.z = (desiredVelocity.z);

            //update speed according to if player is holding sprint

            if (SlopeAngle() < controller.slopeLimit)
            {
                if (CharacterInput.SprintInput && !CharacterInput.TacticalSprintInput) speed = isCrouching ? crouchSpeed * speedMultiplier : sprintSpeed * speedMultiplier;
                else if (!CharacterInput.TacticalSprintInput) speed = speed = isCrouching ? crouchSpeed * speedMultiplier : walkSpeed * speedMultiplier;

                if (CharacterInput.TacticalSprintInput) speed = speed = isCrouching ? crouchSpeed * speedMultiplier : tacticalSprintSpeed * speedMultiplier;
            }
            else
            {
                speed = 0;
            }

            //Do crouching
            if(IsChrouchBlocked)
            {
                CharacterInput.IsChrouchInputActive = false;

                if (!CharacterInput.RawCrouchInput)
                    CharacterInput.CrouchInput = true;
                else
                    CharacterInput.CrouchInput = CharacterInput.RawCrouchInput;
            }
            else
            {
                CharacterInput.IsChrouchInputActive = true;

                CharacterInput.CrouchInput = CharacterInput.RawCrouchInput;
            }

            isCrouching = CharacterInput.CrouchInput;

            ApplyCrouching();

            //update gravity and jumping
            if (controller.isGrounded)
            {
                //set small force when grounded in order to staplize the controller
                currentGravityForce = Physics.gravity.y * stickToGroundForce;

                //check jumping input
                if (CharacterInput.JumpInput)
                {
                    attemptedToJump = true;

                    onJump?.Invoke();

                    //update velocity in order to jump
                    currentGravityForce += jumpHeight - currentGravityForce;

                    //play jump sound
                        jumpSFX.Play();
                }
                
                velocity.y = currentGravityForce;
            }
            else if (velocity.magnitude * 3.5f < maxFallSpeed)
            {
                //add gravity
                currentGravityForce += Physics.gravity.y * gravity * Time.deltaTime;

                velocity.y = currentGravityForce;
            }

            if (controller.isGrounded)
            {
                Vector3 input = CharacterInput.MoveInput;
            }

            Vector3 clampedVel = Vector3.ClampMagnitude(new Vector3(velocity.x, 0, velocity.z), tacticalSprintSpeed);

            velocity.x = clampedVel.x;
            velocity.z = clampedVel.z;
            
            //move and update CollisionFlags in order to check if collition is coming from above ot center or bottom
            CollisionFlags = controller.Move(velocity * Time.deltaTime);

            //rotate camera
            UpdateCameraRotation();

            tacticalSprintAmount = CharacterInput.TacticalSprintInput ? 1 : 0;

            MoveWithMovingPlatforms();
        }

        protected virtual void LateUpdate()
        {
            UpdateCharacterManager();
        }

        //Use this function to understand how to integrate your own character controller
        protected virtual void UpdateCharacterManager()
        {
            //Feed character manager with the info it needs
            //The character manager uses these info to invoke OnJump or OnLand events
            //Other components use the walkSpeed and sprintSpeed values to calculate certain things
            characterManager.SetValues(controller.velocity, controller.isGrounded, walkSpeed, sprintSpeed);

            //Get the info we need for this movement script from character manager
            //Other components change this value e.g when aiming, speedMultiplier changes
            speedMultiplier = characterManager.speedMultiplier;
        }

        public void ApplyCrouching()
        {
            //set controller height according to if player is crouching
            controller.height = isCrouching ?
            Mathf.SmoothDamp(controller.height, crouchHeight, ref currentCrouchVel, crouchTime) :
            Mathf.SmoothDamp(controller.height, defaultHeight, ref currentCrouchVel, crouchTime);

            controller.center = Vector3.up * controller.height * 0.5f;

            cameraTransform.position = transform.position + ((Vector3.up * (controller.height - 1) + offset + controller.center));
        }

        public virtual void PlayLandSFX()
        {
            onLand?.Invoke();

                landSFX.Play();
        }

        public virtual void FixedUpdate()
        {
            //update step sounds
            ProgressStepCycle();
        }

        protected virtual void ProgressStepCycle()
        {
            //stop if not grounded
            if (!controller.isGrounded || footstepsSFX.Length <= 0) return;

            //check if taking input and input
            if (controller.velocity.sqrMagnitude > 0 && (CharacterInput.MoveInput.x != 0 || CharacterInput.MoveInput.y != 0))
            {
                //update step cycle
                stepCycle += (controller.velocity.magnitude + (controller.velocity.magnitude * (!characterManager.IsVelocityZero() ? 1f : 1))) * Time.fixedDeltaTime;
            }

            //check step cycle not equal to next step in order to update right
            if (!(stepCycle > nextStep))
            {
                return;
            }

            //update
            nextStep = stepCycle + (CharacterInput.IsSprintingAtAll() ? sprintingStepInterval : walkingStepInterval);
           
            int currentFootStepIndex = Random.Range(0, footstepsSFX.Length);

            onStep?.Invoke(currentFootStepIndex);

            if (footstepsSFX != null)
            {
                Audio currentFootStepAudio = footstepsSFX[currentFootStepIndex];

                    currentFootStepAudio.Play(true);
            }
        }

        protected virtual void UpdateCameraRotation()
        {
            if (prevCamRotation != cameraTransform.rotation) OnCameraRotationUpdated();

            yRotation += CharacterInput.LookInput.x;
            xRotation -= CharacterInput.LookInput.y;


            xRotation = Mathf.Clamp(xRotation, minimumX, maximumX);

            //Avoid Nan for x rot
            if(float.IsNaN(xRotation))
            {
                xRotation = 0;
            }

            //Avoid Nan for y rot
            if (float.IsNaN(yRotation))
            {
                yRotation = 0;
            }

            cameraRotation = Quaternion.Slerp(cameraRotation, Quaternion.Euler(xRotation, yRotation, 0), Time.deltaTime * 100);
            playerRotation = Quaternion.Slerp(playerRotation, Quaternion.Euler(0, yRotation, 0), Time.deltaTime * 100);

            Orientation.SetRotation(playerRotation, !globalOrientation);
            cameraTransform.SetRotation(cameraRotation, !globalOrientation);

            prevCamRotation = cameraTransform.rotation;
        }

        private Quaternion prevCamRotation;

        protected virtual void OnCameraRotationUpdated() { }

        public virtual bool OnSlope()
        {
            //check if slope angle is more than 0
            if (SlopeAngle() > 0)
            {
                return true;
            }

            return false;
        }

        public virtual bool OnMaxedAngleSlope()
        {
            if (controller.isGrounded && Physics.Raycast(transform.position, Vector3.down, out RaycastHit hit, controller.height))
            {
                slopeDirection = hit.normal;
                return Vector3.Angle(slopeDirection, Vector3.up) > controller.slopeLimit;
            }

            return false;
        }

        public virtual Vector3 SlopeDirection()
        {
            //setup a raycast from position to down at the bottom of the collider
            RaycastHit slopeHit;

            if (Physics.Raycast(Orientation.position, Vector3.down, out slopeHit, (controller.height / 2) + 0.1f) && SlopeAngle() < controller.slopeLimit)
            {
                //get the direction result according to slope normal
                return Vector3.ProjectOnPlane(Orientation.forward, slopeHit.normal);
            }

            //if not on slope then slope is forward ;)
            return Orientation.forward;
        }

        public virtual float SlopeAngle()
        {
            //setup a raycast from position to down at the bottom of the collider
            RaycastHit slopeHit;
            if (Physics.Raycast(transform.position, Vector3.down, out slopeHit))
            {
                //get the direction result according to slope normal
                return (Vector3.Angle(Vector3.down, slopeHit.normal) - 180) * -1;
            }

            //if not on slope then slope is forward ;)
            return 0;
        }

        private float speedMultiplier = 1;

        public virtual void SetSpeedMultiplier(float speedMultiplier)
        {
            this.speedMultiplier = speedMultiplier;
        }

        public virtual void ResetSpeedMultiplier()
        {
            speedMultiplier = 1;
        }

        public void SetActive(bool value)
        {
            isActive = value;

            //Set active for the camera
            Camera[] cameras = GetComponentsInChildren<Camera>();

            foreach (Camera cam in cameras)
            {
                cam.enabled = value;
            }

            controller.detectCollisions = value;

            //Set active for the audio listener
            AudioListener[] audioListeners = GetComponentsInChildren<AudioListener>();

            foreach (AudioListener audioListener in audioListeners)
            {
                AudioEchoFilter echoFilter = audioListener.GetComponent<AudioEchoFilter>();
                AudioReverbFilter reverbFilter = audioListener.GetComponent<AudioReverbFilter>();
                AudioHighPassFilter highPassFilter = audioListener.GetComponent<AudioHighPassFilter>();
                AudioLowPassFilter lowPassFilter = audioListener.GetComponent<AudioLowPassFilter>();
                AudioDistortionFilter distortionFilter = audioListener.GetComponent<AudioDistortionFilter>();

                if(echoFilter) echoFilter.enabled = value;
                if(reverbFilter)reverbFilter.enabled = value;
                if(highPassFilter) highPassFilter.enabled = value;
                if(lowPassFilter) lowPassFilter.enabled = value;
                if(distortionFilter) distortionFilter.enabled = value;
                
                audioListener.enabled = value;
            }
        }

        protected virtual void OnControllerColliderHit(ControllerColliderHit hit)
        {
            //if hit something while jumping from the above then go down again
            if (CollisionFlags == CollisionFlags.Above)
            {
                velocity.y = 0;
            }

            Rigidbody otherRb = hit.rigidbody;
            if (otherRb == null || otherRb.isKinematic) return;

            // Direction from player into the object
            Vector3 normal = hit.normal;
            Vector3 pushDir = -normal;

            // Project player velocity onto push direction
            float impactSpeed = Vector3.Dot(controller.velocity, pushDir);
            if (impactSpeed <= 0f) return; // No forward impact

            // Pure momentum exchange: p = m * v
            float momentum = (70 * impactSpeed) / otherRb.mass < 10 ? 10 : otherRb.mass;

            // Impulse to apply
            Vector3 impulse = pushDir * momentum * Time.deltaTime * 100;

            otherRb.AddForceAtPosition(impulse, hit.point, ForceMode.Impulse);
        }

        private Vector3 feetPosition;
        private Vector3 totalVelocity;
        private Vector3 currentVel;
        private float currentCrouchVel;
        RaycastHit feetHit;
        

        private void MoveWithMovingPlatforms()
        {
            // Calculate the position of the feet based on character height
            feetPosition = transform.position - ((transform.up * (controller.height / 2)) - controller.center);

            if (Physics.Raycast(feetPosition, Vector3.down, out feetHit, 0.05f))
            {
                if (feetHit.transform != transform)
                    totalVelocity = GetTransformVelocity(feetHit.transform);
            }

            // Move the character controller based on total velocity
            transform.position += totalVelocity;

            onMovingPlatform = totalVelocity.magnitude > 0;
        }

        private Vector3 GetTransformVelocity(Transform hitTransform)
        {
            Speedometer speedometer = null;

            if (autoDetectMovingPlatforms)
                speedometer = hitTransform.GetOrAddComponent<Speedometer>();
            else
                speedometer = hitTransform.GetComponent<Speedometer>();

            if (speedometer == null)
            {
                return Vector3.zero;
            }

            if (speedometer.TryGetComponent<Ignore>(out Ignore ignore))
            {
                if (ignore.ignoreMovingPlatform)
                {
                    return Vector3.zero;
                }
            }

            // If the Speedometer component exists, return its velocity
            if (speedometer != null)
            {
                return speedometer.GetPointVelocity(transform.position) * Time.unscaledDeltaTime; // Apply delta time for frame-rate independent movement
            }

            return Vector3.zero; // Return zero if no Speedometer is found
        }

        [ContextMenu("Setup/Network Components")]
        public void Convert()
        {
            FPSFrameworkCore.InvokeConvertMethod("ConvertPlayer", this, new object[] { this });
        }
    }
}