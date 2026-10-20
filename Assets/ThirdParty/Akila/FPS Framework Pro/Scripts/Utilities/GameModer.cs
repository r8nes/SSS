using Akila.FPSFramework;
using System.Linq;
using UnityEngine;

#if MIRROR
using Mirror;
#endif

namespace Akila.FPSFrameworkPro
{
#if MIRROR
    public class GameModer : NetworkBehaviour
#else
    public class GameModer : MonoBehaviour
    #endif
    {
#if MIRROR
        [Header("Basic Modes")]
        public float startDelay = 5;
        public float matchLength = 5;

        //TODO: Finish team based logic
        //public bool teamBased;

        public bool lockInputOnEnd = true;


        [Header("States")]
        [Akila.FPSFramework.ReadOnly]
        public bool isLocalPlayerWinning;
        [Akila.FPSFramework.ReadOnly, SyncVar]
        public float timeLeftInSeconds;
        [Akila.FPSFramework.ReadOnly, SyncVar]
        public GameState gameState;

        public static GameModer Instance;

        public Actor localPlayer { get; set; }
        public Actor winningPlayer { get; set; }
        public string localPlayerName { get; private set; }
        public string winningPlayerName { get; private set; }

        public float timeUntilStart { get; private set; }

        private Actor[] players;

        private void Awake()
        {
            if (Instance == null)
                Instance = this;
            else
                Destroy(gameObject);
        }

        private void Start()
        {
            timeLeftInSeconds = matchLength * 60;
        }

        private void Update()
        {
            if (winningPlayer != null)
            {
                winningPlayerName = winningPlayer.actorName;
            }

            if (localPlayer != null)
            {
                localPlayerName = localPlayer.actorName;

                if (players != null && players.Length > 0)
                {
                    int maxKills = players.Max(p => p.kills);
                    int topCount = players.Count(p => p.kills == maxKills);
                    isLocalPlayerWinning = localPlayer.kills == maxKills && topCount == 1;

                    winningPlayer = players.OrderByDescending(p => p.kills).First();
                }
                else
                {
                    isLocalPlayerWinning = false;
                }
            }
            else
            {
                isLocalPlayerWinning = false;
            }

            //Only manage the game on the server side.
            if (!isServer) return;

            if (gameState == GameState.Ended && lockInputOnEnd)
            {
                RpcDisableInput();
            }

            //Start game if there's more than one player in the session.
            //The check for gameHasStarted is to not reset game when players leave leaving a single player again.
            if (gameState != GameState.Starting)
            {
                //TODO: Enable this when team based is done
                /*
                if (teamBased)
                {
                    if (GetTeamsCount() >= 2)
                        gameState = GameState.InProgress;
                }
                else
                {
                    if (GetPlayersCount() >= 2)
                        gameState = GameState.InProgress;
                }
                */

                if (GetPlayersCount() >= 2)
                {
                    gameState = GameState.Starting;
                }
            }

            if (gameState == GameState.Starting)
            {
                timeUntilStart += Time.deltaTime;
            }

            if (gameState == GameState.Starting && timeUntilStart >= startDelay)
            {
                gameState = GameState.InProgress;
            }

            //Do count down if game has started is true
            if (gameState == GameState.InProgress && gameState != GameState.Ended)
            {
                timeLeftInSeconds -= Time.deltaTime;
            }

            if (timeLeftInSeconds <= 0) gameState = GameState.Ended;

            timeLeftInSeconds = Mathf.Clamp(timeLeftInSeconds, 0, float.MaxValue);
        }

        [ClientRpc]
        private void RpcDisableInput()
        {
            FPSFrameworkCore.IsInputActive = false;
        }

        /// <summary>
        /// Updates the cashed players array in GameModer class.
        /// </summary>
        /// <returns></returns>
        public Actor[] RefreshPlayers()
        {
            return players = Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
                    .OfType<Actor>()
                    .ToArray(); ;
        }

        /// <summary>
        /// Returns the count of the cashed players list.
        /// </summary>
        /// <returns></returns>
        public int GetPlayersCount()
        {
            if (players == null) return 0;

            return players.Length;
        }

        public enum GameState
        {
            WaitingForPlayers,
            Starting,
            InProgress,
            Ended
        }
#endif
    }
}