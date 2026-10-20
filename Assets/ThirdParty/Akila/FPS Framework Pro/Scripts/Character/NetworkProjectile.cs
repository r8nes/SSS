using Akila.FPSFramework;
using System;
using System.Collections;
using UnityEngine;

#if MIRROR
using Mirror;
#endif

namespace Akila.FPSFrameworkPro
{
    /// <summary>
    /// Handles networked projectile behavior.
    /// Synchronizes projectile state and hit detection across clients and server.
    /// </summary>
    [RequireComponent(typeof(Projectile))]
#if MIRROR
    public class NetworkProjectile : NetworkBehaviour
#else
    public class NetworkProjectile : MonoBehaviour
#endif
    {
#if MIRROR
        public bool friendlyFire = false;

        /// <summary>
        /// Reference to the local Projectile component.
        /// </summary>
        private Projectile targetProjectile;

        private ProximityScaler proximityScaler;

        /// <summary>
        /// The player GameObject that fired this projectile.
        /// Synchronized across network.
        /// </summary>
        [SyncVar, HideInInspector]
        public NetworkIdentity sourcePlayer;

        /// <summary>
        /// The direction vector of the projectile's initial force.
        /// Synchronized across network.
        /// </summary>
        [SyncVar, HideInInspector]
        public Vector3 velocity;

        [SyncVar, HideInInspector]
        public LayerMask hittableLayers;

        [SyncVar, HideInInspector]
        public Vector3 startPosition;

        private void Awake()
        {
            targetProjectile = GetComponent<Projectile>();
        }

        /// <summary>
        /// Initialization logic, executed once on Start.
        /// Sets up projectile, applies force, and schedules destruction.
        /// </summary>
        private void Start()
        {
            // Initially disable projectile active state
            targetProjectile.isActive = false;

            targetProjectile.hittableLayers = hittableLayers;

            // Subscribe to hit event to handle impacts
            targetProjectile.onHit.AddListener(DoHit);

            // Apply initial impulse force for projectile movement

            // Access player's networked item manager to configure projectile properties
            NetworkItemsManager networkItemsManager = sourcePlayer.transform.SearchFor<NetworkItemsManager>();

            if (networkItemsManager.firearm.TargetFirearm.preset.tracerRounds)
            {
                proximityScaler = gameObject.GetOrAddComponent<ProximityScaler>();

                proximityScaler.multipler = networkItemsManager.firearm.TargetFirearm.preset.projectileSize;
            }
            else
            {
                transform.localScale = Vector3.one * networkItemsManager.firearm.TargetFirearm.preset.projectileSize;
            }

            targetProjectile.source = networkItemsManager.firearm.TargetFirearm;

            // Schedule destruction after lifetime expiration on server only
            if (isServer)
            {
                StartCoroutine(DestroySelf());
            }

            // Start scaled down for a spawn effect (scale and trail)
            transform.localScale = Vector3.zero;
            GetComponentInChildren<TrailRenderer>().widthMultiplier = 0;
        }

        /// <summary>
        /// Called when projectile hits an object.
        /// Handles decal spawning, ignores certain hits, and processes damage.
        /// </summary>
        /// <param name="obj">Hit object GameObject.</param>
        /// <param name="ray">Raycast used for hit detection.</param>
        /// <param name="hit">RaycastHit information.</param>
        protected virtual void DoHit(GameObject obj, Ray ray, RaycastHit hit)
        {
            if (hit.point == Vector3.zero)
                return;

            // Ignore hits on objects with Ignore component enabled for hit detection
            if (hit.transform.TryGetComponent(out Ignore _ignore) && _ignore.ignoreFirearmHits)
            {
                return;
            }

            Transform playerTransform = sourcePlayer ? sourcePlayer.transform : transform;

            Transform hitTramsform = hit.transform;

            if (playerTransform.FindDeepChild(hitTramsform.name) != null && !sourcePlayer.isLocalPlayer)
            {
                return;
            }

            NetworkActor networkActor = hit.transform.SearchFor<NetworkActor>();

            if (networkActor != null)
            {
                NetworkActor actorOnSource = sourcePlayer.SearchFor<NetworkActor>();

                if (actorOnSource && friendlyFire == false)
                {
                    if (actorOnSource.teamID == networkActor.teamID)
                    {
                        return;
                    }
                }
            }


            // Check for custom decal component and override default decal if present
            if (hit.transform.TryGetComponent(out CustomDecal customDecal))
            {
                targetProjectile.defaultDecal = customDecal.decalVFX;
            }

            // Ignore hitting the shooter's own character manager
            if (sourcePlayer == null || sourcePlayer.transform.SearchFor<NetworkItemsManager>().firearm.TargetFirearm?.characterManager?.transform == hit.transform)
            {
                return;
            }

            NetworkIdentity networkIdentity = obj.transform.SearchFor<NetworkIdentity>();

            if (networkIdentity == null)
            {

            }

            // Trigger global firearm hit callbacks (e.g., sounds, effects)
            Firearm.InvokeHitCallbacks(obj, ray, hit, targetProjectile.source.preset.impactForce);

            // Spawn decal at hit location if defined
            if (targetProjectile.defaultDecal != null)
            {
                Vector3 hitPoint = hit.point;
                Quaternion decalRotation = FPSFrameworkCore.GetFromToRotation(hit, targetProjectile.decalDirection);
                GameObject decalInstance = Instantiate(targetProjectile.defaultDecal, hitPoint, decalRotation);

                if (customDecal && customDecal.parent || customDecal == null)
                {
                    decalInstance.transform.localScale *= sourcePlayer.transform.SearchFor<NetworkItemsManager>().firearm.TargetFirearm.preset.decalSize;
                    decalInstance.transform.SetParent(hit.transform);
                }

                float decalLifetime = customDecal?.lifeTime ?? 60f;
                Destroy(decalInstance, decalLifetime);
            }

            // Handle damage application if hit object has NetworkIdentity

            if (networkIdentity != null)
            {
                float damage = targetProjectile.CalculateDamage();

                IDamageable damageable = obj.transform.SearchFor<IDamageable>();

                IDamageablePart damageablePart = obj.GetComponent<IDamageablePart>();

                if (damageable != null)
                {
                    if (!damageable.gameObject.TryGetComponent<NetworkDamageable>(out NetworkDamageable netDamageable))
                    {
                        Debug.LogError($"A networked projectile has hit a non-networked IDamageable. Check for network components and NetworkIdentity on {damageable.gameObject.name}");
                    }
                }

                // Modify damage based on damageable group multipliers
                if (damageablePart != null)
                {
                    damage = damageablePart.damageMultipler * damage;
                }


                // Inform server of the hit and damage to apply
                CmdHit(networkIdentity.gameObject, damage);
            }
            else
            {
                bool foundDamageable = obj.TryGetComponent<IDamageable>(out IDamageable damageable);
                bool foundPart = obj.TryGetComponent<IDamageablePart>(out IDamageablePart part);

                if (foundDamageable && damageable != null)
                {
                    Debug.LogError($"A networked projectile has hit a non-networked IDamageable. Check for network components and NetworkIdentity on {damageable.gameObject.name}");
                }

                if (foundPart && part != null)
                {
                    Debug.LogError($"A networked projectile has hit a non-networked IDamageablePart. Check for network components and NetworkIdentity on {part.gameObject.name}");
                }
            }

            IDamageable targetDamageable = obj.transform.SearchFor<IDamageable>();

            if (targetDamageable != null && targetDamageable.Health > 0)
            {
                UIManager.Instance?.Hitmarker?.Show(targetDamageable.Health <= 1);
            }
        }

        /// <summary>
        /// Command sent to the server to apply damage when a projectile hits an object.
        /// </summary>
        /// <param name="obj">The hit object GameObject.</param>
        /// <param name="damage">The calculated damage to apply.</param>
        [Command(requiresAuthority = false)]
        protected virtual void CmdHit(GameObject obj, float damage)
        {
            try
            {
                if (connectionToClient == null) return;

                if (obj == null) return;

                // Prevent applying damage if the client owns the hit target
                if (connectionToClient == obj.GetComponent<NetworkIdentity>().connectionToClient)
                {
                    return;
                }

                NetworkDamageable networkDamageable = obj.transform.SearchFor<NetworkDamageable>();

                if (networkDamageable) networkDamageable.damagingPlayer = sourcePlayer;

                networkDamageable?.Damage(damage);
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }
        }

        /// <summary>
        /// Destroys the projectile object on the server.
        /// </summary>
        [Server]
        private IEnumerator DestroySelf()
        {
            yield return new WaitForSeconds(targetProjectile.lifeTime);
            NetworkServer.Destroy(gameObject);
        }
#endif
    }
}
