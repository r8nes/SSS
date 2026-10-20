using Akila.FPSFramework.Experimental;
using System;
using System.Collections;
using System.Reflection;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem.HID;
using UnityEngine.Pool;

namespace Akila.FPSFramework
{
    [AddComponentMenu("Akila/FPS Framework/Weapons/Projectile")]
    public class Projectile : MonoBehaviour
    {
        [Header("Base Settings")]
        public LayerMask hittableLayers = -1;
        public Vector3Direction decalDirection = Vector3Direction.forward;
        public float gravity = 1;
        public float force = 10;
        public int lifeTime = 5;
        public GameObject defaultDecal;
        public float hitRadius = 0.03f;

        [Header("FlyBy Settings")]
        public LayerMask flyByHittableLayers = -1;
        public float flyByZone = 3;

        [Header("Range Control")]
        public float range = 300;
        public AnimationCurve damageRangeCurve = new AnimationCurve(new Keyframe[] { new Keyframe(0, 1), new Keyframe(1, 0.3f) });


        public Firearm source { get; set; }
        public Vector3 direction { get; set; }
        public Vector3 initialVelocity { get; set; }
        public float maxVelocity { get; protected set; }
        public bool isActive { get; set; } = true;

        private Explosive explosive;
        private ProximityScaler proximityScaler;

        public Vector3 startPosition;
        public Vector3 lastPosition;

        private readonly RaycastHit[] hitBuffer = new RaycastHit[8];
        private readonly RaycastHit[] flyByBuffer = new RaycastHit[8];
        public UnityEvent<GameObject, Ray, RaycastHit> onHit { get; set; } = new UnityEvent<GameObject, Ray, RaycastHit>();
        public UnityEvent<GameObject, Ray, RaycastHit> onFlyByObj { get; set; } = new UnityEvent<GameObject, Ray, RaycastHit>();

        public GameObject sourcePlayer { get; set; }

        /// <summary>
        /// Setup all this projectile's fields.
        /// </summary>
        /// <param name="source">Source firearm which this projectile will copy things from</param>
        /// <param name="direction">The direction of movement for this projectile</param>
        /// <param name="initialVelocity">The initial velocity for this projectile. By default this field is used for shooter velocity</param>
        /// <param name="speed">The maximum speed for this projectiles</param>
        /// <param name="range">The maximum distance from the initial firing location, if this is in half of the distance and max damage is 10, damage will be 5.</param>
        public void Setup(Firearm source, Vector3 direction, Vector3 rayPosition)
        {
            this.startPosition = rayPosition;
            lastPosition = rayPosition;

            float muzzleModifier = source?.firearmAttachmentsManager != null ? source.firearmAttachmentsManager.muzzleVelocity : 1;

            //Player and source firearm
            this.source = source;

            if (source && source.actor != null)
                this.sourcePlayer = source.actor.gameObject;

            //Direction and speed
            this.direction = direction;
            this.initialVelocity = source.velocity;
            this.range = source.preset.range * source.firearmAttachmentsManager.range;
            maxVelocity = source.preset.muzzleVelocity * source.firearmAttachmentsManager.muzzleVelocity;

            //Scale
            if (source)
            {
                if(source.preset.tracerRounds)
                {
                    proximityScaler = gameObject.GetOrAddComponent<ProximityScaler>();

                    proximityScaler.multipler = source.preset.projectileSize;

                    proximityScaler.ZeroAll();
                }
                else
                {
                    transform.localScale = Vector3.one * source.preset.projectileSize;
                }
            }

            skipFirstFrame = true;
        }

        protected virtual void Start()
        {
            explosive = GetComponent<Explosive>();

            if (explosive && source && source.actor != null) explosive.DamageSource = source.actor.gameObject;
        }

        public float CalculateDamage()
        {
            float distanceFromStartPos = Vector3.Distance(transform.position, startPosition);

            float countFactor =  source.preset.shotCount;

            if (source != null)
            {
                distanceFromStartPos = Mathf.Clamp(distanceFromStartPos, 1, float.MaxValue);

                float posToRange = distanceFromStartPos / range;

                posToRange = Mathf.Clamp01(posToRange);

                float damageToRange = damageRangeCurve.Evaluate(posToRange);

                float finalDamage = damageToRange * source.preset.damage / countFactor;
                return finalDamage;
            }

            Debug.LogError("Couldn't calculate damage due to null source firearm field. Damage will be default to 30.", gameObject);

            return 30;
        }

