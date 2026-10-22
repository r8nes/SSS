using Mirror;
using UnityEngine;

namespace ShooterShop.Days
{
    public sealed class LocalPlayerSpawner : ILocalPlayerSpawner
    {
        public void ReturnToSpawn()
        {
            var player = NetworkClient.localPlayer;
            var spawnPoints = NetworkManager.startPositions;

            if (player == null || spawnPoints.Count == 0)
                return;

            var spawn = spawnPoints[(int)(player.netId % (uint)spawnPoints.Count)];

            if (spawn == null)
                return;

            var controller = player.GetComponent<CharacterController>();
            var wasEnabled = controller != null && controller.enabled;

            if (controller != null)
                controller.enabled = false;

            player.transform.SetPositionAndRotation(spawn.position, spawn.rotation);
            Physics.SyncTransforms();

            if (controller != null)
                controller.enabled = wasEnabled;
        }
    }
}
