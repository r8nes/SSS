using Akila.FPSFramework.UI;
using System;
using Akila.FPSFramework;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

#if MIRROR
using Mirror;
#endif

namespace Akila.FPSFrameworkPro
{
    public class Dashboard : MonoBehaviour
    {
        #if MIRROR
        public GameObject dashboardHolder;
        public GameObject title;
        public GameObject dashBoardItem;

        [Akila.FPSFramework.ReadOnly]
        public List<PlayerStats> players = new List<PlayerStats>();

        public InputAction openDashboardInputAction;

        public Menu menu;

        private void Start()
        {
            dashBoardItem.transform.SetParent(transform, false);

            title.transform.SetParent(transform, false);

            menu = GetComponentInChildren<Menu>();

            menu.onOpen.AddListener(AddPlayerItems);

            openDashboardInputAction.Enable();

            InvokeRepeating(nameof(RefreshPlayersList), 0, 1);
        }

        private void FixedUpdate()
        {
            foreach (var player in players)
            {
                player.Update();
            }

            if (openDashboardInputAction.IsPressed())
            {
                menu.OpenMenu();
                AddPlayerItems();
            }
            else
            {
                menu.CloseMenu();
            }
        }

        private void RefreshPlayersList()
        {
            players = GetPlayers();
        }

        protected void AddPlayerItems()
        {
            //Remove children
            dashboardHolder.transform.ClearChildren();

            GameObject tit = Instantiate(title.transform, dashboardHolder.transform).gameObject;

            tit.SetActive(true);

            foreach (PlayerStats player in players)
            {
                Transform item = Instantiate(dashBoardItem.transform, dashboardHolder.transform);

                item.gameObject.SetActive(true);

                item.name = "PlayerItem";

                string playerName = player.name;

                if (player.actorComponent != null)
                {
                    NetworkIdentity networkIdentity = player.actorComponent.SearchFor<NetworkIdentity>();

                    if (networkIdentity != null)
                    {
                        if (networkIdentity.isLocalPlayer)
                            playerName += $" (You)";
                    }
                }
                else
                {
                    playerName += $" (You)";
                }

                item.Find("Player Name").GetComponent<TextMeshProUGUI>().SetText(playerName);
                item.Find("Player Deaths").GetComponent<TextMeshProUGUI>().SetText($"D: {player.deaths.ToString()}");
                item.Find("Player Kills").GetComponent<TextMeshProUGUI>().SetText($"K: {player.kills.ToString()}");
            }
        }

        /// <summary>
        /// Returns a list of PlayerStatus, with the actor list of which that has IActor interface
        /// </summary>
        /// <returns></returns>
        public List<PlayerStats> GetPlayers()
        {
            List<PlayerStats> list = new List<PlayerStats>();

            IActor[] actors = FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.InstanceID).OfType<IActor>().ToArray();

            foreach (IActor actor in actors)
            {
                if (actor.transform.GetComponent<CharacterManager>() != null)
                {
                    PlayerStats stats = new PlayerStats();

                    stats.actor = actor;
                    stats.name = actor.actorData.name;

                    Actor actorComponent = actor.gameObject.GetComponent<Actor>();

                    stats.actorComponent = actorComponent;

                    if (actorComponent != null)
                    {
                        stats.kills = actorComponent.kills;
                        stats.deaths = actorComponent.deaths;
                    }

                    onActorGet?.Invoke(actor);

                    list.Add(stats);
                }
            }

            return list;
        }

        /// <summary>
        /// Called per Actor when GetPlayers() is called
        /// If there are 20 actors in the scene, this would be invoked 20 times per call, once for each actor
        /// </summary>
        public Action<IActor> onActorGet;

        [Serializable]
        public class PlayerStats
        {
            public string name;
            public IActor actor;
            public Actor actorComponent;
            public int kills;
            public int deaths;

            public void Update()
            {
                name = actor.actorData.name;
                kills = actorComponent.kills;
                deaths = actorComponent.deaths;
            }
        }
#endif
    }
}