using Akila.FPSFramework;
using UnityEngine;
using System;

#if MIRROR
using Mirror;
#endif

namespace Akila.FPSFrameworkPro
{
    /// <summary>
    /// Network-aware wrapper for an object implementing <see cref="IDamageable"/>.
    /// Handles health syncing, remote damage, and death behavior across clients and server.
    /// </summary>
#if MIRROR
    public class NetworkDamageable : NetworkBehaviour
#else
    public class NetworkDamageable : MonoBehaviour
#endif
    {
#if MIRROR
        /// <summary>
        /// The network-synchronized health value.
        /// </summary>
        [SyncVar(hook = nameof(OnHealthChange)), HideInInspector]
        public float health;

        [SyncVar(hook = nameof(SetDamageSource)), HideInInspector]
        public NetworkIdentity damagingPlayer;

        protected virtual void OnHealthChange(float oldValue, float newValue)
        {
            if(oldValue >  newValue)
            {
                if (TargetDamageable == null)
                    TargetDamageable = transform.SearchFor<IDamageable>();

                if(TargetDamageable == null)
                {
                    Debug.LogError("TargetDamageable is not set.", gameObject);

                    return;
                }

                if(newValue <= 0)
                {
                    //TargetDamageable.OnDeath?.Invoke();

                    TargetDamageable.DeadConfirmed = true;
                }
            }
        }

        protected virtual void SetDamageSource(NetworkIdentity oldValue, NetworkIdentity newValue)
        {
            if (newValue == null) return;

            TargetDamageable.DamageSource = newValue.gameObject;
        }

        /// <summary>
        /// Local reference to the damageable interface.
        /// </summary>
        public IDamageable TargetDamageable { get; private set; }

        private bool isPlayer;

        private void Start()
        {
            TargetDamageable = GetComponent<IDamageable>();
            damageable = GetComponent<Damageable>();

            if (TargetDamageable == null)
            {
                Debug.LogError($"[{nameof(NetworkDamageable)}] Missing IDamageable implementation on '{gameObject.name}'.", gameObject);
                enabled = false;
                return;
            }

            // Ensure local handling is disabled; logic now runs via network
            TargetDamageable.isDamagableDisabled = true;
            TargetDamageable.allowDamageableEffects = isLocalPlayer;

            TargetDamageable.OnDeath.AddListener(OnDeath);

            if (isServer)
                health = TargetDamageable.Health;
        }

        Damageable damageable;

        private void Update()
        {
            if (TargetDamageable == null)
                return;

            if (damageable != null)
            {
                if (damageable.isTryingToHeal && isServer)
                {
                    health += Time.deltaTime * damageable.regenerationRate;
                }
            }

            // Sync health and source to the local damageable object
            TargetDamageable.Health = health;
        }

        /// <summary>
        /// Called when this actor dies on the local client.
        /// </summary>
        private void OnDeath()
        {
            if (TargetDamageable == null)
                return;

            if(TargetDamageable.GetType() == typeof(Damageable))
            {
                Damageable damageable = (Damageable) TargetDamageable;

                if (damageable.deathEffect)
                {
                    if (damageable.deathEffect.TryGetComponent<NetworkIdentity>(out NetworkIdentity id))
                    {
                        if (isServer)
                        {
                            GameObject deathEffectNetworked = Instantiate(damageable.deathEffect, transform.position, transform.rotation);

                            NetworkServer.Spawn(deathEffectNetworked);
                        }
                    }
                    else
                    {
                        GameObject deathEffectNetworked = Instantiate(damageable.deathEffect, transform.position, transform.rotation);
                    }
                }
            }

            if (TargetDamageable.transform.SearchFor<CharacterManager>() != null && !isLocalPlayer)
                return;

            if (TargetDamageable == null)
            {
                Debug.LogWarning($"[{nameof(NetworkDamageable)}] OnDeath called but TargetDamageable is null.");
                return;
            }

            if (TargetDamageable.transform.SearchFor<ICharacterController>() != null)
            {
                Vector3 damagePos = transform.position;

                DeathCamera.Instance?.Enable(gameObject, damagePos);
            }

            isPlayer = TargetDamageable.transform.SearchFor<ICharacterController>() != null;


            if (isPlayer)
                CmdOnDeathWithAuthoity();
            else
                CmdOnDeathWithoutAuthoirty();

            TargetDamageable.DeadConfirmed = true;
        }

        [Command(requiresAuthority = false)]
        protected virtual void CmdOnDeathWithoutAuthoirty()
        {
            CmdOnDeath();
        }

        [Command]
        protected virtual void CmdOnDeathWithAuthoity()
        {
            CmdOnDeath();
        }

        protected virtual void CmdOnDeath()
        {
            try
            {

                Actor actor = GetComponent<Actor>();

                if (actor != null)
                {
                    NetworkActor networkActor = actor.GetComponent<NetworkActor>();

                    if (actor == null)
                    {
                        Debug.LogError($"[{nameof(NetworkDamageable)}] No Actor found on '{gameObject.name}' for respawning.");
                    }

                    if (networkActor == null)
                    {
                        Debug.LogError("Actor is not a network object. Make sure to attach NetworkActor to player prefab", gameObject);
                    }

                }
                
                RpcOnDeath();
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        [ClientRpc]
        private void RpcOnDeath()
        {
            if (isServer)
            {
                HandleServerDeathLogic();
            }

            if (isClient)
                HandleClientDeathLogic();

            // Always shared logic (client and server)
            HandleSharedDeathVisuals();

            Actor actor = transform.SearchFor<Actor>();

            if (damagingPlayer && actor != null)
                RpcShowKillFeed(damagingPlayer, actor.actorName);
        }

        private void HandleSharedDeathVisuals()
        {
            // Enable ragdoll if it exists
            transform.SearchFor<Ragdoll>()?.Enable();

            // Unparent camera orientation to keep view alive briefly
            if (TryGetComponent(out FirstPersonController fpsController))
            {
                fpsController.Orientation.SetParent(null);
            }
        }

        private void HandleServerDeathLogic()
        {
            Actor actor = transform.SearchFor<Actor>();

            if (actor != null)
            {
                var networkActor = actor.GetComponent<NetworkActor>();
                if (networkActor == null)
                {
                    Debug.LogError($"[{nameof(NetworkDamageable)}] Missing NetworkActor on {actor.actorName}.", gameObject);
                    return;
                }

                // Add death to victim
                networkActor.deaths++;
                networkActor.networkData.deaths = networkActor.deaths;

                // Killer handling
                if (damagingPlayer)
                {
                    var killer = damagingPlayer.GetComponent<Actor>();
                    var killerNetworkActor = killer?.GetComponent<NetworkActor>();

                    if (killerNetworkActor != null)
                    {
                        killerNetworkActor.kills++;
                        killerNetworkActor.networkData.kills = killerNetworkActor.kills;
                    }
                    else
                    {
                        Debug.LogError($"[{nameof(NetworkDamageable)}] Killer is missing NetworkActor.", gameObject);
                    }
                }

                // Respawn
                var spawnManager = FindAnyObjectByType<NetworkSpawnManager>();
                if (spawnManager == null)
                {
                    Debug.LogError($"[{nameof(NetworkDamageable)}] NetworkSpawnManager not found in scene.");
                    return;
                }

                networkActor.networkData.Update(networkActor.networkData);

                if (connectionToClient != null)
                    spawnManager.SpawnNetworkActor(networkActor.networkData, connectionToClient, 2);

            }

            if (TargetDamageable is Damageable damageable)
            {
                if (damageable.destroyOnDeath)
                {
                    Invoke(nameof(DestroySelf), damageable.destroyDelay);
                }
            }
            else if (TargetDamageable is Explosive explosive)
            {
                if (explosive.destroyOnExplode)
                {
                    Invoke(nameof(DestroySelf), explosive.clearDelay);
                }
            }
            else
            {
                Invoke(nameof(DestroySelf), customDeathDelay);
            }
        }

        /// <summary> 
        /// Delay before destroying this NetworkIdentity when the associated IDamageable 
        /// is not of type <see cref="Damageable"/>. 
        /// </summary> 
        /// <remarks> 
        /// This is used for custom implementations of <see cref="IDamageable"/> that do not 
        /// expose a <c>destroyOnDeath</c> flag. In such cases, destruction cannot be determined 
        /// directly, so a fallback timed destruction is applied. 
        /// 
        /// Set this value to control how long the object persists on the server after "death". 
        /// If set to 0 or a negative value, destruction should occur immediately. 
        /// </remarks> 
        /// <value> 
        /// Time in seconds before invoking destruction. 
        ///</value>
        public float customDeathDelay { get; set; } = float.MaxValue;

        [Server]
        private void DestroySelf()
        {
            NetworkServer.Destroy(netIdentity.gameObject);
        }

        private void HandleClientDeathLogic()
        {
            var actor = transform.SearchFor<Actor>();

            if (actor != null)
            {
                if (isLocalPlayer)
                {
                    if (actor.Damageable == null)
                    {
                        Debug.LogError($"[{nameof(NetworkDamageable)}] Damageable is null for {actor.actorName}.", gameObject);
                        return;
                    }

                    actor.Damageable.DeadConfirmed = true;
                    actor.OnDeathConfirmed?.Invoke();

                    if (actor.playerCard && actor.playerCardActive && actor.playerUIEnabled)
                        actor.playerCard.Disable(actor);

                    if (TryGetComponent<NetworkFirstPersonController>(out NetworkFirstPersonController networkFirstPersonController))
                    {
                        if (networkFirstPersonController.playerMeshes != null)
                        {
                            networkFirstPersonController.ToggleMeshes(true);
                            networkFirstPersonController.ToggleColliders(true);
                        }
                    }
                }
            }
        }

        private void RpcShowKillFeed(NetworkIdentity killer, string victimName)
        {
            if (killer == null) return;

            // Only show the kill feed on the local player
            if (killer == null || !killer.isLocalPlayer) return;

            UIManager uiManager = UIManager.Instance;
            if (uiManager == null) return;

            KillFeed killFeed = uiManager.KillFeed;
            if (killFeed == null) return;

            Actor victimActor = transform.SearchFor<Actor>();
            Actor killerActor = killer.GetComponent<Actor>();
            if (victimActor == null || killerActor == null) return;

            NetworkActor networkActor = killer.GetComponent<NetworkActor>();
            if (networkActor == null) return;

            // Added one kill since kills update later on the server
            int displayKills = networkActor.kills;

            if (!isServer)
                displayKills++;

            killFeed.Show(killerActor.actorName, displayKills, victimName);
        }


        /// <summary>
        /// Inflicts damage to this entity from a specified source.
        /// </summary>
        /// <param name="damage">Amount of damage to apply.</param>
        /// <param name="sourcePlayer">The GameObject that caused the damage.</param>
        public void Damage(float damage)
        {
            if(damageable)
            {
                damageable.autoHealDelayTime = damageable.autoHealDelay;
            }

            if (damage <= 0f)
            {
                Debug.LogWarning($"[{nameof(NetworkDamageable)}] Attempted to apply non-positive damage: {damage}");
                return;
            }

            CmdDamage(damage);
        }

        /// <summary>
        /// Server-side method to apply damage and update sync vars.
        /// </summary>
        /// <param name="damage">Amount of damage to apply.</param>
        /// <param name="sourcePlayer">Entity that dealt the damage.</param>
        [Command(requiresAuthority = false)]
        private void CmdDamage(float damage)
        {
            health -= damage;

            // Clamp health to zero to avoid negative values
            if (health < 0f)
                health = 0f;
        }
#endif
    }
}
