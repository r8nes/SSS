namespace ShooterShop.Raids
{
    public interface IRaid
    {
        bool IsActive { get; }
        bool IsAmbushActive { get; }
        void ServerStartAmbush();
    }
}
