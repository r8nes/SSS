using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Akila.FPSFramework
{
    public class Melee : InventoryItem
    {
        [Header("Core")]
        public GameObject defaultDecal;
        public Vector3Direction decalDirection;
        public LayerMask hittableLayers = -1;
        public float range = 1;
        public float damage = 30;
        public float hitForce = 5;
        public float cameraKick = 1;
        public float fireRate = 4;
        public bool multiHit;

        [Header("View Filtering")]
        [Range(-1f, 1f)]
        public float centerHitThreshold = 0.6f;

        [Space]
        public float maxAimDeviation = 20;
        public float attackAnimationsFadeTime = 0.1f;
        public string[] attackAnimations;

        private Camera mainCamera;

        const int MAX_HITS = 16;
        private readonly RaycastHit[] hitBuffer = new RaycastHit[MAX_HITS];
        private readonly HashSet<IDamageable> damageables = new HashSet<IDamageable>();

        protected override void Awake()
        {
            base.Awake();
            mainCamera = Camera.main;
        }

        int attackAnimIndex = 0;
        private float fireTimer;

        protected override void Update()
        {
            base.Update();

            if (itemInput.SingleFire && proceduralAnimator.IsDefaultingInRotation(maxAimDeviation, true, true, false))
            {
                PerformAttack();
            }
        }

        protected virtual void PerformAttack()
        {
            if (Time.time <= fireTimer)
                return;

            fireTimer = Time.time + 1f / fireRate;

            damageables.Clear();

            Vector3 camPos = mainCamera.transform.position;
            Vector3 camForward = mainCamera.transform.forward;

            Ray ray = new Ray(camPos, camForward);

            int hitCount = Physics.SphereCastNonAlloc(
                ray,
                range * 0.5f,
                hitBuffer,
                range,
                hittableLayers,
                QueryTriggerInteraction.Ignore
            );

            GameObject singleObjHit = null;

            for (int i = 0; i < hitCount; i++)
            {
                RaycastHit hit = hitBuffer[i];

                if (hit.transform == playerObj.transform)
                    continue;

                Vector3 dirToHit = (hit.point - camPos).normalized;
                float dot = Vector3.Dot(camForward, dirToHit);

                if (dot < centerHitThreshold)
                    continue;

                if (!multiHit && singleObjHit != null)
                    break;

                if (hit.transform.TryGetComponent<IDamageablePart>(out IDamageablePart part) && part.parentDamageable != null)
                {
                    if (damageables.Add(part.parentDamageable))
                    {
                        PerformHitDamage(hit);
                    }
                }
                else if (hit.transform.GetComponent<IDamageable>() != null)
                {
                    PerformHitDamage(hit);
                }

                if (!multiHit)
                    singleObjHit = hit.transform.gameObject;

                Firearm.InvokeHitCallbacks(characterManager.gameObject, ray, hit, hitForce);

                if (hit.rigidbody)
                {
                    hit.rigidbody.AddForceAtPosition(hit.normal * hitForce, hit.point, ForceMode.Impulse);
                }
            }

            if (characterManager?.cameraManager?.cameraKickAnimation)
            {
                var kick = characterManager.cameraManager.cameraKickAnimation;
                kick.Play(0);
                kick.weight = cameraKick;
            }

            PlayAttackAnimations();
        }

        protected virtual void PlayAttackAnimations()
        {
            attackAnimIndex++;
            if (attackAnimIndex >= attackAnimations.Length)
                attackAnimIndex = 0;

            for (int i = 0; i < animators.Length; i++)
            {
                animators[i].CrossFade(attackAnimations[attackAnimIndex], attackAnimationsFadeTime, 0, 0);
            }
        }

        protected virtual void PerformHitDamage(RaycastHit hit)
        {
            GameObject obj = hit.transform.gameObject;

            if (obj.TryGetComponent<Ignore>(out Ignore ignore))
            {
                if (ignore.ignoreMeleeHits)
                    return;
            }

            SpawnHitEffect(hit);

            IDamageable damageable = obj.transform.SearchFor<IDamageable>();
            IDamageablePart damageablePart = obj.GetComponent<IDamageablePart>();

            float damageMultiplier = damageablePart != null ? damageablePart.damageMultipler : 1f;

            if (damageable == null)
                return;

            damageable.Damage(damage * damageMultiplier, playerObj);
        }

        public void SpawnHitEffect(RaycastHit hit)
        {
            CustomDecal customDecal = null;

            if (hit.transform.TryGetComponent(out customDecal))
            {
                defaultDecal = customDecal.decalVFX;
            }

            if (defaultDecal != null)
            {
                Vector3 hitPoint = hit.point;
                Quaternion decalRotation = FPSFrameworkCore.GetFromToRotation(hit, decalDirection);

                GameObject decalInstance = Instantiate(defaultDecal, hitPoint, decalRotation);

                if ((customDecal && customDecal.parent) || customDecal == null)
                {
                    decalInstance.transform.SetParent(hit.transform);
                }

                float decalLifetime = customDecal?.lifeTime ?? 60f;
                Destroy(decalInstance, decalLifetime);
            }
        }

        public override void Drop(bool removeFromList = true)
        {
            if (replacement != null)
                base.Drop(removeFromList);
        }

        private void OnDrawGizmosSelected()
        {
            if (!Camera.main)
                return;

            Gizmos.color = Color.red;

            Vector3 center = Camera.main.transform.position + Camera.main.transform.forward * (range * 0.5f);
            Gizmos.DrawWireSphere(center, range * 0.5f);
        }
    }
}