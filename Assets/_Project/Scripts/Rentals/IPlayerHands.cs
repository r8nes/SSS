namespace ShooterShop.Rentals
{
    public interface IPlayerHands
    {
        RentalWeaponConfig HeldRental { get; }
        void Give(RentalWeaponConfig weapon);
        void RemoveHeld();
    }
}
