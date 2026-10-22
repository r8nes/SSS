namespace ShooterShop.Rentals
{
    public interface IPlayerHands
    {
        RentalWeaponConfig HeldRental { get; }
        bool IsHoldingWeapon { get; }
        void Give(RentalWeaponConfig weapon);
        void RemoveHeld();
    }
}
