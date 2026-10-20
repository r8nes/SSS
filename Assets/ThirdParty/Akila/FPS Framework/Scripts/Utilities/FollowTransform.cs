using UnityEngine;

namespace Akila.FPSFramework
{
    public class FollowTransform : MonoBehaviour
    {
        [SerializeField] Transform target;

        private Vector3 localPosition;
        private Quaternion localRotation;

        private void Start()
        {
            if (target == null)
                return;

            Initialize(target);
        }

        public void Initialize(Transform target)
        {
            this.target = target;

            localPosition = target.InverseTransformPoint(transform.position);
            localRotation = Quaternion.Inverse(target.rotation) * transform.rotation;
        }

        private void LateUpdate()
        {
            if (target == null)
                return;

            transform.position = target.TransformPoint(localPosition);
            transform.rotation = target.rotation * localRotation;
        }
    }
}