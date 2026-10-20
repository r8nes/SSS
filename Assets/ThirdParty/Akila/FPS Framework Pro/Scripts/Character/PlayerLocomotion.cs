using Akila.FPSFramework;
using UnityEngine;

#if MIRROR
using Mirror;
#endif

namespace Akila.FPSFrameworkPro
{
    /// <summary>
    /// Synchronizes player movement animations for the local player.
    /// Updates animator parameters (movement, grounded, jumping, landing)
    /// based on real-time character input and physics state.
    /// </summary>
    [RequireComponent(typeof(Speedometer), typeof(Animator))]
#if MIRROR
    public class PlayerLocomotion : NetworkBehaviour
#else
    public class PlayerLocomotion : MonoBehaviour
#endif
    {
#if MIRROR
        #region --- References ---

        /// <summary>
        /// Reference to the CharacterManager component handling physics and grounded checks.
        /// </summary>
        protected CharacterManager characterManager;

        /// <summary>
        /// Reference to the CharacterInput component providing player movement inputs.
        /// </summary>
        protected CharacterInput characterInput;

        /// <summary>
        /// Reference to the Animator controlling the character’s movement animations.
        /// </summary>
        protected Animator animator;

        /// <summary>
        /// Reference to the Speedometer used to measure player velocity magnitude.
        /// </summary>
        private Speedometer speedometer;

        #endregion

        #region --- Private Fields ---

        /// <summary>
        /// Smoothed movement input along the X axis for animation blending.
        /// </summary>
        private float moveX;

        /// <summary>
        /// Smoothed movement input along the Y axis for animation blending.
        /// </summary>
        private float moveY;

        /// <summary>
        /// Current grounded state.
        /// </summary>
        private bool isGrounded;

        /// <summary>
        /// Previous frame grounded state for detecting jump/land transitions.
        /// </summary>
        private bool prevIsGrounded;

        #endregion

        private void Start()
        {
            // Cache required components
            speedometer = GetComponent<Speedometer>();
            animator = GetComponent<Animator>();

            // Try to find input and manager components in hierarchy
            characterInput = transform.SearchFor<CharacterInput>();
            characterManager = transform.SearchFor<CharacterManager>();

            // Safety check to ensure dependencies exist
            if (characterInput == null)
                FPSFrameworkCore.LogWarning("PlayerLocomotion: CharacterInput not found.", MessageLevel.Normal, gameObject);

            if (characterManager == null)
                FPSFrameworkCore.LogWarning("PlayerLocomotion: CharacterManager not found.", MessageLevel.Normal, gameObject);
        }

        private void Update()
        {
            // Process only for the local player
            if (!isLocalPlayer)
                return;

            // Skip animation updates if required components are missing
            if (animator == null || characterInput == null || characterManager == null)
                return;

            // Retrieve grounded state
            isGrounded = characterManager.isGrounded;

            // Measure player movement speed
            float speed = (speedometer != null) ? speedometer.speedMagnitude : 1f;

            // Smoothly interpolate movement inputs to avoid jittery transitions
            moveX = Mathf.Lerp(moveX, characterInput.MoveInputRaw.x * speed, Time.deltaTime * 10f);
            moveY = Mathf.Lerp(moveY, characterInput.MoveInputRaw.y * speed, Time.deltaTime * 10f);

            // Update Animator parameters for blend tree and transitions
            animator.SetFloat("MoveX", moveX);
            animator.SetFloat("MoveY", moveY);
            animator.SetBool("IsGrounded", isGrounded);
            animator.SetBool("IsRunning", characterInput.IsSprintingAtAll());

            // Detect and trigger animation transitions

            // Jump start: player leaves the ground
            if (!isGrounded && prevIsGrounded)
            {
                FPSFrameworkCore.Log("Player jump detected.", MessageLevel.Maximum, gameObject);
                animator.Play("Jump", 0, 0.3f);
            }

            // Landing: player hits the ground
            if (isGrounded && !prevIsGrounded)
            {
                FPSFrameworkCore.Log("Player landed.", MessageLevel.Maximum, gameObject);
                animator.Play("Land", 0, 0.3f);
            }

            // Remember grounded state for next frame
            prevIsGrounded = isGrounded;
        }
#endif
    }
}
