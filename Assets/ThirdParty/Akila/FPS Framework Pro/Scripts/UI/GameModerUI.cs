using Akila.FPSFramework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static Akila.FPSFrameworkPro.GameModer;

namespace Akila.FPSFrameworkPro
{
    [RequireComponent(typeof(GameModer))]
    public class GameModerUI : MonoBehaviour
    {
        #if MIRROR
        [Header("HUD")]
        public TextMeshProUGUI timerText;
        public TextMeshProUGUI waitingText;

        [Separator]
        public CanvasGroup endGameScreen;
        [Tooltip("Background image of the end game screen.")]
        public Image endGameScreenBackground;

        [Tooltip("Text showing the current game state (Victory, Lost, Draw).")]
        public TextMeshProUGUI gameStatesText;

        [Tooltip("Text showing the winning player's name or result message.")]
        public TextMeshProUGUI winningPlayerText;

        [Tooltip("Background color for winning.")]
        public Color winColor;

        [Tooltip("Background color for losing or draw.")]
        public Color loseColor;

        private GameModer gameModer;

        private void Start()
        {
            gameModer = GetComponent<GameModer>();

            if (endGameScreen != null) endGameScreen.alpha = 0;
        }

        private void Update()
        {
            if (waitingText != null)
            {
                waitingText.text = gameModer.gameState == GameState.WaitingForPlayers ? "Waiting For Opponent" : "";
            }

            if (timerText != null)
            {
                int minutes = Mathf.FloorToInt(gameModer.timeLeftInSeconds / 60f);
                int seconds = Mathf.FloorToInt(gameModer.timeLeftInSeconds % 60f);

                timerText.text = $"{minutes:00}:{seconds:00}";
            }

            if (endGameScreenBackground != null)
            {
                if (gameModer.gameState == GameState.Ended)
                {
                    endGameScreenBackground.color = gameModer.isLocalPlayerWinning ? winColor : loseColor;
                }
            }

            if (endGameScreen != null)
            {
                if (gameModer.gameState == GameState.Ended)
                {
                    endGameScreen.alpha = Mathf.Lerp(endGameScreen.alpha, 1, Time.deltaTime * 10);
                }
            }

            if (gameStatesText != null)
            {
                if (gameModer.gameState == GameState.Ended)
                {
                    gameStatesText.text = gameModer.isLocalPlayerWinning ? "VICTORY" : "DEFEAT";
                }
            }

            if (winningPlayerText != null)
            {
                if (gameModer.gameState == GameState.Ended)
                {
                    winningPlayerText.text = gameModer.winningPlayerName;
                }
            }
        }
#endif
    }
}