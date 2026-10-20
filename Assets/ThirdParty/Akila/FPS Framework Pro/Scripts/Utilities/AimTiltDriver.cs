using UnityEngine;
using UnityEngine.Serialization;

namespace Akila.FPSFrameworkPro
{
    /// <summary>
    /// AimTiltDriver is used in FPSF Pro's third-person character system
    /// to rotate bone drivers based on the vertical orientation of a target transform.
    /// This allows bones (such as spine or head) to tilt naturally up or down,
    /// creating reactive animations for aiming and looking without requiring
    /// additional keyframed animations.
    /// </summary>
    public class AimTiltDriver : MonoBehaviour
    {
        /// <summary>
        /// The transform whose orientation drives the tilt effect.
        /// </summary>
        [FormerlySerializedAs("t")]
        [Tooltip("The transform whose orientation drives the tilt effect.")]
        [SerializeField] private Transform targetTransform;

        /// <summary>
        /// The local rotation offset applied when tilting up or down.
        /// </summary>
        [FormerlySerializedAs("rotation")]
        [Tooltip("The local rotation offset applied when tilting up or down.")]
        [SerializeField] private Vector3 tiltRotation;

        /// <summary>
        /// The stored starting rotation of this object.
        /// </summary>
        private Quaternion baseRotation;

        /// <summary>
        /// Cached target rotation when tilting upward.
        /// </summary>
        private Quaternion positiveTilt;

        /// <summary>
        /// Cached target rotation when tilting downward.
        /// </summary>
        private Quaternion negativeTilt;

        /// <summary>
        /// Initializes the base rotation of this object.
        /// </summary>
        private void Start()
        {
            baseRotation = transform.localRotation;
        }

        /// <summary>
        /// Updates the tilt every frame based on the vertical orientation
        /// of the target transform relative to world up.
        /// </summary>
        private void LateUpdate()
        {
            if (targetTransform == null) return;

            // Measure vertical orientation relative to world up
            float dotProduct = Vector3.Dot(targetTransform.forward, Vector3.up);

            // dotProduct = 1 when looking straight up, -1 when looking straight down
            // Negate to make down = 1, up = -1
            float tiltFactor = -dotProduct;

            // Precompute tilt rotations based on configured tilt offset
            positiveTilt = baseRotation * Quaternion.Euler(tiltRotation);
            negativeTilt = baseRotation * Quaternion.Euler(-tiltRotation);

            // Apply tilt proportionally depending on tiltFactor
            if (tiltFactor >= 0)
            {
                transform.localRotation = Quaternion.Slerp(baseRotation, positiveTilt, tiltFactor);
            }
            else
            {
                transform.localRotation = Quaternion.Slerp(baseRotation, negativeTilt, -tiltFactor);
            }
        }
    }
}