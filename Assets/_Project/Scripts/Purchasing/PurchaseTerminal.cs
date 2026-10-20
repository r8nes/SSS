using System;
using Akila.FPSFramework;
using Sirenix.OdinInspector;
using UnityEngine;

namespace ShooterShop.Purchasing
{
    public sealed class PurchaseTerminal : MonoBehaviour, IInteractable
    {
        [SerializeField] private string _interactionName = "Order stock";

        public event Action Interacted;

        public bool requireHoldToInteract => false;

        [ShowInInspector, Sirenix.OdinInspector.ReadOnly]
        public bool isHighlighted { get; set; }

        public string GetInteractionName() => _interactionName;

        public void Interact(InteractionsManager source) => Interacted?.Invoke();
    }
}
