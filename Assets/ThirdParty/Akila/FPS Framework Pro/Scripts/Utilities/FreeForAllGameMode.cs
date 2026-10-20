using Akila.FPSFramework;
using UnityEngine;

namespace Akila.FPSFrameworkPro
{
    [RequireComponent(typeof(GameModer))]
    public class FreeForAllGameMode : MonoBehaviour
    {
#if MIRROR
        public Actor GetTopKillsPlayer()
        {
            IActor[] players = GameModer.Instance.RefreshPlayers();

            if (players == null || players.Length == 0)
                return null;

            Actor topPlayer = players[0] as Actor;
            if (topPlayer == null)
                return null;

            int maxKills = topPlayer.kills;
            int minKills = topPlayer.kills;

            for (int i = 1; i < players.Length; i++)
            {
                if (players[i] is Actor actor)
                {
                    if (actor.kills > maxKills)
                    {
                        maxKills = actor.kills;
                        topPlayer = actor;
                    }

                    if (actor.kills < minKills)
                        minKills = actor.kills;
                }
            }

            // Only return null if multiple players exist and all have equal kills
            if (players.Length > 1 && maxKills == minKills)
                return null;

            return topPlayer;
        }
#endif
    }
}