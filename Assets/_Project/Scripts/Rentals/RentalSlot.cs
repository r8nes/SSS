using Akila.FPSFramework;
using Sirenix.OdinInspector;
using UnityEngine;

namespace ShooterShop.Rentals
{
    public sealed class RentalSlot : MonoBehaviour, IInteractable
    {
        [Required]
        [SerializeField] private RentalWeaponHolder _holder;

        [MinValue(0)]
        [SerializeField] private int _index;

        private bool _isHighlighted;

        public bool requireHoldToInteract => false;
        public bool isHighlighted
        {
            get => _isHighlighted;
            set
            {
                _isHighlighted = value;
                _holder.SetSlotHighlighted(_index, value);
            }
        }

        public string GetInteractionName() => _holder.GetSlotPrompt(_index);

        public void Interact(InteractionsManager source) => _holder.InteractSlot(_index);
    }
}
