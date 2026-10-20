using Akila.FPSFramework;
using Akila.FPSFramework.UI;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

#if MIRROR
using Mirror;
using Mirror.Discovery;
#endif

namespace Akila.FPSFrameworkPro
{
    /// <summary>
    /// Handles UI-based LAN discovery using Mirror's NetworkDiscovery system.
    /// Displays discovered servers, allows joining as a client, or hosting a new session.
    /// </summary>
    public class UIBasedNetworkDiscovery : MonoBehaviour
    {
#if MIRROR
        [Header("UI References")]
        [Tooltip("Parent GameObject holding the discovery UI elements.")]
        public GameObject holder;

        [Tooltip("Prefab for displaying each discovered server as a button.")]
        public Button serverButtonPrefab;

        [Tooltip("Button used to start hosting a server.")]
        public Button hostButton;

        [Tooltip("Text message shown when no servers are found.")]
        public TextMeshProUGUI noServersMessage;

        [Tooltip("Parent transform where discovered server buttons will be instantiated.")]
        public Transform serversListParent;

        public CarouselSelector teamSelectorDropdown;

        [Header("Network Discovery")]
        [Tooltip("Reference to the NetworkDiscovery component used for LAN discovery.")]
        public NetworkDiscovery discovery;

        [Space]
        public UnityEvent onConnectedAsHost;
        public UnityEvent onConnectedAsClient;

        /// <summary>
        /// Internal dictionary storing all currently discovered servers (keyed by serverId).
        /// </summary>
        private readonly Dictionary<long, ServerResponse> discoveredServers = new Dictionary<long, ServerResponse>();

        public int localTeamID
        {
            get
            {
                return teamSelectorDropdown.value;
            }
        }
        private void Start()
        {
            // Validate references
            if (discovery == null)
            {
                FPSFrameworkCore.LogError("NetworkDiscovery reference is missing!", MessageLevel.Normal, gameObject);
                enabled = false;
                return;
            }

            if (serverButtonPrefab == null)
            {
                FPSFrameworkCore.LogError("Server Button Prefab is not assigned!", MessageLevel.Normal, gameObject);
                enabled = false;
                return;
            }

            // Hide the prefab template
            serverButtonPrefab.gameObject.SetActive(false);

            // Register callback for when servers are found
            discovery.OnServerFound.AddListener(OnServerDiscovered);

            // Fallback if no holder is assigned
            if (holder == null)
                holder = gameObject;

            RefreshServersList();
        }

        private void Update()
        {
            // Show "no servers" message when the list is empty
            noServersMessage.gameObject.SetActive(discoveredServers.Count <= 0);

            // Disable host button when already in a network session
            hostButton.interactable = !NetworkManager.singleton.isNetworkActive;

            // Hide discovery UI while connected as client
            holder.SetActive(!NetworkClient.active);

            // Clean up the UI if all servers disappeared
            if (discoveredServers.Count <= 0 && serversListParent.childCount != 0)
            {
                serversListParent.ClearChildren(false, new string[] { noServersMessage.name });
            }
        }

        /// <summary>
        /// Starts hosting a server and advertises it over LAN.
        /// </summary>
        public void StartHost()
        {
            if (NetworkManager.singleton == null)
            {
                FPSFrameworkCore.LogError("No NetworkManager found in scene!", MessageLevel.Normal, gameObject);
                return;
            }

            // Clear old results before starting
            discoveredServers.Clear();

            // Start hosting
            NetworkManager.singleton.StartHost();

            // Broadcast to LAN clients
            discovery.AdvertiseServer();

            onConnectedAsHost?.Invoke();

            FPSFrameworkCore.Log("Hosting server started and advertised.", MessageLevel.Maximum, gameObject);
        }

        /// <summary>
        /// Refreshes the server list by clearing existing entries and starting a new discovery search.
        /// </summary>
        public void RefreshServersList()
        {
            if (discovery == null)
            {
                FPSFrameworkCore.LogError("NetworkDiscovery reference is missing!", MessageLevel.Normal, gameObject);
                return;
            }

            // Clear UI and data
            serversListParent.ClearChildren(false, new string[] { noServersMessage.name });
            discoveredServers.Clear();

            // Begin searching for servers
            discovery.StartDiscovery();

            FPSFrameworkCore.Log("Started server discovery.", MessageLevel.Maximum, gameObject);
        }

        /// <summary>
        /// Called automatically when a new server is discovered.
        /// Adds it to the UI list if not already present.
        /// </summary>
        /// <param name="info">Information about the discovered server.</param>
        public void OnServerDiscovered(ServerResponse info)
        {
            // Prevent duplicates
            if (discoveredServers.ContainsKey(info.serverId))
                return;

            // Store new discovery
            discoveredServers[info.serverId] = info;

            // Log discovery details
            FPSFrameworkCore.Log(
                $"Discovered Server: {info.serverId} | {info.EndPoint} | {info.uri}",
                MessageLevel.Maximum,
                gameObject
            );

            // Create new button in UI
            Button newButton = Instantiate(serverButtonPrefab, serversListParent);
            newButton.gameObject.SetActive(true);
            newButton.name = $"Server_{info.serverId}";

            // Assign button label
            TextMeshProUGUI text = newButton.GetComponentInChildren<TextMeshProUGUI>();
            if (text != null)
            {
                int index = discoveredServers.Count;
                text.text = $"{index}. Server ({info.serverId})";
            }

            // Add connection behavior
            newButton.onClick.AddListener(() =>
            {
                if (NetworkManager.singleton == null)
                {
                    FPSFrameworkCore.LogError("No NetworkManager found to start client!", MessageLevel.Normal, gameObject);
                    return;
                }

                FPSFrameworkCore.Log($"Connecting to server at {info.uri}...", MessageLevel.Maximum, gameObject);

                NetworkManager.singleton.StartClient(info.uri);

                onConnectedAsClient?.Invoke();
            });
        }
#endif
    }
}