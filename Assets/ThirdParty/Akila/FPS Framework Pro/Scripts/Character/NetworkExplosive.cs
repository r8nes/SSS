using Akila.FPSFramework;
using UnityEngine;
using System;

#if MIRROR
using Mirror;
#endif
namespace Akila.FPSFrameworkPro
{
    /// <summary>
    /// Networked wrapper for an explosive object that synchronizes explosion events and applies damage/forces over the network.
    /// </summary>
    [RequireComponent(typeof(Explosive))]
#if MIRROR
    public class NetworkExplosive : NetworkBehaviour
#else
    public class NetworkExplosive : MonoBehaviour
#endif
    {
        #if MIRROR
        /// <summary>
        /// Reference to the local Explosive component.
        /// </summary>
        public Explosive TargetExplosive { get; private set; }

        [HideInInspector, SyncVar]
        public NetworkIdentity sourcePlayer;

        private float maxHealth;

        private void Start()
        {
            TargetExplosive = GetComponent<Explosive>();

            if (TargetExplosive == null)
            {
                Debug.LogError($"[{nameof(NetworkExplosive)}] Explosive component not found on {gameObject.name}.", gameObject);
                enabled = false;
                return;
            }

            maxHealth = TargetExplosive.health;

            TargetExplosive.isActive = false;

            // Subscribe to the explosion event, sending command to server on explosion
            TargetExplosive.onExplode.AddListener(CmdExplode);

            // Subscribe to explosion application event to apply explosion effects on networked objects
            TargetExplosive.onExplosionApplied += ApplyExplosion;
        }

        /// <summary>
        /// Applies explosion effects to the specified object over the network.
        /// </summary>
        /// <param name="obj">Transform of the object affected by the explosion.</param>
        /// <param name="origin">Position of the explosion origin.</param>
        /// <param name="dir">Direction of the explosion force.</param>
        /// <param name="kill">If true, applies maximum damage.</param>
        private void ApplyExplosion(Transform obj, Vector3 origin, Vector3 dir, bool kill = false)
        {
            if (obj == null)
            {
                Debug.LogError($"[{nameof(NetworkExplosive)}] ApplyExplosion called with null target object.");
                return;
            }

            NetworkIdentity identity = obj.SearchFor<NetworkIdentity>();

            if (identity == null)
            {
                // Object is not networked, ignore
                return;
            }

            CmdApplyExplosion(identity.transform, origin, dir, kill);
        }

        /// <summary>
        /// Server-side command to apply explosion damage and forces to a networked object.
        /// </summary>
        /// <param name="obj">Transform of the affected object.</param>
        /// <param name="origin">Explosion origin point.</param>
        /// <param name="dir">Explosion force direction.</param>
        /// <param name="kill">Whether to apply lethal damage.</param>
        [Command(requiresAuthority = false)]
        private void CmdApplyExplosion(Transform obj, Vector3 origin, Vector3 dir, bool kill)
        {
            try
            {
                if (obj == null)
                {
                    return;
                }

                // Prevent infinite recursion by ignoring damage to self
                if (obj == transform)
                {
                    return;
                }

                float finalDamage = TargetExplosive.maxDamage;

                // Attempt to find any damageable component on the object or its children/parents
                IDamageable damageable = obj.SearchFor<IDamageable>();

                if (damageable != null)
                {
                    NetworkDamageable netDamageable = obj.GetComponent<NetworkDamageable>();
                    if (netDamageable == null)
                    {
                        Debug.LogError($"[{nameof(NetworkExplosive)}] Object '{obj.name}' has IDamageable but no NetworkDamageable component.");
                        return;
                    }

                    float distanceFromOrigin = Vector3.Distance(origin, obj.position);
                    float damageMultiplier = Mathf.Clamp01(1f - (distanceFromOrigin / TargetExplosive.damageZone));

                    finalDamage *= damageMultiplier;

                    if (kill)
                        finalDamage = maxHealth;

                    netDamageable.damagingPlayer = sourcePlayer;

                    netDamageable.Damage(finalDamage);
                }

                // Apply physics explosion force if Rigidbody exists
                if (obj.TryGetComponent<Rigidbody>(out Rigidbody rb))
                {
                    rb.AddExplosionForce(TargetExplosive.force, transform.position, TargetExplosive.damageZone, 1f, ForceMode.VelocityChange);
                }
            }
            catch (Exception e)
            {
                Debug.LogException(e, gameObject);
            }
        }

        /// <summary>
        /// Command to trigger the explosion on the server.
        /// </summary>
        [Command(requiresAuthority = false)]
        private void CmdExplode()
        {
            RpcExplode();

            // Destroy the explosive object shortly after explosion to reduce errors
            Invoke(nameof(Clear), Time.fixedDeltaTime);
        }

        /// <summary>
        /// ClientRpc that triggers explosion effects on all clients.
        /// </summary>
        [ClientRpc]
        private void RpcExplode()
        {
            TargetExplosive = GetComponent<Explosive>();

            if (TargetExplosive == null)
            {
                Debug.LogWarning($"[{nameof(NetworkExplosive)}] RpcExplode called but TargetExplosive is null.");
                return;
            }

            // Add explosion visual/audio effects locally on each client
            TargetExplosive.AddEffects();
        }

        /// <summary>
        /// Removes the explosive object from the network.
        /// </summary>
        private void Clear()
        {
            if (NetworkServer.active)
            {
                NetworkServer.Destroy(gameObject);
            }
        }
#endif
    }
}
