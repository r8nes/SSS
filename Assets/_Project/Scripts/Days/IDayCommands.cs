namespace ShooterShop.Days
{
    public interface IDayCommands
    {
        bool CanStartNextDay { get; }
        void StartNextDay();
        bool CanOpenShop { get; }
        void OpenShop();
        bool CanCloseShop { get; }
        void CloseShop();
    }
}
