using System.Threading;
using Cysharp.Threading.Tasks;
using Mirror;
using ShooterShop.Saves;
using VContainer.Unity;

namespace ShooterShop.Store
{
    public sealed class StartingStockDelivery : IAsyncStartable
    {
        private readonly StartingState _startingState;
        private readonly IDeliveryService _delivery;

        public StartingStockDelivery(StartingState startingState, IDeliveryService delivery)
        {
            _startingState = startingState;
            _delivery = delivery;
        }

        public async UniTask StartAsync(CancellationToken cancellation)
        {
            if (await UniTask.WaitUntil(() => NetworkServer.active, cancellationToken: cancellation).SuppressCancellationThrow())
                return;

            foreach (var (product, amount) in _startingState.Stock)
                _delivery.DeliverNow(product, amount);
        }
    }
}
