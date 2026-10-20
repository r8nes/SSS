using UnityEngine;

namespace Akila.FPSFrameworkPro
{
    [ExecuteAlways]
    public class TransformAligner : MonoBehaviour
    {
        public bool preview = false;
        public Transform reference;
        public bool pos;

        private Quaternion initialRotationOffset;
        private Vector3 initialOffsetWorld;

        private void Start()
        {
            if (reference == null) return;

            // Rotation offset
            initialRotationOffset = Quaternion.Inverse(reference.rotation) * transform.rotation;

            // Position offset in world space at start
            initialOffsetWorld = transform.position;
        }

        private void LateUpdate()
        {
            if (reference == null) return;

            if (Application.isPlaying == false && preview == false) return;

            if (GetComponentInParent<Animator>().enabled == false) return;

            // Apply rotation correction
            transform.rotation = reference.rotation * initialRotationOffset;

            // Apply additive position (animation pose + preserved offset)
            if (pos)
            {
                transform.position += transform.position;
            }
        }
    }
}