using System;
using Mirror;
using UnityEngine;

namespace ShooterShop.Feedback
{
    public sealed class ShopFeed : NetworkBehaviour, IShopFeed, IShopFeedEvents
    {
        public event Action<string, NotificationKind> Notified;
        public event Action<Vector3, string, NotificationKind> PoppedUp;

        public void Notify(string message, NotificationKind kind = NotificationKind.Info)
        {
            if (isServer)
                RpcNotify(message, kind);
        }

        public void Popup(Vector3 position, string text, NotificationKind kind = NotificationKind.Good)
        {
            if (isServer)
                RpcPopup(position, text, kind);
        }

        [ClientRpc]
        private void RpcNotify(string message, NotificationKind kind) => Notified?.Invoke(message, kind);

        [ClientRpc]
        private void RpcPopup(Vector3 position, string text, NotificationKind kind) => PoppedUp?.Invoke(position, text, kind);
    }
}
