using VContainer.Unity;

namespace ShooterShop.Audio
{
    public sealed class MusicCue : IStartable
    {
        private readonly IMusicPlayer _player;
        private readonly MusicPlaylist _playlist;

        public MusicCue(IMusicPlayer player, MusicPlaylist playlist)
        {
            _player = player;
            _playlist = playlist;
        }

        public void Start() => _player.Play(_playlist);
    }
}
