using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Akila.FPSFramework;
using UnityEngine.UI;
using TMPro;

#if MIRROR
using Mirror;
#endif

namespace Akila.FPSFrameworkPro
{
    /// <summary>
    /// Simple deathmatch game mode manager handling timer, player stats, and UI updates.
    /// </summary>
#if MIRROR
    public class SimpleDeathMatch : NetworkBehaviour
#else
    public class SimpleDeathMatch : MonoBehaviour
#endif
    {
#if MIRROR
        [Header("UI References")]
        [Tooltip("Background image of the end game screen.")]
        public Image endGameScreenBackground;

        [Tooltip("Text showing the match timer.")]
        public TextMeshProUGUI timerText;

        [Tooltip("Text showing the current game state (Victory, Lost, Draw).")]
        public TextMeshProUGUI gameStatesText;

        [Tooltip("Text showing the winning player's name or result message.")]
        public TextMeshProUGUI winningPlayerText;

        [Header("Colors")]
        [Tooltip("Background color for winning.")]
        public Color winColor;

        public Color drawColor;

        [Tooltip("Background color for losing or draw.")]
        public Color loseColor;

        [Header("Match Settings")]
        [Tooltip("Match length in minutes.")]
        public float matchLengthMinutes = 5f;

        [SyncVar]
        private float matchLengthSeconds;

        private readonly List<Dashboard.PlayerStats> players = new();
        private IActor[] actors;

        public Actor winningPlayer;
        private string winningPlayerName;

        private CanvasGroup endGameCanvasGroup;

        private void Start()
        {
            matchLengthSeconds = matchLengthMinutes * 60f;

            // Cache CanvasGroup for fade-in effect
            endGameCanvasGroup = endGameScreenBackground.GetComponent<CanvasGroup>();
            if (endGameCanvasGroup == null)
            {
                Debug.LogWarning("CanvasGroup component missing on EndGameScreenBackground. Adding one.");
                endGameCanvasGroup = endGameScreenBackground.gameObject.AddComponent<CanvasGroup>();
            }

            endGameCanvasGroup.alpha = 0f;
        }

        private void Update()
        {
            UpdateTimerUI();

            if (!isServer) return;

            UpdateMatchTimer();

            if (matchLengthSeconds <= 0)
            {
                EndMatch();
            }
        }

        /// <summary>
        /// Updates the match timer countdown on the UI.
        /// </summary>
        private void UpdateTimerUI()
        {
            int minutes = Mathf.FloorToInt(matchLengthSeconds / 60f);
            int seconds = Mathf.FloorToInt(matchLengthSeconds % 60f);
            timerText.text = $"{minutes:00}:{seconds:00}";
        }

        [Server]
        private void UpdateMatchTimer()
        {
            matchLengthSeconds -= Time.deltaTime;
            matchLengthSeconds = Mathf.Clamp(matchLengthSeconds, 0f, matchLengthMinutes * 60f);
        }

        [ServerCallback]
        private void EndMatch()
        {
            // Determine winning player first
            UpdateWinningPlayer();

            // Tell all clients to stop input
            RpcDisableInput();

            // Determine if local player is winner (will be checked on each client)
            bool isLocalPlayerWinner = winningPlayer != null &&
                                       winningPlayer.gameObject.GetComponent<NetworkIdentity>().isLocalPlayer;

            // Show end game UI on clients
            RpcShowEndGameScreen(isLocalPlayerWinner, winningPlayerName);
        }

        [ClientRpc]
        private void RpcDisableInput()
        {
            FPSFrameworkCore.IsInputActive = false;
        }


        [ClientRpc]
        private void RpcShowEndGameScreen(bool localPlayerWon, string winnerName)
        {
            StopAllCoroutines();
            StartCoroutine(FadeInEndGameScreen(localPlayerWon, winnerName));
        }

        private IEnumerator FadeInEndGameScreen(bool localPlayerWon, string winnerName)
        {
            float fadeSpeed = 1.5f;

            // Set UI text and color first
            if (localPlayerWon)
            {
                endGameScreenBackground.color = winColor;
                gameStatesText.text = "VICTORY";
                winningPlayerText.text = "You won!";
            }
            else if (!string.IsNullOrEmpty(winnerName))
            {
                endGameScreenBackground.color = loseColor;
                gameStatesText.text = "DEFEAT";
                winningPlayerText.text = $"{winnerName} won!";
            }
            else
            {
                endGameScreenBackground.color = drawColor;
                gameStatesText.text = "DRAW";
                winningPlayerText.text = "Nobody won!";
            }

            // Smooth fade in
            while (endGameCanvasGroup.alpha < 1f)
            {
                endGameCanvasGroup.alpha = Mathf.MoveTowards(
                    endGameCanvasGroup.alpha,
                    1f,
                    Time.deltaTime * fadeSpeed
                );
                yield return null;
            }
        }

        private void FixedUpdate()
        {
            // Gather all current actors
            actors = FindObjectsByType<Actor>(FindObjectsInactive.Include, FindObjectsSortMode.None);

            players.Clear();

            foreach (Actor actor in actors)
            {
                var playerStats = new Dashboard.PlayerStats
                {
                    name = actor.name,
                    kills = actor.kills,
                    deaths = actor.deaths,
                    actor = actor
                };

                players.Add(playerStats);
            }

            if (isServer)
                UpdateWinningPlayer();
        }

        /// <summary>
        /// Finds the player with the highest kills and handles draw detection.
        /// </summary>
        [Server]
        private void UpdateWinningPlayer()
        {
            int highestKills = -1;
            IActor topPlayer = null;
            bool isDraw = false;

            foreach (var playerStats in players)
            {
                if (playerStats.kills > highestKills)
                {
                    highestKills = playerStats.kills;
                    topPlayer = playerStats.actor;
                    isDraw = false;
                }
                else if (playerStats.kills == highestKills && playerStats.kills != -1)
                {
                    isDraw = true;
                }
            }

            // Optional: treat 0 kills for everyone as draw
            if (highestKills <= 0)
            {
                isDraw = true;
            }

            if (isDraw)
            {
                winningPlayer = null;
                winningPlayerName = string.Empty;
            }
            else
            {
                winningPlayer = (Actor)topPlayer;
                winningPlayerName = topPlayer != null ? topPlayer.actorData.name : string.Empty;
            }
        }
#endif
    }
}