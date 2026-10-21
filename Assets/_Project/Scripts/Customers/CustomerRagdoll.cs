using System.Collections.Generic;
using PrimeTween;
using Sirenix.OdinInspector;
using UnityEngine;

namespace ShooterShop.Customers
{
    public sealed class CustomerRagdoll : MonoBehaviour
    {
        [Required]
        [SerializeField] private Animator _animator;

        [Required, ChildGameObjectsOnly]
        [InfoBox("Moved down while the corpse sinks under the floor")]
        [SerializeField] private Transform _sinkRoot;

        [Required, ChildGameObjectsOnly, ListDrawerSettings(ShowFoldout = false)]
        [SerializeField] private List<Rigidbody> _bodies = new();

        [Required, ChildGameObjectsOnly, ListDrawerSettings(ShowFoldout = false)]
        [SerializeField] private List<Collider> _hitboxes = new();

        public void Freeze() => SetPhysics(false);

        public void Activate(Vector3 impulse, Vector3 hitPoint)
        {
            _animator.enabled = false;
            SetPhysics(true);

            var closest = FindClosest(hitPoint);

            if (closest != null)
                closest.AddForceAtPosition(impulse, hitPoint, ForceMode.Impulse);
        }

        public void Sink(float depth, float duration)
        {
            SetPhysics(false);

            foreach (var hitbox in _hitboxes)
                hitbox.enabled = false;

            var start = _sinkRoot.position;
            Tween.Position(_sinkRoot, start, start + Vector3.down * depth, duration, Ease.InSine);
        }

        private void SetPhysics(bool isSimulated)
        {
            foreach (var body in _bodies)
            {
                body.isKinematic = !isSimulated;
                body.interpolation = isSimulated ? RigidbodyInterpolation.Interpolate : RigidbodyInterpolation.None;
            }
        }

        private Rigidbody FindClosest(Vector3 point)
        {
            Rigidbody closest = null;
            var best = float.MaxValue;

            foreach (var body in _bodies)
            {
                var distance = (body.worldCenterOfMass - point).sqrMagnitude;

                if (distance < best)
                {
                    best = distance;
                    closest = body;
                }
            }

            return closest;
        }
    }
}
