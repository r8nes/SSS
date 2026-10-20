namespace ShooterShop.Days
{
    public interface IDayCommands
    {
        bool CanStartNextDay { get; }
        void StartNextDay();
    }
}
