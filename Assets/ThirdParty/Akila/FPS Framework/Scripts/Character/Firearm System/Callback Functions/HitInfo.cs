using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Akila.FPSFramework
{
    /// <summary>
    /// This class will have almost all of the data yout need to interact with the hits from projectile or explosion
    /// </summary>
    public class HitInfo
    {
        public GameObject sourcePlayer { get; set; }
        public Vector3 hitOrigin { get; set; }
        public Vector3 hitDirection { get; set; }
        public Vector3 hitNormal { get; set; }
        public Vector3 hitPoint { get; set; }

        public float hitDistance { get => Vector3.Distance(hitOrigin, hitPoint); }

        public Collider hitCollider { get; set; }
        public Rigidbody hitRigidbody { get; set; }
        public GameObject hitObject { get; set; }
        public float hitForceMagnitude { get; set; }

        public bool isCriticalHit
        {
            get
            {
                if (hitObject)
                {
                    if (hitObject.TryGetComponent<IDamageablePart>(out IDamageablePart damageableParent))
                    {
                        return damageableParent.isCriticalPart;
                    }
                }

                return false;
            }
        }

        public HitInfo(GameObject sourcePlayer, Vector3 hitOrigin, Vector3 hitDirection, Vector3 hitNormal, Vector3 hitPoint, Collider hitCollider, Rigidbody hitRigidbody, GameObject hitObject, float hitForceMagnitude)
        {
            this.sourcePlayer = sourcePlayer;
            this.hitOrigin = hitOrigin;
            this.hitDirection = hitDirection;
            this.hitNormal = hitNormal;
            this.hitPoint = hitPoint;
            this.hitCollider = hitCollider;
            this.hitRigidbody = hitRigidbody;
            this.hitObject = hitObject;
            this.hitForceMagnitude = hitForceMagnitude;
        }
    }
}