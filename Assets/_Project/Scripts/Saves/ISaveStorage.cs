namespace ShooterShop.Saves
{
    public interface ISaveStorage
    {
        bool TryLoad(out SaveData data);
        void Save(SaveData data);
        void Delete();
    }
}