        private void Update()
        {
            Ray ray = new Ray(lastPosition, -(lastPosition - transform.position));
            float distance = Vector3.Distance(transform.position, lastPosition);

            int hitCount = Physics.SphereCastNonAlloc(ray, hitRadius, hitBuffer, distance, hittableLayers, QueryTriggerInteraction.Ignore);
            int flybyHitCount = Physics.SphereCastNonAlloc(ray, flyByZone, flyByBuffer, distance, flyByHittableLayers, QueryTriggerInteraction.Ignore);

            for (int i = 0; i < hitCount; i++)
            {
                if (hitBuffer[i].point != Vector3.zero && distance != 0)
                {
                    if (lastPosition != Vector3.zero)
                        UpdateHits(ray, hitBuffer[i]);
                }
            }

            for (int i = 0; i < hitCount; i++)
            {
                if (flyByBuffer[i].point != Vector3.zero && distance != 0)
                {
                    if (lastPosition != Vector3.zero)
                        UpdateFlyByHits(ray, flyByBuffer[i]);
                }
            }
        }

        private bool skipFirstFrame;

        private void LateUpdate()
        {
            if(skipFirstFrame)
            {
                skipFirstFrame = false;
                return;
            }

            lastPosition = transform.position;
        }

        protected virtual void UpdateFlyByHits(Ray ray, RaycastHit hit)
        {
            if (source == null) return;

            //stop if object has ignore component
            if (hit.transform == null) return;

            if (hit.transform.TryGetComponent(out Ignore _ignore) && _ignore.ignoreFirearmHits || sourcePlayer && hit.transform == sourcePlayer.transform) return;

            onFlyByObj.Invoke(hit.transform.gameObject, ray, hit);
            OnFlyByObject(hit);

            GameObject obj = hit.transform.gameObject;

            // Try to get the IOnAnyHit interface implementation on the hit object, its children, and its parent
            IOnProjectileFlyBy onFlyBy = obj.transform.GetComponent<IOnProjectileFlyBy>();
            IOnProjectileFlyByInChildren onFlyByInChildren = obj.transform.GetComponentInParent<IOnProjectileFlyByInChildren>();
            IOnProjectileFlyByInParent onFlyByInParent = obj.transform.GetComponentInChildren<IOnProjectileFlyByInParent>();

            if (onFlyBy != null) onFlyBy.OnProjectileFlyBy(this);
            if (onFlyByInChildren != null) onFlyByInChildren.OnProjectileFlyByInChildren(this);
            if (onFlyByInParent != null) onFlyByInParent.OnProjectileFlyByInParent(this);
        }

        protected virtual void OnFlyByObject(RaycastHit hit)
        {
            
        }

        protected virtual void UpdateHits(Ray ray, RaycastHit hit)
        {
            if (source == null) return;

            //stop if object has ignore component
            if (hit.transform == null) return;

            if (hit.transform.TryGetComponent(out Ignore _ignore) && _ignore.ignoreFirearmHits || sourcePlayer && hit.transform == sourcePlayer.transform) return;
            onHit?.Invoke(hit.transform.gameObject, ray, hit);
            OnHit(hit);

            if (!isActive) return;

            if (explosive)
            {
                explosive.Explode();
                Destroy(gameObject);
                return;
            }

            Firearm.UpdateHits(source.firearm, defaultDecal, ray, hit, CalculateDamage(), decalDirection);
        }

        public virtual void OnHit(RaycastHit hit) { }

        private void OnDrawGizmos()
        {
            Gizmos.color = Color.red;

            Gizmos.DrawWireSphere(transform.position, hitRadius);
        }

        [ContextMenu("Setup/Network Components")]
        public void Convert()
        {
            FPSFrameworkCore.InvokeConvertMethod("ConvertProjectile", this, new object[] { this });
        }
    }
}