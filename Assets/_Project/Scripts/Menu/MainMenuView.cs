using Akila.FPSFramework;
using Sirenix.OdinInspector;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace ShooterShop.Menu
{
    public sealed class MainMenuView : MonoBehaviour
    {
        [BoxGroup("Session"), Required]
        [SerializeField] private InteractiveButton _soloButton;

        [BoxGroup("Session"), Required]
        [SerializeField] private InteractiveButton _hostButton;

        [BoxGroup("Session"), Required]
        [SerializeField] private InteractiveButton _newGameButton;

        [BoxGroup("Session"), Required]
        [SerializeField] private TMP_Text _saveInfo;

        [BoxGroup("Join"), Required]
        [SerializeField] private TMP_InputField _addressInput;

        [BoxGroup("Join"), Required]
        [SerializeField] private InteractiveButton _joinButton;

        [BoxGroup("Servers"), Required]
        [SerializeField] private Transform _serverListRoot;

        [BoxGroup("Servers"), Required, AssetsOnly]
        [SerializeField] private ServerEntryView _serverEntryPrefab;

        [BoxGroup("Servers")]
        [SerializeField] private GameObject _noServersLabel;

        private readonly List<ServerEntryView> _entries = new();

        public InteractiveButton SoloButton => _soloButton;
        public InteractiveButton HostButton => _hostButton;
        public InteractiveButton JoinButton => _joinButton;
        public InteractiveButton NewGameButton => _newGameButton;
        public string Address => _addressInput.text.Trim();

        public void SetSaveInfo(string info, bool hasSave)
        {
            _saveInfo.text = info;
            _newGameButton.gameObject.SetActive(hasSave);
        }

        public ServerEntryView AddServer(string label)
        {
            var entry = Instantiate(_serverEntryPrefab, _serverListRoot);
            entry.SetLabel(label);
            _entries.Add(entry);
            RefreshEmptyState();
            return entry;
        }

        public void ClearServers()
        {
            foreach (var entry in _entries)
                Destroy(entry.gameObject);

            _entries.Clear();
            RefreshEmptyState();
        }

        public void SetInteractable(bool interactable)
        {
            _soloButton.interactable = interactable;
            _hostButton.interactable = interactable;
            _joinButton.interactable = interactable;
            _newGameButton.interactable = interactable;
            _addressInput.interactable = interactable;

            foreach (var entry in _entries)
                entry.Button.interactable = interactable;
        }

        private void RefreshEmptyState()
        {
            if (_noServersLabel != null)
                _noServersLabel.SetActive(_entries.Count == 0);
        }
    }
}
