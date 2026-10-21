using System;
using Akila.FPSFramework;
using ShooterShop.Feedback;
using Sirenix.OdinInspector;
using UnityEngine;

namespace ShooterShop.Purchasing
{
    public sealed class PurchaseTerminal : MonoBehaviour, IInteractable
    {
        [SerializeField] private string _interactionName = "Order stock";

        [Required]
        [SerializeField] private InteractableOutline _outline;

        private bool _isHighlighted;

        public event Action Interacted;

        public bool requireHoldToInteract => false;

        [ShowInInspector, Sirenix.OdinInspector.ReadOnly]
        public bool isHighlighted
        {
            get => _isHighlighted;
            set
            {
                _isHighlighted = value;
                _outline.SetVisible(value);
            }
        }

        public string GetInteractionName() => _interactionName;

        public void Interact(InteractionsManager source) => Interacted?.Invoke();
    }
}
