using Akila.FPSFramework;
using UnityEngine;

#if MIRROR
using Mirror;
#endif

namespace Akila.FPSFrameworkPro
{
    /// <summary>
    /// Network-aware wrapper for the <see cref="Actor"/> class.
    /// Handles synchronization of stats like kills and deaths over the network.
    /// </summary>
    [RequireComponent(typeof(Actor))]
#if MIRROR
    public class NetworkActor : NetworkBehaviour
#else
    public class NetworkActor : MonoBehaviour
#endif
    {
        #if MIRROR
        //public GameObject playerNameCard;

        /// <summary>
        /// The actor component this network actor controls.
        /// </summary>
        public Actor TargetActor { get; private set; }

        [SyncVar, HideInInspector]
        public int kills, deaths;
        [SyncVar(hook = nameof(OnTeamIDChanged)), HideInInspector]
        public int teamID;

        [SyncVar, HideInInspector]
        public string playerName;

        public ActorData networkData;

        private void Awake()
        {
            // Cache the Actor component.
            TargetActor = GetComponent<Actor>();
            networkData = new ActorData();

            TargetActor.playerUIEnabled = false;
        }

        private void Start()
        {
            TargetActor.playerUIEnabled = isLocalPlayer;

            UIBasedNetworkDiscovery uIBasedNetworkDiscovery = FindFirstObjectByType<UIBasedNetworkDiscovery>();

            if (uIBasedNetworkDiscovery != null && isLocalPlayer)
            {
                CmdSetTeamId(uIBasedNetworkDiscovery.localTeamID);
            }

            // Initialize player card for the local player only.
            if (isLocalPlayer)
            {
                EnableAndUpdateCard();

                // Subscribe to the death event to disable the card on death.
                TargetActor.OnDeathConfirmed.AddListener(() =>
                {
                    TargetActor.playerCard?.Disable(TargetActor);
                });
            }

            TargetActor.Damageable.OnDeath.RemoveListener(TargetActor.ConfirmDeath);


            if (TargetActor.playerCard != null && isLocalPlayer)
            {
                TargetActor.playerCard.Enable(TargetActor);
                TargetActor.playerCard.UpdateCard(TargetActor);
                TargetActor.playerCard.Setup(TargetActor);
            }

            if (isLocalPlayer)
            {
                GameModer.Instance.localPlayer = TargetActor;

                if(teamID == 0)
                {
                    NetworkSpawnManager networkSpawnManager = FindFirstObjectByType<NetworkSpawnManager>();

                    if (networkSpawnManager != null)
                    {
                        Transform spawnPoint = networkSpawnManager.TargetSpawnManager.GetPlayerSpawnPoint(0);

                        transform.position = spawnPoint.position;
                        transform.rotation = spawnPoint.rotation;
                    }
                }
            }

            GameModer.Instance.RefreshPlayers();

            //TODO: Fix player card errors
            /*
            if (playerNameCard != null)
            {
                Canvas canvas = FindFirstObjectByType<Canvas>();

                if (canvas != null)
                {
                    GameObject nameCard = Instantiate(playerNameCard, canvas.transform);

                    Transform text = nameCard.transform.FindDeepChild("Text");

                    TextMeshProUGUI textMeshProUGUI = text.GetComponent<TextMeshProUGUI>();

                    textMeshProUGUI?.SetText(playerName);
                }
            }
            */
        }

        [Command]
        private void CmdSetTeamId(int value)
        {
            teamID = value;
        }

        private void OnTeamIDChanged(int oldValue, int newValue)
        {
            NetworkSpawnManager networkSpawnManager = FindFirstObjectByType<NetworkSpawnManager>();

            if (networkSpawnManager == null) return;

            Transform spawnPoint = networkSpawnManager.TargetSpawnManager.GetPlayerSpawnPoint(newValue);

            transform.position = spawnPoint.position;
            transform.rotation = spawnPoint.rotation;

        }

        private void Update()
        {
            // Ensure only the local player interacts with their player card.
            TargetActor.playerCardActive = isLocalPlayer;

            if (isLocalPlayer && TargetActor.playerUIEnabled)
            {
                // Continuously update the player card locally.
                TargetActor.playerCard?.UpdateCard(TargetActor);
            }

            TargetActor.kills = kills;
            TargetActor.deaths = deaths;
            TargetActor.teamId = teamID;

            networkData.kills = kills;
            networkData.deaths = deaths;
            networkData.teamID = teamID;
        }

        /// <summary>
        /// Enables and updates the player card if it exists.
        /// </summary>
        private void EnableAndUpdateCard()
        {
            if (TargetActor.playerCard != null)
            {
                TargetActor.playerCard.Enable(TargetActor);
                TargetActor.playerCard.UpdateCard(TargetActor);
            }
        }
#endif
    }
}
