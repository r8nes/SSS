using Cysharp.Threading.Tasks;

namespace ShooterShop.Loading
{
    public interface ILoadingScreen
    {
        UniTask ShowAsync();
        void Hide();
    }
}
