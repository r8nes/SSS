namespace ShooterShop.Audio
{
    public interface IMusicPlayer
    {
        MusicPlaylist Current { get; }
        void Play(MusicPlaylist playlist);
    }
}
