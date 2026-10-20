using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.VFX;

namespace Akila.FPSFramework
{
    /// <summary>
    /// Controls the transition between animated and ragdoll states for a player character.
    /// Supports both standard ragdolling and a hybrid mode that preserves external forces.
    /// </summary>
    [AddComponentMenu("Akila/FPS Framework/Player/Ragdoll")]
    public class Ragdoll : MonoBehaviour, IOnAnyHitInChildren
    {
        public Animator animator;
        public bool isEnabled;
        public float forceMultiplier = 1;

        protected Rigidbody[] rigidbodies;

        protected virtual void Start()
        {
            if (animator == null)
                animator = transform.SearchFor<Animator>();
            
            rigidbodies = GetComponentsInChildren<Rigidbody>();

            if (isEnabled)
                Enable();
            else
                Disable();
        }

        protected virtual void Update()
        {
            foreach (Rigidbody rb in rigidbodies)
            {
                rb.interpolation = RigidbodyInterpolation.Interpolate;
                rb.isKinematic = !isEnabled;
            }
        }

        public virtual void Enable()
        {
            isEnabled = true;
            animator.enabled = false;

            StartCoroutine(ApplyDeathForce());
        }

        public virtual void Disable()
        { 
            isEnabled = false;
            animator.enabled = true;
        }

        float forceMagnitude;
        Vector3 hitDirection;
        Rigidbody hitRigidbody;

        protected virtual IEnumerator ApplyDeathForce()
        {
            yield return new WaitForSeconds(Time.fixedDeltaTime);

            if (hitRigidbody)
                hitRigidbody.AddForce(hitDirection * forceMagnitude * forceMultiplier * 10, ForceMode.Impulse);
        }

        public void OnAnyHitInChildren(HitInfo hitInfo)
        {
            hitRigidbody = hitInfo.hitRigidbody;
            hitDirection = hitInfo.hitDirection;
            forceMagnitude = hitInfo.hitForceMagnitude;
        }
    }
}