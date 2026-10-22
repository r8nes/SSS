using Akila.FPSFramework;
using ShooterShop.Feedback;
using Sirenix.OdinInspector;
using UnityEngine;
using VContainer;

namespace ShooterShop.Days
{
    public sealed class ShopOpenSign : MonoBehaviour, IInteractable
    {
        [SerializeField] private string _interactionName = "Open shop";

        [SerializeField] private string _closeInteractionName = "Close shop";

        [Required]
        [SerializeField] private InteractableOutline _outline;

        private IDayState _state;
        private IDayCommands _commands;
        private bool _isHighlighted;

        public bool requireHoldToInteract => false;
        public bool isHighlighted
        {
            get => _isHighlighted;
            set
            {
                _isHighlighted = value;
                _outline.SetVisible(value && _commands != null && (_commands.CanOpenShop || _commands.CanCloseShop));
            }
        }

        [Inject]
        public void Construct(IDayState state, IDayCommands commands)
        {
            _state = state;
            _commands = commands;
        }

        public string GetInteractionName()
        {
            if (_commands == null)
                return string.Empty;

            if (_commands.CanOpenShop)
                return $"{_interactionName} (Day {_state.Day})";

            return _commands.CanCloseShop ? _closeInteractionName : string.Empty;
        }

        public void Interact(InteractionsManager source)
        {
            if (_commands == null)
                return;

            if (_commands.CanOpenShop)
                _commands.OpenShop();
            else
                _commands.CloseShop();
        }
    }
}
