using Akila.FPSFramework;
using System.Collections;
using UnityEngine;

#if MIRROR
using Mirror;
#endif

namespace Akila.FPSFrameworkPro
{
    /// <summary>
    /// Handles spawning and respawning of networked player actors.
    /// Supports delayed spawning using async/await.
    /// </summary>
    [RequireComponent(typeof(SpawnManager))]
#if MIRROR
    public class NetworkSpawnManager : NetworkBehaviour
#else
    public class NetworkSpawnManager : MonoBehaviour
#endif
    {
        #if MIRROR
        public SpawnManager TargetSpawnManager
        {
            get
            {
                if(sp == null)
                    sp = GetComponent<SpawnManager>();

                return sp;
            }
        }

        private SpawnManager sp;

        /// <summary>
        /// Respawns the player for the given connection on the server.
        /// Destroys the old player GameObject if it exists and instantiates a new one after delay.
        /// </summary>
        /// <param name="actorData">The Actor whose stats will be transferred to the new player instance.</param>
        /// <param name="conn">The NetworkConnection of the client to respawn for.</param>
        public virtual void SpawnNetworkActor(ActorData actorData, NetworkConnectionToClient conn, float delay)
        {
            StartCoroutine(SpawnNetworkActorCore(actorData, conn, delay));
        }

        protected virtual IEnumerator SpawnNetworkActorCore(ActorData actorData, NetworkConnectionToClient conn, float delay)
        {
            if (delay > 0)
            {
                yield return new WaitForSeconds(delay);
            }

            SpawnNetworkActorImmediate(actorData, conn);
        }

        /// <summary>
        /// Respawns the player for the given connection on the server.
        /// Destroys the old player GameObject if it exists and instantiates a new one.
        /// </summary>
        /// <param name="actorData">The Actor whose stats will be transferred to the new player instance.</param>
        /// <param name="conn">The NetworkConnection of the client to respawn for.</param>
        [Server]
        public virtual void SpawnNetworkActorImmediate(ActorData actorData, NetworkConnectionToClient conn)
        {
            // Destroy old player GameObject if it exists for this connection
            if (conn.identity != null)
            {
                NetworkServer.Destroy(conn.identity.gameObject);
            }

            // Get spawn position and rotation from NetworkManager's start positions
            Transform spawnPoint = NetworkManager.singleton.GetStartPosition();

            // Instantiate new player prefab at spawn point
            GameObject newPlayer = Instantiate(NetworkManager.singleton.playerPrefab, spawnPoint.position, spawnPoint.rotation);

            // Add the player object to the network for this connection
            NetworkServer.AddPlayerForConnection(conn, newPlayer);

            // Transfer kills and deaths stats from old actor to the new one
            NetworkActor newNetworkActor = newPlayer.GetComponent<NetworkActor>();

            newNetworkActor.kills = actorData.kills;
            newNetworkActor.deaths = actorData.deaths;
            newNetworkActor.teamID = actorData.teamID;
        }
#endif
    }
}